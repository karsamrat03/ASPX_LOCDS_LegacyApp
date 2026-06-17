param(
    [string]$SqlInstance = ".\\SQLEXPRESS",
    [string]$DatabaseName = "LOCDS",
    [ValidateSet("Mock", "Mirror")]
    [string]$Mode = "Mock",
    [string]$MirrorBackupPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Data

function New-ConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Instance,
        [Parameter(Mandatory = $true)]
        [string]$Database
    )

    return "Server=$Instance;Database=$Database;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;"
}

function Split-SqlBatches {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SqlText
    )

    return [regex]::Split($SqlText, "(?im)^\s*GO\s*(?:--.*)?$")
}

function Invoke-SqlText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Instance,
        [Parameter(Mandatory = $true)]
        [string]$Database,
        [Parameter(Mandatory = $true)]
        [string]$SqlText
    )

    $connectionString = New-ConnectionString -Instance $Instance -Database $Database
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    try {
        $batches = Split-SqlBatches -SqlText $SqlText
        foreach ($batch in $batches) {
            if ([string]::IsNullOrWhiteSpace($batch)) {
                continue
            }

            $command = $connection.CreateCommand()
            $command.CommandTimeout = 0
            $command.CommandText = $batch
            [void]$command.ExecuteNonQuery()
        }
    }
    finally {
        $connection.Dispose()
    }
}

function Get-RestoreFileList {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Instance,
        [Parameter(Mandatory = $true)]
        [string]$BackupPath
    )

    $escapedBackupPath = $BackupPath.Replace("'", "''")
    $query = "RESTORE FILELISTONLY FROM DISK = N'$escapedBackupPath';"

    $connectionString = New-ConnectionString -Instance $Instance -Database "master"
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()

    try {
        $command = $connection.CreateCommand()
        $command.CommandText = $query
        $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
        $table = New-Object System.Data.DataTable
        [void]$adapter.Fill($table)
        return ,$table
    }
    finally {
        $connection.Dispose()
    }
}

function Invoke-SqlFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Instance,
        [Parameter(Mandatory = $true)]
        [string]$Database,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        throw "SQL file not found: $Path"
    }

    Write-Host "Executing $Path" -ForegroundColor Cyan
    $sqlText = Get-Content -Path $Path -Raw
    Invoke-SqlText -Instance $Instance -Database $Database -SqlText $sqlText
}

function Ensure-Database {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Instance,
        [Parameter(Mandatory = $true)]
        [string]$Database
    )

    $sql = "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];"
    Invoke-SqlText -Instance $Instance -Database "master" -SqlText $sql
}

$root = Split-Path -Parent $PSScriptRoot
$dbFolder = Join-Path $root "LOCDS\\LOCDS.DAL\\Database"
$ddlLoan = Join-Path $dbFolder "LOCDS.LoanOrigination.ddl.sql"
$seed = Join-Path $dbFolder "LOCDS.DemoSeed.sql"

if ($Mode -eq "Mock") {
    Write-Host "Preparing SQL-backed demo dataset (mode: Mock)" -ForegroundColor Yellow

    Ensure-Database -Instance $SqlInstance -Database $DatabaseName
    Invoke-SqlFile -Instance $SqlInstance -Database $DatabaseName -Path $ddlLoan
    Invoke-SqlFile -Instance $SqlInstance -Database $DatabaseName -Path $seed

    Write-Host "Demo database is ready: $SqlInstance / $DatabaseName" -ForegroundColor Green
    Write-Host "Use this connection string in LOCDS.Web/Web.config:" -ForegroundColor Green
    Write-Host "Server=$SqlInstance;Database=$DatabaseName;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($MirrorBackupPath)) {
    $MirrorBackupPath = $env:LOCDS_MIRROR_BAK
}

if ([string]::IsNullOrWhiteSpace($MirrorBackupPath)) {
    throw "Mode 'Mirror' requires -MirrorBackupPath or LOCDS_MIRROR_BAK to a sanitized .bak file."
}

if (-not (Test-Path $MirrorBackupPath)) {
    throw "Mirror backup file not found: $MirrorBackupPath"
}

Write-Host "Restoring mirror demo database from sanitized backup" -ForegroundColor Yellow

$backupPathSql = $MirrorBackupPath.Replace("'", "''")
$databaseSql = $DatabaseName.Replace("]", "]]" )

$fileList = Get-RestoreFileList -Instance $SqlInstance -BackupPath $MirrorBackupPath
if ($fileList.Rows.Count -eq 0) {
    throw "Unable to read backup file metadata: $MirrorBackupPath"
}

$moveClauses = @()
$dataIndex = 0
$logIndex = 0
foreach ($row in $fileList.Rows) {
    $logicalName = [string]$row["LogicalName"]
    $physicalName = [string]$row["PhysicalName"]
    $type = [string]$row["Type"]

    $folder = Split-Path -Parent $physicalName
    if ([string]::IsNullOrWhiteSpace($folder)) {
        $folder = "C:\\Program Files\\Microsoft SQL Server\\MSSQL16.SQLEXPRESS\\MSSQL\\DATA"
    }

    if ($type -eq "L") {
        $logIndex++
        $targetName = if ($logIndex -eq 1) { "$DatabaseName`_log.ldf" } else { "$DatabaseName`_log$logIndex.ldf" }
    }
    else {
        $dataIndex++
        $targetName = if ($dataIndex -eq 1) { "$DatabaseName.mdf" } else { "$DatabaseName`_$dataIndex.ndf" }
    }

    $targetPath = (Join-Path $folder $targetName).Replace("'", "''")
    $logicalEscaped = $logicalName.Replace("'", "''")
    $moveClauses += "MOVE N'$logicalEscaped' TO N'$targetPath'"
}

$moveSql = [string]::Join(",`n    ", $moveClauses)

$restoreSql = @"
IF DB_ID(N'$DatabaseName') IS NOT NULL
BEGIN
    ALTER DATABASE [$databaseSql] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END;

RESTORE DATABASE [$databaseSql]
FROM DISK = N'$backupPathSql'
WITH REPLACE,
    RECOVERY,
    $moveSql;

IF DB_ID(N'$DatabaseName') IS NOT NULL
BEGIN
    ALTER DATABASE [$databaseSql] SET MULTI_USER;
END;
"@

Invoke-SqlText -Instance $SqlInstance -Database "master" -SqlText $restoreSql

Write-Host "Mirror database restore completed: $SqlInstance / $DatabaseName" -ForegroundColor Green