param([string]$BuildFolder = (Join-Path (Split-Path $PSScriptRoot -Parent) 'Builds/Windows'), [string]$VoiceFixtures)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($BuildFolder)
$exe = Join-Path $root 'EchoSound.exe'
$logs = Join-Path (Split-Path $PSScriptRoot -Parent) '.utmp'
if (!(Test-Path -LiteralPath $exe)) { throw "Build the Windows companion first: $exe" }

function Run-Check($Flag, $LogName) {
    $log = Join-Path $logs $LogName
    $arguments = "-batchmode -nographics $Flag -logFile `"$log`""
    if ($VoiceFixtures) { $arguments += " -ai-voice-fixtures `"$([IO.Path]::GetFullPath($VoiceFixtures))`"" }
    $game = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(240)
        while (!$game.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $deadline) { throw "Game integration test timed out: $log" }
        }
        $output = Get-Content -LiteralPath $log -Raw
        if ($game.ExitCode -ne 0 -or !$output.Contains('OFFLINE AI SMOKE PASSED:')) { throw "Game integration test failed: $log" }
        # Restrict the check to engines belonging to this build, never a user's other inference service.
        $remaining = @(Get-Process llama-server,whisper-server -ErrorAction SilentlyContinue | Where-Object {
            $_.Path -and $_.Path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)
        })
        if ($remaining.Count -gt 0) { throw "AI processes survived the game exit: $($remaining.Id)" }
        Write-Output "PASS $Flag (game exited; no bundled AI processes remain)"
    }
    finally {
        if (!$game.HasExited) { $game.Kill(); $game.WaitForExit(5000) | Out-Null }
        $game.Dispose()
    }
}

Run-Check '-ai-smoke-test' 'ai-player.log'
$model = Join-Path $root 'EchoSound_Data/StreamingAssets/AI/models/Qwen_Qwen3-1.7B-Q4_K_M.gguf'
$disabled = $model + '.test-disabled'
if (Test-Path -LiteralPath $disabled) { throw "A previous test did not restore $disabled. Restore it before running again." }
Move-Item -LiteralPath $model -Destination $disabled
try { Run-Check '-ai-smoke-fallback' 'ai-fallback.log' }
finally { Move-Item -LiteralPath $disabled -Destination $model }
