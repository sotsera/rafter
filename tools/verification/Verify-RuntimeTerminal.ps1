param([switch]$RedirectedOnly)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../shared/VerificationProcess.ps1')
if (-not $RedirectedOnly -and ([Console]::IsOutputRedirected -or [Console]::IsErrorRedirected)) {
    throw 'Run in a terminal/pseudoterminal with both streams attached, or select -RedirectedOnly.'
}
$repository = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$runId = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N')
$evidence = Join-Path $repository ("artifacts/evidence/verification/runtime-terminal/" + $runId)
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
Write-Host "Evidence: $evidence"
$assembly = Join-Path $repository 'tests/testassets/consumers/runtime/project/bin/Release/net10.0/Consumer.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw 'Build tests/testassets/consumers/runtime/project/Consumer.csproj in Release first.' }
$results = @()

function Invoke-ReviewCase([string]$Scenario, [string]$Mode, [bool]$RedirectOutput, [bool]$RedirectError, [int]$Expected) {
    $id = "$Scenario-$Mode-$RedirectOutput-$RedirectError"
    $recordPath = Join-Path $evidence "$id.json"
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.WorkingDirectory = $repository
    $start.RedirectStandardOutput = $RedirectOutput
    $start.RedirectStandardError = $RedirectError
    $start.Environment['RAFTER_CAPTURE'] = $recordPath
    $start.Environment['TERM'] = 'xterm-256color'
    $start.Environment.Remove('NO_COLOR') | Out-Null
    if ($Mode -eq 'no-color') { $start.Environment['NO_COLOR'] = '1' }
    foreach ($argument in @($assembly, $Scenario)) { $start.ArgumentList.Add($argument) }
    if ($Mode -in @('plain', 'quiet', 'verbose')) { $start.ArgumentList.Add("--$Mode") }
    if ($Scenario -eq 'review-help') { $start.ArgumentList.Add('--help') }
    $completed = Invoke-VerificationProcess $start 30 $evidence
    if ($completed.ExitCode -ne $Expected) { throw "Unexpected exit $($completed.ExitCode): $id" }
    $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
    if ($record.ExitCode -ne $Expected -or $record.OutputRedirected -ne $RedirectOutput -or $record.ErrorRedirected -ne $RedirectError) {
        throw "Wrong recorded exit or handle state: $id"
    }
    foreach ($stream in @('Output', 'Error')) {
        $redirected = if ($stream -eq 'Output') { $RedirectOutput } else { $RedirectError }
        if (($redirected -or $Mode -eq 'plain') -and $record.$stream.Contains([char]27)) { throw "ANSI leaked: $id $stream" }
        if ($Mode -eq 'no-color' -and $record.$stream -match '\x1b\[(?:38|48);|\x1b\[(?:3[0-7]|4[0-7]|9[0-7]|10[0-7])m') {
            throw "Color leaked with NO_COLOR: $id $stream"
        }
    }
    $stdout = $record.Output -replace '\x1b\[[0-9;]*m', ''
    $stderr = $record.Error -replace '\x1b\[[0-9;]*m', ''
    if ($Scenario -eq 'review-help') {
        if ($stderr -or $stdout -notlike '*Command options*' -or $stdout -notlike '*[[]red]brackets[[]/]*' -or $stdout -notlike '*Body order: build, test.*') {
            throw "Wrong help content: $id"
        }
    }
    elseif ($Scenario.StartsWith('review-capture-')) {
        if ($stdout -match 'private stdout|x{20}' -or $stderr -match 'private stderr|x{20}') {
            throw "Captured text replayed automatically: $id"
        }
        if ($Mode -eq 'quiet' -and $stderr.Contains('Process |')) { throw "Quiet lifecycle leaked: $id" }
        $overflow = $Scenario -in @('review-capture-overflow', 'review-capture-caught')
        if ($stderr.Contains('Capture limit exceeded | dotnet') -ne ($overflow -and $Mode -ne 'quiet')) {
            throw "Wrong overflow progress: $id"
        }
        if ($Scenario -eq 'review-capture-overflow' -and
            ($stderr -notlike '*Process capture limit exceeded | dotnet*' -or
             $stderr -notlike '*Partial capture (UTF-16 code units): stdout 1024 | stderr 0*')) {
            throw "Wrong capture failure: $id"
        }
        if ($Scenario -eq 'review-capture-reject' -and
            ($stderr -notlike '*Exit code: 7*' -or $stderr -notlike '*Capture (UTF-16 code units): stdout 14 | stderr 14*')) {
            throw "Wrong complete capture rejection: $id"
        }
        if ($Scenario -eq 'review-capture-caught' -and
            ($stderr.Contains('Process capture limit exceeded') -or $stdout -notlike '*Recovered capture: 1024.*')) {
            throw "Wrong recovered capture: $id"
        }
        if ($Scenario -eq 'review-capture-success' -and
            ($stdout -notlike '*Captured stdout: 14.*' -or
             $stderr.Contains('Capture (UTF-16 code units)') -ne ($Mode -eq 'verbose'))) {
            throw "Wrong successful capture: $id"
        }
        if ($Expected -eq 1 -and $stderr -notlike '*[[]Failed]*') { throw "Missing failed summary: $id" }
    }
    else {
        $richOutput = -not $RedirectOutput -and $Mode -ne 'plain'
        if ($stdout.Contains('  > ') -ne $richOutput -and $Scenario -ne 'review-launch') { throw "Wrong child prefix: $id" }
        if ($Mode -eq 'quiet' -and ($stderr.Contains('Process |') -or $stderr.Contains('[build]'))) { throw "Quiet lifecycle leaked: $id" }
        if ($Expected -eq 1 -and $stderr -notlike '*[[]Failed]*') { throw "Missing failed summary: $id" }
        if ($Mode -ne 'quiet' -and $Scenario -eq 'review-success' -and $stderr -notlike '*[[]test] <-- (build)*') { throw "Missing dependency heading: $id" }
    }
    return [pscustomobject]@{ Case = $id; ExitCode = $Expected; Passed = $true; Record = $recordPath }
}

$redirects = if ($RedirectedOnly) { @($true) } else { @($false, $true) }
foreach ($mode in @('normal', 'plain', 'no-color', 'quiet', 'verbose')) {
    foreach ($out in $redirects) {
        foreach ($err in $redirects) {
            $results += Invoke-ReviewCase 'review-success' $mode $out $err 0
        }
    }
}
foreach ($scenario in @('review-help', 'review-continue', 'review-fail', 'review-throw', 'review-handler', 'review-launch')) {
    $expected = if ($scenario -in @('review-help', 'review-continue')) { 0 } else { 1 }
    foreach ($mode in @('normal', 'plain', 'quiet')) {
        $results += Invoke-ReviewCase $scenario $mode $RedirectedOnly.IsPresent $RedirectedOnly.IsPresent $expected
    }
}
foreach ($scenario in @('review-capture-success', 'review-capture-overflow', 'review-capture-reject', 'review-capture-caught')) {
    $expected = if ($scenario -in @('review-capture-overflow', 'review-capture-reject')) { 1 } else { 0 }
    foreach ($mode in @('normal', 'plain', 'no-color', 'quiet', 'verbose')) {
        foreach ($out in $redirects) {
            foreach ($err in $redirects) {
                $results += Invoke-ReviewCase $scenario $mode $out $err $expected
            }
        }
    }
}
$resultName = if ($RedirectedOnly) { 'results-redirected.json' } else { 'results-terminal.json' }
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $evidence $resultName) -Encoding utf8
Write-Output "Runtime cases passed: $($results.Count). Evidence: $evidence"
