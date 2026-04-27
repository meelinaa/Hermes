# Verschiebt alle Verzeichnisse im Repo-Root, deren Name mit "_out" beginnt,
# nach old-out-builds/ (Ad-hoc-Publish-Outputs — nicht Teil des normalen Builds).
# Ausführen vom Repository-Root:
#   powershell -ExecutionPolicy Bypass -File scripts/archive-root-out-folders.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot
$dest = Join-Path $repoRoot 'old-out-builds'
New-Item -ItemType Directory -Path $dest -Force | Out-Null

$moved = @(Get-ChildItem -Directory -Path $repoRoot | Where-Object { $_.Name -like '_out*' })
if ($moved.Count -eq 0) {
    Write-Host "Keine Ordner mit Namen _out* im Root gefunden."
    exit 0
}

foreach ($d in $moved) {
    Write-Host "Verschiebe $($d.Name) -> old-out-builds\"
    Move-Item -LiteralPath $d.FullName -Destination $dest -Force
}

Write-Host "Fertig ($($moved.Count) Ordner)."
