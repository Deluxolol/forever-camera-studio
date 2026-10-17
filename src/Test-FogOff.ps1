$ErrorActionPreference='Stop'
Add-Type -Path @("$PSScriptRoot\NativeCamera.cs","$PSScriptRoot\NativeDetachedCamera.cs","$PSScriptRoot\NativeFog.cs","$PSScriptRoot\NativeVisuals.cs","$PSScriptRoot\NativeLighting.cs","$PSScriptRoot\NativeSky.cs","$PSScriptRoot\NativeWeather.cs","$PSScriptRoot\NativeThunder.cs","$PSScriptRoot\PlayerPointerCodec.cs","$PSScriptRoot\CameraProject.cs")
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$before=$camera.ReadFogStrengths()
try {
    $camera.SetFogSuppressed($true)
    for($i=0;$i -lt 20;$i++) {
        Start-Sleep -Milliseconds 250
        foreach($value in $camera.ReadFogStrengths()){if($value -ne 0){throw 'A native fog profile regained nonzero strength.'}}
    }
    'PASS: all six fog strengths remain zero for five seconds after one application.'
} finally {
    $camera.SetFogSuppressed($false)
    $after=$camera.ReadFogStrengths()
    for($i=0;$i -lt $before.Length;$i++){if($after[$i] -ne $before[$i]){throw 'A fog strength was not restored.'}}
    $camera.Dispose()
    'PASS: all six original fog strengths restored.'
}





