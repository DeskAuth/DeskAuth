// DeskAuth, based on WinAuth. Copyright (C) 2011 Colin Mackie and contributors.
// Licensed under the GNU General Public License v3 or later. See LICENSE.

using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DeskAuth.Services;

/// <summary>
/// Notification area icon, using Shell_NotifyIcon directly
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const int CallbackMessage = NativeMethods.WM_APP + 1;

    private readonly HwndSource _window;
    private readonly uint _taskbarCreatedMessage;
    private readonly IntPtr _icon;
    private string _toolTip = "DeskAuth";
    private bool _visible;

    public TrayIcon()
    {
        // hidden window to receive the icon's messages
        _window = new HwndSource(new HwndSourceParameters("DeskAuthTray") { Width = 0, Height = 0, WindowStyle = 0 });
        _window.AddHook(WndProc);
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessage("TaskbarCreated");

        _icon = LoadIcon();
    }

    /// <summary>
    /// Create the tray icon at the size for the display scaling, from the app's .ico file
    /// </summary>
    private static IntPtr LoadIcon()
    {
        int size = NativeMethods.GetSystemMetricsForDpi(NativeMethods.SM_CXSMICON, NativeMethods.GetDpiForSystem());
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/DeskAuth;component/Resources/deskauth.ico"));
        using var stream = resource.Stream;
        using var reader = new BinaryReader(stream);

        // ICONDIR header, then a 16 byte entry per image; a width of 0 means 256
        reader.ReadUInt16();
        reader.ReadUInt16();
        int count = reader.ReadUInt16();
        var entries = new List<(int Size, int Length, int Offset)>();
        for (int i = 0; i < count; i++)
        {
            int width = reader.ReadByte();
            reader.ReadBytes(7);
            entries.Add((width == 0 ? 256 : width, reader.ReadInt32(), reader.ReadInt32()));
        }
        // the smallest image at least as big as needed, so Windows only scales down
        var best = entries.Where(e => e.Size >= size).OrderBy(e => e.Size).FirstOrDefault();
        if (best.Length == 0)
        {
            best = entries.MaxBy(e => e.Size);
        }

        stream.Position = best.Offset;
        byte[] image = reader.ReadBytes(best.Length);
        return NativeMethods.CreateIconFromResourceEx(image, image.Length, true, 0x00030000, size, size, 0);
    }

    /// <summary>
    /// Left click (or keyboard select)
    /// </summary>
    public event EventHandler? Click;

    /// <summary>
    /// Right click; the handler should show a menu
    /// </summary>
    public event EventHandler? ContextMenuRequested;

    public string ToolTip
    {
        get => _toolTip;
        set
        {
            _toolTip = value;
            if (_visible)
            {
                var data = CreateData(NativeMethods.NIF_TIP | NativeMethods.NIF_SHOWTIP);
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref data);
            }
        }
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            if (value == _visible)
            {
                return;
            }
            _visible = value;
            if (value)
            {
                Add();
            }
            else
            {
                var data = CreateData(0);
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref data);
            }
        }
    }

    /// <summary>
    /// Window handle that should be made foreground before showing a tray menu, so the menu closes when clicking elsewhere
    /// </summary>
    public IntPtr Handle => _window.Handle;

    private void Add()
    {
        var data = CreateData(NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP | NativeMethods.NIF_SHOWTIP);
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref data);
        data.uVersion = NativeMethods.NOTIFYICON_VERSION_4;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_SETVERSION, ref data);
    }

    private NativeMethods.NOTIFYICONDATA CreateData(int flags) => new()
    {
        cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = _toolTip,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == CallbackMessage)
        {
            // with NOTIFYICON_VERSION_4 the event is in the low word of lParam
            switch ((int)lParam & 0xffff)
            {
                case NativeMethods.WM_LBUTTONUP:
                    Click?.Invoke(this, EventArgs.Empty);
                    break;
                // sent for right click and keyboard (Shift+F10); WM_RBUTTONUP is also sent but would show a second menu
                case NativeMethods.WM_CONTEXTMENU:
                    ContextMenuRequested?.Invoke(this, EventArgs.Empty);
                    break;
            }
            handled = true;
        }
        else if (msg == _taskbarCreatedMessage && _visible)
        {
            // Explorer restarted
            Add();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Visible = false;
        _window.Dispose();
        if (_icon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_icon);
        }
    }
}
