# Builds every variant in release mode. Requires: Node 22+, Rust (MSVC), Flutter, .NET 10 + maui-windows workload,
# Visual Studio C++ desktop workload + Windows SDK 10.0.26100.
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
function Step($name, $dir, [scriptblock]$body) {
  Write-Host "== $name" -ForegroundColor Cyan
  Push-Location (Join-Path $root $dir); try { & $body } finally { Pop-Location }
}
$env:PATH = "$env:USERPROFILE\.cargo\bin;C:\src\flutter\bin;$env:PATH"

Step 'electron'     'apps/electron'     { npm ci; npm run build }
Step 'tauri'        'apps/tauri'        { npm ci; npx tauri build }
Step 'flutter'      'apps/flutter'      { flutter build windows --release }
Step 'react-native' 'apps/react-native' { npm ci; npx @react-native-community/cli run-windows --release --arch x64 --no-launch --no-packager --no-deploy --msbuildprops WindowsTargetPlatformVersion=10.0.26100.0 }
Step 'maui-xaml'    'apps/maui-xaml'    { dotnet publish -c Release -o (Join-Path $root 'artifacts/maui-xaml') }
Step 'maui-blazor'  'apps/maui-blazor'  { dotnet publish -c Release -o (Join-Path $root 'artifacts/maui-blazor') }
Step 'harness'      'bench/Harness'     { dotnet build -c Release }
