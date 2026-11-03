using System.Runtime.InteropServices;
using Godot;

namespace SpaceGarden;

// Keeps the widget off the taskbar (Jeremy, Nov 3): it lives in the tray alone. Windows only: the window is a tool
// window (WS_EX_TOOLWINDOW, without WS_EX_APPWINDOW), which has no taskbar button and isn't in Alt+Tab. The button it
// was shown with at launch is taken off by the taskbar itself (ITaskbarList.DeleteTab: hiding and showing the window
// again would do it too, but flashed). With no taskbar button to bring a minimised window back, Minimise hides the
// window instead (Hide), and the tray icon or the hotkey shows it again.
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
        if (settled || !IsWindowVisible(hWnd)) return;
        settled = true;
        try
        {
            var taskbar = (ITaskbarList)new TaskbarList();
            taskbar.HrInit();
            taskbar.DeleteTab(hWnd);
            if (OperatingSystem.IsWindows()) Marshal.ReleaseComObject(taskbar);
        }
        catch (Exception e)
        {
            GD.PushWarning($"Couldn't take the widget off the taskbar: {e.Message}");
        }
    }

    [ComImport, Guid("56FDF342-FD6D-11d0-958A-006097C9A090"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface ITaskbarList
    {
        void HrInit();
        void AddTab(nint hWnd);
        void DeleteTab(nint hWnd);
        void ActivateTab(nint hWnd);
        void SetActiveAlt(nint hWnd);
    }

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090")]
    class TaskbarList { }

    // Minimised: out of sight entirely (there's no taskbar button to minimise to).
    public static void Hide(bool hide)
    {
        if (!Supported || hide == hidden) return;
        hidden = hide;
        ShowWindow(Window, hide ? SwHide : SwShowNoActivate);
    }
}
