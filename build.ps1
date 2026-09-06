# Сборка AlarmSoundStudio системным компилятором .NET Framework (без установки SDK).
# Использование:  powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Find-Csc {
    foreach ($base in @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319")) {
        if (Test-Path "$base\csc.exe") { return $base }
    }
    throw "csc.exe не найден (нужен .NET Framework 4.x)"
}

function Find-WpfDir {
    foreach ($base in @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319")) {
        if (Test-Path "$base\WPF\PresentationFramework.dll") { return "$base\WPF" }
        if (Test-Path "$base\PresentationFramework.dll") { return $base }
    }
    throw "WPF-сборки не найдены"
}

$fx = Find-Csc
$wpf = Find-WpfDir
$csc = Join-Path $fx 'csc.exe'
$outDir = Join-Path $root 'build'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$refs = @(
    (Join-Path $wpf 'PresentationCore.dll'),
    (Join-Path $wpf 'PresentationFramework.dll'),
    (Join-Path $wpf 'WindowsBase.dll'),
    (Join-Path $fx  'System.Xaml.dll'),
    (Join-Path $fx  'System.dll'),
    (Join-Path $fx  'System.Core.dll'),
    (Join-Path $fx  'Microsoft.CSharp.dll')
)
$refArgs = $refs | ForEach-Object { "/r:`"$_`"" }

& $csc /nologo /target:winexe /platform:anycpu /out:"$outDir\AlarmSoundStudio.exe" $refArgs /codepage:65001 (Join-Path $root 'src\App.cs')
if ($LASTEXITCODE -ne 0) { throw "ошибка компиляции" }

Write-Host "Готово: $outDir\AlarmSoundStudio.exe"
Write-Host "Запускайте exe из папки, где рядом лежит tools\audio-engine.ps1 (можно просто скопировать папку целиком)."
