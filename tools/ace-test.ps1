$ErrorActionPreference = 'Continue'
$out = "$env:TEMP\ace-test.txt"
$lines = @()

# добавить ACE на все файлы и папки testmin
$tmin = (Get-AppxPackage TestMinPackage).InstallLocation
$lines += "granting on $tmin"
$r1 = icacls $tmin /grant '*S-1-19-512-4096:(OI)(CI)(RX,D,WDAC,WO,WA)' /T /C 2>&1
$lines += "grant dirs: exit=$LASTEXITCODE"

# политика WDAC
$lines += "=== citool -lp ==="
$citool = "$env:WINDIR\System32\citool.exe"
if (Test-Path $citool) {
    $ci = & $citool -lp 2>&1
    $lines += ($ci | Out-String)
} else { $lines += "citool not found" }

# повторная активация
$lines += "=== activation retry ==="
$src = @'
using System; using System.Runtime.InteropServices;
namespace Activ {
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IApplicationActivationManager { IntPtr ActivateApplication([In] string a, [In] string b, [In] int o, out uint p); }
    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")] public class ApplicationActivationManager {}
    public static class L { public static string Go(string id) { uint p; var m=(IApplicationActivationManager)new ApplicationActivationManager(); IntPtr h=m.ActivateApplication(id,"",0,out p); return "hr=0x"+h.ToInt64().ToString("X8")+" pid="+p; } }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
try { $lines += [Activ.L]::Go('TestMinPackage_mg37rd95qdnb4!App') } catch { $lines += "activation failed: " + $_.Exception.InnerException.Message }
Start-Sleep -Seconds 4
$lines += "TestMin.exe running: " + ((Get-Process TestMin -ErrorAction SilentlyContinue | Measure-Object).Count)
Get-Process TestMin -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$lines | Set-Content $out -Encoding UTF8
