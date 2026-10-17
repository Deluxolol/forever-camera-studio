$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build\ForeverCameraStudio.exe'))
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
try {
    $window.Show();[Windows.Forms.Application]::DoEvents()
    $number=$type.GetField('speed',$flags).GetValue($window)
    $arrows=$number.GetType().GetField('arrows',$flags).GetValue($number)
    $down=$arrows.GetType().GetMethod('OnMouseDown',$flags)
    $up=$arrows.GetType().GetMethod('OnMouseUp',$flags)
    $number.Value=10
    $before=$number.Value
    $down.Invoke($arrows,@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,10,3,0)))
    $up.Invoke($arrows,@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,10,3,0)))
    if($number.Value -ne ($before+$number.Increment)){throw 'Increase click failed.'}
    $down.Invoke($arrows,@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,10,22,0)))
    $up.Invoke($arrows,@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,10,22,0)))
    if($number.Value -ne $before){throw 'Decrease click failed.'}
    $number.Text=([decimal]12.5).ToString([Globalization.CultureInfo]::CurrentCulture)
    if($number.Value -ne [decimal]12.5){throw 'Typed value was not parsed.'}
    $number.Value=$number.Maximum;$number.UpButton()
    if($number.Value -ne $number.Maximum){throw 'Maximum boundary failed.'}
    $number.Value=$number.Minimum;$number.DownButton()
    if($number.Value -ne $number.Minimum){throw 'Minimum boundary failed.'}
    $number.Value=10
    $down.Invoke($arrows,@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,10,3,0)))
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while($watch.ElapsedMilliseconds -lt 650){[Windows.Forms.Application]::DoEvents();Start-Sleep -Milliseconds 15}
    $up.Invoke($arrows,@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::Left,1,10,3,0)))
    if($number.Value -le (10+$number.Increment)){throw 'Holding the arrow did not repeat.'}
    $number.Enabled=$false
    if($arrows.Enabled){throw 'Disabled input left its arrows enabled.'}
    'PASS: custom input clicks, held repeat, typed decimals, bounds and disabled state; no game connection.'
} finally {$window.Dispose()}
