param(
    [Parameter(Mandatory = $true)]
    [string] $WorkspaceRoot
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$compilerReferences = @(
    (Join-Path $PSHOME 'System.Drawing.Common.dll'),
    (Join-Path $PSHOME 'System.Drawing.Primitives.dll'),
    (Join-Path $PSHOME 'System.Collections.dll'),
    (Join-Path $PSHOME 'System.Runtime.dll'),
    (Join-Path $PSHOME 'System.Runtime.InteropServices.dll'),
    (Join-Path $PSHOME 'System.Private.CoreLib.dll'),
    (Join-Path $PSHOME 'netstandard.dll')
)
Add-Type -ReferencedAssemblies $compilerReferences -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class SpriteSheetTools
{
    public static Rectangle GetVisibleBounds(Bitmap bitmap)
    {
        Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int length = Math.Abs(data.Stride) * data.Height;
            byte[] pixels = new byte[length];
            Marshal.Copy(data.Scan0, pixels, 0, length);
            int left = bitmap.Width;
            int top = bitmap.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (pixels[(y * data.Stride) + (x * 4) + 3] <= 12) continue;
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }
            if (right < left || bottom < top) throw new InvalidOperationException("A sprite cell has no visible pixels.");
            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    public static void RemoveConnectedBackground(Bitmap bitmap)
    {
        Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int length = Math.Abs(data.Stride) * data.Height;
            byte[] pixels = new byte[length];
            Marshal.Copy(data.Scan0, pixels, 0, length);

            bool[] visited = new bool[bitmap.Width * bitmap.Height];
            int[] queue = new int[bitmap.Width * bitmap.Height];
            int head = 0;
            int tail = 0;
            for (int x = 0; x < bitmap.Width; x++)
            {
                EnqueueIfBackground(x, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                EnqueueIfBackground(((bitmap.Height - 1) * bitmap.Width) + x, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
            }
            for (int y = 0; y < bitmap.Height; y++)
            {
                EnqueueIfBackground(y * bitmap.Width, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                EnqueueIfBackground((y * bitmap.Width) + bitmap.Width - 1, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
            }

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % bitmap.Width;
                int y = index / bitmap.Width;
                int offset = (y * data.Stride) + (x * 4);
                pixels[offset] = 0;
                pixels[offset + 1] = 0;
                pixels[offset + 2] = 0;
                pixels[offset + 3] = 0;
                if (x > 0) EnqueueIfBackground(index - 1, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                if (x + 1 < bitmap.Width) EnqueueIfBackground(index + 1, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                if (y > 0) EnqueueIfBackground(index - bitmap.Width, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                if (y + 1 < bitmap.Height) EnqueueIfBackground(index + bitmap.Width, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
            }

            Marshal.Copy(pixels, 0, data.Scan0, length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void EnqueueIfBackground(
        int index,
        int width,
        int stride,
        byte[] pixels,
        bool[] visited,
        int[] queue,
        ref int tail)
    {
        if (visited[index]) return;
        visited[index] = true;
        int x = index % width;
        int y = index / width;
        int offset = (y * stride) + (x * 4);
        byte blue = pixels[offset];
        byte green = pixels[offset + 1];
        byte red = pixels[offset + 2];
        byte alpha = pixels[offset + 3];
        int maximum = Math.Max(red, Math.Max(green, blue));
        int minimum = Math.Min(red, Math.Min(green, blue));
        if (alpha == 0 || minimum < 218 || maximum - minimum > 18) return;
        queue[tail++] = index;
    }
}
'@

$generatedRoot = 'C:\Users\praty\.codex\generated_images\01a00896-ea0f-7d32-9f25-a0891009d164'
$sheets = @(
    @{ Pet = 'Cat';   Animation = 'run';   File = 'exec-1cf53ae5-d913-41e9-8ca1-3b4d7b4b5eb4.png' },
    @{ Pet = 'Cat';   Animation = 'idle';  File = 'exec-a7eeb5fb-34b0-4e28-9086-8d784f7865e4.png' },
    @{ Pet = 'Cat';   Animation = 'sleep'; File = 'exec-d7c6678d-b5fe-4b93-a287-ffd017775780.png' },
    @{ Pet = 'Corgi'; Animation = 'run';   File = 'exec-646b582e-78de-4ee0-acf7-856b45baffbd.png' },
    @{ Pet = 'Corgi'; Animation = 'idle';  File = 'exec-a183146c-f864-4865-88c6-f23a67f3799c.png' },
    @{ Pet = 'Corgi'; Animation = 'sleep'; File = 'exec-88bb1243-3bcb-4e9e-a6e9-f53acb959716.png' }
)

foreach ($sheet in $sheets) {
    $inputPath = Join-Path $generatedRoot $sheet.File
    $petRoot = Join-Path $WorkspaceRoot "src\HelloCompanion.App\Assets\Pets\$($sheet.Pet)"
    $sourceRoot = Join-Path $petRoot 'SourceSheets'
    $animationRoot = Join-Path $petRoot "Animations\$($sheet.Animation)"
    New-Item -ItemType Directory -Force -Path $sourceRoot, $animationRoot | Out-Null
    Copy-Item -LiteralPath $inputPath -Destination (Join-Path $sourceRoot "$($sheet.Animation)-sheet.png") -Force

    $sheetBitmap = [System.Drawing.Bitmap]::FromFile($inputPath)
    try {
        if ($sheetBitmap.GetPixel(0, 0).A -ne 0) {
            [SpriteSheetTools]::RemoveConnectedBackground($sheetBitmap)
        }
        $cellWidth = [Math]::Floor($sheetBitmap.Width / 2)
        $cellHeight = [Math]::Floor($sheetBitmap.Height / 2)
        $cells = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
        $bounds = [System.Collections.Generic.List[System.Drawing.Rectangle]]::new()

        try {
            for ($row = 0; $row -lt 2; $row++) {
                for ($column = 0; $column -lt 2; $column++) {
                    $cell = New-Object System.Drawing.Bitmap $cellWidth, $cellHeight, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                    $graphics = [System.Drawing.Graphics]::FromImage($cell)
                    try {
                        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                        $graphics.DrawImage(
                            $sheetBitmap,
                            [System.Drawing.Rectangle]::new(0, 0, $cellWidth, $cellHeight),
                            [System.Drawing.Rectangle]::new($column * $cellWidth, $row * $cellHeight, $cellWidth, $cellHeight),
                            [System.Drawing.GraphicsUnit]::Pixel)
                    }
                    finally { $graphics.Dispose() }
                    $cells.Add($cell)
                    $bounds.Add([SpriteSheetTools]::GetVisibleBounds($cell))
                }
            }

            $maximumWidth = ($bounds | Measure-Object -Property Width -Maximum).Maximum
            $maximumHeight = ($bounds | Measure-Object -Property Height -Maximum).Maximum
            $scale = [Math]::Min(232.0 / $maximumWidth, 232.0 / $maximumHeight)

            for ($index = 0; $index -lt $cells.Count; $index++) {
                $output = New-Object System.Drawing.Bitmap 256, 256, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                $graphics = [System.Drawing.Graphics]::FromImage($output)
                try {
                    $graphics.Clear([System.Drawing.Color]::Transparent)
                    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
                    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                    $source = $bounds[$index]
                    $destinationWidth = [Math]::Max(1, [Math]::Round($source.Width * $scale))
                    $destinationHeight = [Math]::Max(1, [Math]::Round($source.Height * $scale))
                    $destinationX = [Math]::Round((256 - $destinationWidth) / 2)
                    $destinationY = 244 - $destinationHeight
                    $graphics.DrawImage(
                        $cells[$index],
                        [System.Drawing.Rectangle]::new($destinationX, $destinationY, $destinationWidth, $destinationHeight),
                        $source,
                        [System.Drawing.GraphicsUnit]::Pixel)
                }
                finally { $graphics.Dispose() }

                $outputPath = Join-Path $animationRoot ('{0}-{1:00}.png' -f $sheet.Animation, ($index + 1))
                $output.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
                $output.Dispose()
            }
        }
        finally {
            foreach ($cell in $cells) { $cell.Dispose() }
        }
    }
    finally { $sheetBitmap.Dispose() }
}
