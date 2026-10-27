using System.Text.Json;

namespace AnyPSConversor;

/// <summary>Error shown to the user; the message is a <see cref="Strings"/> key.</summary>
public sealed class UserException(string key, params object[] args) : Exception(key)
{
    public string Key { get; } = key;
    public object[] Args { get; } = args;
}

/// <summary>A game/app folder ready for conversion. <see cref="Load"/> reads the PS5 format.</summary>
public sealed record GameInfo(
    string Folder,
    string TitleId,
    string Name,
    string? IconPath,
    bool HasParamJson,
    IReadOnlyList<string> Modules)
{
    /// <summary>The game's console: the one that knows how to convert it.</summary>
    public required IConsolePlatform Platform { get; init; }

    /// <summary>Module folders the relinker looks for next to eboot.bin.</summary>
    public static readonly string[] ModuleDirectories = ["sce_module", "sce_modules", "prx"];

    /// <summary>
    /// Module extensions the relinker recognizes. Other files in the module folders, such as the .esbak
    /// copies that signing tools leave behind, are not modules: passed along, the relinker would see two libc.prx
    /// and reject the game ("Ambiguous guest dependency name").
    /// </summary>
    public static readonly string[] ModuleExtensions = [".prx", ".sprx", ".suprx", ".debug_prx"];

    public static bool IsModuleFile(string name) =>
        ModuleExtensions.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase));

    /// <param name="path">Game folder, a folder above it, or the eboot.bin itself.</param>
    /// <param name="fallbackName">Name used when there is no sce_sys/param.json.</param>
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

    /// <summary>Name safe for a file and folder.</summary>
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
    /// Folder with eboot.bin: itself or the shallowest among the subfolders (up to 4 levels), like those
    /// coming from a downloaded archive with extra folders. Two games at the same level is ambiguous.
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

    // Same choice as AnyPS5: defaultLanguage (in localizedParameters or at the top), then en-US, then the first language.
    /// <summary>Title ID and name in the contents of a sce_sys/param.json.</summary>
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
