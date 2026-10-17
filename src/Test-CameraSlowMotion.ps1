$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true)
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$window=[Activator]::CreateInstance($type,[object[]]@($false))
function Field([string]$name){$type.GetField($name,$flags).GetValue($window)}
try {
    $window.Show();[Windows.Forms.Application]::DoEvents()
    (Field 'slowEnabled').Checked=$true
    if(-not (Field 'cameraSlowEnabled') -or [Math]::Abs((Field 'slowScale')-0.25) -gt 0.001){throw 'Camera-only speed did not enable without a game connection.'}
    $points=[Collections.Generic.List[Machinae.Forever.CameraPoint]]::new()
    $points.Add([Machinae.Forever.CameraPoint]::new())
    $end=[Machinae.Forever.CameraPoint]::new();$end.X=100;$points.Add($end)
    $motion=[Machinae.Forever.CameraMotion]::new();$motion.Start($points,10,$false,$false)
    $pose=$motion.Advance(4*(Field 'slowScale'),$points[0])
    if([Math]::Abs($pose.X-10) -gt 0.001){throw 'Quarter-speed route timing did not advance by one second over four real seconds.'}
    (Field 'slowDuration').Value=0.1;(Field 'slowTo').Value=0.5
    $type.GetField('slowElapsed',$flags).SetValue($window,[double]0)
    $type.GetField('slowTransition',$flags).SetValue($window,$true)
    Start-Sleep -Milliseconds 150
    $type.GetMethod('TickCamera',$flags).Invoke($window,@()) | Out-Null
    if((Field 'slowTransition') -or [Math]::Abs((Field 'slowScale')-0.5) -gt 0.001){throw 'Camera-only transition failed.'}
    $type.GetMethod('StopAll',$flags).Invoke($window,@()) | Out-Null
    if((Field 'cameraSlowEnabled') -or (Field 'slowEnabled').Checked -or (Field 'slowScale') -ne 1){throw 'StopAll did not reset camera-only motion speed.'}
    $native=$assembly.GetType('Machinae.Forever.NativeCamera')
    foreach($method in @('SetSlowMotion','WriteClockOperand','VirtualProtectEx','VirtualAllocEx')){
        if($null -ne $native.GetMethod($method,[Reflection.BindingFlags]'Instance,Static,Public,NonPublic')){throw "Removed client-clock method remains: $method"}
    }
    'PASS: camera-only speed, route timing, transition and reset without game access; client-clock patch APIs absent.'
} finally {$window.Dispose()}
