using AnyPSConversor;
using Xunit;

namespace AnyPSConversor.Tests;

public class DiskSpaceTests
{
    const long GiB = 1L << 30;

    [Fact]
    public void ExplainsHowMuchIsMissing()
    {
        var error = Assert.Throws<UserException>(() => DiskSpace.Ensure(Path.GetTempPath(), 40 * GiB, available: 19 * GiB));

        Assert.Equal("err_disk_space", error.Key);
        Assert.Equal(DiskSpace.Format(DiskSpace.WithMargin(40 * GiB)), error.Args[0]);
        Assert.Equal(DiskSpace.Format(19 * GiB), error.Args[2]);
        Assert.Equal(DiskSpace.Format(DiskSpace.WithMargin(40 * GiB) - 19 * GiB), error.Args[3]);
    }

    [Fact]
    public void AcceptsWhenThereIsRoom() => DiskSpace.Ensure(Path.GetTempPath(), GiB, available: 10 * GiB);

    [Fact]
    public void SumsTheNeedsOfTheSameDisk()
    {
        var temp = Path.GetTempPath();
        DiskSpace.EnsureAll([(temp, 6 * GiB)], _ => 10 * GiB);

        // Destination and temporary folder on the same drive: each would fit alone, both together don't.
        var error = Assert.Throws<UserException>(() =>
            DiskSpace.EnsureAll([(temp, 6 * GiB), (Path.Combine(temp, "outra"), 6 * GiB)], _ => 10 * GiB));
        Assert.Equal("err_disk_space", error.Key);
    }

    [Fact]
    public void RecognizesDiskFullErrors()
    {
        Assert.True(DiskSpace.IsDiskFull(new IOException("full", unchecked((int)0x80070070))));
        Assert.True(DiskSpace.IsDiskFull(new IOException("full", unchecked((int)0x80070027))));
        Assert.False(DiskSpace.IsDiskFull(new IOException("other", unchecked((int)0x80070005))));
    }

    [Fact]
    public void ArchiveExtractionChecksSpaceBeforeWriting()
    {
        // An archive whose declared contents fit on no drive is rejected before writing anything.
        var zip = Path.Combine(Path.GetTempPath(), "anyps5-space-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            File.WriteAllBytes(zip, ZipWithDeclaredSize(long.MaxValue / 4));
            var error = Assert.Throws<UserException>(() => Archives.Extract(zip, null, Archives.TempRoot, new Progress<ExtractProgress>(), CancellationToken.None));
            Assert.Equal("err_disk_space", error.Key);
        }
        finally
        {
            File.Delete(zip);
        }
    }

    /// <summary>Zip64 with an empty entry that declares a huge uncompressed size in the central directory.</summary>
    static byte[] ZipWithDeclaredSize(long size)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream);
        // Named eboot.bin to pass the required-files check, which comes before the space check.
        var name = "jogo/eboot.bin"u8.ToArray();
        // Local header
        w.Write(0x04034b50); w.Write((short)45); w.Write((short)0); w.Write((short)0); w.Write(0); w.Write(0);
        w.Write(0); w.Write(-1); w.Write((short)name.Length); w.Write((short)20); w.Write(name);
        w.Write((short)1); w.Write((short)16); w.Write(size); w.Write(0L);
        var central = stream.Position;
        // Central directory
        w.Write(0x02014b50); w.Write((short)45); w.Write((short)45); w.Write((short)0); w.Write((short)0); w.Write(0); w.Write(0);
        w.Write(0); w.Write(-1); w.Write((short)name.Length); w.Write((short)20); w.Write((short)0); w.Write((short)0);
        w.Write((short)0); w.Write(0); w.Write(0); w.Write(name);
        w.Write((short)1); w.Write((short)16); w.Write(size); w.Write(0L);
        var end = stream.Position;
        // End of central directory
        w.Write(0x06054b50); w.Write((short)0); w.Write((short)0); w.Write((short)1); w.Write((short)1);
        w.Write((int)(end - central)); w.Write((int)central); w.Write((short)0);
        return stream.ToArray();
    }
}
