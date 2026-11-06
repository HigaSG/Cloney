# Compila o Cloney e gera o instalador em dist\Cloney-Setup-<versão>.exe
# Uso: powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$version = '0.1.0'

$ico = Join-Path $root 'src\Cloney\Assets\cloney.ico'
if (-not (Test-Path $ico)) {
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'tools\make-app-icon.ps1')
}

$publish = Join-Path $root 'publish'
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }

dotnet publish (Join-Path $root 'src\Cloney\Cloney.csproj') -c Release -r win-x64 --self-contained true `
    -p:Version=$version -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou ($LASTEXITCODE)" }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 não encontrado. Instale com: winget install JRSoftware.InnoSetup' }

& $iscc "/DAppVersion=$version" (Join-Path $root 'installer\Cloney.iss')
if ($LASTEXITCODE -ne 0) { throw "ISCC falhou ($LASTEXITCODE)" }

Write-Host "`nInstalador pronto: $(Join-Path $root "dist\Cloney-Setup-$version.exe")" -ForegroundColor Green
