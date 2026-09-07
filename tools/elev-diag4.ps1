$ErrorActionPreference = 'Continue'
$out = "$env:TEMP\elev-diag4.txt"
$lines = @()
$lines += "devmode: " + (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
$ruPkg = Get-AppxPackage Microsoft.WindowsAlarms -PackageTypeFilter All | Where-Object { $_.InstallLocation -match 'language-ru' }
$pri = Join-Path $ruPkg.InstallLocation 'resources.pri'
try {
    $fs = [IO.File]::Open($pri, 'Open', 'ReadWrite')
    $fs.Close()
    $lines += "WRITE TEST (devmode on): OK - direct patching possible!"
} catch {
    $lines += "WRITE TEST (devmode on): FAILED: " + $_.Exception.Message
}
$lines | Set-Content $out -Encoding UTF8
