namespace AnyPSConversor;

/// <summary>User preferences in %APPDATA%\AnyPS-Conversor: only what they chose in the window.</summary>
public static class Settings
{
    public const string Name = "AnyPS-Conversor";

    /// <summary>Name of the program's folders up to version 0.6, when it was called AnyPS5 Conversor.</summary>
    public const string LegacyName = "AnyPS5-Conversor";

    public static string Folder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);
    static readonly string OutputFile = Path.Combine(Folder, "output.txt");
    static readonly string AutoPlayFile = Path.Combine(Folder, "auto-play.txt");

    /// <summary>Open the game as soon as the conversion finishes; off by default.</summary>
    public static bool AutoPlay
    {
        get
        {
            try
            {
                return File.Exists(AutoPlayFile) && File.ReadAllText(AutoPlayFile).Trim() == "1";
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        set => Save(AutoPlayFile, value ? "1" : "0");
    }

    public static string DefaultOutput { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AnyPS");

    /// <summary>Default output folder up to version 0.6; only to clean up what was left there.</summary>
    public static string LegacyDefaultOutput { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AnyPS5");

    /// <summary>
    /// On the first run after the rename, renames the old folders in %APPDATA% and %LOCALAPPDATA%
    /// to the new ones: the user keeps the language, the chosen location and the logs. Only the normal startup of the
    /// program calls this, before reading any setting.
    /// </summary>
    public static void MigrateLegacyFolders()
    {
        // An old version still open uses its own folders; the migration waits for the next startup.
        if (System.Diagnostics.Process.GetProcessesByName(LegacyName).Length > 0)
            return;
        foreach (var root in new[] { Environment.SpecialFolder.ApplicationData, Environment.SpecialFolder.LocalApplicationData })
        {
            var parent = Environment.GetFolderPath(root);
            var legacy = Path.Combine(parent, LegacyName);
            var folder = Path.Combine(parent, Name);
            try
            {
                if (Directory.Exists(legacy) && !Directory.Exists(folder))
                    Directory.Move(legacy, folder);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Where to save converted games (and extract archives); the default is %USERPROFILE%\AnyPS.</summary>
    public static string Output
    {
        get
        {
            try
            {
                var saved = File.Exists(OutputFile) ? File.ReadAllText(OutputFile).Trim() : "";
                return saved.Length > 0 && Path.IsPathFullyQualified(saved) ? saved : DefaultOutput;
            }
            catch (IOException) { return DefaultOutput; }
            catch (UnauthorizedAccessException) { return DefaultOutput; }
        }
        set => Save(OutputFile, value);
    }

    static void Save(string file, string value)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(file, value);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
