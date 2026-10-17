namespace AnyPSConversor;

/// <summary>Preferências do usuário em %APPDATA%\AnyPS-Conversor: só o que ele escolheu na janela.</summary>
public static class Settings
{
    public const string Name = "AnyPS-Conversor";

    /// <summary>Nome das pastas do programa até a versão 0.6, quando ele se chamava AnyPS5 Conversor.</summary>
    public const string LegacyName = "AnyPS5-Conversor";

    public static string Folder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);
    static readonly string OutputFile = Path.Combine(Folder, "output.txt");
    static readonly string AutoPlayFile = Path.Combine(Folder, "auto-play.txt");

    /// <summary>Abrir o jogo assim que a conversão terminar; desligado por padrão.</summary>
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

    /// <summary>Pasta de saída padrão até a versão 0.6; só para limpar o que sobrou lá.</summary>
    public static string LegacyDefaultOutput { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AnyPS5");

    /// <summary>
    /// Na primeira execução depois da troca de nome, renomeia as pastas antigas em %APPDATA% e %LOCALAPPDATA%
    /// para as novas: o usuário não perde o idioma, o local escolhido nem os registros. Só a abertura normal do
    /// programa chama isto, antes de ler qualquer configuração.
    /// </summary>
    public static void MigrateLegacyFolders()
    {
        // Uma versão antiga aberta ainda usa as pastas dela; a migração fica para a próxima abertura.
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
