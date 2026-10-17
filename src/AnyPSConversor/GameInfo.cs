using System.Text.Json;

namespace AnyPSConversor;

/// <summary>Erro mostrado ao usuário; a mensagem é uma chave de <see cref="Strings"/>.</summary>
public sealed class UserException(string key, params object[] args) : Exception(key)
{
    public string Key { get; } = key;
    public object[] Args { get; } = args;
}

/// <summary>Pasta de um jogo/app pronta para conversão. <see cref="Load"/> lê o formato do PS5.</summary>
public sealed record GameInfo(
    string Folder,
    string TitleId,
    string Name,
    string? IconPath,
    bool HasParamJson,
    IReadOnlyList<string> Modules)
{
    /// <summary>Console do jogo: quem sabe convertê-lo.</summary>
    public required IConsolePlatform Platform { get; init; }

    /// <summary>Pastas de módulos que o relinker procura ao lado do eboot.bin.</summary>
    public static readonly string[] ModuleDirectories = ["sce_module", "sce_modules", "prx"];

    /// <summary>
    /// Extensões de módulo que o relinker reconhece. Outros arquivos nas pastas de módulos, como as cópias
    /// .esbak que ferramentas de assinatura deixam, não são módulos: repassados, o relinker veria dois libc.prx
    /// e recusaria o jogo ("Ambiguous guest dependency name").
    /// </summary>
    public static readonly string[] ModuleExtensions = [".prx", ".sprx", ".suprx", ".debug_prx"];

    public static bool IsModuleFile(string name) =>
        ModuleExtensions.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <param name="path">Pasta do jogo, uma pasta acima dela ou o próprio eboot.bin.</param>
    /// <param name="fallbackName">Nome usado quando não há sce_sys/param.json.</param>
    public static GameInfo Load(string path, string? fallbackName = null)
    {
        var folder = FindGameFolder(File.Exists(path) ? Path.GetDirectoryName(path)! : path);

        CheckExecutable(Path.Combine(folder, "eboot.bin"));

        var paramPath = Path.Combine(folder, "sce_sys", "param.json");
        var (titleId, name) = File.Exists(paramPath) ? ReadParam(File.ReadAllText(paramPath)) : (null, null);
        var iconPath = Path.Combine(folder, "sce_sys", "icon0.png");

        var modules = ModuleDirectories
            .Select(d => Path.Combine(folder, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d))
            .Where(f => IsModuleFile(f) && IsExecutable(f))
            .ToList();

        var fallback = fallbackName ?? Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar));
        return new GameInfo(
            folder,
            titleId ?? fallback,
            name ?? titleId ?? fallback,
            File.Exists(iconPath) ? iconPath : null,
            File.Exists(paramPath),
            modules) { Platform = Ps5Platform.Instance };
    }

    /// <summary>Nome seguro para arquivo e pasta.</summary>
    public string SafeName
    {
        get
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(Name.Select(c => invalid.Contains(c) ? ' ' : c).ToArray()).Trim().TrimEnd('.');
            return cleaned.Length == 0 ? "Game" : cleaned;
        }
    }

    /// <summary>
    /// Pasta com o eboot.bin: a própria ou a mais rasa entre as subpastas (até 4 níveis), como as que
    /// vêm de um arquivo baixado com pastas a mais. Dois jogos no mesmo nível é ambíguo.
    /// </summary>
    static string FindGameFolder(string folder)
    {
        if (!Directory.Exists(folder))
            throw new UserException("err_no_eboot");
        IEnumerable<string> level = [folder];
        for (var depth = 0; depth <= 4; depth++)
        {
            var list = level.ToList();
            var games = list.Where(d => File.Exists(Path.Combine(d, "eboot.bin"))).ToList();
            if (games.Count == 1)
                return games[0];
            if (games.Count > 1)
                throw new UserException("err_many_games", string.Join(", ", games.Select(Path.GetFileName)));
            level = list.SelectMany(d => Directory.EnumerateDirectories(d));
        }
        throw new UserException("err_no_eboot");
    }

    static void CheckExecutable(string path)
    {
        var head = ReadHead(path);
        if (SelfUnwrapper.IsElf(head))
            return;
        if (!SelfUnwrapper.IsSelf(head))
            throw new UserException("err_not_executable", Path.GetFileName(path));
        try
        {
            SelfUnwrapper.Unwrap(File.ReadAllBytes(path));
        }
        catch (EncryptedSelfException)
        {
            throw new UserException("err_encrypted", Path.GetFileName(path));
        }
        catch (SelfException e)
        {
            throw new UserException("err_unwrap", Path.GetFileName(path), e.Message);
        }
    }

    static bool IsExecutable(string path)
    {
        var head = ReadHead(path);
        return SelfUnwrapper.IsSelf(head) || SelfUnwrapper.IsElf(head);
    }

    static byte[] ReadHead(string path)
    {
        var head = new byte[4];
        using var stream = File.OpenRead(path);
        return stream.Read(head) == 4 ? head : [];
    }

    // Mesma escolha do AnyPS5: defaultLanguage (em localizedParameters ou no topo), depois en-US, depois o primeiro idioma.
    /// <summary>Código e nome do título no conteúdo de um sce_sys/param.json.</summary>
    public static (string? TitleId, string? Name) ReadParam(string text)
    {
        try
        {
            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            var titleId = root.TryGetProperty("titleId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
            if (!root.TryGetProperty("localizedParameters", out var localized) || localized.ValueKind != JsonValueKind.Object)
                return (titleId, null);

            string? language = null;
            if (localized.TryGetProperty("defaultLanguage", out var lang) && lang.ValueKind == JsonValueKind.String)
                language = lang.GetString();
            else if (root.TryGetProperty("defaultLanguage", out lang) && lang.ValueKind == JsonValueKind.String)
                language = lang.GetString();

            JsonElement entry = default;
            var found = (language is not null && localized.TryGetProperty(language, out entry) && entry.ValueKind == JsonValueKind.Object)
                || (localized.TryGetProperty("en-US", out entry) && entry.ValueKind == JsonValueKind.Object);
            if (!found)
            {
                foreach (var property in localized.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Object)
                        continue;
                    entry = property.Value;
                    found = true;
                    break;
                }
            }

            var name = found && entry.TryGetProperty("titleName", out var title) && title.ValueKind == JsonValueKind.String
                ? title.GetString()
                : null;
            return (titleId, string.IsNullOrWhiteSpace(name) ? null : name);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
