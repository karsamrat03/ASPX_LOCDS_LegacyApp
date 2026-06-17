param(
    [Parameter(Mandatory = $false)]
    [string]$SolutionPath = "LOCDS.sln",

    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Release",

    [Parameter(Mandatory = $true)]
    [string]$IisSiteName,

    [Parameter(Mandatory = $false)]
    [string]$PublishRoot = "artifacts\\publish",

    [Parameter(Mandatory = $false)]
    [string]$DatabaseScriptsPath = "LOCDS\\LOCDS.DAL\\Database",

    [Parameter(Mandatory = $false)]
    [string]$SqlConnectionString = $env:LOCDS_CONNECTION_STRING,

    [Parameter(Mandatory = $false)]
    [string]$WarmupRelativePath = "health-check.aspx"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Resolve-MsBuildPath {
    $candidates = @(
        "${env:ProgramFiles(x86)}\\Microsoft Visual Studio\\2022\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "${env:ProgramFiles}\\Microsoft Visual Studio\\2022\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "${env:ProgramFiles(x86)}\\Microsoft Visual Studio\\2022\\Professional\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "${env:ProgramFiles(x86)}\\Microsoft Visual Studio\\2022\\Enterprise\\MSBuild\\Current\\Bin\\MSBuild.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    $fromPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($fromPath) {
        return $fromPath.Source
    }

    throw "MSBuild not found. Install Build Tools 2022 or add msbuild to PATH."
}

function Invoke-DatabaseMigrations {
    param(
        [string]$ScriptsPath,
        [string]$ConnectionString
    )

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        throw "SqlConnectionString is required. Pass -SqlConnectionString or set LOCDS_CONNECTION_STRING env variable."
    }

    if (-not (Test-Path $ScriptsPath)) {
        Write-Host "Database scripts path not found, skipping migrations: $ScriptsPath"
        return
    }

    $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if (-not $sqlcmd) {
        throw "sqlcmd not found. Install SQL Server command-line tools to run migrations."
    }

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($ConnectionString)
    $server = $builder.DataSource
    $database = $builder.InitialCatalog
    if ([string]::IsNullOrWhiteSpace($database)) {
        $database = "master"
    }

    $scripts = Get-ChildItem -Path $ScriptsPath -Filter *.sql | Sort-Object Name
    foreach ($script in $scripts) {
        Write-Host "Applying migration script: $($script.Name)"

        if ($builder.IntegratedSecurity) {
            & $sqlcmd.Source -b -S $server -d $database -E -i $script.FullName -l 60
        }
        else {
            & $sqlcmd.Source -b -S $server -d $database -U $builder.UserID -P $builder.Password -i $script.FullName -l 60
        }

        if ($LASTEXITCODE -ne 0) {
            throw "Migration failed for script '$($script.FullName)' with exit code $LASTEXITCODE."
        }
    }
}

function Get-IisSitePhysicalPath {
    param([string]$SiteName)

    Import-Module WebAdministration
    $site = Get-Item "IIS:\\Sites\\$SiteName"
    if (-not $site) {
        throw "IIS site '$SiteName' not found."
    }

    return $site.physicalPath
}

function Publish-WebProject {
    param(
        [string]$MsBuild,
        [string]$Solution,
        [string]$BuildConfiguration,
        [string]$PublishPath,
        [string]$ConnectionFromEnv
    )

    $solutionFullPath = Resolve-Path $Solution
    $publishFullPath = Join-Path (Get-Location) $PublishPath

    if (Test-Path $publishFullPath) {
        Remove-Item -Recurse -Force $publishFullPath
    }

    New-Item -ItemType Directory -Path $publishFullPath | Out-Null

    $properties = @(
        "/p:Configuration=$BuildConfiguration",
        "/p:DeployOnBuild=true",
        "/p:WebPublishMethod=FileSystem",
        "/p:PublishUrl=$publishFullPath"
    )

    if (-not [string]::IsNullOrWhiteSpace($ConnectionFromEnv)) {
        $properties += "/p:LOCDS_CONNECTION_STRING=$ConnectionFromEnv"
    }

    & $MsBuild $solutionFullPath /m /v:minimal $properties

    if ($LASTEXITCODE -ne 0) {
        throw "MSBuild publish failed with exit code $LASTEXITCODE."
    }

    return $publishFullPath
}

function Deploy-ToIis {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$SiteName
    )

    Import-Module WebAdministration

    Write-Host "Stopping IIS site: $SiteName"
    Stop-Website -Name $SiteName

    if (-not (Test-Path $DestinationPath)) {
        New-Item -ItemType Directory -Path $DestinationPath | Out-Null
    }

    Write-Host "Syncing files to IIS path: $DestinationPath"
    robocopy $SourcePath $DestinationPath /MIR /R:2 /W:2 /XD "App_Data\\Logs" | Out-Null

    Write-Host "Starting IIS site: $SiteName"
    Start-Website -Name $SiteName
}

function Invoke-Warmup {
    param(
        [string]$SiteName,
        [string]$RelativePath
    )

    Import-Module WebAdministration
    $binding = Get-WebBinding -Name $SiteName -Protocol "http" | Select-Object -First 1
    if (-not $binding) {
        throw "No HTTP binding found for site '$SiteName'."
    }

    $hostHeader = $binding.bindingInformation.Split(':')[2]
    $port = $binding.bindingInformation.Split(':')[1]
    if ([string]::IsNullOrWhiteSpace($hostHeader)) {
        $hostHeader = "localhost"
    }

    $uri = "http://$hostHeader`:$port/$RelativePath"
    Write-Host "Warm-up ping: $uri"

    $response = Invoke-WebRequest -Uri $uri -UseBasicParsing -TimeoutSec 30
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "Warm-up ping failed with status $($response.StatusCode)."
    }
}

$msbuildPath = Resolve-MsBuildPath
$publishedOutput = Publish-WebProject -MsBuild $msbuildPath -Solution $SolutionPath -BuildConfiguration $Configuration -PublishPath $PublishRoot -ConnectionFromEnv $SqlConnectionString
Invoke-DatabaseMigrations -ScriptsPath $DatabaseScriptsPath -ConnectionString $SqlConnectionString
$iisPath = Get-IisSitePhysicalPath -SiteName $IisSiteName
Deploy-ToIis -SourcePath $publishedOutput -DestinationPath $iisPath -SiteName $IisSiteName
Invoke-Warmup -SiteName $IisSiteName -RelativePath $WarmupRelativePath

Write-Host "Deployment completed successfully."
