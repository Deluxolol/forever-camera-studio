param([Parameter(Mandatory=$true)][string]$CompilerPath)
$ErrorActionPreference='Stop'
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output=Join-Path $PSScriptRoot 'build'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$arguments=@('/nologo','/target:winexe','/platform:x64','/langversion:12',('/out:"'+(Join-Path $output 'ForeverCameraStudio.exe')+'"'))
$arguments += '/win32icon:"'+(Join-Path $PSScriptRoot 'Assets\AppIcon.ico')+'"'
$arguments += '/resource:"'+(Join-Path $PSScriptRoot 'Assets\AppIcon.ico')+'",ForeverCameraStudio.AppIcon.ico'
$arguments += '/resource:"'+(Join-Path $PSScriptRoot 'Assets\AppIcon.png')+'",ForeverCameraStudio.AppIcon.png'
foreach($name in @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xml.dll')) {$arguments += '/reference:"'+(Join-Path $framework $name)+'"'}
foreach($name in @('NativeCamera.cs','NativeDetachedCamera.cs','DetachedCameraInput.cs','NativeFog.cs','NativeVisuals.cs','NativeLighting.cs','NativeSky.cs','NativeWeather.cs','NativeThunder.cs','PlayerPointerCodec.cs','CameraProject.cs','StudioFonts.cs','Program.cs')) {$arguments += '"'+(Join-Path $PSScriptRoot $name)+'"'}
$response=Join-Path $output 'build.rsp'
$arguments | Set-Content -LiteralPath $response -Encoding utf8
& $CompilerPath ('@'+$response)
if($LASTEXITCODE -ne 0) {throw 'Camera-tool compilation failed.'}
Write-Output (Join-Path $output 'ForeverCameraStudio.exe')


