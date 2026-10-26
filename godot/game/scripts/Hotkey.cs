using System.Runtime.InteropServices;
using Godot;

namespace SpaceGarden;

// A system-wide hotkey (Ctrl+Alt+G) that reaches the widget whatever has the focus, since in Ambient mode the window
// never has it. Windows only: RegisterHotKey sends WM_HOTKEY to the widget's window, whose window procedure is
// wrapped to catch it (Godot's own ignores it). The key is taken from other apps while the garden runs. Pressing
// only sets a flag; the widget takes it on its next frame (TakePress), outside Godot's message handling.
public static class Hotkey
{
    public const string Name = "Ctrl+Alt+G";

    const int Id = 0x5347; // "SG"
    const uint ModAlt = 0x1, ModControl = 0x2, ModNoRepeat = 0x4000;
    const uint KeyG = 0x47;
    const uint WmHotkey = 0x0312;
    const int GwlpWndProc = -4;

    delegate nint WndProc(nint hWnd, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    static extern bool RegisterHotKey(nint hWnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    static extern nint CallWindowProc(nint previous, nint hWnd, uint message, nint wParam, nint lParam);

    static WndProc? hook; // kept alive: the window calls it from native code
    static nint previous, window;
    static bool pressed;

    // Whether it could be registered (another app may hold the same keys).
    public static bool Registered { get; private set; }

    public static void Register()
    {
        if (OS.GetName() != "Windows" || Registered) return;
        window = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        hook = Hook;
        previous = SetWindowLongPtr(window, GwlpWndProc, Marshal.GetFunctionPointerForDelegate(hook));
        Registered = RegisterHotKey(window, Id, ModControl | ModAlt | ModNoRepeat, KeyG);
        if (!Registered) GD.PushWarning($"Space Garden: couldn't register the {Name} hotkey (another app has it?)");
    }

    public static void Unregister()
    {
        if (hook == null) return;
        if (Registered) UnregisterHotKey(window, Id);
        SetWindowLongPtr(window, GwlpWndProc, previous);
        Registered = false;
        hook = null;
    }

    // Whether it's been pressed since the last call.
    public static bool TakePress()
    {
        bool was = pressed;
        pressed = false;
        return was;
    }

    static nint Hook(nint hWnd, uint message, nint wParam, nint lParam)
    {
        if (message == WmHotkey && wParam == Id)
        {
            pressed = true;
            return 0;
        }
        return CallWindowProc(previous, hWnd, message, wParam, lParam);
    }
}
