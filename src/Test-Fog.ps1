$ErrorActionPreference='Stop'
Add-Type -Path @("$PSScriptRoot\NativeCamera.cs","$PSScriptRoot\NativeDetachedCamera.cs","$PSScriptRoot\NativeFog.cs","$PSScriptRoot\NativeVisuals.cs","$PSScriptRoot\NativeLighting.cs","$PSScriptRoot\NativeSky.cs","$PSScriptRoot\NativeWeather.cs","$PSScriptRoot\NativeThunder.cs","$PSScriptRoot\PlayerPointerCodec.cs","$PSScriptRoot\CameraProject.cs")
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$before=$camera.CaptureFog()
try {
    $camera.SetFogDistance(20000)
    $ends=@()
    for($i=0;$i -lt 100;$i++) {
        Start-Sleep -Milliseconds 200
        $fog=$camera.CaptureFog();$ends+=$fog.End
        if(-not $fog.NativeOverride -or [Math]::Abs($fog.Start) -gt 0.01){throw 'The native fog override did not remain active.'}
    }
    if([Math]::Abs($ends[-1]-20000) -gt 1){throw 'Fog did not reach the requested distance after blending.'}
    "PASS: one native override write held fog start at 0 and fog end at $($ends[-1]) for twenty seconds; original end=$($before.End)."
} finally {
    $camera.RestoreFog()
    Start-Sleep -Milliseconds 500
    $after=$camera.CaptureFog()
    if($after.NativeOverride -ne $before.NativeOverride){throw 'The original fog override flag was not restored.'}
    "Fog restored: start=$($after.Start), end=$($after.End), limit=$($after.Limit)."
    $camera.Dispose()
}





