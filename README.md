# LOCDS (Loan Origination and Credit Decisioning System)

## Quick Start

### 1. Clone and Open

```powershell
git clone <your-github-repo-url>
cd ASPX
```

Open `LOCDS.sln` in Visual Studio 2022.

### 2. Prerequisites (New Machine)

- Windows 10/11
- Visual Studio 2022 (recommended) with ASP.NET and web development workload
- .NET Framework 4.8.x runtime/targeting pack
- .NET SDK (for SDK-style projects/tests)
- SQL Server (Express is fine for local)
- IIS Express (installed with Visual Studio) or IIS

### 3. Prepare Database (Demo)

Mock data setup (fast local demo):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-demo-db.ps1 -Mode Mock -SqlInstance ".\SQLEXPRESS" -DatabaseName "LOCDS"
```

Mirror restore setup (production-like sanitized backup):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\setup-demo-db.ps1 -Mode Mirror -SqlInstance ".\SQLEXPRESS" -DatabaseName "LOCDS" -MirrorBackupPath "C:\backups\LOCDS_Demo_Sanitized.bak"
```

### 4. Configure Connection String

Update `LOCDS/LOCDS.Web/Web.config`:

```xml
<add name="LOCDSConnection"
	connectionString="Server=.\SQLEXPRESS;Database=LOCDS;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;"
	providerName="System.Data.SqlClient" />
```

### 5. Run Application

- Set `LOCDS.Web` as startup project in Visual Studio.
- Run with IIS Express (F5).
- Open `http://localhost:8080` if needed.

### 6. Validate Health

Check:

- `LOCDS/LOCDS.Web/health-check.aspx`

## Publish To GitHub

### 1. Initialize and Push

```powershell
git init
git add .
git commit -m "Initial LOCDS commit"
git branch -M main
git remote add origin https://github.com/<org-or-user>/<repo>.git
git push -u origin main
```

### 2. Should You Publish Entire Folder?

Yes for source code and scripts. Do not publish build artifacts and machine-specific files.
This repository includes a ready-to-use `.gitignore` so you can safely run `git add .`.

### 3. Optional

If you want to version large binary backups (for example `.bak`), use Git LFS.

## Architecture Overview

LOCDS is an ASP.NET Web Forms solution structured with layered separation of concerns:

- LOCDS.Web: presentation layer, Web Forms pages, security modules, global error handling, Unity composition root.
- LOCDS.BLL: business services for loan applications, scoring, underwriting, offers, and auditing.
- LOCDS.DAL: repositories, unit-of-work, SQL connection abstractions, and database DDL scripts.
- LOCDS.Common: shared constants, enums, exceptions, and infrastructure utilities.
- LOCDS.Entities: domain entities and core model contracts.
- LOCDS.Tests: NUnit + Moq tests for service behavior.

### Request Flow

1. User hits LOCDS.Web page.
2. Security modules enforce authz + anti-CSRF checks.
3. Web layer delegates business actions to BLL services.
4. BLL uses DAL repositories through IUnitOfWork.
5. Errors and audit events are logged with correlation IDs.

## Local Setup

### Prerequisites

- Windows with IIS + ASP.NET 4.8 features enabled
- Visual Studio 2022 (or Build Tools + MSBuild)
- SQL Server
- sqlcmd CLI (for migration script execution)

### 1. Configure Database Connection

Set an environment variable for the database connection string:

```powershell
$env:LOCDS_CONNECTION_STRING = "Server=localhost;Database=LOCDS;Trusted_Connection=True;Encrypt=False;"
```

Update LOCDS.Web/Web.config for local runtime if needed:

- connectionStrings/LOCDSConnection
- connectionStrings/SessionStateDb

### 2. Initialize Database

Run scripts in order from LOCDS/LOCDS.DAL/Database:

- LOCDS.ddl.sql
- LOCDS.LoanOrigination.ddl.sql

### Enterprise Demo Data Setup (Realtime vs Mirror vs Mock)

For client demos, avoid in-memory fallback data by always running with a SQL-backed dataset.

#### Option A: Realtime (Preferred for UAT-like demos)

1. Point LOCDSConnection to your managed SQL instance with near-realtime replicated data.
2. Keep all PII sanitized and use a dedicated demo schema/database.
3. Use a readonly login for reporting screens and a controlled write login for workflow actions.

Example connection string:

```xml
<add name="LOCDSConnection"
	connectionString="Server=sql-demo.company.net;Database=LOCDS_Demo;User ID=locds_demo_app;Password=***;Encrypt=True;TrustServerCertificate=False;MultipleActiveResultSets=True;"
	providerName="System.Data.SqlClient" />
```

#### Option B: Mirror (Sanitized backup restore)

Use when you want production-like data shape without live dependencies.

```powershell
./scripts/setup-demo-db.ps1 -Mode Mirror -SqlInstance ".\SQLEXPRESS" -DatabaseName "LOCDS" -MirrorBackupPath "C:\backups\LOCDS_Demo_Sanitized.bak"
```

#### Option C: Mock SQL (Fast local enterprise-style demo)

Creates schema + realistic seeded data (no in-memory dummy rows required):

```powershell
./scripts/setup-demo-db.ps1 -Mode Mock -SqlInstance ".\SQLEXPRESS" -DatabaseName "LOCDS"
```

This command runs:

- LOCDS.ddl.sql
- LOCDS.LoanOrigination.ddl.sql
- LOCDS.DemoSeed.sql

#### Recommended Demo Checklist

1. Run setup-demo-db.ps1 in Mock or Mirror mode.
2. Update LOCDS.Web/Web.config connectionStrings LOCDSConnection to that database.
3. Open Dashboard and verify non-empty queue and status filters.
4. Navigate Loan Application -> Underwriting Decision -> Decision Sheet -> Loan Offer.
5. Keep one SQL backup ready for instant reset between client sessions.

### 3. Build and Run

Use Visual Studio build/run or execute MSBuild from Developer PowerShell:

```powershell
msbuild LOCDS.sln /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

### 4. Run Tests

```powershell
dotnet test LOCDS/LOCDS.Tests/LOCDS.Tests.csproj
```

## Deployment Artifacts

- LOCDS/LOCDS.Web/Web.Release.config
- scripts/deploy.ps1
- LOCDS/LOCDS.Web/health-check.aspx
- LOCDS/LOCDS.Web/robots.txt
- LOCDS/LOCDS.Web/security.txt

### Deploy Script Example

```powershell
./scripts/deploy.ps1 -IisSiteName "Default Web Site/LOCDS" -Configuration Release
```

The script performs:

1. Build + filesystem publish from solution
2. Database migration script execution
3. IIS content sync and site restart
4. Warm-up ping to health-check endpoint
