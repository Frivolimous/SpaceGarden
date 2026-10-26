using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using FileAccess = Godot.FileAccess;

namespace SpaceGarden;

// The garden (its parts' places are their places on the desktop) plus what only the overlay needs (when it was
// last seen, the Size and Idle frame rate settings), as JSON in user://. Saves aren't migrated until release: one
// from another Version is set aside and a new garden starts.
public sealed record SaveData(int Version, GardenSave Garden, double LastSeenUnix, float PlantScale = 0.75f, int IdleFps = 30);

public static class SaveFile
{
    // 2: the economy (power, wilting, capacity). 3: genes and the unlock track. 4: groves and fruit blobs.
    public const int Version = 4;

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
            var text = FileAccess.GetFileAsString(Path);
            using (var doc = JsonDocument.Parse(text))
            {
                int version = doc.RootElement.TryGetProperty("Version", out var v) && v.TryGetInt32(out var n) ? n : 1;
                if (version != Version)
                {
                    // Kept beside the new one rather than overwritten, in case it's wanted.
                    DirAccess.RenameAbsolute(Path, Path.Replace(".json", $".v{version}.json"));
                    GD.Print($"The save is from version {version}, not {Version}; starting a new garden.");
                    return null;
                }
            }
            return JsonSerializer.Deserialize<SaveData>(text, Options);
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
