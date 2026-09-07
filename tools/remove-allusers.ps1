$ErrorActionPreference = 'Continue'
$out = "$env:TEMP\remove-allusers.txt"
$lines = @()
$lines += "before:"
$lines += (Get-AppxPackage -AllUsers Microsoft.WindowsAlarms | Select-Object PackageFullName, PackageUserInformation | Out-String)
Get-AppxPackage -AllUsers Microsoft.WindowsAlarms | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
$lines += "after: " + ((Get-AppxPackage -AllUsers Microsoft.WindowsAlarms | Measure-Object).Count) + " packages remain"
$lines += (Get-AppxPackage -AllUsers Microsoft.WindowsAlarms | Select-Object PackageFullName | Out-String)
$lines | Set-Content $out -Encoding UTF8
