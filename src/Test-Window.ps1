$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms,System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'build\ForeverCameraStudio.exe'))
$type=$assembly.GetType('Machinae.Forever.CameraWindow',$true)
$window=[Activator]::CreateInstance($type,[object[]]@($false))
try {
    $window.Show()
    [Windows.Forms.Application]::DoEvents()
    $bitmap=[Drawing.Bitmap]::new($window.Width,$window.Height)
    try {
        $window.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$bitmap.Width,$bitmap.Height))
        $bitmap.Save((Join-Path $PSScriptRoot 'window-preview.png'))
    } finally {$bitmap.Dispose()}
    $content=@($window.Controls | Where-Object {$_.Tag -eq 'StudioContent'})[0]
    $pages=@($content.Controls | Where-Object {$_.Tag -eq 'StudioPage'})
    if($pages.Count -ne 4) {throw 'Missing studio pages.'}
    foreach($page in $pages) {
        foreach($other in $pages){$other.Visible=$false}
        $page.Visible=$true
        foreach($panel in $window.Controls){foreach($button in $panel.Controls){if($button -is [Windows.Forms.Button] -and $button.Text -eq $page.Name){$button.PerformClick()}}}
        [Windows.Forms.Application]::DoEvents()
        foreach($child in $page.Controls) {
            if($child.Bottom -gt $page.ClientSize.Height -or $child.Right -gt $page.ClientSize.Width) {throw "Control outside $($page.Name): $($child.Text)"}
        }
        $picture=[Drawing.Bitmap]::new($window.Width,$window.Height)
        $filename=switch($page.Name){'Lens & Display'{'Lens'}'Flight & Routes'{'Camera'}'Environment'{'Environment'}'Weather'{'Weather'}}
        try {$window.DrawToBitmap($picture,[Drawing.Rectangle]::new(0,0,$picture.Width,$picture.Height));$picture.Save((Join-Path $PSScriptRoot ('studio-'+$filename+'.png')))}finally{$picture.Dispose()}
    }
    Write-Output 'PASS: camera window constructed and rendered without connecting or changing game state.'
} finally {$window.Dispose()}

