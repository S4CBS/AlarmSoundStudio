$ErrorActionPreference = 'Continue'
$out = "$env:TEMP\proc-trace.txt"
$lines = @()

Register-CimIndicationEvent -Query "SELECT * FROM Win32_ProcessStartTrace WHERE ProcessName = 'Time.exe'" -SourceIdentifier ProcStart | Out-Null
Register-CimIndicationEvent -Query "SELECT * FROM Win32_ProcessStopTrace WHERE ProcessName = 'Time.exe'" -SourceIdentifier ProcStop | Out-Null

# активация
$explorer = Start-Process cmd.exe -ArgumentList '/c', 'start "" shell:AppsFolder\WindowsAlarmsPlus_mg37rd95qdnb4!App' -PassThru

$deadline = (Get-Date).AddSeconds(20)
while ((Get-Date) -lt $deadline) {
    $s = Wait-Event -SourceIdentifier ProcStart -Timeout 1
    if ($s) {
        $pid2 = $s.SourceEventArgs.NewEvent.ProcessID
        $lines += "START pid=$pid2 time=$([datetime]::Now.ToString('HH:mm:ss.fff'))"
        Remove-Event -EventIdentifier $s.EventIdentifier
        # наблюдаем за жизнью процесса до 10 сек
        $d2 = (Get-Date).AddSeconds(10)
        $alive = $true
        while ((Get-Date) -lt $d2 -and $alive) {
            $e = Wait-Event -SourceIdentifier ProcStop -Timeout 1
            if ($e) {
                $epid = $e.SourceEventArgs.NewEvent.ProcessID
                $code = $e.SourceEventArgs.NewEvent.ExitStatus
                if ($epid -eq $pid2) {
                    $lines += "STOP pid=$epid exitcode=$code time=$([datetime]::Now.ToString('HH:mm:ss.fff'))"
                    $alive = $false
                }
                Remove-Event -EventIdentifier $e.EventIdentifier
            }
        }
        if ($alive) { $lines += "STILL ALIVE after 10s - app works!" }
        break
    }
}
if ($lines.Count -eq 0) { $lines += "NO PROCESS START OBSERVED IN 20s" }
Unregister-Event -SourceIdentifier ProcStart -ErrorAction SilentlyContinue
Unregister-Event -SourceIdentifier ProcStop -ErrorAction SilentlyContinue
Get-Process Time -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$lines | Set-Content $out -Encoding UTF8
