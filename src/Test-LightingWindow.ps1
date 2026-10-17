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
    $camera=Field 'camera';if($null -eq $camera){throw 'Could not attach.'}
    $read=$camera.GetType().GetMethod('Read',$flags)
    $light=$camera.GetType().GetMethod('FindFogLight',$flags).Invoke($camera,@())
    $original=$read.Invoke($camera,[object[]]@(($light+0x23F8),32))
    (FindButton $window 'Environment').PerformClick()
    $type.GetField('sunlightColor',$flags).SetValue($window,[Drawing.Color]::FromArgb(230,188,128))
    $type.GetField('ambientColor',$flags).SetValue($window,[Drawing.Color]::FromArgb(72,85,105))
    (Field 'lightingEnabled').Checked=$true;Pump 250
    $state=$camera.CaptureLighting()
    if(-not $camera.LightingOverridden -or ($state.Sunlight -band 0xFFFFFF) -ne 0xE6BC80 -or ($state.Ambient -band 0xFFFFFF) -ne 0x485569){throw 'Lighting checkbox did not apply both colors.'}
    (FindButton $window 'Restore lighting').PerformClick();Pump 100
    if($camera.LightingOverridden){throw 'Restore lighting failed.'}
    (Field 'lightingEnabled').Checked=$false
    foreach($preset in @(@('Noon',720),@('Sunset',1110),@('Night',0))){
        (FindButton $window $preset[0]).PerformClick();Pump 150
        if(-not $camera.WorldTimeOverridden -or [Math]::Abs($camera.ReadWorldTime()-$preset[1]) -gt 1){throw "Daylight preset failed: $($preset[0])"}
    }
    (Field 'lightingEnabled').Checked=$true;Pump 100
    $type.GetMethod('StopAll',$flags).Invoke($window,@()) | Out-Null
    $restored=$read.Invoke($camera,[object[]]@(($light+0x23F8),32))
    if($camera.LightingOverridden -or $camera.WorldTimeOverridden -or (Field 'lightingEnabled').Checked -or [Convert]::ToBase64String($original) -ne [Convert]::ToBase64String($restored)){throw 'StopAll did not restore lighting and daylight.'}
    'PASS: lighting UI applies both colors; Restore lighting, all three daylight presets and F10/StopAll restore correctly.'
} finally {$window.Close();$window.Dispose()}
