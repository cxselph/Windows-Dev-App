using System.IO;
using System.Text.Json;
using WindowsDevApp.Models;

namespace WindowsDevApp.Services;

public class ProfileData
{
    public List<Profile> Profiles { get; set; } = new();
    public string? SelectedProfileId { get; set; }
}

public static class ProfileStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowsDevApp");
    private static readonly string FilePath = Path.Combine(Dir, "profiles.json");

    public static ProfileData Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new ProfileData();
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<ProfileData>(json) ?? new ProfileData();
        }
        catch
        {
            return new ProfileData();
        }
    }

    public static void Save(ProfileData data)
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}
