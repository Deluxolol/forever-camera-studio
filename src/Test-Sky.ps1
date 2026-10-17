$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$read=$camera.GetType().GetMethod('Read',$flags)
$table=$camera.GetType().GetMethod('FindSkyTable',$flags).Invoke($camera,@())
$stateFlags=[Reflection.BindingFlags]'Instance,NonPublic'
$data=$table.GetType().GetField('Data',$stateFlags).GetValue($table)
$count=$table.GetType().GetField('Count',$stateFlags).GetValue($table)
$stride=$table.GetType().GetField('Stride',$stateFlags).GetValue($table)
$original=$read.Invoke($camera,[object[]]@($data,($count*$stride)))
$sky=[uint32[]]@(0x304878,0x486890,0x7090B0,0xA0B8D0,0xD8B090,0x806080)
$clouds=[uint32[]]@(0xD0C0A0,0x607080,0x302840)
$light=$camera.GetType().GetMethod('FindFogLight',$flags).Invoke($camera,@())
$gamma=$camera.GetType().GetMethod('LightingGamma',$flags).Invoke($camera,@())
try {
    $camera.SetSkyColors($sky,$clouds,$true,$true)
    $matched=$false
    for($attempt=0;$attempt -lt 30;$attempt++){
        Start-Sleep -Milliseconds 100
        $actual=$camera.CaptureSky();$matched=$true
        for($i=0;$i -lt 6;$i++){if(($actual.Sky[$i] -band 0xFFFFFF) -ne $sky[$i]){$matched=$false}}
        for($i=0;$i -lt 3;$i++){
            $packed=[BitConverter]::ToUInt32($read.Invoke($camera,[object[]]@(($light+0xBCC+$i*4),4)),0)
            for($channel=0;$channel -lt 3;$channel++){
                $shift=16-$channel*8
                $expectedChannel=[Math]::Round(255*[Math]::Pow((($clouds[$i] -shr $shift) -band 255)/255.0,$gamma))
                if([Math]::Abs((($packed -shr $shift) -band 255)-$expectedChannel) -gt 1){$matched=$false}
            }
        }
        if($matched){break}
    }
    if(-not $matched){throw ('Native sky/cloud colors did not match: sky {0}; clouds {1}' -f (($actual.Sky|ForEach-Object {'{0:X8}' -f $_}) -join ','),(($actual.Clouds|ForEach-Object {'{0:X8}' -f $_}) -join ','))}
    'PASS: six sky bands and three cloud colors reach native rendering inputs after a single application.'
    $changed=$read.Invoke($camera,[object[]]@($data,($count*$stride)))
    $put=$camera.GetType().GetMethod('PutSkyBits',[Reflection.BindingFlags]'Static,NonPublic')
    $get=$camera.GetType().GetMethod('GetSkyBits',[Reflection.BindingFlags]'Static,NonPublic')
    $expected=[byte[]]$original.Clone()
    $fields=@(5,6,7,8,9,10,12,14,13)
    for($row=0;$row -lt $count;$row++){for($i=0;$i -lt 9;$i++){$color=if($i -lt 6){$sky[$i]}else{$clouds[$i-6]};$put.Invoke($null,[object[]]@($expected,($row*$stride),(87+($fields[$i]-5)*25),$color)) | Out-Null}}
    if([Convert]::ToBase64String($changed) -ne [Convert]::ToBase64String($expected)){throw 'Unrelated database fields changed.'}
    $camera.SetSkyColors($sky,$clouds,$false,$true)
    if($camera.SkyColorsOverridden -or -not $camera.CloudColorsOverridden){throw 'Independent sky restoration failed.'}
    'PASS: only the nine selected color fields changed; sky and cloud overrides can be released independently.'
} finally {
    $camera.RestoreSkyColors()
    $restored=$read.Invoke($camera,[object[]]@($data,($count*$stride)))
    $camera.Dispose()
    if([Convert]::ToBase64String($original) -ne [Convert]::ToBase64String($restored)){throw 'Original sky database was not restored exactly.'}
    'PASS: all original database bytes restored exactly.'
}
