$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build\ForeverCameraStudio.exe'))
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
try {
    $window.Show();[Windows.Forms.Application]::DoEvents()
    $type.GetMethod('Attach',$flags).Invoke($window,@()) | Out-Null
    $camera=$type.GetField('camera',$flags).GetValue($window)
    $before=$camera.ReadViewDistance()
    $field=$type.GetField('viewDistance',$flags).GetValue($window)
    $check=$type.GetField('viewEnabled',$flags).GetValue($window)
    $field.Value=4000;$check.Checked=$true
    if(-not $camera.ViewDistanceOverridden -or $camera.ReadViewDistance() -ne 4000){throw 'UI did not apply view distance.'}
    $fogField=$type.GetField('fogDistance',$flags).GetValue($window)
    $fogCheck=$type.GetField('fogEnabled',$flags).GetValue($window)
    $fogField.Value=10000;$fogCheck.Checked=$true
    Start-Sleep -Milliseconds 200
    if(-not $camera.FogOverridden -or -not $camera.CaptureFog().NativeOverride){throw 'UI did not apply native fog override.'}
    $noFog=$type.GetField('noFog',$flags).GetValue($window)
    $strengths=$camera.ReadFogStrengths()
    $noFog.Checked=$true
    foreach($value in $camera.ReadFogStrengths()){if($value -ne 0){throw 'Disable fog did not zero all profiles.'}}
    $type.GetMethod('StopAll',$flags).Invoke($window,@()) | Out-Null
    if($check.Checked -or $camera.ViewDistanceOverridden -or $camera.ReadViewDistance() -ne $before){throw 'StopAll did not restore view distance and checkbox.'}
    if($fogCheck.Checked -or $camera.FogOverridden -or $camera.CaptureFog().NativeOverride){throw 'StopAll did not restore fog and checkbox.'}
    if($noFog.Checked -or $camera.FogSuppressed){throw 'StopAll did not clear Disable fog.'}
    $restored=$camera.ReadFogStrengths()
    for($i=0;$i -lt $strengths.Length;$i++){if($restored[$i] -ne $strengths[$i]){throw 'StopAll did not restore fog strengths.'}}
    'PASS: view distance, fog distance and Disable fog work through the controls; StopAll restores native state and all three checkboxes.'
} finally {$window.Close();$window.Dispose()}
