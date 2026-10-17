$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$f=[Reflection.BindingFlags]'Instance,NonPublic'
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
$input=$null
function Assert($condition,$message){if(!$condition){throw $message}}
try {
 $window.Show();[Windows.Forms.Application]::DoEvents()
 $check=$type.GetField('showCharacter',$f).GetValue($window)
 $collision=$type.GetField('collision',$f).GetValue($window)
 $check.Checked=$true;Assert (!$collision.Enabled) 'Collision should be disabled with visible character.'
 $project=$type.GetMethod('Project',$f).Invoke($window,@());Assert $project.ShowCharacter 'Preference not captured in preset.'
 $filename=Join-Path $PSScriptRoot 'test-results/character-camera-preset.xml';$project.Save($filename)
 Assert ([Machinae.Forever.CameraProject]::Load($filename).ShowCharacter) 'Preference did not round-trip.'
 $xml=[IO.File]::ReadAllText($filename).Replace('<ShowCharacter>true</ShowCharacter>','');[IO.File]::WriteAllText($filename,$xml)
 Assert (![Machinae.Forever.CameraProject]::Load($filename).ShowCharacter) 'Legacy presets must default to the existing camera.'
 $check.Checked=$false;Assert $collision.Enabled 'Collision did not enable for spectator camera.'
 'PASS: Show character UI, collision state, new preset round-trip and legacy preset compatibility.'
 $inputType=$assembly.GetType('Machinae.Forever.DetachedCameraInput',$true)
 $gate=[Func[bool]]{$true};$input=[Activator]::CreateInstance($inputType,[object[]]@($gate))
 $keys=$inputType.GetField('keys',$f).GetValue($input)
 $pose=[Machinae.Forever.CameraPoint]::new();$pose.Fov=90
 # Expected screen-relative movement at the four cardinal headings, independent
 # of the controller's trigonometry. In WoW, yaw 0 faces +X and right is -Y.
 $headings=@(@(0,1,0,0,-1),@(90,0,1,1,0),@(180,-1,0,0,1),@(-90,0,-1,-1,0))
 foreach($heading in $headings){
  $pose.Yaw=$heading[0]
  foreach($entry in @(@(0x57,1,$heading[1],$heading[2]),@(0x53,-1,$heading[1],$heading[2]),@(0x44,1,$heading[3],$heading[4]),@(0x41,-1,$heading[3],$heading[4]),@(0x27,1,$heading[3],$heading[4]),@(0x25,-1,$heading[3],$heading[4]))){
   $keys[$entry[0]]=$true
   $next=$inputType.GetMethod('Advance').Invoke($input,[object[]]@($pose,[double]0.05,[single]10))
   Assert ([Math]::Abs($next.X-0.5*$entry[1]*$entry[2])-lt0.001-and[Math]::Abs($next.Y-0.5*$entry[1]*$entry[3])-lt0.001) "Wrong screen-relative direction for key $($entry[0]) at yaw $($heading[0])."
   $keys[$entry[0]]=$false
  }
 }
 $pose.Yaw=0
 'PASS: W/S forward/back and A/D/arrow strafing match the view at four camera headings.'
 $keys[0x57]=$true;$keys[0x44]=$true
 $next=$inputType.GetMethod('Advance').Invoke($input,[object[]]@($pose,[double]0.05,[single]10))
 $length=[Math]::Sqrt($next.X*$next.X+$next.Y*$next.Y)
 Assert ([Math]::Abs($length-0.5)-lt0.001) 'Diagonal flight is not normalized.'
 $keys[0x57]=$false;$keys[0x44]=$false
 $inputType.GetField('dx',$f).SetValue($input,[int]100)
 $inputType.GetField('dy',$f).SetValue($input,[int]10000)
 $next=$inputType.GetMethod('Advance').Invoke($input,[object[]]@($pose,[double]0.016,[single]10))
 Assert ([Math]::Abs($next.Yaw+15)-lt0.001-and[Math]::Abs($next.Pitch-89.9)-lt0.001) 'Mouse-right turn or pitch limit incorrect.'
 $input.Dispose();$input=$null
 $gate=[Func[bool]]{$false};$input=[Activator]::CreateInstance($inputType,[object[]]@($gate))
 $keys=$inputType.GetField('keys',$f).GetValue($input);$keys[0x57]=$true
 $next=$inputType.GetMethod('Advance').Invoke($input,[object[]]@($pose,[double]0.016,[single]10))
 Assert ($null-eq$next-and!$keys[0x57]) 'Input was not released on focus loss.'
 'PASS: input hooks installed/released, normalized flight, mouse look, pitch bounds and focus-loss cleanup.'
}finally{if($input){$input.Dispose()};$window.Dispose()}
