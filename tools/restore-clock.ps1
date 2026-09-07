<#
.SYNOPSIS
  Возврат оригинальных "Часов": снять модифицированную копию, зарегистрировать
  эталонную копию оригинала (main-orig), восстановить данные будильников.
  Запускается от администратора (UAC из приложения).
#>
param([string]$ResultFile)
$ErrorActionPreference = 'Continue'
try {
    $state = Get-Content (Join-Path $env:LOCALAPPDATA 'AlarmSoundStudio\deploy-state.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $origCopy = $null
    foreach ($candDir in @($state.WorkRoot, (Join-Path C:\Users\MoneyFarming 'Documents\Same\AlarmSoundStudio\mod'), (Join-Path C:\Users\MoneyFarming\AppData\Roaming 'AlarmSoundStudio\mod'))) {
        if (Test-Path (Join-Path $candDir 'main-orig\AppxManifest.xml')) { $origCopy = Join-Path $candDir 'main-orig'; break }
    }
    $mani = Join-Path $origCopy 'AppxManifest.xml'
    if (-not (Test-Path $mani)) { throw 'эталонная копия main-orig не найдена' }

    # снять модифицированную копию (всех пользователей)
    Get-AppxPackage -AllUsers Microsoft.WindowsAlarms | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    # вернуть оригинал (loose, но с НЕпатченным PRI и оригинальной версией)
    Add-AppxPackage -Register $mani -ErrorAction Stop
    # данные будильников
    $dataDir = "$env:LOCALAPPDATA\Packages\Microsoft.WindowsAlarms_8wekyb3d8bbwe"
    $bk = Join-Path $state.WorkRoot 'data-backup'
    if (Test-Path "$bk\Settings\settings.dat") {
        if (-not (Test-Path "$dataDir\Settings")) { New-Item -ItemType Directory -Path "$dataDir\Settings" -Force | Out-Null }
        Copy-Item "$bk\Settings\settings.dat" "$dataDir\Settings\settings.dat" -Force
    }
    if (Test-Path "$bk\LocalState") {
        if (-not (Test-Path "$dataDir\LocalState")) { New-Item -ItemType Directory -Path "$dataDir\LocalState" -Force | Out-Null }
        Copy-Item "$bk\LocalState\*" "$dataDir\LocalState\" -Recurse -Force -ErrorAction SilentlyContinue
    }
    Start-Process explorer.exe -ArgumentList 'shell:AppsFolder\Microsoft.WindowsAlarms_8wekyb3d8bbwe!App'
    $res = '{"status":"ok","restored":true}'
    if ($ResultFile) { [IO.File]::WriteAllText($ResultFile, $res, [Text.Encoding]::UTF8) }
} catch {
    $em = $_.Exception.Message
    if ($_.Exception.InnerException) { $em = $_.Exception.InnerException.Message }
    $res = ('{"status":"error","message":' + ($em | ConvertTo-Json -Compress) + '}')
    if ($ResultFile) { [IO.File]::WriteAllText($ResultFile, $res, [Text.Encoding]::UTF8) }
}
