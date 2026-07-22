using System.Text.Json;

namespace CampusNetAutoLogin;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        AppLog.Write($"程序启动 | 参数: {string.Join(' ', args)}");
        if (args.Length >= 2 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase))
        {
            SelfTest.Run(args[1]);
            return;
        }
        if (args.Length >= 2 && args[0].Equals("--wifi-scan-test", StringComparison.OrdinalIgnoreCase))
        {
            WifiScanTestAsync(args[1]).GetAwaiter().GetResult();
            return;
        }

        ApplicationConfiguration.Initialize();
        bool autoMode = args.Any(x => x.Equals("--auto", StringComparison.OrdinalIgnoreCase));
        bool silentMode = args.Any(x => x.Equals("--silent", StringComparison.OrdinalIgnoreCase));
        Application.Run(new Form1(autoMode, silentMode));
    }

    private static async Task WifiScanTestAsync(string output)
    {
        object result;
        try
        {
            var networks = await WifiManager.ScanAsync();
            result = new
            {
                success = true,
                count = networks.Count,
                current = WifiManager.GetCurrentSsid(),
                networks = networks.Select(x => new { x.Ssid, x.Signal, x.SecurityEnabled, x.Authentication }).ToArray()
            };
        }
        catch (Exception ex) { result = new { success = false, error = ex.Message }; }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
    }
}
