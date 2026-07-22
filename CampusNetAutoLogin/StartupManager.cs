using Microsoft.Win32;

namespace CampusNetAutoLogin;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CampusNetAutoLogin";

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled)
        {
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法确定程序路径");
            key.SetValue(ValueName, BuildCommand(executable), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }
    }

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public static string BuildCommand(string executable) => $"\"{executable}\" --auto --silent";
}
