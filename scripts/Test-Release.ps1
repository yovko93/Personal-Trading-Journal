# Requires a completed Release build. This is the complete local and CI test gate,
# not a filtered acceptance shortcut. The two filters are exact complements.
[CmdletBinding()]
param([string]$ResultsDirectory = 'TestResults')

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repository = Split-Path $PSScriptRoot -Parent
$resultsRoot = [IO.Path]::GetFullPath($ResultsDirectory, $repository)
$run = Join-Path $resultsRoot ('release-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$stages = @(
    @{ Name = 'exclusive-wpf'; Target = 'tests/PersonalTradingJournal.Desktop.Tests/PersonalTradingJournal.Desktop.Tests.csproj'; Filter = 'Workload=ExclusiveWpf'; Projects = 1 },
    @{ Name = 'parallel'; Target = 'PersonalTradingJournal.sln'; Filter = 'Workload!=ExclusiveWpf'; Projects = 4 }
)
$originalResults = $env:PTJ_TEST_RESULTS_DIRECTORY
$originalTimings = $env:PTJ_CALENDAR_TIMINGS
$records = [Collections.Generic.List[object]]::new()
$identities = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$exitCode = 0
Push-Location $repository
try {
    foreach ($stage in $stages) {
        $directory = Join-Path $run $stage.Name
        New-Item -ItemType Directory -Path $directory | Out-Null
        $env:PTJ_TEST_RESULTS_DIRECTORY = $directory
        $env:PTJ_CALENDAR_TIMINGS = '1'
        $started = [DateTimeOffset]::UtcNow
        Write-Host "Starting $($stage.Name): $($stage.Filter) at $($started.ToString('O'))"
        $arguments = @('test', $stage.Target, '-c', 'Release', '--no-build', '--no-restore',
            '--filter', $stage.Filter, '--logger', 'trx', '--results-directory', $directory,
            '--blame-hang', '--blame-hang-timeout', '3m', '--blame-hang-dump-type', 'mini',
            '--diag', (Join-Path $directory 'vstest.log'))
        & dotnet @arguments | Tee-Object -FilePath (Join-Path $directory 'test-output.log')
        $result = $LASTEXITCODE
        if ($result -ne 0) { $exitCode = 1 }
        # Read only top-level TRX: supervised child results are evidence, not
        # extra cases in the complete-set totals. Missing/empty/skipped results
        # fail the gate, as do duplicate test identities across the partitions.
        $files = @(Get-ChildItem -LiteralPath $directory -Filter '*.trx' -File)
        if ($files.Count -ne $stage.Projects) {
            Write-Warning "Expected $($stage.Projects) project results in $($stage.Name), found $($files.Count)."
            $exitCode = 1
        }
        foreach ($file in $files) {
            [xml]$trx = Get-Content -LiteralPath $file.FullName -Raw
            $counts = $trx.TestRun.ResultSummary.Counters
            $tests = @($trx.TestRun.Results.UnitTestResult)
            if ([int]$counts.total -eq 0 -or [int]$counts.notExecuted -ne 0 -or
                [int]$counts.failed -ne 0 -or [int]$counts.executed -ne [int]$counts.total) { $exitCode = 1 }
            foreach ($test in $tests) {
                $identity = "$($test.testId)|$($test.testName)"
                if (!$identities.Add($identity)) {
                    Write-Warning "A test ran in both Release partitions: $($test.testName)"
                    $exitCode = 1
                }
            }
            $storage = @($trx.TestRun.TestDefinitions.UnitTest)[0].storage
            $records.Add([pscustomobject]@{
                Stage = $stage.Name; Project = [IO.Path]::GetFileNameWithoutExtension($storage)
                Total = [int]$counts.total; Passed = [int]$counts.passed
                Failed = [int]$counts.failed; Skipped = [int]$counts.notExecuted
                StageStartedUtc = $started.ToString('O'); StageFinishedUtc = [DateTimeOffset]::UtcNow.ToString('O')
            })
        }
        $records | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding utf8
        # Even after test failures run the other partition once, so the gate
        # reports the complete set rather than hiding unrelated results.
    }
    $records | Format-Table Stage, Project, Total, Passed, Failed, Skipped -AutoSize
    Write-Host "Complete Release results: $run"
}
finally {
    $env:PTJ_TEST_RESULTS_DIRECTORY = $originalResults
    $env:PTJ_CALENDAR_TIMINGS = $originalTimings
    Pop-Location
}
exit $exitCode
