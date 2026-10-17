$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$frames=@()
foreach($size in @(16,20,24,32,40,48,64,128,256)){
 $scale=4
 $large=[Drawing.Bitmap]::new($size*$scale,$size*$scale)
 $g=[Drawing.Graphics]::FromImage($large)
 $path=[Drawing.Drawing2D.GraphicsPath]::new([Drawing.Drawing2D.FillMode]::Alternate)
 $brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#399DDB'))
 try{
  $g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.ScaleTransform(($size*$scale/100.0),($size*$scale/100.0))
  # Exact quadratic-to-cubic conversion of the selected SVG eye outline.
  $path.AddBezier(10,50,[single](110/3),18,[single](190/3),18,90,50)
  $path.AddBezier(90,50,[single](190/3),82,[single](110/3),82,10,50)
  $path.CloseFigure();$path.AddEllipse(37,37,26,26)
  $g.FillPath($brush,$path)
  $bitmap=[Drawing.Bitmap]::new($size,$size)
  $small=[Drawing.Graphics]::FromImage($bitmap)
  try{$small.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy;$small.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$small.DrawImage($large,0,0,$size,$size)}finally{$small.Dispose()}
  try{
   if($size-eq256){$bitmap.Save((Join-Path $PSScriptRoot 'Assets/AppIcon.png'),[Drawing.Imaging.ImageFormat]::Png)}
   $stream=[IO.MemoryStream]::new();$writer=[IO.BinaryWriter]::new($stream)
   try{
    $writer.Write([int]40);$writer.Write([int]$size);$writer.Write([int]($size*2));$writer.Write([short]1);$writer.Write([short]32)
    $writer.Write([int]0);$writer.Write([int]($size*$size*4));foreach($n in 1..4){$writer.Write([int]0)}
    for($y=$size-1;$y-ge0;$y--){for($x=0;$x-lt$size;$x++){$pixel=$bitmap.GetPixel($x,$y);$writer.Write([byte]$pixel.B);$writer.Write([byte]$pixel.G);$writer.Write([byte]$pixel.R);$writer.Write([byte]$pixel.A)}}
    $stride=[int]([Math]::Ceiling($size/32.0)*4)
    for($y=$size-1;$y-ge0;$y--){$mask=[byte[]]::new($stride);for($x=0;$x-lt$size;$x++){if($bitmap.GetPixel($x,$y).A-eq0){$mask[[int][Math]::Floor($x/8.0)]=$mask[[int][Math]::Floor($x/8.0)]-bor(0x80-shr($x%8))}};$writer.Write($mask)}
    $writer.Flush();$frames+=@{Size=$size;Data=$stream.ToArray()}
   }finally{$writer.Dispose();$stream.Dispose()}
  }finally{$bitmap.Dispose()}
 }finally{$brush.Dispose();$path.Dispose();$g.Dispose();$large.Dispose()}
}
$stream=[IO.File]::Create((Join-Path $PSScriptRoot 'Assets/AppIcon.ico'));$writer=[IO.BinaryWriter]::new($stream)
try{
 $writer.Write([short]0);$writer.Write([short]1);$writer.Write([short]$frames.Count)
 $offset=6+16*$frames.Count
 foreach($frame in $frames){$side=if($frame.Size-eq256){0}else{$frame.Size};$writer.Write([byte]$side);$writer.Write([byte]$side);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([short]1);$writer.Write([short]32);$writer.Write([int]$frame.Data.Length);$writer.Write([int]$offset);$offset+=$frame.Data.Length}
 foreach($frame in $frames){$writer.Write([byte[]]$frame.Data)}
}finally{$writer.Dispose();$stream.Dispose()}
'Created blue eye icon: nine sizes, transparent background.'
