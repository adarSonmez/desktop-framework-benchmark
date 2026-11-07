# Runs one variant once in benchmark mode and prints its result JSON. Usage: scripts/run-once.ps1 <exe> [args...]
param([Parameter(Mandatory)][string]$Exe, [string[]]$ExeArgs = @(), [int]$IdleMs = 1000, [int]$TimeoutSec = 120)
$out = Join-Path $env:TEMP "db-run-once.json"
$data = Join-Path $env:TEMP "db-run-once-items.json"
Remove-Item $out, "$out.ready" -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot '..\spec\data\items.json') $data -Force
$env:BENCH_OUT = $out; $env:BENCH_DATA = $data; $env:BENCH_IDLE_MS = "$IdleMs"
$p = if ($ExeArgs.Count) { Start-Process $Exe -ArgumentList $ExeArgs -PassThru } else { Start-Process $Exe -PassThru }
if (-not $p.WaitForExit($TimeoutSec * 1000)) { Write-Warning "timeout"; $p.Kill($true) }
Remove-Item Env:BENCH_OUT, Env:BENCH_DATA, Env:BENCH_IDLE_MS
if (Test-Path $out) { Get-Content $out } else { Write-Warning "no result (ready file: $(Test-Path "$out.ready"))" }
