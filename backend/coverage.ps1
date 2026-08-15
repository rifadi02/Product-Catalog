<#
.SYNOPSIS
    Runs every test suite with coverage and merges the results into one report.

.DESCRIPTION
    `dotnet test` writes one coverage file per test project, so reading any single file gives a
    figure for a slice of the solution rather than for the solution. Domain and Application are
    covered by the unit suite, Api by the controller and middleware unit tests, and Infrastructure
    almost entirely by the Testcontainers integration suite — quoting one report would understate
    three assemblies and overstate none of them usefully. ReportGenerator merges all three.

    The integration suite needs a Docker daemon. Without one its tests skip rather than fail (see
    DockerFactAttribute), and Infrastructure coverage will read far lower than it is — this script
    says so rather than letting the number be read as a verdict.

.PARAMETER NoBuild
    Skip the build, for a re-run after a build that already succeeded.

.PARAMETER Open
    Open the HTML report when it is finished.

.EXAMPLE
    ./coverage.ps1
.EXAMPLE
    ./coverage.ps1 -Open
#>
[CmdletBinding()]
param([switch]$NoBuild, [switch]$Open)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Write-Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Write-Warn($message) { Write-Host "  ! $message" -ForegroundColor Yellow }

$output = Join-Path $PSScriptRoot 'artifacts/coverage'

docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Warn 'No Docker daemon: the integration tests will skip and Infrastructure coverage will'
    Write-Warn 'be reported far below its real value. Start Docker for a figure worth quoting.'
}

Write-Step 'Restoring local tools'
dotnet tool restore

Write-Step 'Clearing previous results'
Get-ChildItem -Path . -Recurse -Directory -Filter 'TestResults' |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $output -Recurse -Force -ErrorAction SilentlyContinue

if (-not $NoBuild) {
    Write-Step 'Building'
    dotnet build Catalog.slnx -c Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Step 'Running tests with coverage'
dotnet test Catalog.slnx -c Release --no-build `
    --settings coverlet.runsettings `
    --collect:'XPlat Code Coverage'

$testsFailed = $LASTEXITCODE -ne 0

Write-Step 'Merging coverage reports'
dotnet reportgenerator `
    '-reports:**/TestResults/**/coverage.cobertura.xml' `
    "-targetdir:$output" `
    '-reporttypes:Html;TextSummary;Cobertura;MarkdownSummaryGithub' `
    '-verbosity:Warning'

Get-Content (Join-Path $output 'Summary.txt') |
    Select-Object -First 16 |
    ForEach-Object { Write-Host $_ }

Write-Host ''
Write-Host "  Full report  $(Join-Path $output 'index.html')"

if ($Open) { Invoke-Item (Join-Path $output 'index.html') }

# A merged report from a red run describes code that does not work. Surface that as the exit code.
if ($testsFailed) {
    Write-Warn 'Tests failed. The coverage figures above describe a failing build.'
    exit 1
}
