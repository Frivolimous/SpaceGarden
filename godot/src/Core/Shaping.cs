using System.Numerics;

namespace SpaceGarden.Core;

// The shaping tools (DESIGN.md, Shaping the garden): overlays the player places to give the plant form, not parts of
// it, so they take no node slot and have no economy effect.
//   - A Gravity Pot holds one part: the part moves to the pot and is locked there, and everything beyond it on its
//     branch is pushed gently the way the pot faces.
//   - A Trellis is a straight line: the parts tied to it are held on the line, sliding along it, so the plant's own
//     push spaces them out.
//   - A Gravity Well pulls every part towards it, for the plant to lean that way.
// How many of each the garden may place is in its Capacity (the garden's size raises it: see Garden.GainTools).
public enum ShapingKind { Pot, Trellis, Well }

// Which bit of a tool the pointer is on: its body (to move it), a pot's turning knob, or one of a trellis's ends.
public enum ShapingHandle { Body, Turn, EndA, EndB }

public sealed class ShapingTool
{
    // Sizes in garden units. A pot is a flower pot holding its part up to its middle: a rim PotRim deep straight across
    // the part's middle, overhanging the bowl by PotLip each side, and under it a round bowl PotGap wider than the part
    // (see Garden.PotRadius), on the side away from the way it faces. Its turning knob is PotKnob beyond the part. An
    // empty pot is sized for EmptyPot.
    public const float PotGap = 3, PotRim = 5, PotLip = 2.5f, PotKnob = 16, EmptyPot = 10;
    public const float WellRadius = 16;
    public const float TrellisLength = 160, TrellisMinLength = 50;
    // How far from a handle (a pot's knob, a trellis's end) the pointer still takes it, besides the pointer's slop.
    public const float HandleReach = 14;

    public readonly ShapingKind Kind;
    // Where a pot's part sits, a trellis's middle, a well's centre.
    public Vector2 Position;
    // The way a pot faces (its part's branch is pushed this way), or a trellis's line.
    public float Angle;
    public float Length; // a trellis's

    public ShapingTool(ShapingKind kind, Vector2 position, float angle = -MathF.PI / 2, float length = TrellisLength)
    {
        Kind = kind;
        Position = position;
        Angle = angle;
        Length = kind == ShapingKind.Trellis ? Math.Max(TrellisMinLength, length) : 0;
    }

    public Vector2 Facing => new(MathF.Cos(Angle), MathF.Sin(Angle));

    public (Vector2 A, Vector2 B) Ends => (Position - Facing * Length / 2, Position + Facing * Length / 2);

    // The nearest point on a trellis's line to p.
    public Vector2 OnLine(Vector2 p)
    {
        var along = Math.Clamp(Vector2.Dot(p - Position, Facing), -Length / 2, Length / 2);
        return Position + Facing * along;
    }

    // Moves one end of a trellis to p, the other staying where it is.
    public void MoveEnd(ShapingHandle end, Vector2 p)
    {
        var (a, b) = Ends;
        var fixedEnd = end == ShapingHandle.EndA ? b : a;
        var d = end == ShapingHandle.EndA ? fixedEnd - p : p - fixedEnd;
        if (d.Length() < TrellisMinLength) d = (d.LengthSquared() < 1e-6f ? Facing : Vector2.Normalize(d)) * TrellisMinLength;
        Angle = MathF.Atan2(d.Y, d.X);
        Length = d.Length();
        Position = end == ShapingHandle.EndA ? fixedEnd - d / 2 : fixedEnd + d / 2;
    }

    // Turns a pot to face p.
    public void TurnTo(Vector2 p)
    {
        var d = p - Position;
        if (d.LengthSquared() > 1) Angle = MathF.Atan2(d.Y, d.X);
    }
}

public sealed record ShapingSave(ShapingKind Kind, float X, float Y, float Angle, float Length, List<int> Tied);
