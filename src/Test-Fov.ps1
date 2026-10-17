param([int]$Seconds=6,[float]$Degrees=60)
$ErrorActionPreference='Stop'
Add-Type -Path @("$PSScriptRoot\NativeCamera.cs","$PSScriptRoot\NativeDetachedCamera.cs","$PSScriptRoot\NativeFog.cs","$PSScriptRoot\NativeVisuals.cs","$PSScriptRoot\NativeLighting.cs","$PSScriptRoot\NativeSky.cs","$PSScriptRoot\NativeWeather.cs","$PSScriptRoot\NativeThunder.cs","$PSScriptRoot\PlayerPointerCodec.cs")
$process=Get-Process -Name WowB -ErrorAction Stop
$camera=[Machinae.Forever.NativeCamera]::new($process)
$before=$camera.Capture()
Write-Output ('Before: camera=0x{0:X}, FOV={1:N2} degrees, zoom={2:N2}' -f $before.Address,($before.Fov*180/[Math]::PI),$before.ZoomCurrent)
try {
    $timer=[Diagnostics.Stopwatch]::StartNew()
    $writes=0
    while($timer.Elapsed.TotalSeconds -lt $Seconds) {
        $camera.OverrideFov($Degrees)
        $writes++
        [Threading.Thread]::Sleep(1)
    }
    Write-Output "Applied $Degrees degree FOV for $Seconds seconds ($writes validated writes)."
} finally {
    $camera.RestoreFov()
    $after=$camera.Capture()
    $camera.Dispose()
    Write-Output ('Restored: FOV={0:N2} degrees' -f ($after.Fov*180/[Math]::PI))
}





