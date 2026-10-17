$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$null=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$type=[Machinae.Forever.NativeCamera].Assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Field([string]$name){,$type.GetField($name,$flags).GetValue($window)}
function Pump([int]$ms){$watch=[Diagnostics.Stopwatch]::StartNew();while($watch.ElapsedMilliseconds -lt $ms){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 10}}
function Button($parent,[string]$text){foreach($child in $parent.Controls){if($child -is [Windows.Forms.Button] -and $child.Text -eq $text){return $child};$found=Button $child $text;if($null -ne $found){return $found}};return $null}
function WaitWeather([int]$kind,[double]$intensity){
    for($i=0;$i -lt 60;$i++){Pump 100;$state=$camera.CaptureWeather();if(-not $state.Pending -and $state.Kind -eq $kind -and $state.ActiveKind -eq $kind -and [Math]::Abs($state.Intensity-$intensity) -lt 0.001){return}}
    throw ('Weather UI did not reach the requested state: '+($state|ConvertTo-Json -Compress))
}
try {
    $window.Show();[Windows.Forms.Application]::DoEvents()
    $type.GetMethod('Attach',$flags).Invoke($window,@()) | Out-Null
    $camera=Field 'camera';if($null -eq $camera){throw 'Could not attach.'}
    $original=$camera.CaptureWeather()
    (Button $window 'Weather').PerformClick()
    (Field 'weatherEnabled').Checked=$true;WaitWeather 1 0.65
    (Button $window 'Snow').PerformClick();WaitWeather 2 0.65
    (Field 'weatherSlider').Value=30;WaitWeather 2 0.3
    Pump 1500
    $stable=$camera.CaptureWeather()
    if($stable.RecordId -ne 628 -or $stable.Kind -ne 2 -or $stable.NativeKind -ne 0 -or $stable.Pending){throw 'Snow readback did not keep the particulate preset stable.'}
    if((Field 'weatherIntensity').Value -ne 30){throw 'Intensity slider and field disagree.'}
    (Button $window 'Restore original weather').PerformClick();Pump 150
    if($camera.WeatherOverridden -or (Field 'weatherEnabled').Checked){throw 'Restore original weather did not release the UI override.'}
    (Field 'weatherEnabled').Checked=$true;WaitWeather 2 0.3
    $type.GetMethod('StopAll',$flags).Invoke($window,@()) | Out-Null
    $restored=$camera.CaptureWeather()
    if($camera.WeatherOverridden -or (Field 'weatherEnabled').Checked -or $restored.Kind -ne $original.Kind -or [Math]::Abs($restored.Intensity-$original.Intensity) -gt 0.001){throw 'F10/StopAll did not restore original weather.'}
    'PASS: real UI rain/snow selection, intensity slider, Restore original weather and F10/StopAll restore.'
} finally {$window.Close();$window.Dispose()}
