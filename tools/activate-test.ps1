$src = @'
using System;
using System.Runtime.InteropServices;

namespace Activ {
    [ComImport, Guid("2e941141-7f97-4756-ba1d-9decde894a3d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IApplicationActivationManager {
        IntPtr ActivateApplication([In] string appUserModelId, [In] string arguments, [In] int options, out uint processId);
        IntPtr ActivateForFile([In] string appUserModelId, [In] IntPtr itemArray, [In] string verb, out uint processId);
        IntPtr ActivateForProtocol([In] string appUserModelId, [In] IntPtr itemArray, out uint processId);
    }

    [ComImport, Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C")]
    public class ApplicationActivationManager {}

    public static class Launcher {
        public static string Go(string aumid) {
            var mgr = (IApplicationActivationManager)new ApplicationActivationManager();
            uint pid;
            IntPtr hr = mgr.ActivateApplication(aumid, "", 0, out pid);
            return "hr=0x" + hr.ToInt64().ToString("X8") + " pid=" + pid;
        }
    }
}
'@
Add-Type -TypeDefinition $src -Language CSharp
foreach ($aumid in @('WindowsAlarmsPlus_mg37rd95qdnb4!App', 'Microsoft.WindowsAlarms_8wekyb3d8bbwe!App')) {
    "=== $aumid ==="
    try { [Activ.Launcher]::Go($aumid) } catch { "exception: " + $(if ($_.Exception.InnerException) { $_.Exception.InnerException.Message } else { $_.Exception.Message }) }
}
