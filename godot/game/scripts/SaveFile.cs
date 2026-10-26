using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using FileAccess = Godot.FileAccess;

namespace SpaceGarden;

// The garden (its parts' places are their places on the desktop) plus what only the overlay needs (when it was
// last seen, the Size and Idle frame rate settings), as JSON in user://.
public sealed record SaveData(GardenSave Garden, double LastSeenUnix, float PlantScale = 0.75f, int IdleFps = 30);

public static class SaveFile
{
    public static string? Path = "user://garden.json";

    static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static SaveData? Load()
    {
        if (Path == null || !FileAccess.FileExists(Path)) return null;
        try
        {
            return JsonSerializer.Deserialize<SaveData>(FileAccess.GetFileAsString(Path), Options);
        }
        catch (Exception e)
        {
            GD.PushWarning($"Couldn't read the save ({e.Message}); starting a new garden.");
            return null;
        }
    }

    public static void Write(SaveData data)
    {
        if (Path == null) return;
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(data, Options));
    }
}
