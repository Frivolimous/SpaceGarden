using System.Numerics;

namespace SpaceGarden.Core.Tests;

// The Sway Controller (Jeremy, Nov 3): placed anywhere, its dial scales the wind's sway for the whole garden; gained at
// garden size 60.
public class SwayTests
{
    static Garden WithStems(int seed = 7)
    {
        var garden = Garden.New(Layout.Radial, seed);
        garden.Capacity = new Capacity(5, 0, 1, Sways: 1);
        garden.PassiveGrowth = false;
        var tip = garden.Core;
        for (int i = 0; i < 4; i++) tip = garden.GrowBranch(tip, NodeKind.Stem)!;
        for (int i = 0; i < 600; i++) garden.Step(); // settled
        return garden;
    }

    [Fact]
    public void ItsDialRunsFromNoneThroughTheUsualToMost()
    {
        var dial = new ShapingTool(ShapingKind.Sway, Vector2.Zero); // placed pointing up
        Assert.Equal(0.5f, dial.Dial, 3);
        Assert.Equal(1f, dial.Sway, 3);
        dial.Dial = 0;
        Assert.Equal(0f, dial.Sway, 3);
        dial.Dial = 1;
        Assert.Equal(Tuning.SwayMax, dial.Sway, 3);
        dial.Dial = 0.75f;
        Assert.Equal((1 + Tuning.SwayMax) / 2, dial.Sway, 3);

        // Turned by its knob: left of up is less, right of up more, and past either end it stays there.
        dial.DialTo(new Vector2(-10, 0));
        Assert.InRange(dial.Sway, 0.1f, 0.9f);
        dial.DialTo(new Vector2(10, 0));
        Assert.InRange(dial.Sway, 1.1f, Tuning.SwayMax - 0.1f);
        dial.DialTo(new Vector2(1, 10)); // just right of straight down: the right end
        Assert.Equal(Tuning.SwayMax, dial.Sway, 3);
        dial.DialTo(new Vector2(-1, 10)); // just left of it: the left end
        Assert.Equal(0f, dial.Sway, 3);
    }

    [Fact]
    public void ItScalesTheWholeGardensSwayWhereverItIs()
    {
        float Swing(float dial)
        {
            var garden = WithStems();
            var tool = garden.Place(ShapingKind.Sway, new Vector2(500, 500))!; // far from the plant
            tool.Dial = dial;
            var tip = garden.Nodes[^1];
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 3000; i++)
            {
                garden.Step();
                float along = tip.Position.X + tip.Position.Y;
                min = Math.Min(min, along);
                max = Math.Max(max, along);
            }
            return max - min;
        }
        float none = Swing(0), usual = Swing(0.5f), most = Swing(1);
        Assert.True(none < usual * 0.2f, $"none {none}, usual {usual}");
        Assert.True(most > usual * 1.5f, $"most {most}, usual {usual}");
    }

    [Fact]
    public void ItTiesNothingAndIsGainedAtSixty()
    {
        var garden = WithStems();
        var tool = garden.Place(ShapingKind.Sway, Vector2.Zero)!;
        Assert.False(garden.Tie(garden.Nodes[1], tool));
        Assert.Equal(0, Tuning.SwaysAtSize(59));
        Assert.Equal(1, Tuning.SwaysAtSize(60));
        Assert.Equal(60, Tuning.NextToolSize(ShapingKind.Sway, 10));
        Assert.Null(Tuning.NextToolSize(ShapingKind.Sway, 60));
        Assert.Null(Garden.New(Layout.Radial).Place(ShapingKind.Sway, Vector2.Zero)); // none at the start

        // Its dial is saved (as its angle).
        tool.Dial = 0.9f;
        var loaded = Garden.FromSave(garden.ToSave());
        Assert.Equal(0.9f, loaded.Shaping.Single(t => t.Kind == ShapingKind.Sway).Dial, 3);
    }
}
