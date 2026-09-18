param(
    [Parameter(Mandatory = $true)][scriptblock]$Command,
    [string]$EnvFile = (Join-Path $PSScriptRoot '../.env')
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $EnvFile) {
    foreach ($line in Get-Content -LiteralPath $EnvFile) {
        $trimmed = $line.Trim()
        if (!$trimmed -or $trimmed.StartsWith('#')) { continue }
        if ($trimmed -notmatch '^([A-Z][A-Z0-9_]*)=(.*)$') { throw 'Linha inválida no arquivo .env (use CHAVE=valor).' }
        $key = $Matches[1]
        $value = $Matches[2].Trim()
        if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) { $value = $value.Substring(1, $value.Length - 2) }
        [Environment]::SetEnvironmentVariable($key, $value, 'Process')
    }
}
Push-Location (Join-Path $PSScriptRoot '..')
try { & $Command; if ($LASTEXITCODE -ne 0) { throw "O comando terminou com código $LASTEXITCODE." } }
finally { Pop-Location }
