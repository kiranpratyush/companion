param(
    [Parameter(Mandatory = $true)] [string] $InputPath,
    [Parameter(Mandatory = $true)] [string] $OutputDirectory,
    [int] $Columns = 3,
    [int] $Rows = 3,
    [string[]] $FrameNames = @(),
    [string[]] $CellRectangles = @()
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$references = @(
    (Join-Path $PSHOME 'System.Drawing.Common.dll'),
    (Join-Path $PSHOME 'System.Drawing.Primitives.dll'),
    (Join-Path $PSHOME 'System.Runtime.dll'),
    (Join-Path $PSHOME 'System.Runtime.InteropServices.dll'),
    (Join-Path $PSHOME 'System.Private.CoreLib.dll'),
    (Join-Path $PSHOME 'netstandard.dll')
)
Add-Type -ReferencedAssemblies $references -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class GridSpriteTools
{
    public static void RemoveEdgeConnectedFragments(Bitmap bitmap)
    {
        Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int length = Math.Abs(data.Stride) * data.Height;
            byte[] pixels = new byte[length];
            Marshal.Copy(data.Scan0, pixels, 0, length);
            bool[] remove = new bool[bitmap.Width * bitmap.Height];
            int[] queue = new int[bitmap.Width * bitmap.Height];
            int head = 0, tail = 0;
            for (int x = 0; x < bitmap.Width; x++)
            {
                AddEdgeOpaque(x, bitmap.Width, data.Stride, pixels, remove, queue, ref tail);
                AddEdgeOpaque(((bitmap.Height - 1) * bitmap.Width) + x, bitmap.Width, data.Stride, pixels, remove, queue, ref tail);
            }
            for (int y = 0; y < bitmap.Height; y++)
            {
                AddEdgeOpaque(y * bitmap.Width, bitmap.Width, data.Stride, pixels, remove, queue, ref tail);
                AddEdgeOpaque((y * bitmap.Width) + bitmap.Width - 1, bitmap.Width, data.Stride, pixels, remove, queue, ref tail);
            }
            while (head < tail)
            {
                int index = queue[head++], x = index % bitmap.Width, y = index / bitmap.Width;
                AddEdgeOpaque(index - 1, x > 0, bitmap.Width, data.Stride, pixels, remove, queue, ref tail);
                AddEdgeOpaque(index + 1, x + 1 < bitmap.Width, bitmap.Width, data.Stride, pixels, remove, queue, ref tail);
                AddEdgeOpaque(index - bitmap.Width, y > 0, bitmap.Width, data.Stride, pixels, remove, queue, ref tail);
                AddEdgeOpaque(index + bitmap.Width, y + 1 < bitmap.Height, bitmap.Width, data.Stride, pixels, remove, queue, ref tail);
            }
            for (int index = 0; index < remove.Length; index++)
            {
                if (!remove[index]) continue;
                int x = index % bitmap.Width, y = index / bitmap.Width;
                int offset = (y * data.Stride) + (x * 4);
                pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = pixels[offset + 3] = 0;
            }
            Marshal.Copy(pixels, 0, data.Scan0, length);
        }
        finally { bitmap.UnlockBits(data); }
    }

    public static void KeepLargestConnectedComponent(Bitmap bitmap)
    {
        Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            int length = Math.Abs(data.Stride) * data.Height;
            byte[] pixels = new byte[length];
            Marshal.Copy(data.Scan0, pixels, 0, length);
            int pixelCount = bitmap.Width * bitmap.Height;
            int[] labels = new int[pixelCount];
            int[] queue = new int[pixelCount];
            int nextLabel = 0, largestLabel = 0, largestCount = 0;

            for (int start = 0; start < pixelCount; start++)
            {
                int sx = start % bitmap.Width, sy = start / bitmap.Width;
                if (labels[start] != 0 || pixels[(sy * data.Stride) + (sx * 4) + 3] == 0) continue;
                int label = ++nextLabel, head = 0, tail = 0, count = 0;
                labels[start] = label;
                queue[tail++] = start;
                while (head < tail)
                {
                    int index = queue[head++]; count++;
                    int x = index % bitmap.Width, y = index / bitmap.Width;
                    AddOpaque(index - 1, x > 0, label, bitmap.Width, data.Stride, pixels, labels, queue, ref tail);
                    AddOpaque(index + 1, x + 1 < bitmap.Width, label, bitmap.Width, data.Stride, pixels, labels, queue, ref tail);
                    AddOpaque(index - bitmap.Width, y > 0, label, bitmap.Width, data.Stride, pixels, labels, queue, ref tail);
                    AddOpaque(index + bitmap.Width, y + 1 < bitmap.Height, label, bitmap.Width, data.Stride, pixels, labels, queue, ref tail);
                }
                if (count > largestCount) { largestCount = count; largestLabel = label; }
            }

            for (int index = 0; index < pixelCount; index++)
            {
                if (labels[index] == largestLabel) continue;
                int x = index % bitmap.Width, y = index / bitmap.Width;
                int offset = (y * data.Stride) + (x * 4);
                pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = pixels[offset + 3] = 0;
            }
            Marshal.Copy(pixels, 0, data.Scan0, length);
        }
        finally { bitmap.UnlockBits(data); }
    }

    public static void RemoveWhiteBackground(Bitmap bitmap)
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
                Enqueue(x, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                Enqueue(((bitmap.Height - 1) * bitmap.Width) + x, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
            }
            for (int y = 0; y < bitmap.Height; y++)
            {
                Enqueue(y * bitmap.Width, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                Enqueue((y * bitmap.Width) + bitmap.Width - 1, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
            }

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % bitmap.Width;
                int y = index / bitmap.Width;
                int offset = (y * data.Stride) + (x * 4);
                pixels[offset] = pixels[offset + 1] = pixels[offset + 2] = pixels[offset + 3] = 0;
                if (x > 0) Enqueue(index - 1, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                if (x + 1 < bitmap.Width) Enqueue(index + 1, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                if (y > 0) Enqueue(index - bitmap.Width, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
                if (y + 1 < bitmap.Height) Enqueue(index + bitmap.Width, bitmap.Width, data.Stride, pixels, visited, queue, ref tail);
            }

            Marshal.Copy(pixels, 0, data.Scan0, length);
        }
        finally { bitmap.UnlockBits(data); }
    }

    public static Rectangle VisibleBounds(Bitmap bitmap)
    {
        Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int length = Math.Abs(data.Stride) * data.Height;
            byte[] pixels = new byte[length];
            Marshal.Copy(data.Scan0, pixels, 0, length);
            int left = bitmap.Width, top = bitmap.Height, right = -1, bottom = -1;
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
            {
                if (pixels[(y * data.Stride) + (x * 4) + 3] <= 12) continue;
                left = Math.Min(left, x); top = Math.Min(top, y);
                right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
            if (right < left) throw new InvalidOperationException("A grid cell contains no visible sprite.");
            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }
        finally { bitmap.UnlockBits(data); }
    }

    private static void Enqueue(int index, int width, int stride, byte[] pixels, bool[] visited, int[] queue, ref int tail)
    {
        if (visited[index]) return;
        visited[index] = true;
        int x = index % width, y = index / width;
        int offset = (y * stride) + (x * 4);
        byte b = pixels[offset], g = pixels[offset + 1], r = pixels[offset + 2], a = pixels[offset + 3];
        int maximum = Math.Max(r, Math.Max(g, b));
        int minimum = Math.Min(r, Math.Min(g, b));
        if (a == 0 || minimum < 235 || maximum - minimum > 14) return;
        queue[tail++] = index;
    }

    private static void AddOpaque(int index, bool inBounds, int label, int width, int stride, byte[] pixels, int[] labels, int[] queue, ref int tail)
    {
        if (!inBounds || labels[index] != 0) return;
        int x = index % width, y = index / width;
        if (pixels[(y * stride) + (x * 4) + 3] == 0) return;
        labels[index] = label;
        queue[tail++] = index;
    }

    private static void AddEdgeOpaque(int index, int width, int stride, byte[] pixels, bool[] remove, int[] queue, ref int tail)
        => AddEdgeOpaque(index, true, width, stride, pixels, remove, queue, ref tail);

    private static void AddEdgeOpaque(int index, bool inBounds, int width, int stride, byte[] pixels, bool[] remove, int[] queue, ref int tail)
    {
        if (!inBounds || remove[index]) return;
        int x = index % width, y = index / width;
        if (pixels[(y * stride) + (x * 4) + 3] == 0) return;
        remove[index] = true;
        queue[tail++] = index;
    }
}
'@

$frameCount = $Columns * $Rows
if ($FrameNames.Count -notin 0, $frameCount) {
    throw "FrameNames must be empty or contain exactly $frameCount names."
}
if ($CellRectangles.Count -notin 0, $frameCount) {
    throw "CellRectangles must be empty or contain exactly $frameCount x,y,width,height values."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$sheet = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $InputPath))
try {
    [GridSpriteTools]::RemoveWhiteBackground($sheet)
    $cellWidth = [Math]::Floor($sheet.Width / $Columns)
    $cellHeight = [Math]::Floor($sheet.Height / $Rows)
    $overlapX = [Math]::Floor($cellWidth * 0.12)
    $overlapY = [Math]::Floor($cellHeight * 0.12)
    $cells = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()
    $bounds = [System.Collections.Generic.List[System.Drawing.Rectangle]]::new()
    try {
        for ($row = 0; $row -lt $Rows; $row++) {
            for ($column = 0; $column -lt $Columns; $column++) {
                $cellIndex = ($row * $Columns) + $column
                if ($CellRectangles.Count) {
                    $parts = $CellRectangles[$cellIndex].Split(',') | ForEach-Object { [int]$_.Trim() }
                    if ($parts.Count -ne 4) { throw "Invalid rectangle '$($CellRectangles[$cellIndex])'." }
                    $sourceLeft, $sourceTop, $sourceWidth, $sourceHeight = $parts
                    $sourceRight = $sourceLeft + $sourceWidth
                    $sourceBottom = $sourceTop + $sourceHeight
                }
                else {
                    $sourceLeft = [Math]::Max(0, ($column * $cellWidth) - $overlapX)
                    $sourceTop = [Math]::Max(0, ($row * $cellHeight) - $overlapY)
                    $sourceRight = if ($column -eq $Columns - 1) { $sheet.Width } else { [Math]::Min($sheet.Width, (($column + 1) * $cellWidth) + $overlapX) }
                    $sourceBottom = if ($row -eq $Rows - 1) { $sheet.Height } else { [Math]::Min($sheet.Height, (($row + 1) * $cellHeight) + $overlapY) }
                }
                $sourceWidth = $sourceRight - $sourceLeft
                $sourceHeight = $sourceBottom - $sourceTop
                $cell = [System.Drawing.Bitmap]::new($sourceWidth, $sourceHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                $graphics = [System.Drawing.Graphics]::FromImage($cell)
                try {
                    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                    $graphics.DrawImage($sheet,
                        [System.Drawing.Rectangle]::new(0, 0, $sourceWidth, $sourceHeight),
                        [System.Drawing.Rectangle]::new($sourceLeft, $sourceTop, $sourceWidth, $sourceHeight),
                        [System.Drawing.GraphicsUnit]::Pixel)
                }
                finally { $graphics.Dispose() }
                $cells.Add($cell)
                $bounds.Add([GridSpriteTools]::VisibleBounds($cell))
            }
        }

        $maximumWidth = ($bounds | Measure-Object Width -Maximum).Maximum
        $maximumHeight = ($bounds | Measure-Object Height -Maximum).Maximum
        $scale = [Math]::Min(232.0 / $maximumWidth, 232.0 / $maximumHeight)
        for ($index = 0; $index -lt $frameCount; $index++) {
            $output = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $graphics = [System.Drawing.Graphics]::FromImage($output)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $source = $bounds[$index]
                $width = [Math]::Max(1, [Math]::Round($source.Width * $scale))
                $height = [Math]::Max(1, [Math]::Round($source.Height * $scale))
                $x = [Math]::Round((256 - $width) / 2)
                $y = 244 - $height
                $graphics.DrawImage($cells[$index], [System.Drawing.Rectangle]::new($x, $y, $width, $height), $source, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally { $graphics.Dispose() }

            $name = if ($FrameNames.Count) { $FrameNames[$index] } else { 'frame-{0:00}' -f ($index + 1) }
            $output.Save((Join-Path $OutputDirectory "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
            $output.Dispose()
        }
    }
    finally { foreach ($cell in $cells) { $cell.Dispose() } }
}
finally { $sheet.Dispose() }
