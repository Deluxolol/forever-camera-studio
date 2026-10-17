param([int]$Seconds=20)
$ErrorActionPreference='Stop'
Add-Type -Path @("$PSScriptRoot\NativeCamera.cs","$PSScriptRoot\NativeDetachedCamera.cs","$PSScriptRoot\NativeFog.cs","$PSScriptRoot\NativeVisuals.cs","$PSScriptRoot\NativeLighting.cs","$PSScriptRoot\NativeSky.cs","$PSScriptRoot\NativeWeather.cs","$PSScriptRoot\NativeThunder.cs","$PSScriptRoot\PlayerPointerCodec.cs")
$process=Get-Process -Name WowB -ErrorAction Stop
$camera=[Machinae.Forever.NativeCamera]::new($process)
$before=$camera.Capture()
Write-Output ('Camera before: {0:N2}, {1:N2}, {2:N2}; yaw={3:N3}, pitch={4:N3}' -f $before.X,$before.Y,$before.Z,$before.Yaw,$before.Pitch)
try {
    $camera.EnableFreeCamera()
    Write-Output "FREE CAMERA ENABLED for $Seconds seconds. Native movement/mouse input can now be tested."
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while($watch.Elapsed.TotalSeconds -lt $Seconds) { [Threading.Thread]::Sleep(100) }
    $after=$camera.Capture()
    Write-Output ('Camera after: {0:N2}, {1:N2}, {2:N2}' -f $after.X,$after.Y,$after.Z)
} finally {
    $camera.DisableFreeCamera()
    $camera.Dispose()
    Write-Output 'Free-camera bit cleared and spectator-camera fields restored.'
}





