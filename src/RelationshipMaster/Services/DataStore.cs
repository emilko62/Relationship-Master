using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RelationshipMaster.Models;

namespace RelationshipMaster.Services;

/// <summary>Persists fields and settings as JSON under %AppData%\RelationshipMaster.</summary>
public static class DataStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Folder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RelationshipMaster");

    private static string DataPath => Path.Combine(Folder, "fields.json");
    private static string KeyPath => Path.Combine(Folder, "apikey.bin");

    public static AppData Load()
    {
        try
        {
            if (File.Exists(DataPath))
            {
                var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(DataPath), Json);
                if (data != null && data.Fields.Count > 0) return data;
            }
        }
        catch (Exception ex)
        {
            // Keep a copy of the unreadable file rather than silently overwriting it.
            try { File.Copy(DataPath, DataPath + ".corrupt", overwrite: true); } catch { }
            MessageBox.Show($"Could not read saved data, starting fresh.\n{ex.Message}", "Relationship Master");
        }
        return SampleData.Create();
    }

    public static void Save(AppData data)
    {
        Directory.CreateDirectory(Folder);
        var tmp = DataPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(data, Json));
        File.Move(tmp, DataPath, overwrite: true);
    }

    public static void Export(AppData data, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(data, Json));

    public static AppData? Import(string path) =>
        JsonSerializer.Deserialize<AppData>(File.ReadAllText(path), Json);

    /// <summary>Returns the stored API key (DPAPI-encrypted, current Windows user), or the ANTHROPIC_API_KEY env var.</summary>
    public static string? LoadApiKey()
    {
        try
        {
            if (File.Exists(KeyPath))
            {
                var bytes = ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser);
                var key = Encoding.UTF8.GetString(bytes);
                if (!string.IsNullOrWhiteSpace(key)) return key;
            }
        }
        catch { /* fall through to env var */ }
        var env = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        return string.IsNullOrWhiteSpace(env) ? null : env;
    }

    public static void SaveApiKey(string? key)
    {
        Directory.CreateDirectory(Folder);
        if (string.IsNullOrWhiteSpace(key))
        {
            if (File.Exists(KeyPath)) File.Delete(KeyPath);
            return;
        }
        var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(KeyPath, enc);
    }
}
