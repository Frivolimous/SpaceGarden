using Godot;

namespace SpaceGarden;

// Dev aid: "-- --screenshot=out.png [--frames=N]" saves the viewport after N physics ticks (1/60 s) and quits;
// "--at=60,600,1800" saves out_60.png, out_600.png, ... instead. The PNG keeps the window's transparency
// (add --backdrop=101820 to see the plant against a colour). "--prune=N" prunes the biggest branch off the core's
// first stem at tick N, to catch the cut branch drifting off.
public static class Screenshot
{
    public static async void CaptureIfRequested(Node owner)
    {
        string? path = null;
        int? pruneAt = null;
        var frames = new List<int> { 60 };
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--screenshot=")) path = arg["--screenshot=".Length..];
            else if (arg.StartsWith("--frames=")) frames = new List<int> { int.Parse(arg["--frames=".Length..]) };
            else if (arg.StartsWith("--at=")) frames = arg["--at=".Length..].Split(',').Select(int.Parse).ToList();
            else if (arg.StartsWith("--prune=")) pruneAt = int.Parse(arg["--prune=".Length..]);
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
                if (frame == pruneAt && owner is Main main) PruneBiggest(main.Garden);
            }
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var file = frames.Count == 1 ? path : path.Replace(".png", $"_{target}.png");
            owner.GetViewport().GetTexture().GetImage().SavePng(file);
            GD.Print($"Screenshot saved: {file}");
        }
        tree.Quit();
    }

    static void PruneBiggest(Garden garden)
    {
        int Size(PlantNode n) => 1 + n.Branches.Sum(Size) + n.Fruits.Count;
        var stem = garden.Core.Branches.FirstOrDefault();
        if (stem?.Branches.MaxBy(Size) is { } branch) garden.Prune(branch);
    }
}
