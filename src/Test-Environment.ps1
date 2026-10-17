$ErrorActionPreference='Stop'
Add-Type -Path @("$PSScriptRoot\NativeCamera.cs","$PSScriptRoot\NativeDetachedCamera.cs","$PSScriptRoot\NativeFog.cs","$PSScriptRoot\NativeVisuals.cs","$PSScriptRoot\NativeLighting.cs","$PSScriptRoot\NativeSky.cs","$PSScriptRoot\NativeWeather.cs","$PSScriptRoot\NativeThunder.cs","$PSScriptRoot\PlayerPointerCodec.cs","$PSScriptRoot\CameraProject.cs")
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$before=$camera.ReadViewDistance();$effectiveBefore=$camera.ReadEffectiveViewDistance()
try {
    $camera.SetViewDistance(4000)
    Start-Sleep -Milliseconds 700
    $actual=$camera.ReadViewDistance();$effective=$camera.ReadEffectiveViewDistance()
    if([Math]::Abs($actual-4000) -gt 0.01){throw 'Native view-distance override did not remain active.'}
    if([Math]::Abs($effective-$effectiveBefore) -lt 1){throw 'World-scene distance did not respond to the override.'}
    "PASS: native distance $before -> $actual; effective scene distance $effectiveBefore -> $effective."
    $camera.RestoreViewDistance()
    Start-Sleep -Milliseconds 700
    if([Math]::Abs($camera.ReadViewDistance()-$before) -gt 0.01 -or [Math]::Abs($camera.ReadEffectiveViewDistance()-$effectiveBefore) -gt 0.1){throw 'View-distance restore did not reach the world scene.'}
    'PASS: original native and scene distance restored.'
} finally {$camera.Dispose()}





