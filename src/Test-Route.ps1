$ErrorActionPreference='Stop'
Add-Type -Path @("$PSScriptRoot\NativeCamera.cs","$PSScriptRoot\NativeDetachedCamera.cs","$PSScriptRoot\NativeFog.cs","$PSScriptRoot\NativeVisuals.cs","$PSScriptRoot\NativeLighting.cs","$PSScriptRoot\NativeSky.cs","$PSScriptRoot\NativeWeather.cs","$PSScriptRoot\NativeThunder.cs","$PSScriptRoot\PlayerPointerCodec.cs","$PSScriptRoot\CameraProject.cs")
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$original=$camera.Capture()
try {
    $camera.EnableFreeCamera()
    $camera.SetFreeCameraCollision($false)
    $start=$camera.CaptureFreePose()
    $middle=$start.Copy();$middle.Z+=2;$middle.Yaw+=8;$middle.Fov=75
    $end=$start.Copy();$end.X+=3;$end.Z+=1;$end.Yaw+=15;$end.Fov=65
    $motion=[Machinae.Forever.CameraMotion]::new()
    $route=[Collections.Generic.List[Machinae.Forever.CameraPoint]]::new()
    $route.Add($start);$route.Add($middle);$route.Add($end)
    $motion.Start($route,4,$false,$true)
    $clock=[Diagnostics.Stopwatch]::StartNew();$last=0
    while($motion.Playing) {
        $now=$clock.Elapsed.TotalSeconds
        $point=$motion.Advance(($now-$last),$camera.CaptureFreePose());$last=$now
        $camera.SetFreePose($point)
        Start-Sleep -Milliseconds 16
    }
    Start-Sleep -Milliseconds 450
    $actual=$camera.Capture()
    $distance=[Math]::Sqrt([Math]::Pow($actual.X-$end.X,2)+[Math]::Pow($actual.Y-$end.Y,2)+[Math]::Pow($actual.Z-$end.Z,2))
    if($distance -gt 0.1){throw "Rendered camera did not follow waypoint position: error=$distance"}
    if([Math]::Abs($actual.Fov*180/[Math]::PI-65) -gt 0.2){throw 'Rendered FOV did not converge.'}
    if([Math]::Abs([Machinae.Forever.CameraMotion]::ShortestAngle($actual.Yaw*180/[Math]::PI-$end.Yaw)) -gt 0.2){throw 'Rendered yaw did not follow waypoint.'}
    'PASS: native rendered position, yaw and FOV follow Bezier route.'
    $motion.AutoEnabled=$true;$motion.PanZ=1;$motion.Rotation=5
    $before=$camera.CaptureFreePose()
    for($i=0;$i -lt 50;$i++) {$camera.SetFreePose($motion.Advance(0.02,$camera.CaptureFreePose()));Start-Sleep -Milliseconds 20}
    Start-Sleep -Milliseconds 100
    $after=$camera.Capture()
    if([Math]::Abs($after.Z-$before.Z-1) -gt 0.1){throw 'Auto-pan did not move rendered camera by one unit.'}
    'PASS: automatic pan updates the rendered camera.'
    $motion.Stop();$camera.SetFreePose($start)
} finally {
    $camera.DisableFreeCamera()
    Start-Sleep -Milliseconds 250
    if([Math]::Abs($camera.Capture().Fov-$original.Fov) -gt 0.0001){throw 'Original FOV not restored.'}
    $camera.Dispose()
    'Camera restored.'
}





