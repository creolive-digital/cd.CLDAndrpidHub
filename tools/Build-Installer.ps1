param([string]$Compiler = (Join-Path $PSScriptRoot '..\.tools\InnoSetup\ISCC.exe'))
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not (Test-Path -LiteralPath $Compiler)) { throw 'Install Inno Setup 6 and supply its ISCC.exe path using -Compiler.' }
Push-Location $projectRoot
try {
    dotnet publish AndroidHub.csproj -c Release -o build/win-x64 --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    & $Compiler (Join-Path $projectRoot 'installer\CLD-Android-Hub.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $artifact = Join-Path $projectRoot 'dist\CLD-Android-Hub-2.1.0-Setup-x64.exe'
    $hash = (Get-FileHash -LiteralPath $artifact -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  CLD-Android-Hub-2.1.0-Setup-x64.exe" | Set-Content (Join-Path $projectRoot 'dist\SHA256SUMS.txt')
    Get-Item -LiteralPath $artifact
}
finally { Pop-Location }
