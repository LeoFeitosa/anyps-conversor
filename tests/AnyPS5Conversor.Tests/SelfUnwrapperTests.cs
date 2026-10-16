using System.Buffers.Binary;
using AnyPS5Conversor;
using Xunit;

namespace AnyPS5Conversor.Tests;

public class SelfUnwrapperTests
{
    const int SegmentSize = 0x1000;
    const int Alignment = 0x4000;

    /// <summary>SELF mínimo: um registro em 0x40, ELF interno em 0x200 e um PT_LOAD em 0x400.</summary>
    internal static byte[] BuildSelf(Func<int, byte> segmentByte)
    {
        var data = new byte[0x400 + SegmentSize];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 0x1D3D154F);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(0x48), 0x400);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(0x50), SegmentSize);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(0x58), SegmentSize);

        const int inner = 0x200;
        "\u007FELF"u8.CopyTo(data.AsSpan(inner));
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(inner + 0x20), 0x40);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(inner + 0x28), 0xDEAD);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(inner + 0x36), 56);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(inner + 0x38), 1);

        var phdr = data.AsSpan(inner + 0x40, 56);
        BinaryPrimitives.WriteUInt32LittleEndian(phdr, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(phdr[4..], 5);
        BinaryPrimitives.WriteUInt64LittleEndian(phdr[8..], 0x1234);
        BinaryPrimitives.WriteUInt64LittleEndian(phdr[32..], SegmentSize);
        BinaryPrimitives.WriteUInt64LittleEndian(phdr[40..], SegmentSize);
        BinaryPrimitives.WriteUInt64LittleEndian(phdr[48..], Alignment);

        for (var i = 0; i < SegmentSize; i++)
            data[0x400 + i] = segmentByte(i);
        return data;
    }

    [Fact]
    public void UnwrapsSegmentToAlignedOffset()
    {
        var self = BuildSelf(i => (byte)(i % 7));

        var elf = SelfUnwrapper.Unwrap(self);

        Assert.True(SelfUnwrapper.IsElf(elf));
        Assert.Equal(Alignment + SegmentSize, elf.Length);
        Assert.Equal(0UL, BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan(0x28)));
        Assert.Equal((ulong)Alignment, BinaryPrimitives.ReadUInt64LittleEndian(elf.AsSpan(0x40 + 8)));
        Assert.Equal(self.AsSpan(0x400, SegmentSize).ToArray(), elf.AsSpan(Alignment, SegmentSize).ToArray());
    }

    [Fact]
    public void RefusesEncryptedSegments()
    {
        var random = new Random(1);
        var self = BuildSelf(_ => (byte)random.Next(256));

        Assert.Throws<EncryptedSelfException>(() => SelfUnwrapper.Unwrap(self));
    }

    [Fact]
    public void RecognizesMagicBytes()
    {
        Assert.True(SelfUnwrapper.IsElf(new byte[] { 0x7F, 0x45, 0x4C, 0x46 }));
        Assert.True(SelfUnwrapper.IsSelf(new byte[] { 0x4F, 0x15, 0x3D, 0x1D }));
        Assert.False(SelfUnwrapper.IsElf(new byte[] { 0x7F, 0x45, 0x4C, 0x00 }));
    }

    [Fact]
    public void RefusesFilesThatAreNotSelf()
    {
        var data = new byte[0x400];
        "\u007FELF"u8.CopyTo(data);

        Assert.True(SelfUnwrapper.IsElf(data));
        Assert.False(SelfUnwrapper.IsSelf(data));
        Assert.Throws<SelfException>(() => SelfUnwrapper.Unwrap(data));
    }
}
