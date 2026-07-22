using System.Text.Json;

namespace CampusNetAutoLogin;

internal static class SelfTest
{
    public static void Run(string outputPath)
    {
        var checks = new Dictionary<string, object>();
        try
        {
            const string secret = "测试密码-P@ssw0rd";
            string encrypted = CredentialProtection.Protect(secret);
            checks["dpapi_roundtrip"] = CredentialProtection.Unprotect(encrypted) == secret;
            checks["dpapi_not_plaintext"] = !encrypted.Contains(secret, StringComparison.Ordinal);
            string js = PortalAutomation.BuildScript("student", "secret", true);
            checks["drcom_fields"] = js.Contains("DDDDD") && js.Contains("upass") && js.Contains("0MKKey");
            checks["generic_fields"] = js.Contains("username") && js.Contains("password");
            checks["startup_command"] = StartupManager.BuildCommand(@"C:\Program Files\Campus Login\app.exe")
                == "\"C:\\Program Files\\Campus Login\\app.exe\" --auto --silent";
            var settings = new AppSettings();
            settings.Networks.Add(new SavedWifiProfile { Ssid = "Test", EncryptedWifiPassword = encrypted });
            checks["multi_network_model"] = settings.Networks.Count == 1 && settings.Networks[0].Ssid == "Test";
            checks["default_portal"] = settings.PortalUrl == "http://10.200.84.3/a79.htm";
            checks["success"] = checks.Values.OfType<bool>().All(x => x);
        }
        catch (Exception ex)
        {
            checks["success"] = false;
            checks["error"] = ex.Message;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
