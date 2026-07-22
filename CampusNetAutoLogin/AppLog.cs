namespace CampusNetAutoLogin;

internal static class AppLog
{
    private static readonly object Gate = new();
    public static string PathName => Path.Combine(AppSettings.DataDirectory, "activity.log");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DataDirectory);
            lock (Gate)
                File.AppendAllText(PathName, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        catch { }
    }
}
