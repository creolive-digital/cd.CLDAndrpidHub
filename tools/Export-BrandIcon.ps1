Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$root = Split-Path $PSScriptRoot -Parent
$reader = [System.Xml.XmlReader]::Create((Join-Path $root 'BrandAssets\Brand.xaml'))
$resources = [Windows.Markup.XamlReader]::Load($reader)
$reader.Close()
$images = @()
foreach ($size in @(16,32,48,256)) {
 $visual = New-Object Windows.Media.DrawingVisual
 $context = $visual.RenderOpen()
 $context.DrawImage($resources['CldLogo'], [Windows.Rect]::new(0,0,$size,$size))
 $context.Close()
 $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $bitmap.Render($visual)
 $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
 $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $stream = New-Object IO.MemoryStream
 $encoder.Save($stream)
 $images += ,$stream.ToArray()
 if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $root 'BrandAssets\scrcpy.png'), $stream.ToArray()) }
 $stream.Dispose()
}
$file=[IO.File]::Create((Join-Path $root 'BrandAssets\cld-hub.ico'))
$writer=[IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]4)
$offset=6+16*4
for ($i=0;$i -lt 4;$i++) {
 $dimension=@(16,32,48,0)[$i]
 $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
 $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
 $offset += $images[$i].Length
}
foreach ($bytes in $images) { $writer.Write([byte[]]$bytes) }
$writer.Dispose()
