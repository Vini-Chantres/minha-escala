param([string]$Destination = (Join-Path $PSScriptRoot '../publish'))
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location (Join-Path $projectRoot 'frontend')
try {
    npm.cmd ci
    if ($LASTEXITCODE) { throw 'Falha no restore frontend.' }
    npm.cmd run build
    if ($LASTEXITCODE) { throw 'Falha no build frontend.' }
} finally { Pop-Location }
dotnet publish (Join-Path $projectRoot 'backend/Api/Api.csproj') -c Release -o $Destination
if ($LASTEXITCODE) { throw 'Falha no publish .NET.' }
New-Item -ItemType Directory -Force -Path (Join-Path $Destination 'wwwroot') | Out-Null
Copy-Item -Path (Join-Path $projectRoot 'frontend/dist/*') -Destination (Join-Path $Destination 'wwwroot') -Recurse -Force
Write-Host "Aplicação pronta em $Destination. Configure as variáveis no servidor e use HTTPS."
