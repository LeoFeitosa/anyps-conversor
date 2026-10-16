namespace AnyPSConversor;

/// <summary>Preferências do usuário em %APPDATA%\AnyPS-Conversor: só o que ele escolheu na janela.</summary>
public static class Settings
{
    public const string Name = "AnyPS-Conversor";

    /// <summary>Nome das pastas do programa até a versão 0.6, quando ele se chamava AnyPS5 Conversor.</summary>
    public const string LegacyName = "AnyPS5-Conversor";

    public static string Folder { get; } = Migrate(Environment.SpecialFolder.ApplicationData);
    static readonly string OutputFile = Path.Combine(Folder, "output.txt");

    public static string DefaultOutput { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AnyPS");

    /// <summary>Pasta de saída padrão até a versão 0.6; só para limpar o que sobrou lá.</summary>
    public static string LegacyDefaultOutput { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AnyPS5");

    /// <summary>
    /// Pasta do programa dentro de <paramref name="root"/>. Na primeira execução depois da troca de nome, a pasta
    /// antiga é renomeada para a nova, e o usuário não perde o idioma, o local escolhido nem os registros.
    /// </summary>
    public static string Migrate(Environment.SpecialFolder root)
    {
        var parent = Environment.GetFolderPath(root);
        var folder = Path.Combine(parent, Name);
        var legacy = Path.Combine(parent, LegacyName);
        try
        {
            if (Directory.Exists(legacy) && !Directory.Exists(folder))
                Directory.Move(legacy, folder);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return folder;
    }

    /// <summary>Onde salvar os jogos convertidos (e extrair os compactados); o padrão é %USERPROFILE%\AnyPS.</summary>
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
