$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location (Join-Path $projectRoot 'backend')
try {
    dotnet tool restore
    if ($LASTEXITCODE) { throw 'Falha no restore das ferramentas .NET.' }
    dotnet restore MinhaEscala.slnx
    if ($LASTEXITCODE) { throw 'Falha no restore .NET.' }
    dotnet build MinhaEscala.slnx --no-restore -c Release
    if ($LASTEXITCODE) { throw 'Falha no build .NET.' }
    dotnet test MinhaEscala.slnx --no-build -c Release
    if ($LASTEXITCODE) { throw 'Falha nos testes .NET.' }
} finally { Pop-Location }
Push-Location (Join-Path $projectRoot 'frontend')
try {
    npm.cmd ci
    if ($LASTEXITCODE) { throw 'Falha no restore npm.' }
    foreach ($task in @('lint', 'build', 'test')) {
        npm.cmd run $task
        if ($LASTEXITCODE) { throw "Falha em npm run $task." }
    }
} finally { Pop-Location }
