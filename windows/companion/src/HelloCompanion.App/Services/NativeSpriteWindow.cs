using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace HelloCompanion.App.Services;

internal sealed class LayeredSpriteFrame : IDisposable
{
    public LayeredSpriteFrame(
        string imagePath,
        int width,
        int height,
        bool flipHorizontally,
        bool pixelArt = false)
        : this(RenderImage(imagePath, width, height, flipHorizontally, pixelArt))
    {
    }

    public LayeredSpriteFrame(Bitmap rendered)
    {
        Width = rendered.Width;
        Height = rendered.Height;

        BitmapInfo bitmapInfo = BitmapInfo.Create(Width, Height);
        BitmapHandle = NativeSpriteMethods.CreateDIBSection(
            IntPtr.Zero,
            ref bitmapInfo,
            0,
            out IntPtr bitmapBits,
            IntPtr.Zero,
            0);

        if (BitmapHandle == IntPtr.Zero)
        {
            rendered.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create a sprite bitmap.");
        }

        Rectangle rectangle = new(0, 0, Width, Height);
        BitmapData bitmapData = rendered.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            int rowLength = Width * 4;
            byte[] row = new byte[rowLength];
            for (int y = 0; y < Height; y++)
            {
                IntPtr sourceRow = bitmapData.Scan0 + (y * bitmapData.Stride);
                Marshal.Copy(sourceRow, row, 0, rowLength);
                Marshal.Copy(row, 0, bitmapBits + (y * rowLength), rowLength);
            }
        }
        finally
        {
            rendered.UnlockBits(bitmapData);
            rendered.Dispose();
        }
    }

    private static Bitmap RenderImage(
        string imagePath,
        int width,
        int height,
        bool flipHorizontally,
        bool pixelArt)
    {
        using Bitmap source = new(imagePath);
        Bitmap rendered = new(width, height, PixelFormat.Format32bppPArgb);
        using (Graphics graphics = Graphics.FromImage(rendered))
        {
            graphics.Clear(Color.Transparent);
            graphics.CompositingMode = CompositingMode.SourceCopy;
            graphics.CompositingQuality = pixelArt
                ? CompositingQuality.HighSpeed
                : CompositingQuality.HighQuality;
            graphics.InterpolationMode = pixelArt
                ? InterpolationMode.NearestNeighbor
                : InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = pixelArt
                ? PixelOffsetMode.Half
                : PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = pixelArt
                ? SmoothingMode.None
                : SmoothingMode.HighQuality;

            double scale = Math.Min((double)width / source.Width, (double)height / source.Height);
            int drawWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
            int drawHeight = Math.Max(1, (int)Math.Round(source.Height * scale));
            int x = (width - drawWidth) / 2;
            int y = height - drawHeight;
            graphics.DrawImage(source, new Rectangle(x, y, drawWidth, drawHeight));
        }

        if (flipHorizontally)
        {
            rendered.RotateFlip(RotateFlipType.RotateNoneFlipX);
        }

        return rendered;
    }

    public IntPtr BitmapHandle { get; }
    public int Width { get; }
    public int Height { get; }

    public void Dispose()
    {
        if (BitmapHandle != IntPtr.Zero)
        {
            NativeSpriteMethods.DeleteObject(BitmapHandle);
        }
    }
}

internal sealed class NativeSpriteWindow : IDisposable
{
    private const uint BaseExtendedStyle = 0x00080000 | 0x00000080 | 0x00000008 | 0x08000000;
    private const uint TransparentExtendedStyle = 0x00000020;
    private const uint PopupStyle = 0x80000000;
    private const int ShowWithoutActivation = 4;
    private const uint LeftButtonUpMessage = 0x0202;
    private static readonly object ClassGate = new();
    private static readonly Dictionary<IntPtr, WeakReference<NativeSpriteWindow>> Windows = [];
    private static readonly NativeSpriteMethods.WindowProcedure WindowProcedureDelegate = WindowProcedure;
    private static readonly string WindowClassName = $"HelloCompanion.Sprite.{Environment.ProcessId}";
    private static bool _classRegistered;

    private readonly IntPtr _windowHandle;
    private readonly IntPtr _memoryDeviceContext;
    private IntPtr _selectedBitmap;
    private bool _shown;
    private bool _disposed;

    public NativeSpriteWindow(bool acceptsClicks = false)
    {
        EnsureWindowClass();

        uint extendedStyle = BaseExtendedStyle |
                             (acceptsClicks ? 0 : TransparentExtendedStyle);
        _windowHandle = NativeSpriteMethods.CreateWindowEx(
            extendedStyle,
            WindowClassName,
            "Hello Companion pet",
            PopupStyle,
            0,
            0,
            1,
            1,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeSpriteMethods.GetModuleHandle(null),
            IntPtr.Zero);

        if (_windowHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create a pet overlay window.");
        }

        lock (ClassGate)
        {
            Windows[_windowHandle] = new WeakReference<NativeSpriteWindow>(this);
        }

        _memoryDeviceContext = NativeSpriteMethods.CreateCompatibleDC(IntPtr.Zero);
        if (_memoryDeviceContext == IntPtr.Zero)
        {
            lock (ClassGate)
            {
                Windows.Remove(_windowHandle);
            }

            NativeSpriteMethods.DestroyWindow(_windowHandle);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create a sprite drawing context.");
        }
    }

    public event EventHandler? Clicked;

    public void Render(LayeredSpriteFrame frame, int x, int y)
    {
        if (_disposed)
        {
            return;
        }

        if (_selectedBitmap != frame.BitmapHandle)
        {
            NativeSpriteMethods.SelectObject(_memoryDeviceContext, frame.BitmapHandle);
            _selectedBitmap = frame.BitmapHandle;
        }

        NativePoint destination = new(x, y);
        NativePoint source = new(0, 0);
        NativeSize size = new(frame.Width, frame.Height);
        BlendFunction blend = BlendFunction.PerPixelAlpha;

        if (!NativeSpriteMethods.UpdateLayeredWindow(
                _windowHandle,
                IntPtr.Zero,
                ref destination,
                ref size,
                _memoryDeviceContext,
                ref source,
                0,
                ref blend,
                0x00000002))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not render a pet sprite.");
        }

        if (!_shown)
        {
            NativeSpriteMethods.ShowWindow(_windowHandle, ShowWithoutActivation);
            _shown = true;
        }
    }

    public void Hide()
    {
        if (!_disposed && _shown)
        {
            NativeSpriteMethods.ShowWindow(_windowHandle, 0);
            _shown = false;
        }
    }

    private static void EnsureWindowClass()
    {
        lock (ClassGate)
        {
            if (_classRegistered)
            {
                return;
            }

            NativeWindowClass windowClass = new()
            {
                WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProcedureDelegate),
                Instance = NativeSpriteMethods.GetModuleHandle(null),
                ClassName = WindowClassName
            };

            if (NativeSpriteMethods.RegisterClass(ref windowClass) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the pet window class.");
            }

            _classRegistered = true;
        }
    }

    private static IntPtr WindowProcedure(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == LeftButtonUpMessage)
        {
            NativeSpriteWindow? window = null;
            lock (ClassGate)
            {
                if (Windows.TryGetValue(windowHandle, out WeakReference<NativeSpriteWindow>? reference))
                {
                    reference.TryGetTarget(out window);
                }
            }

            if (window is not null)
            {
                window.Clicked?.Invoke(window, EventArgs.Empty);
                return IntPtr.Zero;
            }
        }

        return NativeSpriteMethods.DefWindowProc(windowHandle, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (ClassGate)
        {
            Windows.Remove(_windowHandle);
        }

        NativeSpriteMethods.DeleteDC(_memoryDeviceContext);
        NativeSpriteMethods.DestroyWindow(_windowHandle);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint(int x, int y)
{
    public int X = x;
    public int Y = y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeSize(int width, int height)
{
    public int Width = width;
    public int Height = height;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct BlendFunction
{
    public byte BlendOperation;
    public byte BlendFlags;
    public byte SourceConstantAlpha;
    public byte AlphaFormat;

    public static BlendFunction PerPixelAlpha => new()
    {
        BlendOperation = 0,
        BlendFlags = 0,
        SourceConstantAlpha = 255,
        AlphaFormat = 1
    };
}

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfoHeader
{
    public uint Size;
    public int Width;
    public int Height;
    public ushort Planes;
    public ushort BitCount;
    public uint Compression;
    public uint SizeImage;
    public int XPixelsPerMeter;
    public int YPixelsPerMeter;
    public uint ColorsUsed;
    public uint ColorsImportant;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfo
{
    public BitmapInfoHeader Header;
    public uint Color;

    public static BitmapInfo Create(int width, int height) => new()
    {
        Header = new BitmapInfoHeader
        {
            Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 32,
            Compression = 0,
            SizeImage = (uint)(width * height * 4)
        }
    };
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NativeWindowClass
{
    public uint Style;
    public IntPtr WindowProcedure;
    public int ClassExtraBytes;
    public int WindowExtraBytes;
    public IntPtr Instance;
    public IntPtr Icon;
    public IntPtr Cursor;
    public IntPtr BackgroundBrush;
    public string? MenuName;
    public string ClassName;
}

internal static class NativeSpriteMethods
{
    internal delegate IntPtr WindowProcedure(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern ushort RegisterClass(ref NativeWindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] internal static extern IntPtr DefWindowProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DestroyWindow(IntPtr windowHandle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool ShowWindow(IntPtr windowHandle, int command);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UpdateLayeredWindow(IntPtr windowHandle, IntPtr destinationDeviceContext, ref NativePoint destinationPoint, ref NativeSize size, IntPtr sourceDeviceContext, ref NativePoint sourcePoint, uint colorKey, ref BlendFunction blend, uint flags);
    [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteDC(IntPtr deviceContext);
    [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr graphicsObject);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool DeleteObject(IntPtr graphicsObject);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateDIBSection(IntPtr deviceContext, ref BitmapInfo bitmapInfo, uint usage, out IntPtr bits, IntPtr section, uint offset);
}
