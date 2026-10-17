$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$read=$camera.GetType().GetMethod('Read',$flags)
$light=$camera.GetType().GetMethod('FindFogLight',$flags).Invoke($camera,@())
$original=$read.Invoke($camera,[object[]]@(($light+0x23F8),32))
try {
    $camera.SetLighting([uint32]0xE6BC80,[uint32]0x485569)
    for($i=0;$i -lt 10;$i++){
        Start-Sleep -Milliseconds 100
        $state=$camera.CaptureLighting()
        if(-not $state.NativeOverride -or ($state.Sunlight -band 0xFFFFFF) -ne 0xE6BC80 -or ($state.Ambient -band 0xFFFFFF) -ne 0x485569){throw ('Native lighting did not retain colors: {0:X8}/{1:X8}' -f $state.Sunlight,$state.Ambient)}
    }
    'PASS: native sunlight and ambient colors retained for one second after a single application.'
} finally {
    $camera.RestoreLighting()
    $restored=$read.Invoke($camera,[object[]]@(($light+0x23F8),32))
    $camera.Dispose()
    if([Convert]::ToBase64String($original) -ne [Convert]::ToBase64String($restored)){throw 'Original lighting override fields were not restored exactly.'}
    'PASS: exact original lighting inputs and flag restored.'
}
