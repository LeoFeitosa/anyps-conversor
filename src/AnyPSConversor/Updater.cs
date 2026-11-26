using System.Diagnostics;
using System.Security.Cryptography;

namespace AnyPSConversor;

/// <summary>
/// Keeps the program current from its GitHub releases with no action from the user. At every start the
/// newest release is looked up; a newer one is downloaded in the background and checked against the SHA-256
/// the release publishes, then the running executable is swapped: at once when nothing is in progress,
/// otherwise when the window closes. The only requests go to github.com and carry nothing but the download.
/// A development build (version with a suffix) never replaces itself.
/// </summary>
public static class Updater
{
    const string LatestUrl = "https://github.com/LeoFeitosa/anyps-conversor/releases/latest";
    const string ExeName = "AnyPS-Conversor.exe";

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(15) };

    static string ExePath => Environment.ProcessPath!;
    static string PendingPath => ExePath + ".new";
    static string PreviousPath => ExePath + ".old";

    /// <summary>Set to anything to keep this run on its current version.</summary>
    public static bool Disabled => Environment.GetEnvironmentVariable("ANYPS_NO_UPDATE") is not null;

    /// <summary>
    /// Housekeeping at start: removes the executable left by the last swap and, when a verified download is
    /// waiting (the window was closed before it could be installed, or the program crashed), installs it and
    /// starts the new version. True when this process must end so that the new one takes over.
    /// </summary>
    public static bool InstallPendingOnStart(string[] args)
    {
        try { File.Delete(PreviousPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        if (Disabled || !File.Exists(PendingPath))
            return false;
        try
        {
            Swap();
            var start = new ProcessStartInfo(ExePath) { UseShellExecute = true };
            foreach (var arg in args)
                start.ArgumentList.Add(arg);
            Process.Start(start);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Looks up the newest release and downloads it when it is newer than <paramref name="currentVersion"/>.
    /// Returns when a verified executable waits at <see cref="PendingPath"/>, or null when there is nothing to do.
    /// Network and file errors end the attempt quietly: the next start tries again.
    /// </summary>
    public static async Task<string?> DownloadNewerAsync(string currentVersion, CancellationToken cancellation)
    {
        if (Disabled || !IsReleaseBuild(currentVersion))
            return null;
        try
        {
            using var latest = await Http.GetAsync(LatestUrl, HttpCompletionOption.ResponseHeadersRead, cancellation);
            var tag = TagOf(latest.RequestMessage?.RequestUri);
            if (tag is null || !IsNewer(tag, currentVersion))
                return null;
            var expected = ExpectedHash(await Http.GetStringAsync($"{LatestUrl}/download/{ExeName}.sha256", cancellation));
            if (expected is null)
                return null;
            var temp = PendingPath + ".part";
            await using (var source = await Http.GetStreamAsync($"{LatestUrl}/download/{ExeName}", cancellation))
            await using (var target = File.Create(temp))
                await source.CopyToAsync(target, cancellation);
            if (!await HashMatchesAsync(temp, expected, cancellation))
            {
                File.Delete(temp);
                return null;
            }
            File.Move(temp, PendingPath, overwrite: true);
            return tag;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>Puts the verified download in place of the running executable; the new version runs from the next start.</summary>
    public static void InstallPending()
    {
        if (!File.Exists(PendingPath))
            return;
        try { Swap(); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>Installs the verified download and starts the new version; the caller then ends this process.</summary>
    public static bool InstallPendingAndRestart()
    {
        if (!File.Exists(PendingPath))
            return false;
        try
        {
            Swap();
            Process.Start(new ProcessStartInfo(ExePath) { UseShellExecute = true });
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    // A running executable cannot be overwritten on Windows, but it can be renamed.
    static void Swap()
    {
        File.Move(ExePath, PreviousPath, overwrite: true);
        try { File.Move(PendingPath, ExePath); }
        catch
        {
            File.Move(PreviousPath, ExePath);
            throw;
        }
    }

    /// <summary>"v0.13.0" from the page a release's "latest" link lands on.</summary>
    public static string? TagOf(Uri? uri)
    {
        var tag = uri?.Segments.LastOrDefault()?.Trim('/');
        return tag is not null && tag.StartsWith('v') && Version.TryParse(tag[1..], out _) ? tag : null;
    }

    public static bool IsReleaseBuild(string version) => !version.Contains('-');

    /// <summary>True when the release <paramref name="tag"/> is above the running <paramref name="version"/> ("0.12.4" or "0.12.4-dev.3").</summary>
    public static bool IsNewer(string tag, string version) =>
        Version.TryParse(tag.TrimStart('v'), out var released) && Version.TryParse(version.Split('-')[0], out var running) && released > running;

    /// <summary>The hash in a "&lt;sha256&gt;  AnyPS-Conversor.exe" line, as the release publishes it.</summary>
    public static string? ExpectedHash(string sha256File)
    {
        var hash = sha256File.Trim().Split(' ', 2)[0].ToLowerInvariant();
        return hash.Length == 64 && hash.All(Uri.IsHexDigit) ? hash : null;
    }

    static async Task<bool> HashMatchesAsync(string file, string expected, CancellationToken cancellation)
    {
        await using var stream = File.OpenRead(file);
        var hash = await SHA256.HashDataAsync(stream, cancellation);
        return Convert.ToHexStringLower(hash) == expected;
    }
}
