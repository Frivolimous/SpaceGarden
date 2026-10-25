using System.Runtime.InteropServices;
using Godot;

namespace SpaceGarden;

// Lets clicks fall through the whole window to the desktop, switched on and off as the pointer leaves and enters the
// plant. Windows only: the window is made layered (fully opaque, so the transparent drawing is unchanged) and
// WS_EX_TRANSPARENT is toggled. This replaces Godot's passthrough region, which on Windows is also a window region:
// nothing outside it is drawn, so changing it made the window flash and would clip the toolbar and dragged parts.
public static class ClickThrough
{
    const int GwlExStyle = -20;
    const long WsExLayered = 0x80000;
    const long WsExTransparent = 0x20;
    const uint LwaAlpha = 0x2;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern nint GetWindowLongPtr(nint hWnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    static extern nint SetWindowLongPtr(nint hWnd, int index, nint value);

    [DllImport("user32.dll")]
    static extern bool SetLayeredWindowAttributes(nint hWnd, uint colourKey, byte alpha, uint flags);

    public static bool Supported => OS.GetName() == "Windows";

    // Called every frame: cheap, and it also restores the style if Godot rewrote it (it does when, say, always on
    // top changes).
    public static void Set(bool passThrough)
    {
        if (!Supported) return;
        var hWnd = (nint)DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle);
        long style = GetWindowLongPtr(hWnd, GwlExStyle);
        long wanted = style | WsExLayered;
        wanted = passThrough ? wanted | WsExTransparent : wanted & ~WsExTransparent;
        if (wanted == style) return;
        SetWindowLongPtr(hWnd, GwlExStyle, (nint)wanted);
        if ((style & WsExLayered) == 0) SetLayeredWindowAttributes(hWnd, 0, 255, LwaAlpha);
    }
}
