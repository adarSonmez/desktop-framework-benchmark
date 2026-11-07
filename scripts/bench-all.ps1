# Runs the harness over all built variants. Close other apps first; keep the machine plugged in.
param([int]$Runs = 10, [int]$IdleMs = 3000, [string]$Only)
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$args = @('--runs', $Runs, '--idle', $IdleMs, '--root', $root)
if ($Only) { $args += @('--only', $Only) }
dotnet (Join-Path $root 'bench/Harness/bin/Release/net10.0-windows/Harness.dll') @args
