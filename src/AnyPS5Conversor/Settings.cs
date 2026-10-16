namespace AnyPS5Conversor;

/// <summary>Preferências do usuário em %APPDATA%\AnyPS5-Conversor: só o que ele escolheu na janela.</summary>
public static class Settings
{
    static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AnyPS5-Conversor");
    static readonly string OutputFile = Path.Combine(Folder, "output.txt");

    public static string DefaultOutput { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AnyPS5");

    /// <summary>Onde salvar os jogos convertidos (e extrair os compactados); o padrão é %USERPROFILE%\AnyPS5.</summary>
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
        set
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(OutputFile, value);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
