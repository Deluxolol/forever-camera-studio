$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true);$f=[Reflection.BindingFlags]'Instance,NonPublic'
$window=[Activator]::CreateInstance($type,[object[]]@($false))
function Field($name){,$type.GetField($name,$f).GetValue($window)}
function Invoke($name){$type.GetMethod($name,$f).Invoke($window,@())}
try{
 $window.Show();[Windows.Forms.Application]::DoEvents();$null=Invoke 'Attach'
 $check=Field 'showCharacter';$check.Checked=$true;$null=Invoke 'EnableCamera'
 $camera=Field 'camera';if(!$camera.CharacterVisibleCamera){throw 'UI did not enable character camera.'}
 $pose=$camera.CaptureFreePose();$pose.Z+=2;$camera.SetFreePose($pose)
 $check.Checked=$false
 if(!$camera.FreeCameraEnabled-or$camera.CharacterVisibleCamera){throw 'UI did not switch to spectator camera.'}
 if($null-ne(Field 'characterInput')){throw 'Input hooks not released on mode switch.'}
 Start-Sleep -Milliseconds 100
 $check.Checked=$true
 if(!$camera.CharacterVisibleCamera-or$null-eq(Field 'characterInput')){throw 'Character camera did not restart with input.'}
 if([Math]::Abs($camera.CaptureFreePose().Z-$pose.Z)-gt0.1){throw 'Mode switch lost camera position.'}
 $null=Invoke 'StopAll'
 if($camera.FreeCameraEnabled-or$null-ne(Field 'characterInput')){throw 'F10 action did not release camera and input.'}
 'PASS: live Show character switch, detached/spectator transitions, position preservation and StopAll cleanup.'
}finally{$null=Invoke 'StopAll';$window.Dispose()}
