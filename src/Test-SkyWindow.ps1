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
    $table=$camera.GetType().GetMethod('FindSkyTable',$flags).Invoke($camera,@())
    $data=$table.GetType().GetField('Data',$flags).GetValue($table)
    $length=$table.GetType().GetField('Count',$flags).GetValue($table)*$table.GetType().GetField('Stride',$flags).GetValue($table)
    $read=$camera.GetType().GetMethod('Read',$flags)
    $original=$read.Invoke($camera,[object[]]@($data,$length))
    (FindButton $window 'Environment').PerformClick()
    # Preserve the actual Color[] references; PowerShell pipeline enumeration copies value types.
    $skyColors=$type.GetField('skyColors',$flags).GetValue($window)
    $cloudColors=$type.GetField('cloudColors',$flags).GetValue($window)
    for($i=0;$i -lt 6;$i++){$skyColors[$i]=[Drawing.Color]::FromArgb(48+$i*24,72+$i*16,120+$i*12)}
    for($i=0;$i -lt 3;$i++){$cloudColors[$i]=[Drawing.Color]::FromArgb(208-$i*64,192-$i*64,160-$i*32)}
    $type.GetMethod('UpdateSkySwatches',$flags).Invoke($window,@()) | Out-Null
    (Field 'skyEnabled').Checked=$true;(Field 'cloudsEnabled').Checked=$true
    for($attempt=0;$attempt -lt 30;$attempt++){
        Pump 100;$actual=$camera.CaptureSky();$matched=$true
        for($i=0;$i -lt 6;$i++){if(($actual.Sky[$i] -band 0xFFFFFF) -ne ($skyColors[$i].ToArgb() -band 0xFFFFFF)){$matched=$false}}
        if($matched){break}
    }
    if(-not $matched){throw ('Sky controls failed: actual {0}; desired {1}; status {2}' -f (($actual.Sky|ForEach-Object {'{0:X8}' -f $_}) -join ','),(($skyColors|ForEach-Object {'{0:X8}' -f $_.ToArgb()}) -join ','),(Field 'status').Text)}
    if(-not $camera.CloudColorsOverridden -or -not $camera.SkyColorsOverridden){throw 'Sky/cloud checkbox controls failed.'}
    (Field 'skyEnabled').Checked=$false
    if($camera.SkyColorsOverridden -or -not $camera.CloudColorsOverridden){throw 'Sky toggle affected cloud ownership.'}
    (Field 'skyEnabled').Checked=$true
    (FindButton $window 'Restore sky & clouds').PerformClick();Pump 100
    if($camera.SkyColorsOverridden -or $camera.CloudColorsOverridden -or (Field 'skyEnabled').Checked -or (Field 'cloudsEnabled').Checked){throw 'Restore button failed.'}
    (Field 'skyEnabled').Checked=$true;(Field 'cloudsEnabled').Checked=$true
    $type.GetMethod('StopAll',$flags).Invoke($window,@()) | Out-Null
    $restored=$read.Invoke($camera,[object[]]@($data,$length))
    if($camera.SkyColorsOverridden -or $camera.CloudColorsOverridden -or (Field 'skyEnabled').Checked -or (Field 'cloudsEnabled').Checked -or [Convert]::ToBase64String($original) -ne [Convert]::ToBase64String($restored)){throw 'StopAll did not restore the original sky database and controls.'}
    'PASS: all sky buttons/swatches, independent sky/cloud toggles, Restore sky & clouds and F10/StopAll restoration through the real UI.'
} finally {$window.Close();$window.Dispose()}
