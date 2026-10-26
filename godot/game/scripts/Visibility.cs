using System.Runtime.InteropServices;
using Godot;

namespace SpaceGarden;

// Whether anyone can see the garden: not while the screen is locked (or a screen saver or another user's session is
// up), the display is off, or a full-screen app (a game, a video, a presentation) fills the garden's monitor. While
// hidden the garden stops drawing and stepping (see Main.CheckVisibility). Windows only; elsewhere it's always visible.
//
// Locked and full screen come from SHQueryUserNotificationState, asked a couple of times a second. Display off only
// arrives as a message, so a message-only window on a thread of its own registers for the display's power state.
public static class Visibility
{
    // SHQueryUserNotificationState's answers.
    const int NotPresent = 1, Busy = 2, RunningD3DFullScreen = 3, PresentationMode = 4;

    [DllImport("shell32.dll")]
    static extern int SHQueryUserNotificationState(out int state);

    [DllImport("user32.dll")]
    static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern nint MonitorFromWindow(nint hWnd, uint flags);

    const uint MonitorDefaultToNearest = 2;

    static volatile bool displayOff;
    static Thread? listener;

    // Why the garden can't be seen now, or null if it can.
    public static string? HiddenReason()
    {
        if (OS.GetName() != "Windows") return null;
        listener ??= StartDisplayListener();
        if (displayOff) return "display off";
        if (SHQueryUserNotificationState(out int state) != 0) return null;
        if (state == NotPresent) return "locked";
        if (state is Busy or RunningD3DFullScreen or PresentationMode)
        {
            // Only one that covers the garden's monitor (our own window never counts: it stops short of full screen).
            var own = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
            var front = GetForegroundWindow();
            if (front != own && MonitorFromWindow(front, MonitorDefaultToNearest) == MonitorFromWindow(own, MonitorDefaultToNearest))
                return "full-screen app";
        }
        return null;
    }

    // ==== DISPLAY ON/OFF ==== \\

    const uint WmPowerBroadcast = 0x0218;
    const nint PbtPowerSettingChange = 0x8013;
    const nint HwndMessage = -3;
    static readonly Guid ConsoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");

    delegate nint WindowProc(nint hWnd, uint msg, nint wParam, nint lParam);
    static readonly WindowProc proc = OnMessage; // kept alive for as long as Windows may call it

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WindowClass
    {
        public uint Size, Style;
        public nint Proc;
        public int ClassExtra, WindowExtra;
        public nint Instance, Icon, Cursor, Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Message
    {
        public nint Window;
        public uint Id;
        public nint WParam, LParam;
        public uint Time;
        public int X, Y;
        public uint Private;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern ushort RegisterClassExW(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern nint CreateWindowExW(uint exStyle, string className, string name, uint style, int x, int y, int width,
        int height, nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll")]
    static extern nint DefWindowProcW(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    static extern int GetMessageW(out Message message, nint hWnd, uint min, uint max);

    [DllImport("user32.dll")]
    static extern nint DispatchMessageW(ref Message message);

    [DllImport("user32.dll")]
    static extern nint RegisterPowerSettingNotification(nint recipient, ref Guid setting, uint flags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern nint GetModuleHandleW(string? name);

    static Thread StartDisplayListener()
    {
        var thread = new Thread(() =>
        {
            var windowClass = new WindowClass
            {
                Size = (uint)Marshal.SizeOf<WindowClass>(),
                Proc = Marshal.GetFunctionPointerForDelegate(proc),
                Instance = GetModuleHandleW(null),
                ClassName = "SpaceGardenDisplayListener",
            };
            if (RegisterClassExW(ref windowClass) == 0) return;
            var window = CreateWindowExW(0, windowClass.ClassName, "", 0, 0, 0, 0, 0, HwndMessage, 0, windowClass.Instance, 0);
            if (window == 0) return;
            var setting = ConsoleDisplayState;
            RegisterPowerSettingNotification(window, ref setting, 0); // Windows replies at once with the current state
            while (GetMessageW(out var message, 0, 0, 0) > 0) DispatchMessageW(ref message);
        })
        { IsBackground = true, Name = "Display listener" };
        thread.Start();
        return thread;
    }

    // POWERBROADCAST_SETTING: the setting's GUID (16 bytes), the data's length (4), then the data: 0 off, 1 on, 2 dimmed.
    static nint OnMessage(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (msg == WmPowerBroadcast && wParam == PbtPowerSettingChange && lParam != 0
            && Marshal.PtrToStructure<Guid>(lParam) == ConsoleDisplayState)
            displayOff = Marshal.ReadInt32(lParam, 20) == 0;
        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }
}
