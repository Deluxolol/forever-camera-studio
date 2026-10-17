$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Field([string]$name){$type.GetField($name,$flags).GetValue($window)}
function Pump([int]$ms){$watch=[Diagnostics.Stopwatch]::StartNew();while($watch.ElapsedMilliseconds -lt $ms){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 10}}
function FindButton($parent,[string]$text){foreach($child in $parent.Controls){if($child -is [Windows.Forms.Button] -and $child.Text -eq $text){return $child};$result=FindButton $child $text;if($null -ne $result){return $result}};return $null}
try {
    $window.Show();[Windows.Forms.Application]::DoEvents()
    $type.GetMethod('Attach',$flags).Invoke($window,@()) | Out-Null
    $camera=Field 'camera';$mask=$camera.ReadRendering()
    (FindButton $window 'Lens & Display').PerformClick()
    $boxes=Field 'renderToggles'
    for($i=0;$i -lt 6;$i++){$boxes[$i].Checked=$false;if(($camera.ReadRendering() -band [uint32][Machinae.Forever.NativeCamera]::RenderBits[$i]) -ne 0){throw "Rendering control $i failed."};$boxes[$i].Checked=$true}
    (Field 'worldHour').Value=6;(Field 'worldMinute').Value=30;(Field 'worldTimeEnabled').Checked=$true
    Pump 400
    if(-not $camera.WorldTimeOverridden -or [Math]::Abs($camera.ReadWorldTime()-390) -gt 2){throw 'World-time controls failed.'}
    (Field 'worldSpeed').Value=60;Pump 1100
    if($camera.ReadWorldTime() -le 390.5){throw 'World-time speed did not advance daylight.'}
    (Field 'slowDuration').Value=0.2;(Field 'slowTo').Value=0.5
    (FindButton $window 'Play transition').PerformClick();Pump 500
    if(-not (Field 'cameraSlowEnabled') -or [Math]::Abs((Field 'slowScale')-0.5) -gt 0.01 -or (Field 'slowTransition')){throw 'Camera speed transition did not finish at its target.'}
    $type.GetMethod('StopAll',$flags).Invoke($window,@()) | Out-Null
    if((Field 'cameraSlowEnabled') -or $camera.WorldTimeOverridden -or (Field 'slowEnabled').Checked -or (Field 'worldTimeEnabled').Checked -or $camera.ReadRendering() -ne $mask){throw 'StopAll did not restore visual settings and controls.'}
    'PASS: six rendering controls, daylight editing and speed, camera speed transition, and StopAll restoration through the real UI.'
} finally {$window.Close();$window.Dispose()}
