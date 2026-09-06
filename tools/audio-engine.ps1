param([string]$Src, [string]$Dst)
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime | Out-Null
    $null = [Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]
    $null = [Windows.Storage.StorageFolder, Windows.Storage, ContentType = WindowsRuntime]
    $null = [Windows.Media.MediaProperties.MediaEncodingProfile, Windows.Media.MediaProperties, ContentType = WindowsRuntime]
    $null = [Windows.Media.Transcoding.MediaTranscoder, Windows.Media.Transcoding, ContentType = WindowsRuntime]

    $m = [System.WindowsRuntimeSystemExtensions].GetMethods()
    $asTaskOp   = ($m | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
    $asTaskActW = ($m | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncActionWithProgress`1' })[0]

    function Await-Op([object]$op, [type]$resultType) {
        $t = $asTaskOp.MakeGenericMethod($resultType).Invoke($null, @($op))
        $t.Wait(-1) | Out-Null
        $t.Result
    }

    if (Test-Path $Dst) { Remove-Item $Dst -Force }
    $srcFile = Await-Op ([Windows.Storage.StorageFile]::GetFileFromPathAsync($Src)) ([Windows.Storage.StorageFile])
    $folder  = Await-Op ([Windows.Storage.StorageFolder]::GetFolderFromPathAsync((Split-Path $Dst -Parent))) ([Windows.Storage.StorageFolder])
    $outFile = Await-Op ($folder.CreateFileAsync((Split-Path $Dst -Leaf), [Windows.Storage.CreationCollisionOption]::ReplaceExisting)) ([Windows.Storage.StorageFile])

    $profile = [Windows.Media.MediaProperties.MediaEncodingProfile]::CreateWav([Windows.Media.MediaProperties.AudioEncodingQuality]::High)
    $trans = New-Object Windows.Media.Transcoding.MediaTranscoder
    $prep  = Await-Op ($trans.PrepareFileTranscodeAsync($srcFile, $outFile, $profile)) ([Windows.Media.Transcoding.PrepareTranscodeResult])
    if (-not $prep.CanTranscode) { throw "Media Foundation: $($prep.FailureReason)" }
    $t = $asTaskActW.MakeGenericMethod([double]).Invoke($null, @($prep.TranscodeAsync()))
    $t.Wait(-1) | Out-Null

    # быстрая проверка результата
    $b = [IO.File]::ReadAllBytes($Dst)
    $magic = [Text.Encoding]::ASCII.GetString($b, 0, 4)
    $wave  = [Text.Encoding]::ASCII.GetString($b, 8, 4)
    if ($magic -ne 'RIFF' -or $wave -ne 'WAVE') { throw 'результат не WAV' }
    Write-Output 'OK'
} catch {
    $em = $_.Exception.Message
    if ($_.Exception.InnerException) { $em = $_.Exception.InnerException.Message }
    Write-Output "ERR:$em"
}
