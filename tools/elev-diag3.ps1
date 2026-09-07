$ErrorActionPreference = 'Continue'
$out = "$env:TEMP\elev-diag3.txt"
$lines = @()
$ruPkg = Get-AppxPackage Microsoft.WindowsAlarms -PackageTypeFilter All | Where-Object { $_.InstallLocation -match 'language-ru' }
$pri = Join-Path $ruPkg.InstallLocation 'resources.pri'
$lines += "Clock processes still running: " + ((Get-Process Time -ErrorAction SilentlyContinue | Measure-Object).Count)
try {
    $fs = [IO.File]::Open($pri, 'Open', 'ReadWrite')
    $fs.Close()
    $lines += "open readwrite (app killed): OK"
    # контрольная запись: читаем байт, пишем обратно
    $b = [IO.File]::ReadAllBytes($pri)
    $lines += "read all: OK, " + $b.Length
} catch {
    $lines += "open readwrite FAILED: " + $_.Exception.Message
}
# также попробуем через обычный Set-Content
try {
    $lines += "Set-Content test..."
    $null = Get-Content $pri -Raw -Encoding Byte -TotalCount 10
    $lines += "stream read: OK"
} catch { $lines += "stream read failed: " + $_.Exception.Message }
$lines | Set-Content $out -Encoding UTF8
