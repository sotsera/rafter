if (-not ('Rafter.Verification.VerificationProcess' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'VerificationProcess.cs')
}

function Invoke-VerificationProcess {
    param(
        [Parameter(Mandatory)][Diagnostics.ProcessStartInfo]$StartInfo,
        [Parameter(Mandatory)][double]$TimeoutSeconds,
        [Parameter(Mandatory)][string]$EvidenceDirectory
    )
    try {
        return [Rafter.Verification.VerificationProcess]::RunAsync($StartInfo,
            [TimeSpan]::FromSeconds($TimeoutSeconds), [Threading.CancellationToken]::None).GetAwaiter().GetResult()
    }
    catch {
        $failure = $_.Exception
        while ($failure.InnerException -and $failure -isnot [Rafter.Verification.VerificationProcessException]) {
            $failure = $failure.InnerException
        }
        $record = Join-Path $EvidenceDirectory ('process-failure-' + [Guid]::NewGuid().ToString('N') + '.json')
        $stdout = ''
        $stderr = ''
        if ($failure -is [Rafter.Verification.VerificationProcessException]) {
            $stdout = $failure.StandardOutput
            $stderr = $failure.StandardError
        }
        [ordered]@{
            Executable = $StartInfo.FileName
            Failure = $failure.ToString()
            StandardOutput = $stdout
            StandardError = $stderr
        } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $record -Encoding utf8
        Write-Host "Verification failure evidence: $record"
        throw
    }
}
