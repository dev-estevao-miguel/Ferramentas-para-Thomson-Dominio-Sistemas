#requires -Version 5.1
<#
Gera o app.ico do Banco Pronto Demo (16 a 256 px) a partir do mesmo desenho vetorial de
src/BancoPronto.App/Views/Controls/BrandLogo.xaml, renderizado pelo WPF.
Uso: powershell -NoProfile -STA -ExecutionPolicy Bypass -File tools\Gerar-Icone.ps1
#>
[CmdletBinding()]
param(
    [string]$Saida
)

$ErrorActionPreference = 'Stop'
if (-not $Saida) {
    $Saida = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..\src\BancoPronto.App\Resources\Branding\app.ico'
}
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase

# Mesmo desenho do BrandLogo.xaml: três discos empilhados sobre o quadrado laranja #D64000.
$xaml = @'
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="48" Height="48">
    <Rectangle Width="48" Height="48" RadiusX="10.56" RadiusY="10.56" Fill="#D64000" />
    <Canvas>
        <Canvas.RenderTransform>
            <TransformGroup>
                <ScaleTransform ScaleX="0.66" ScaleY="0.66" />
                <TranslateTransform X="8.16" Y="8.16" />
            </TransformGroup>
        </Canvas.RenderTransform>
        <Path Data="M9,31.5 A15,5 0 0 1 39,31.5 L39,36.5 A15,5 0 0 1 9,36.5 Z" Fill="#FFFFFF" Stroke="#D64000" StrokeThickness="2.2" />
        <Path Data="M9,21.5 A15,5 0 0 1 39,21.5 L39,26.5 A15,5 0 0 1 9,26.5 Z" Fill="#FFFFFF" Stroke="#D64000" StrokeThickness="2.2" />
        <Path Data="M9,11.5 A15,5 0 0 1 39,11.5 L39,16.5 A15,5 0 0 1 9,16.5 Z" Fill="#1C1B1A" Stroke="#D64000" StrokeThickness="2.2" />
        <Path Data="M9,11.5 A15,5 0 0 0 39,11.5" Stroke="#D64000" StrokeThickness="2" />
    </Canvas>
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
