using Godot;

namespace SpaceGarden;

// Dev aid: "-- --screenshot=out.png [--frames=N]" saves the viewport after N physics ticks (1/60 s) and quits;
// "--at=60,600,1800" saves out_60.png, out_600.png, ... instead. The PNG keeps the window's transparency
// (add --backdrop=101820 to see the plant against a colour).
public static class Screenshot
{
    public static async void CaptureIfRequested(Node owner)
    {
        string? path = null;
        var frames = new List<int> { 60 };
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--screenshot=")) path = arg["--screenshot=".Length..];
            else if (arg.StartsWith("--frames=")) frames = new List<int> { int.Parse(arg["--frames=".Length..]) };
            else if (arg.StartsWith("--at=")) frames = arg["--at=".Length..].Split(',').Select(int.Parse).ToList();
        }
        if (path == null) return;

        var tree = owner.GetTree();
        int frame = 0;
        foreach (var target in frames.Order())
        {
            while (frame < target)
            {
                await owner.ToSignal(tree, SceneTree.SignalName.PhysicsFrame);
                frame++;
            }
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var file = frames.Count == 1 ? path : path.Replace(".png", $"_{target}.png");
            owner.GetViewport().GetTexture().GetImage().SavePng(file);
            GD.Print($"Screenshot saved: {file}");
        }
        tree.Quit();
    }
}
