$ErrorActionPreference='Stop'
Add-Type -Path @("$PSScriptRoot\NativeCamera.cs","$PSScriptRoot\NativeDetachedCamera.cs","$PSScriptRoot\NativeFog.cs","$PSScriptRoot\NativeVisuals.cs","$PSScriptRoot\NativeLighting.cs","$PSScriptRoot\NativeSky.cs","$PSScriptRoot\NativeWeather.cs","$PSScriptRoot\NativeThunder.cs","$PSScriptRoot\PlayerPointerCodec.cs")
$camera=[Machinae.Forever.NativeCamera]::new((Get-Process -Name WowB -ErrorAction Stop))
$original=$camera.Capture()
$samples=@()
try {
    $camera.EnableFreeCamera()
    $camera.SetFreeCameraFov(60)
    for($i=0;$i -lt 24;$i++) {
        [Threading.Thread]::Sleep(100)
        $camera.CheckFreeCamera()
        $s=$camera.Capture()
        $samples += [Math]::Round(($s.Fov*180/[Math]::PI),3)
    }
    $samples -join ', '
    if([Math]::Abs($samples[-1]-60) -gt 0.1) {throw 'The native camera did not converge to the requested FOV.'}
    Write-Output 'PASS: requested FOV once; the native spectator update reached 60 degrees without repeated writes.'
} finally {
    $camera.DisableFreeCamera()
    $restored=$camera.Capture()
    if([Math]::Abs($restored.Fov-$original.Fov) -gt 0.0001) {throw 'The original world-camera FOV was not restored.'}
    for($i=0;$i -lt 10;$i++) {
        [Threading.Thread]::Sleep(100)
        if([Math]::Abs($camera.Capture().Fov-$original.Fov) -gt 0.0001) {throw 'The original world-camera FOV did not remain stable after restoring.'}
    }
    $camera.Dispose()
    Write-Output 'Original camera state restored.'
}





