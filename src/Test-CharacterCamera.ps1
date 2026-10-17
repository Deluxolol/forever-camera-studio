$ErrorActionPreference='Stop'
$null=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$c=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$f=[Reflection.BindingFlags]'Instance,NonPublic';$t=$c.GetType();$read=$t.GetMethod('Read',$f)
function Bytes([long]$a,[int]$n){,$read.Invoke($c,[object[]]@($a,$n))}
function Assert($condition,$message){if(!$condition){throw $message}}
try {
 $player=$t.GetMethod('FindPlayer',$f).Invoke($c,@());$original=$c.Capture();$a=$original.Address
 $flags=Bytes ($player+0x26D8) 4;$focus=Bytes ($a+0x98) 16;$mode=Bytes ($a+0xCC) 4
 $before=$c.Capture();$c.EnableFreeCamera($true)
 Assert $c.CharacterVisibleCamera 'Character camera did not enable.'
 Assert (([BitConverter]::ToUInt32((Bytes ($player+0x26D8) 4),0)-band0x400000)-eq0) 'Spectator flag was set.'
 $start=$c.CaptureFreePose();$end=$start.Copy();$end.Z+=3;$end.X+=2;$end.Yaw=[Machinae.Forever.CameraMotion]::ShortestAngle($start.Yaw+35);$end.Pitch=-15;$end.Fov=70
 $route=[Collections.Generic.List[Machinae.Forever.CameraPoint]]::new();$route.Add($start);$route.Add($end)
 $motion=[Machinae.Forever.CameraMotion]::new();$motion.Start($route,2,$false,$true)
 $watch=[Diagnostics.Stopwatch]::StartNew();$last=0
 while($motion.Playing){$now=$watch.Elapsed.TotalSeconds;$next=$motion.Advance(($now-$last),$c.CaptureFreePose());$last=$now;$c.SetFreePose($next);Start-Sleep -Milliseconds 16}
 Start-Sleep -Milliseconds 250
 $actual=$c.CaptureFreePose()
 foreach($field in @('X','Y','Z','Pitch','Fov')){Assert ([Math]::Abs($actual.$field-$end.$field)-lt0.01) "Detached route did not retain $field"}
 Assert ([Math]::Abs([Machinae.Forever.CameraMotion]::ShortestAngle($actual.Yaw-$end.Yaw))-lt0.01) 'Detached route yaw incorrect.'
 Assert ([Convert]::ToBase64String((Bytes ($player+0x26D8) 4))-eq[Convert]::ToBase64String($flags)) 'Player flags changed.'
 'PASS: detached route position, yaw, pitch and FOV retained; spectator flag stays clear.'
 $c.DisableFreeCamera()
 Assert (!$c.FreeCameraEnabled) 'Camera still enabled.'
 Assert ([Convert]::ToBase64String((Bytes ($a+0xCC) 4))-eq[Convert]::ToBase64String($mode)) 'Camera mode not restored.'
 Assert ([Convert]::ToBase64String((Bytes ($a+0x98) 16))-eq[Convert]::ToBase64String($focus)) 'Camera focus not restored.'
 Start-Sleep -Milliseconds 250
 Assert ([Math]::Abs($c.Capture().Fov-$original.Fov)-lt0.0001) 'FOV not restored.'
 'PASS: normal camera mode, focus, FOV and player flags restored.'
 $c.EnableFreeCamera();Assert (!$c.CharacterVisibleCamera) 'Normal free-camera mode was changed.';$c.DisableFreeCamera()
 'PASS: original spectator free camera still works.'
}finally{$c.Dispose()}
