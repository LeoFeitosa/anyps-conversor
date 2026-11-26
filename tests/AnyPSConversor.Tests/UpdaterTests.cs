using AnyPSConversor;
using Xunit;

namespace AnyPSConversor.Tests;

public class UpdaterTests
{
    [Theory]
    [InlineData("https://github.com/LeoFeitosa/anyps-conversor/releases/tag/v0.13.0", "v0.13.0")]
    [InlineData("https://github.com/LeoFeitosa/anyps-conversor/releases/tag/v1.2.10/", "v1.2.10")]
    [InlineData("https://github.com/LeoFeitosa/anyps-conversor/releases", null)]
    [InlineData("https://github.com/LeoFeitosa/anyps-conversor/releases/tag/anyps5-v0.1.5", null)]
    public void ReadsTheTagOfTheLatestRelease(string url, string? tag) => Assert.Equal(tag, Updater.TagOf(new Uri(url)));

    [Theory]
    [InlineData("v0.13.0", "0.12.4", true)]
    [InlineData("v0.12.4", "0.12.4", false)]
    [InlineData("v0.12.3", "0.12.4", false)]
    [InlineData("v1.0.0", "0.99.9", true)]
    [InlineData("v0.13.0", "0.12.4-dev.7", true)]
    public void ComparesReleaseAgainstRunningVersion(string tag, string version, bool newer) => Assert.Equal(newer, Updater.IsNewer(tag, version));

    [Fact]
    public void ADevelopmentBuildNeverReplacesItself()
    {
        Assert.False(Updater.IsReleaseBuild("0.12.4-dev.7"));
        Assert.True(Updater.IsReleaseBuild("0.12.4"));
    }

    [Theory]
    [InlineData("541FA50FB7255BE93111B72EBE5C320F3CCB936905F8F522914F233177CA17E7  AnyPS-Conversor.exe", "541fa50fb7255be93111b72ebe5c320f3ccb936905f8f522914f233177ca17e7")]
    [InlineData("541fa50fb7255be93111b72ebe5c320f3ccb936905f8f522914f233177ca17e7\n", "541fa50fb7255be93111b72ebe5c320f3ccb936905f8f522914f233177ca17e7")]
    [InlineData("<html>Not Found</html>", null)]
    [InlineData("541fa50f  AnyPS-Conversor.exe", null)]
    public void ReadsThePublishedHash(string file, string? hash) => Assert.Equal(hash, Updater.ExpectedHash(file));
}
