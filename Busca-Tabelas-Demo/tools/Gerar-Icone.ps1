#requires -Version 5.1
<#
Gera o app.ico do B.T (16 a 256 px) a partir do mesmo desenho vetorial de
src/BuscaTabelas.App/Views/Controls/BrandLogo.xaml, renderizado pelo WPF.
Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\Gerar-Icone.ps1
#>
[CmdletBinding()]
param(
    [string]$Saida
)

$ErrorActionPreference = 'Stop'
if (-not $Saida) {
    $Saida = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\src\BuscaTabelas.App\Resources\Branding\app.ico'
}
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

# Mesmo desenho do BrandLogo.xaml, com as cores do tema fixas (laranja #D64000).
$xaml = @'
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="48" Height="48">
    <Rectangle Width="48" Height="48" RadiusX="10.56" RadiusY="10.56" Fill="#D64000" />
    <Rectangle Canvas.Left="8" Canvas.Top="10" Width="27" Height="23" RadiusX="2.5" RadiusY="2.5" Fill="White" />
    <Path Data="M8,12.5 A2.5,2.5 0 0 1 10.5,10 H32.5 A2.5,2.5 0 0 1 35,12.5 V16.5 H8 Z" Fill="#1C1B1A" />
    <Path Data="M8,22.25 H35 M8,27.75 H35 M17,16.5 V33" Stroke="#D64000" StrokeThickness="1.4" Opacity="0.55" />
    <Ellipse Canvas.Left="24.5" Canvas.Top="22.5" Width="15" Height="15" Fill="#D64000" Stroke="White" StrokeThickness="3" />
    <Path Data="M37.3,35.3 L41.5,39.5" Stroke="White" StrokeThickness="3.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" />
</Canvas>
'@

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = foreach ($size in $sizes) {
    $canvas = [Windows.Markup.XamlReader]::Parse($xaml)
    $box = [Windows.Controls.Viewbox]::new()
    $box.Child = $canvas
    $box.Width = $size
    $box.Height = $size
    $box.Measure([Windows.Size]::new($size, $size))
    $box.Arrange([Windows.Rect]::new(0, 0, $size, $size))
    $box.UpdateLayout()

    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($box)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    $encoder.Save($stream)
    [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
}

# ICO com imagens PNG: cabeçalho, uma entrada de 16 bytes por tamanho e os PNGs em sequência.
$output = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($output)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write($image.Bytes) }
$writer.Flush()

$target = [IO.Path]::GetFullPath($Saida)
[IO.File]::WriteAllBytes($target, $output.ToArray())
Write-Output "Ícone gerado: $target ($($sizes -join ', ') px)"
