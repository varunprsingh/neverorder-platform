<#
.SYNOPSIS
    Runs a repo-local PostgreSQL for NeverOrder without Docker or an installer.

.DESCRIPTION
    Downloads the official EnterpriseDB Windows binaries, initialises a cluster under
    .postgres/ and starts it bound to loopback only. No administrator rights, no Windows
    service, nothing installed outside this repository. Every action is idempotent, so
    re-running Start is safe.

.EXAMPLE
    ./scripts/pg-local.ps1
    ./scripts/pg-local.ps1 -Action Status
    ./scripts/pg-local.ps1 -Action Stop
#>
[CmdletBinding()]
[Diagnostics.CodeAnalysis.SuppressMessageAttribute(
    'PSAvoidUsingPlainTextForPassword', 'Password',
    Justification = 'Local dev cluster credential. It has to reach initdb and psql as plain text, and it is already committed in appsettings.Development.json and .env.example.')]
param(
    [ValidateSet('Start', 'Stop', 'Status')]
    [string]$Action = 'Start',

    [int]$Port = 5432,

    [string]$Database = 'neverorder',

    [string]$User = 'neverorder',

    [string]$Password = 'neverorder_dev_pw',

    # EnterpriseDB build tag, e.g. 16.10-1. Must match the major used by docker-compose.
    [string]$Version = '16.10-1'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# pg_ctl reports state through its exit code, so PowerShell 7.4+ must not turn one into an error.
$PSNativeCommandUseErrorActionPreference = $false

$repoRoot = Split-Path -Parent $PSScriptRoot
$baseDir = Join-Path $repoRoot '.postgres'
$installDir = Join-Path $baseDir 'pgsql'
$binDir = Join-Path $installDir 'bin'
$dataDir = Join-Path $baseDir 'data'
$logFile = Join-Path $baseDir 'postgres.log'
$pgCtl = Join-Path $binDir 'pg_ctl.exe'
$psql = Join-Path $binDir 'psql.exe'

# pg_ctl status: 0 = running, 3 = stopped, 4 = no cluster here.
function Get-ClusterState {
    if (-not (Test-Path $pgCtl) -or -not (Test-Path (Join-Path $dataDir 'PG_VERSION'))) {
        return 'Absent'
    }

    & $pgCtl status -D $dataDir *> $null
    switch ($LASTEXITCODE) {
        0 { 'Running' }
        3 { 'Stopped' }
        default { 'Absent' }
    }
}

function Invoke-Psql {
    param(
        [Parameter(Mandatory)][string]$Sql,
        [switch]$Scalar
    )

    $arguments = @('-U', 'postgres', '-h', '127.0.0.1', '-p', $Port, '-d', 'postgres', '-v', 'ON_ERROR_STOP=1')
    if ($Scalar) { $arguments += @('-tAc', $Sql) } else { $arguments += @('-c', $Sql) }

    $output = & $psql @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "psql failed: $output"
    }

    return ($output | Out-String).Trim()
}

function Install-Binaries {
    if (Test-Path $pgCtl) { return }

    $url = "https://get.enterprisedb.com/postgresql/postgresql-$Version-windows-x64-binaries.zip"
    $zipPath = Join-Path $baseDir "postgresql-$Version.zip"

    New-Item -ItemType Directory -Path $baseDir -Force | Out-Null
    Write-Host "Downloading PostgreSQL $Version (~320 MB, one time)..." -ForegroundColor Cyan

    $previousProgress = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'
    try {
        Invoke-WebRequest -Uri $url -OutFile $zipPath -UseBasicParsing
    }
    finally {
        $ProgressPreference = $previousProgress
    }

    Write-Host 'Extracting...' -ForegroundColor Cyan
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    # The archive already contains a top-level pgsql/ directory.
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $baseDir)
    Remove-Item $zipPath -Force
}

function Initialize-Cluster {
    if (Test-Path (Join-Path $dataDir 'PG_VERSION')) { return }

    Write-Host 'Initialising cluster...' -ForegroundColor Cyan

    # initdb only accepts the superuser password from a file; it is deleted immediately after.
    $pwFile = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
    Set-Content -Path $pwFile -Value $Password -NoNewline -Encoding ascii
    try {
        & (Join-Path $binDir 'initdb.exe') `
            -D $dataDir -U postgres --pwfile=$pwFile `
            -E UTF8 --locale=C --auth-host=scram-sha-256 | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'initdb failed.' }
    }
    finally {
        Remove-Item $pwFile -Force -ErrorAction SilentlyContinue
    }
}

function Start-Cluster {
    Write-Host "Starting PostgreSQL on localhost:$Port..." -ForegroundColor Cyan

    # 'localhost' expands to 127.0.0.1 and ::1 only, so the dev cluster is never reachable
    # from the network while still matching however Npgsql resolves the host name.
    # Quote the paths by hand: Start-Process joins -ArgumentList without quoting, which breaks
    # on a repo path containing spaces or parentheses.
    $arguments = 'start -D "{0}" -l "{1}" -w -t 120 -o "-p {2} -c listen_addresses=localhost"' -f
        $dataDir, $logFile, $Port

    # Start-Process also gives pg_ctl its own console. Run it inline and the server joins ours,
    # so closing the terminal sends it Ctrl+C and the cluster dies mid-query (0xC000013A).
    $ctl = Start-Process -FilePath $pgCtl -ArgumentList $arguments -WindowStyle Hidden -PassThru

    # Not -Wait: that waits on the whole process tree, which includes the server we just started.
    $ctl.WaitForExit()

    if ($ctl.ExitCode -ne 0) {
        throw "PostgreSQL failed to start (pg_ctl exit $($ctl.ExitCode)). See $logFile"
    }
}

function Set-ApplicationRoleAndDatabase {
    $escapedPassword = $Password.Replace("'", "''")
    $env:PGPASSWORD = $Password
    try {
        if (-not (Invoke-Psql -Scalar -Sql "SELECT 1 FROM pg_roles WHERE rolname = '$User'")) {
            Invoke-Psql -Sql "CREATE ROLE `"$User`" LOGIN PASSWORD '$escapedPassword'" | Out-Null
            Write-Host "Created role '$User'." -ForegroundColor Green
        }

        if (-not (Invoke-Psql -Scalar -Sql "SELECT 1 FROM pg_database WHERE datname = '$Database'")) {
            Invoke-Psql -Sql "CREATE DATABASE `"$Database`" OWNER `"$User`"" | Out-Null
            Write-Host "Created database '$Database'." -ForegroundColor Green
        }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

switch ($Action) {
    'Status' {
        $state = Get-ClusterState
        Write-Host "Cluster: $state"
        if ($state -eq 'Running') { Write-Host "Listening on localhost:$Port" }
    }

    'Stop' {
        if ((Get-ClusterState) -ne 'Running') {
            Write-Host 'Cluster is not running.'
            break
        }
        & $pgCtl stop -D $dataDir -m fast -w | Out-Null
        Write-Host 'Stopped.' -ForegroundColor Green
    }

    'Start' {
        Install-Binaries
        Initialize-Cluster

        if ((Get-ClusterState) -eq 'Running') {
            Write-Host 'Cluster is already running.'
        }
        else {
            Start-Cluster
        }

        Set-ApplicationRoleAndDatabase

        Write-Host ''
        Write-Host 'PostgreSQL is ready.' -ForegroundColor Green
        Write-Host "  Host=localhost;Port=$Port;Database=$Database;Username=$User;Password=$Password"
        Write-Host "  Log:  $logFile"
        Write-Host "  Stop: ./scripts/pg-local.ps1 -Action Stop"
    }
}
