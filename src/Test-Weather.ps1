$ErrorActionPreference='Stop'
$null=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$read=$camera.GetType().GetMethod('Read',$flags)
$base=$camera.GetType().GetField('moduleBase',$flags).GetValue($camera)
function Bytes([long]$address,[int]$length){,$read.Invoke($camera,[object[]]@($address,$length))}
$frame=[BitConverter]::ToInt64((Bytes ($base+0x505DBB8) 8),0)
$scene=[BitConverter]::ToInt64((Bytes ($frame+0x288) 8),0)
$weather=[BitConverter]::ToInt64((Bytes ($scene+0x40) 8),0)
$original=$camera.CaptureWeather()
$queue=Bytes ($weather+0x11A8) 33
$record=Bytes ($weather+0x1288) 12
try {
    foreach($kind in @(1,2)){
        $camera.SetWeather($kind,0.65)
        $loaded=$false
        for($sample=0;$sample -lt 60;$sample++){
            Start-Sleep -Milliseconds 100
            $state=$camera.CaptureWeather()
            if(-not $state.Pending -and $state.Kind -eq $kind -and $state.ActiveKind -eq $kind -and [Math]::Abs($state.Intensity-0.65) -lt 0.001){$loaded=$true;break}
        }
        if(-not $loaded){throw ("Precipitation $kind did not load: "+($state | ConvertTo-Json -Compress))}
        if($kind -eq 2){
            if($state.NativeKind -ne 0 -or $state.RecordId -ne 628 -or [BitConverter]::ToInt64((Bytes ($weather+0x11E0) 8),0) -ne 0){throw 'Snow did not use the current-client snowfall preset.'}
            if([BitConverter]::ToInt64((Bytes ($weather+0x58) 8),0) -le 0){throw 'Snowfall particulate emitter did not load.'}
            'PASS: snow uses preset 628 with native particulate emitters; legacy snow haze controller is absent.'
        }
        "PASS: native precipitation $kind consumes the request and loads its effect at 65% intensity."
    }
} finally {
    $camera.RestoreWeather()
    $restored=$camera.CaptureWeather()
    if($restored.Kind -ne $original.Kind -or [Math]::Abs($restored.Intensity-$original.Intensity) -gt 0.001 -or [Convert]::ToBase64String((Bytes ($weather+0x11A8) 33)) -ne [Convert]::ToBase64String($queue) -or [Convert]::ToBase64String((Bytes ($weather+0x1288) 12)) -ne [Convert]::ToBase64String($record)){throw ('Original weather did not restore: '+($restored | ConvertTo-Json -Compress))}
    $camera.Dispose()
    'PASS: original weather kind, intensity, record and queue bytes restored.'
}
