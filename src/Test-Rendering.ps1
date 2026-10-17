$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$read=$camera.GetType().GetMethod('Read',$flags)
$base=$camera.GetType().GetField('moduleBase',$flags).GetValue($camera)
function Bytes([long]$address,[int]$length){,$read.Invoke($camera,[object[]]@($address,$length))}
$frame=[BitConverter]::ToInt64((Bytes ($base+0x505DBB8) 8),0)
$scene=[BitConverter]::ToInt64((Bytes ($frame+0x288) 8),0)
$state=[BitConverter]::ToInt64((Bytes ($scene+0x78) 8),0)
$mainBefore=Bytes $state 8
$legacyBefore=Bytes ($base+0x5059B40) 4
try {
    foreach($entry in @(@(0,[uint32]0x410000),@(3,[uint32]0x220000))){
        $index=$entry[0];$mask=$entry[1]
        $camera.SetRendering($index,$false)
        for($sample=0;$sample -lt 5;$sample++){
            Start-Sleep -Milliseconds 100
            $main=[BitConverter]::ToUInt32((Bytes $state 4),0)
            if(($main -band $mask) -ne 0 -or ($camera.ReadRendering() -band [Machinae.Forever.NativeCamera]::RenderBits[$index]) -ne 0){throw "Main rendering passes did not stay off for control $index"}
            $unowned=[uint32]::MaxValue -bxor $mask
            if(($main -band $unowned) -ne ([BitConverter]::ToUInt32($mainBefore,0) -band $unowned)){throw 'Unrelated main-pass bits changed.'}
        }
        $camera.SetRendering($index,$true)
        if(([BitConverter]::ToUInt32((Bytes $state 4),0) -band $mask) -ne $mask){throw 'Main rendering passes did not re-enable.'}
        $camera.RestoreRendering()
    }
    $camera.SetRendering(0,$false);$camera.SetRendering(3,$false)
    if(([BitConverter]::ToUInt32((Bytes $state 4),0) -band 0x630000) -ne 0){throw 'Combined model/building disabling failed.'}
    $camera.SetRendering(0,$true)
    if(([BitConverter]::ToUInt32((Bytes $state 4),0) -band 0x220000) -ne 0){throw 'Enabling models also enabled buildings.'}
    'PASS: M2/WMO main rendering passes and legacy shadow bits toggle independently and persist over multiple frames.'
} finally {
    $camera.RestoreRendering()
    if([Convert]::ToBase64String((Bytes $state 8)) -ne [Convert]::ToBase64String($mainBefore) -or [Convert]::ToBase64String((Bytes ($base+0x5059B40) 4)) -ne [Convert]::ToBase64String($legacyBefore)){throw 'Rendering state did not restore exactly.'}
    $camera.Dispose()
    'PASS: all original main-pass and legacy-mask bytes restored exactly.'
}
