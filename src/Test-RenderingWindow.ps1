$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Field([string]$name){$type.GetField($name,$flags).GetValue($window)}
function FindButton($parent,[string]$text){foreach($child in $parent.Controls){if($child -is [Windows.Forms.Button] -and $child.Text -eq $text){return $child};$result=FindButton $child $text;if($null -ne $result){return $result}};return $null}
try {
    $window.Show();[Windows.Forms.Application]::DoEvents()
    $type.GetMethod('Attach',$flags).Invoke($window,@()) | Out-Null
    $camera=Field 'camera';if($null -eq $camera){throw 'Could not attach.'}
    $mask=$camera.ReadRendering()
    (FindButton $window 'Lens & Display').PerformClick()
    $boxes=Field 'renderToggles'
    foreach($index in @(0,3)){
        $boxes[$index].Checked=$true;$boxes[$index].Checked=$false
        if(($camera.ReadRendering() -band [Machinae.Forever.NativeCamera]::RenderBits[$index]) -ne 0){throw "Rendering UI $index failed to disable."}
        $boxes[$index].Checked=$true
    }
    (FindButton $window 'Restore rendering').PerformClick()
    if($camera.ReadRendering() -ne $mask){throw 'Restore rendering UI failed.'}
    $boxes[0].Checked=$true;$boxes[0].Checked=$false;$boxes[3].Checked=$true;$boxes[3].Checked=$false
    $type.GetMethod('StopAll',$flags).Invoke($window,@()) | Out-Null
    if($camera.ReadRendering() -ne $mask){throw 'F10/StopAll failed to restore rendering.'}
    'PASS: M2 and WMO checkboxes, Restore rendering and F10/StopAll restoration through the real UI.'
} finally {$window.Close();$window.Dispose()}
