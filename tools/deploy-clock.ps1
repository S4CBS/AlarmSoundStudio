<#
.SYNOPSIS
  Автоматическое развертывание модифицированных "Часов" с кастомными именами звуков.
  Фаза 1 (обычные права): копия пакета, патч PRI, бэкап, версия+1
  Фаза 2 (UAC):          снять оригинал со всех пользователей, убрать provisioning
  Фаза 3 (обычные права): Register копии, восстановить данные будильников, запуск
#>
param(
    [Parameter(Mandatory = $true)][ValidateSet('1', '2', '3')][string]$Phase,
    [string]$StateFile,
    [string]$RenamesJson,   # {"1":"02601","3":"02601"} слот -> новое имя
    [string]$ResultFile
)

$ErrorActionPreference = 'Stop'
try {
    [Console]::OutputEncoding = [Text.Encoding]::UTF8
    $state = Get-Content $StateFile -Raw -Encoding UTF8 | ConvertFrom-Json

    if ($Phase -eq '1') {
        # ---- Фаза 1: подготовка (без админа) ----
        $work = Join-Path $state.WorkRoot 'main'
        if (Test-Path $work) { Remove-Item $work -Recurse -Force }
        # источник: если активен наш мод - используем эталон main-orig; иначе оригинал из WindowsApps
        $origCopy = Join-Path $state.WorkRoot 'main-orig'
        $orig = Get-AppxPackage Microsoft.WindowsAlarms | Select-Object -First 1
        if ($orig -and ($orig.InstallLocation -like "*$($state.WorkRoot)*") -and -not (Test-Path (Join-Path $origCopy 'AppxManifest.xml'))) {
            throw 'активен мод, но эталон main-orig не найден - сначала «Вернуть оригинал»'
        }
        if (Test-Path (Join-Path $origCopy 'AppxManifest.xml')) {
            $src = $origCopy
        } elseif ($orig) {
            $src = $orig.InstallLocation
            Copy-Item $src $origCopy -Recurse -Force   # сохраняем эталон для отката
        } else {
            throw 'пакет Часов не найден'
        }
        Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
        Copy-Item $src $work -Recurse -Force

        # бэкап данных будильников (переживет удаление пакета)
        $dataDir = "$env:LOCALAPPDATA\Packages\Microsoft.WindowsAlarms_8wekyb3d8bbwe"
        if (Test-Path $dataDir) {
            $bk = Join-Path $state.WorkRoot 'data-backup'
            if (Test-Path $bk) { Remove-Item $bk -Recurse -Force }
            Copy-Item $dataDir $bk -Recurse -Force
        }

        # патч PRI: EN-имена, кластер ищем по якорю Xylophone (уникальное длинное имя)
        $priPath = Join-Path $work 'resources.pri'
        $b = [IO.File]::ReadAllBytes($priPath)
        $lat = [Text.Encoding]::GetEncoding(28591).GetString($b)
        $utf8 = [Text.Encoding]::UTF8
        $anchorPat = [Text.Encoding]::GetEncoding(28591).GetString($utf8.GetBytes('Xylophone'))
        $am = [regex]::Matches($lat, [regex]::Escape($anchorPat))
        if ($am.Count -ne 1) { throw "якорь Xylophone найден $($am.Count) раз, ожидался 1" }
        $anchor = $am[0].Index

        $enNames = @('Chimes','Xylophone','Chords','Tap','Jingle','Transition','Descending','Bounce','Echo','Ascending')
        $renames = Get-Content $RenamesJson -Raw -Encoding UTF8 | ConvertFrom-Json
        $props = $renames.PSObject.Properties | Sort-Object { [int]$_.Name }

        foreach ($prop in $props) {
            $slot = [int]$prop.Name
            $newName = [string]$prop.Value
            if ($slot -lt 1 -or $slot -gt 10 -or $newName -eq '') { continue }
            $enName = $enNames[$slot - 1]
            $pat = [Text.Encoding]::GetEncoding(28591).GetString($utf8.GetBytes($enName))
            # все вхождения, в кластере (+-64 байта от якоря), перед которыми NUL, после которых NUL
            $candidates = [regex]::Matches($lat, [regex]::Escape($pat)) | Where-Object {
                $p = $_.Index
                ($p -ge $anchor - 64) -and ($p -le $anchor + 64) -and
                ($p % 4 -eq 0) -and
                ($p -gt 0) -and ($b[$p - 1] -eq 0) -and
                ($p + $pat.Length -lt $b.Length) -and ($b[$p + $pat.Length] -eq 0)
            }
            $newPat = [Text.Encoding]::GetEncoding(28591).GetString($utf8.GetBytes($newName))
            $already = [regex]::Matches($lat, [regex]::Escape($newPat)) | Where-Object {
                $p = $_.Index
                ($p -ge $anchor - 64) -and ($p -le $anchor + 64) -and ($p % 4 -eq 0) -and
                ($p -gt 0) -and ($b[$p - 1] -eq 0) -and ($b[$p + $newPat.Length] -eq 0)
            }
            if ($already.Count -eq 1) { $skipped = $true; continue }
            if ($candidates.Count -ne 1) { throw "слот ${slot}: не найдено уникальное место для '$enName'" }
            $off = $candidates[0].Index
            $nb = $utf8.GetBytes($newName)
            if ($nb.Length -gt $enName.Length) { throw "слот ${slot}: имя '$newName' длиннее '$enName'" }
            $i = $off
            foreach ($byte in $nb) { $b[$i] = $byte; $i++ }
            while ($i -le ($off + $enName.Length)) { $b[$i] = 0; $i++ }
        }
        [IO.File]::WriteAllBytes($priPath, $b)

        # версия +1 в полевой разряд
        $maniPath = Join-Path $work 'AppxManifest.xml'
        $mani = Get-Content $maniPath -Raw -Encoding UTF8
        $ver = [regex]::Match($mani, 'Version="([\d\.]+)"').Groups[1].Value
        $parts = $ver.Split('.')
        $parts[2] = [string]([int]$parts[2] + 1)
        $newVer = $parts -join '.'
        $mani = $mani -replace ('Version="' + $ver + '"'), ('Version="' + $newVer + '"')
        [IO.File]::WriteAllText($maniPath, $mani, (New-Object Text.UTF8Encoding $false))
        $newState = ('{{"workroot":"{0}","version":"{1}","work":"{2}"}}' -f $state.WorkRoot.Replace('\','\\'), $newVer, $work.Replace('\','\\'))
        [IO.File]::WriteAllText($StateFile, $newState, (New-Object Text.UTF8Encoding $false))

        [Console]::WriteLine('{"status":"ok","phase":1,"version":"' + $newVer + '"}')
        exit 0
    }

    if ($Phase -eq '2') {
        # ---- Фаза 2: снять оригинал (UAC) ----
        Get-AppxPackage -AllUsers Microsoft.WindowsAlarms | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        $prov = Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -match 'WindowsAlarms' }
        foreach ($p in $prov) { Remove-AppxProvisionedPackage -Online -PackageName $p.PackageName | Out-Null }
        Start-Sleep -Seconds 2
        $left = (Get-AppxPackage -AllUsers Microsoft.WindowsAlarms | Measure-Object).Count
        [Console]::WriteLine(('{"status":"ok","phase":2,"remaining":' + $left + '}'))
        exit 0
    }

    if ($Phase -eq '3') {
        # ---- Фаза 3: регистрация + данные + запуск (без админа) ----
        $work = Join-Path $state.WorkRoot 'main'
        Add-AppxPackage -Register (Join-Path $work 'AppxManifest.xml') -ErrorAction Stop
        # данные будильников из бэкапа
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
        # запуск
        Start-Process explorer.exe -ArgumentList 'shell:AppsFolder\Microsoft.WindowsAlarms_8wekyb3d8bbwe!App'
        [Console]::WriteLine('{"status":"ok","phase":3}')
        exit 0
    }
} catch {
    $em = $_.Exception.Message
    if ($_.Exception.InnerException) { $em = $_.Exception.InnerException.Message }
    [Console]::WriteLine(('{"status":"error","phase":"' + $Phase + '","message":' + ($em | ConvertTo-Json -Compress) + '}'))
}
