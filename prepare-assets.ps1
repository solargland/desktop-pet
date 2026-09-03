$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$code = @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;

public static class BackgroundRemover
{
    public static void Process(string input, string output)
    {
        using (var source = new Bitmap(input))
        using (var bmp = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
        {
            using (var graphics = Graphics.FromImage(bmp))
            {
                graphics.DrawImageUnscaled(source, 0, 0);
            }
            int w = bmp.Width;
            int h = bmp.Height;
            var visited = new bool[w, h];
            var queue = new Queue<Point>();

            for (int x = 0; x < w; x++)
            {
                queue.Enqueue(new Point(x, 0));
                queue.Enqueue(new Point(x, h - 1));
            }
            for (int y = 1; y < h - 1; y++)
            {
                queue.Enqueue(new Point(0, y));
                queue.Enqueue(new Point(w - 1, y));
            }

            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                if (p.X < 0 || p.X >= w || p.Y < 0 || p.Y >= h || visited[p.X, p.Y]) continue;
                visited[p.X, p.Y] = true;
                var c = bmp.GetPixel(p.X, p.Y);
                if (!IsBackground(c)) continue;

                bmp.SetPixel(p.X, p.Y, Color.FromArgb(0, c.R, c.G, c.B));
                queue.Enqueue(new Point(p.X - 1, p.Y));
                queue.Enqueue(new Point(p.X + 1, p.Y));
                queue.Enqueue(new Point(p.X, p.Y - 1));
                queue.Enqueue(new Point(p.X, p.Y + 1));
            }

            bmp.Save(output, ImageFormat.Png);
        }
    }

    private static bool IsBackground(Color c)
    {
        int min = Math.Min(c.R, Math.Min(c.G, c.B));
        int max = Math.Max(c.R, Math.Max(c.G, c.B));
        return min >= 235 && max - min <= 24;
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing

$assetRoot = $PSScriptRoot
$sourceRoot = Join-Path $assetRoot 'assets\source'
$idleRoot = Join-Path $assetRoot 'assets\idle'
$dragRoot = Join-Path $assetRoot 'assets\drag'

function Pick-Input([string] $cleanName, [string] $originalName) {
    $cleanPath = Join-Path $sourceRoot $cleanName
    if (Test-Path -LiteralPath $cleanPath) { return $cleanPath }
    return (Join-Path $sourceRoot $originalName)
}

[BackgroundRemover]::Process((Pick-Input 'clean-idle-1.png' 'idle-1.jpg'), (Join-Path $idleRoot 'idle-1.png'))
[BackgroundRemover]::Process((Pick-Input 'clean-idle-2.png' 'idle-2.jpg'), (Join-Path $idleRoot 'idle-2.png'))
[BackgroundRemover]::Process((Pick-Input 'clean-drag-1.png' 'drag-1.jpg'), (Join-Path $dragRoot 'drag-1.png'))
[BackgroundRemover]::Process((Pick-Input 'clean-drag-2.png' 'drag-2.jpg'), (Join-Path $dragRoot 'drag-2.png'))
[BackgroundRemover]::Process((Pick-Input 'clean-tray.png' 'tray-source.jpg'), (Join-Path $assetRoot 'assets\tray.png'))

$iconSource = [Drawing.Bitmap]::new((Join-Path $assetRoot 'assets\tray.png'))
$iconHandle = $iconSource.GetHicon()
$icon = [Drawing.Icon]::FromHandle($iconHandle)
$iconStream = [IO.File]::Open((Join-Path $assetRoot 'assets\tray.ico'), [IO.FileMode]::Create)
$icon.Save($iconStream)
$iconStream.Dispose()
$icon.Dispose()
$iconSource.Dispose()

Write-Host 'Desktop pet assets prepared.'
