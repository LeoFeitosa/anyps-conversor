using AnyPSConversor;
using Xunit;

namespace AnyPSConversor.Tests;

public class ConverterTests
{
    [Theory]
    [InlineData("Instruction scan       | libc.prx  |   0%", 0.0)]
    [InlineData("Syscall scan           | eboot.bin | 100%", 0.4)]
    [InlineData("PRX conversion         | libc.prx  |  50% (0/1)", 0.5)]
    [InlineData("Write output           | DOOM.exe  | 100%", 1.0)]
    public void ReadsRelinkerProgress(string line, double expected) =>
        Assert.Equal(expected, Converter.RelinkerFraction(line)!.Value, precision: 3);

    [Fact]
    public void ReadsRelinkerStageAndItem()
    {
        var step = Converter.ParseRelinker("PRX conversion         | libc.prx  |  57% (0/1)")!.Value;
        Assert.Equal(2, step.Stage);
        Assert.Equal("libc.prx", step.Item);
    }

    [Theory]
    [InlineData("Conversion complete")]
    [InlineData("NID input: 171 references")]
    [InlineData("Unknown stage | x | 50%")]
    public void IgnoresOtherLines(string line) => Assert.Null(Converter.RelinkerFraction(line));
}
