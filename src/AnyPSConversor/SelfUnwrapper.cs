using System.Buffers.Binary;

namespace AnyPSConversor;

/// <summary>SELF file that cannot be unwrapped.</summary>
public class SelfException(string message) : Exception(message);

/// <summary>All payload segments look encrypted.</summary>
public sealed class EncryptedSelfException(string message) : SelfException(message);

/// <summary>
/// Converts an already decrypted SELF/fSELF container into a clean ELF that the relinker accepts.
/// Faithful port of AnyPS5's tools/unwrap_self.py (https://github.com/boykopovar/AnyPS5, GPL-2.0).
/// </summary>
public static class SelfUnwrapper
{
    const uint SelfMagic = 0x1D3D154F;
    const uint SelfMagicKernel = 0xEEF51454;
    const int RecordOffset = 0x40;
    const int RecordSize = 0x20;
    const int FirstRegion = 0x400;
    const uint PtLoad = 1;
    const double EntropyLimit = 7.5;
    const int EntropySample = 0x10000;

    sealed class Segment
    {
        public int Index;
        public uint Type, Flags;
        public ulong Offset, Vaddr, Size, Memory, Align, Source, New;
    }

    sealed class Region
    {
        public ulong Old, Size, Vaddr, Align, New;
    }

    public static bool IsSelf(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && BinaryPrimitives.ReadUInt32LittleEndian(data) is SelfMagic or SelfMagicKernel;

    public static bool IsElf(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && data[..4].SequenceEqual("\u007FELF"u8);

    public static byte[] Unwrap(byte[] data)
    {
        if (data.Length < FirstRegion)
            throw new SelfException("file is smaller than the SELF header area");
        if (!IsSelf(data))
            throw new SelfException($"not a SELF container (magic 0x{BinaryPrimitives.ReadUInt32LittleEndian(data):x})");

        var records = ReadRecords(data);
        var (elfHeader, segments) = ParseElf(data);
        PlaceSegments(data, segments, records);
        CheckDecrypted(data, segments);

        var regions = segments
            .Where(s => s.Type == PtLoad && s.Size != 0)
            .Select(s => new Region { Old = s.Source, Size = s.Size, Vaddr = s.Vaddr, Align = s.Align })
            .ToList();

        foreach (var s in segments.Where(s => s.Type != PtLoad && s.Size != 0))
            if (RegionFor(regions, s.Source, s.Size) is null)
                regions.Add(new Region { Old = s.Source, Size = s.Size, Vaddr = 0, Align = 1 });

        regions = regions.OrderBy(r => r.Old).ToList();
        ulong cursor = FirstRegion;
        foreach (var region in regions)
        {
            var alignment = region.Align != 0 && (region.Align & (region.Align - 1)) == 0 ? region.Align : 1;
            var placed = AlignUp(cursor, alignment);
            if (region.Vaddr % alignment != placed % alignment)
                placed += (region.Vaddr % alignment + alignment - placed % alignment) % alignment;
            region.New = placed;
            cursor = placed + region.Size;
        }

        if (cursor > int.MaxValue)
            throw new SelfException("output would exceed 2 GiB");
        var result = new byte[cursor];
        foreach (var region in regions)
            Array.Copy(data, (long)region.Old, result, (long)region.New, (long)region.Size);

        foreach (var s in segments)
        {
            if (s.Size == 0 && s.Type == PtLoad)
            {
                s.New = 0;
                continue;
            }
            var region = RegionFor(regions, s.Source, s.Size);
            s.New = region is null ? s.Source : region.New + (s.Source - region.Old);
            if (s.New + s.Size > (ulong)result.Length)
                throw new SelfException($"segment {s.Index} escaped the output");
        }

        elfHeader.CopyTo(result, 0);
        BinaryPrimitives.WriteUInt64LittleEndian(result.AsSpan(0x28), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x3C), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x3E), 0);
        var programOffset = BinaryPrimitives.ReadUInt64LittleEndian(elfHeader.AsSpan(0x20));
        foreach (var s in segments)
        {
            var at = programOffset + (ulong)s.Index * 56;
            if (at + 56 > (ulong)result.Length)
                throw new SelfException("program header table does not fit the output");
            var span = result.AsSpan((int)at, 56);
            BinaryPrimitives.WriteUInt32LittleEndian(span, s.Type);
            BinaryPrimitives.WriteUInt32LittleEndian(span[4..], s.Flags);
            BinaryPrimitives.WriteUInt64LittleEndian(span[8..], s.New);
            BinaryPrimitives.WriteUInt64LittleEndian(span[16..], s.Vaddr);
            BinaryPrimitives.WriteUInt64LittleEndian(span[24..], s.Vaddr);
            BinaryPrimitives.WriteUInt64LittleEndian(span[32..], s.Size);
            BinaryPrimitives.WriteUInt64LittleEndian(span[40..], s.Memory);
            BinaryPrimitives.WriteUInt64LittleEndian(span[48..], s.Align);
        }
        return result;
    }

    static List<(ulong Segment, ulong Size)> ReadRecords(byte[] data)
    {
        var records = new List<(ulong, ulong)>();
        for (var offset = RecordOffset; offset + RecordSize <= FirstRegion; offset += RecordSize)
        {
            var segment = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset + 8));
            var size = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset + 16));
            var sizeAgain = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset + 24));
            if (size == 0 || size != sizeAgain)
                break;
            if (segment >= (ulong)data.Length || segment < 0x350 || segment % 0x10 != 0)
                break;
            if (size > (ulong)data.Length)
                break;
            records.Add((segment, size));
        }
        return records;
    }

    static (byte[] Header, List<Segment> Segments) ParseElf(byte[] data)
    {
        var inner = data.AsSpan().IndexOf("\u007FELF"u8);
        if (inner < 0 || inner + 0x40 > data.Length)
            throw new SelfException("no inner ELF header");
        var header = data.AsSpan(inner, 0x40).ToArray();
        var programOffset = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(0x20));
        var entrySize = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(0x36));
        var count = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(0x38));
        if (entrySize != 56 || count == 0)
            throw new SelfException($"program header table is {count} entries of {entrySize} bytes");

        var segments = new List<Segment>();
        for (var index = 0; index < count; index++)
        {
            var offset = (ulong)inner + programOffset + (ulong)index * 56;
            if (offset + 56 > (ulong)data.Length)
                throw new SelfException("program header table does not fit the file");
            var span = data.AsSpan((int)offset, 56);
            var fileOffset = BinaryPrimitives.ReadUInt64LittleEndian(span[8..]);
            segments.Add(new Segment
            {
                Index = index,
                Type = BinaryPrimitives.ReadUInt32LittleEndian(span),
                Flags = BinaryPrimitives.ReadUInt32LittleEndian(span[4..]),
                Offset = fileOffset,
                Vaddr = BinaryPrimitives.ReadUInt64LittleEndian(span[16..]),
                Size = BinaryPrimitives.ReadUInt64LittleEndian(span[32..]),
                Memory = BinaryPrimitives.ReadUInt64LittleEndian(span[40..]),
                Align = BinaryPrimitives.ReadUInt64LittleEndian(span[48..]),
                Source = fileOffset,
            });
        }
        return (header, segments);
    }

    static void PlaceSegments(byte[] data, List<Segment> segments, List<(ulong Segment, ulong Size)> records)
    {
        var length = (ulong)data.Length;
        var loads = segments.Where(s => s.Type == PtLoad).ToList();
        var used = new HashSet<int>();
        foreach (var load in loads.Where(s => s.Size != 0))
        {
            for (var index = 0; index < records.Count; index++)
            {
                if (used.Contains(index) || records[index].Size != load.Size)
                    continue;
                used.Add(index);
                load.Source = records[index].Segment;
                break;
            }
        }

        (ulong Offset, long Shift)? tail = null;
        foreach (var s in segments.Where(s => s.Type != PtLoad))
        {
            if (s.Vaddr != 0)
            {
                var host = loads.LastOrDefault(l => l.Memory != 0 && l.Vaddr <= s.Vaddr && s.Vaddr < l.Vaddr + l.Memory)
                    ?? throw new SelfException($"segment {s.Index} vaddr 0x{s.Vaddr:x} is in no PT_LOAD");
                s.Source = host.Source + (s.Vaddr - host.Vaddr);
                if (tail is null || s.Offset > tail.Value.Offset)
                    tail = (s.Offset, (long)s.Source - (long)s.Offset);
            }
            else if (s.Size != 0)
            {
                var source = (long)s.Offset + (tail?.Shift ?? 0);
                if (source < 0)
                    throw new SelfException($"segment {s.Index} lands before the file start");
                s.Source = Math.Min((ulong)source, length);
                var keep = length - s.Source;
                s.Size = Math.Min(s.Size, keep);
                s.Memory = Math.Min(s.Memory, keep);
            }
        }

        foreach (var s in segments)
            if (s.Source > length || s.Size > length - s.Source)
                throw new SelfException($"segment {s.Index} does not fit the file");
    }

    static void CheckDecrypted(byte[] data, List<Segment> segments)
    {
        var loads = segments.Where(s => s.Type == PtLoad && s.Size != 0).ToList();
        var encrypted = loads
            .Where(s => Entropy(data.AsSpan((int)s.Source, (int)Math.Min(s.Size, EntropySample))) >= EntropyLimit)
            .ToList();
        if (encrypted.Count > 0 && encrypted.Count == loads.Count)
            throw new EncryptedSelfException("every load segment looks encrypted");
    }

    static double Entropy(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return 0;
        Span<int> counts = stackalloc int[256];
        foreach (var b in data)
            counts[b]++;
        double result = 0;
        foreach (var count in counts)
        {
            if (count == 0)
                continue;
            var p = (double)count / data.Length;
            result -= p * Math.Log2(p);
        }
        return result;
    }

    static Region? RegionFor(List<Region> regions, ulong offset, ulong size) =>
        regions.FirstOrDefault(r => r.Old <= offset && offset + size <= r.Old + r.Size);

    static ulong AlignUp(ulong value, ulong alignment) =>
        alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;
}
