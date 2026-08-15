#!/usr/bin/env bash
#
# Runs every test suite with coverage and merges the results into one report.
#
# `dotnet test` writes one coverage file per test project, so reading any single file gives a
# figure for a slice of the solution rather than for the solution. Domain and Application are
# covered by the unit suite, Api by the controller and middleware unit tests, and Infrastructure
# almost entirely by the Testcontainers integration suite. ReportGenerator merges all three.
#
# The integration suite needs a Docker daemon. Without one its tests skip rather than fail (see
# DockerFactAttribute) and Infrastructure coverage reads far lower than it is — so this script
# says so, rather than letting the number be read as a verdict.
#
# Usage:  ./coverage.sh [--no-build]

set -euo pipefail

cd "$(dirname "$0")"

output="$PWD/artifacts/coverage"

step() { printf '\033[36m==> %s\033[0m\n' "$1"; }
warn() { printf '\033[33m  ! %s\033[0m\n' "$1"; }

if ! docker info --format '{{.ServerVersion}}' > /dev/null 2>&1; then
    warn 'No Docker daemon: the integration tests will skip and Infrastructure coverage will'
    warn 'be reported far below its real value. Start Docker for a figure worth quoting.'
fi

step 'Restoring local tools'
dotnet tool restore

step 'Clearing previous results'
find . -type d -name TestResults -prune -exec rm -rf {} + 2>/dev/null || true
rm -rf "$output"

if [ "${1:-}" != "--no-build" ]; then
    step 'Building'
    dotnet build Catalog.slnx -c Release
fi

step 'Running tests with coverage'
tests_failed=0
dotnet test Catalog.slnx -c Release --no-build \
    --settings coverlet.runsettings \
    --collect:"XPlat Code Coverage" || tests_failed=1

step 'Merging coverage reports'
dotnet reportgenerator \
    "-reports:**/TestResults/**/coverage.cobertura.xml" \
    "-targetdir:$output" \
    "-reporttypes:Html;TextSummary;Cobertura;MarkdownSummaryGithub" \
    "-verbosity:Warning"

head -n 16 "$output/Summary.txt"

echo
echo "  Full report  $output/index.html"

# A merged report from a red run describes code that does not work. Surface that as the exit code.
if [ "$tests_failed" -ne 0 ]; then
    warn 'Tests failed. The coverage figures above describe a failing build.'
    exit 1
fi
