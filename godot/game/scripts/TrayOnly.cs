using System.Runtime.InteropServices;
using Godot;

namespace SpaceGarden;

// Keeps the widget off the taskbar (Jeremy, Nov 3): it lives in the tray alone. Windows only: the window is a tool
// window (WS_EX_TOOLWINDOW, without WS_EX_APPWINDOW), which has no taskbar button and isn't in Alt+Tab. The style
// takes once the window is hidden and shown again, so the first Keep does that. With no taskbar button to bring a
// minimised window back, Minimise hides the window instead (Hide), and the tray icon or the hotkey shows it again.
public static class TrayOnly
{
    const int GwlExStyle = -20;
    const long WsExToolWindow = 0x80, WsExAppWindow = 0x40000;
    const int SwHide = 0, SwShowNoActivate = 4;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern nint GetWindowLongPtr(nint hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(nint hWnd, int command);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(nint hWnd);

    public static bool Supported => OS.GetName() == "Windows";

    static bool settled, hidden;

    static nint Window => (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);

    // Called every frame: cheap, and it puts the style back if Godot rewrote it.
    public static void Keep()
    {
        if (!Supported) return;
        var hWnd = Window;
        long style = GetWindowLongPtr(hWnd, GwlExStyle);
        long wanted = (style | WsExToolWindow) & ~WsExAppWindow;
        if (wanted != style) SetWindowLongPtr(hWnd, GwlExStyle, (nint)wanted);
        if (settled || hidden || !IsWindowVisible(hWnd)) return;
        // (A window shown with a taskbar button keeps it until it's hidden and shown again.)
        ShowWindow(hWnd, SwHide);
        ShowWindow(hWnd, SwShowNoActivate);
        settled = true;
    }

    // Minimised: out of sight entirely (there's no taskbar button to minimise to).
    public static void Hide(bool hide)
    {
        if (!Supported || hide == hidden) return;
        hidden = hide;
        ShowWindow(Window, hide ? SwHide : SwShowNoActivate);
    }
}
