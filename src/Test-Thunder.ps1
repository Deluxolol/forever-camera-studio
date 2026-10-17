$ErrorActionPreference='Stop'
$null=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$original=$camera.CaptureThunder()
try {
    $camera.SetThunder($true)
    $watch=[Diagnostics.Stopwatch]::StartNew();$sound=$false
    while($watch.Elapsed.TotalSeconds -lt 25){
        Start-Sleep -Milliseconds 100
        $state=$camera.CaptureThunder()
        if($state.ActiveId -eq 134 -and $state.SoundKit -in @(297299,373387,373243) -and $state.SoundHandle -ne 0){$sound=$true;break}
    }
    if(-not $sound){throw ('Native thunder did not start a sound: '+($state|ConvertTo-Json -Compress))}
    'PASS: native lightning preset 134 loaded and started SoundKit '+$state.SoundKit+' with sound handle '+$state.SoundHandle
} finally {
    $camera.RestoreThunder()
    $watch=[Diagnostics.Stopwatch]::StartNew()
    do {Start-Sleep -Milliseconds 100;$restored=$camera.CaptureThunder()}while($watch.Elapsed.TotalSeconds -lt 30 -and ($restored.ActiveId -ne $original.ActiveId -or $restored.PendingId -ne $original.PendingId -or $restored.Stopping -ne $original.Stopping))
    $camera.Dispose()
    if($restored.ActiveId -ne $original.ActiveId -or $restored.PendingId -ne $original.PendingId -or $restored.Stopping -ne $original.Stopping){throw ('Thunder did not restore: '+($restored|ConvertTo-Json -Compress))}
    'PASS: original thunder cycle restored through native cleanup.'
}
