$ErrorActionPreference='Stop'
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build/ForeverCameraStudio.exe'))
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process WowB))
$flags=[Reflection.BindingFlags]'Instance,NonPublic'
$read=$camera.GetType().GetMethod('Read',$flags)
$module=(Get-Process WowB).MainModule.BaseAddress.ToInt64()
function ReadVisualBytes([long]$rva,[int]$count){,$read.Invoke($camera,[object[]]@(($module+$rva),$count))}
$originalMask=$camera.ReadRendering()
$originalTime=ReadVisualBytes 0x505DB98 8
$originalClock=ReadVisualBytes 0x202766 8
try {
    for($i=0;$i -lt 6;$i++){
        $camera.SetRendering($i,$false);Start-Sleep -Milliseconds 150
        if(($camera.ReadRendering() -band [uint32][Machinae.Forever.NativeCamera]::RenderBits[$i]) -ne 0){throw "Rendering bit $i did not stay off."}
        $camera.SetRendering($i,$true)
        if(($camera.ReadRendering() -band [uint32][Machinae.Forever.NativeCamera]::RenderBits[$i]) -eq 0){throw "Rendering bit $i did not turn back on."}
    }
    $camera.RestoreRendering()
    if($camera.ReadRendering() -ne $originalMask){throw 'Rendering mask was not restored.'}
    'PASS: all six rendering bits switch off/on and restore the original mask.'
    $camera.SetWorldTime(360);Start-Sleep -Milliseconds 1000
    $morning=$camera.ReadWorldTime()
    $camera.SetWorldTime(1080);Start-Sleep -Milliseconds 500
    $evening=$camera.ReadWorldTime()
    if([Math]::Abs($morning-360) -gt 2 -or [Math]::Abs($evening-1080) -gt 2){throw "Lighting time did not follow the override: $morning / $evening"}
    $camera.RestoreWorldTime()
    if([Convert]::ToBase64String((ReadVisualBytes 0x505DB98 8)) -ne [Convert]::ToBase64String($originalTime)){throw 'Original world-time override was not restored.'}
    "PASS: native lighting time follows 06:00 ($morning) and 18:00 ($evening), then restores."
    if([Convert]::ToBase64String((ReadVisualBytes 0x202766 8)) -ne [Convert]::ToBase64String($originalClock)){throw 'Original frame-time instruction was not restored.'}
    'PASS: rendering/daylight controls leave the client frame-time instruction unchanged.'
} finally {$camera.Dispose()}

