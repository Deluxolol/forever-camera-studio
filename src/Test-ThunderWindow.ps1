$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$null=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$type=[Machinae.Forever.NativeCamera].Assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
function Field([string]$name){,$type.GetField($name,$flags).GetValue($window)}
function Pump([int]$ms){$watch=[Diagnostics.Stopwatch]::StartNew();while($watch.ElapsedMilliseconds -lt $ms){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 10}}
function WaitThunder([int]$activeId){
    for($i=0;$i -lt 300;$i++){Pump 100;$state=$camera.CaptureThunder();if($state.ActiveId -eq $activeId -and $state.PendingId -eq 0 -and -not $state.Stopping){return}}
    throw ('Thunder UI did not reach requested state: '+($state|ConvertTo-Json -Compress))
}
try {
    $window.Show();[Windows.Forms.Application]::DoEvents()
    $type.GetMethod('Attach',$flags).Invoke($window,@())|Out-Null
    $camera=Field 'camera';if($null -eq $camera){throw 'Could not attach.'}
    $original=$camera.CaptureThunder();$weather=$camera.CaptureWeather()
    (Field 'thunderEnabled').Checked=$true;WaitThunder 134
    if(-not $camera.ThunderOverridden){throw 'Thunder checkbox did not enable native thunder.'}
    (Field 'thunderEnabled').Checked=$false;WaitThunder $original.ActiveId
    if($camera.ThunderOverridden){throw 'Thunder checkbox did not release override.'}
    'PASS: Thunder checkbox enables the native cycle and restores it when unchecked.'
    (Field 'thunderEnabled').Checked=$true;WaitThunder 134
    (Field 'weatherEnabled').Checked=$true;Pump 750
    if(-not $camera.WeatherOverridden -or $camera.CaptureThunder().ActiveId -ne 134){throw 'Rain and thunder did not remain active together.'}
    $type.GetMethod('StopAll',$flags).Invoke($window,@())|Out-Null
    WaitThunder $original.ActiveId
    if($camera.ThunderOverridden -or (Field 'thunderEnabled').Checked){throw 'F10 did not release the thunder override.'}
    $after=$camera.CaptureWeather()
    if($after.Kind -ne $weather.Kind -or [Math]::Abs($after.Intensity-$weather.Intensity) -gt 0.001){throw 'Thunder changed precipitation.'}
    if($camera.WeatherOverridden -or (Field 'weatherEnabled').Checked){throw 'F10 did not restore precipitation.'}
    'PASS: rain and thunder work together; F10 restores both effects and checkboxes.'
} finally {$window.Close();$window.Dispose()}
