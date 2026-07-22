using System.Text.Json;

namespace CampusNetAutoLogin;

internal sealed class SavedWifiProfile
{
    public string Ssid { get; set; } = "";
    public string EncryptedWifiPassword { get; set; } = "";
    public bool SecurityEnabled { get; set; } = true;
    public uint Authentication { get; set; }
    public string PortalUsername { get; set; } = "";
    public string EncryptedPortalPassword { get; set; } = "";
    public string PortalUrl { get; set; } = "http://10.200.84.3/a79.htm";
    public int Priority { get; set; } = 10;
    public bool AutoConnect { get; set; } = true;
}

internal sealed class AppSettings
{
    public List<SavedWifiProfile> Networks { get; set; } = [];
    public string ProbeUrl { get; set; } = "http://www.msftconnecttest.com/connecttest.txt";
    public bool AutoStart { get; set; } = true;
    public bool AutoSubmit { get; set; } = true;
    public DateTime LastSavedUtc { get; set; } = DateTime.UtcNow;

    public string Username { get; set; } = "";
    public string EncryptedPassword { get; set; } = "";
    public string PortalUrl { get; set; } = "http://10.200.84.3/a79.htm";

    public static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CampusNetAutoLogin");
    public static string ConfigPath => Path.Combine(DataDirectory, "config.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppSettings();
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(ConfigPath)) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        LastSavedUtc = DateTime.UtcNow;
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
