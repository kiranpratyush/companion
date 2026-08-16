using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HelloCompanion.App.Services;

public sealed class TrayIconService : IDisposable
{
    private const uint WmApp = 0x8000;
    private const uint CallbackMessage = WmApp + 1;
    private const uint WmRightButtonUp = 0x0205;
    private const uint WmLeftButtonDoubleClick = 0x0203;
    private const uint WmContextMenu = 0x007B;
    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NimAdd = 0x00000000;
    private const uint NimDelete = 0x00000002;
    private const uint MfString = 0x00000000;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const uint ImageIcon = 1;
    private const uint LoadResourceShared = 0x00008000;
    private const int DefaultApplicationIcon = 32512;
    private const uint OpenCommand = 1001;
    private const uint TogglePauseCommand = 1002;
    private const uint GreetNowCommand = 1003;
    private const uint ExitCommand = 1004;
    private const uint TogglePetsCommand = 1005;

    private static readonly Dictionary<IntPtr, TrayIconService> Instances = [];
    private static readonly WindowProcedureDelegateType WindowProcedureDelegate = WindowProcedure;
    private static readonly string WindowClassName = $"HelloCompanion.Tray.{Environment.ProcessId}";

    private readonly IntPtr _windowHandle;
    private bool _isPaused;
    private bool _petsVisible;
    private bool _disposed;

    public TrayIconService()
    {
        IntPtr moduleHandle = GetModuleHandle(null);
        WindowClass windowClass = new()
        {
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProcedureDelegate),
            Instance = moduleHandle,
            ClassName = WindowClassName
        };

        if (RegisterClass(ref windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the tray window class.");
        }

        _windowHandle = CreateWindowEx(0, WindowClassName, "Hello Companion tray host", 0,
            0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, moduleHandle, IntPtr.Zero);

        if (_windowHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the tray window.");
        }

        Instances[_windowHandle] = this;
        NotifyIconData iconData = CreateIconData();
        if (!ShellNotifyIcon(NimAdd, ref iconData))
        {
            Instances.Remove(_windowHandle);
            DestroyWindow(_windowHandle);
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not add the notification-area icon.");
        }
    }

    public event EventHandler? OpenRequested;
    public event EventHandler? TogglePauseRequested;
    public event EventHandler? GreetNowRequested;
    public event EventHandler? TogglePetsRequested;
    public event EventHandler? ExitRequested;

    public void SetPaused(bool isPaused) => _isPaused = isPaused;
    public void SetPetsVisible(bool petsVisible) => _petsVisible = petsVisible;

    private NotifyIconData CreateIconData()
    {
        IntPtr icon = LoadImage(IntPtr.Zero, new IntPtr(DefaultApplicationIcon), ImageIcon,
            0, 0, LoadResourceShared);

        return new NotifyIconData
        {
            Size = Marshal.SizeOf<NotifyIconData>(),
            WindowHandle = _windowHandle,
            Id = 1,
            Flags = NifMessage | NifIcon | NifTip,
            CallbackMessage = CallbackMessage,
            IconHandle = icon,
            Tip = "Hello Companion",
            Info = string.Empty,
            InfoTitle = string.Empty
        };
    }

    private void ShowContextMenu()
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            AppendMenu(menu, MfString, OpenCommand, "Open Hello Companion");
            AppendMenu(menu, MfString, TogglePauseCommand, _isPaused ? "Resume greetings" : "Pause greetings");
            AppendMenu(menu, MfString, GreetNowCommand, "Say hello now");
            AppendMenu(menu, MfString, TogglePetsCommand, _petsVisible ? "Hide desktop pets" : "Show desktop pets");
            AppendMenu(menu, MfSeparator, 0, null);
            AppendMenu(menu, MfString, ExitCommand, "Exit");

            GetCursorPos(out Point cursorPosition);
            SetForegroundWindow(_windowHandle);
            uint command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCommand,
                cursorPosition.X, cursorPosition.Y, 0, _windowHandle, IntPtr.Zero);
            PostMessage(_windowHandle, 0, IntPtr.Zero, IntPtr.Zero);

            switch (command)
            {
                case OpenCommand: OpenRequested?.Invoke(this, EventArgs.Empty); break;
                case TogglePauseCommand: TogglePauseRequested?.Invoke(this, EventArgs.Empty); break;
                case GreetNowCommand: GreetNowRequested?.Invoke(this, EventArgs.Empty); break;
                case TogglePetsCommand: TogglePetsRequested?.Invoke(this, EventArgs.Empty); break;
                case ExitCommand: ExitRequested?.Invoke(this, EventArgs.Empty); break;
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private static IntPtr WindowProcedure(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == CallbackMessage && Instances.TryGetValue(windowHandle, out TrayIconService? instance))
        {
            uint mouseMessage = unchecked((uint)lParam.ToInt64());
            if (mouseMessage is WmRightButtonUp or WmContextMenu)
            {
                instance.ShowContextMenu();
                return IntPtr.Zero;
            }

            if (mouseMessage == WmLeftButtonDoubleClick)
            {
                instance.OpenRequested?.Invoke(instance, EventArgs.Empty);
                return IntPtr.Zero;
            }
        }

        return DefWindowProc(windowHandle, message, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        NotifyIconData iconData = CreateIconData();
        ShellNotifyIcon(NimDelete, ref iconData);
        Instances.Remove(_windowHandle);
        DestroyWindow(_windowHandle);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public int Size;
        public IntPtr WindowHandle;
        public uint Id;
        public uint Flags;
        public uint CallbackMessage;
        public IntPtr IconHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State;
        public uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint VersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid ItemGuid;
        public IntPtr BalloonIconHandle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X; public int Y; }
    private delegate IntPtr WindowProcedureDelegateType(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern ushort RegisterClass(ref WindowClass windowClass);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyWindow(IntPtr windowHandle);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ShellNotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadImage(IntPtr instance, IntPtr name, uint type, int desiredWidth, int desiredHeight, uint loadFlags);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AppendMenu(IntPtr menu, uint flags, uint itemId, string? itemText);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr windowHandle, IntPtr rectangle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetForegroundWindow(IntPtr windowHandle);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
}
