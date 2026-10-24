using System.Globalization;
using Devilution.Data;
using Devilution.Diablo;
using Godevilution.Tools.Archive;

namespace Godevilution.Tools;

/** Inventory cursor footprints: objcurs.cel frame heights plus the open objcurs-widths.txt widths. */
public static class NativeSourceFootprintExport
{
    public const string CursorCelPath = "data\\inv\\objcurs.cel";
    public const string TableName = "native-rogue-l1-inventory.tsv";
    private const int FirstItemCursor = 12; // CURSOR_FIRSTITEM
    private const int InventorySlotPixels = 28; // INV_SLOT_SIZE_PX

    public static TsvTable CreateTable(MpqArchive archive, string assetsRoot, AuthoritativeCathedralThemeItemCatalog items)
    {
        var widths = ReadWidths(File.ReadAllText(Path.Combine(assetsRoot, "data", "inv", "objcurs-widths.txt")));
        var sizes = CelFrameSizes(archive.ReadFile(CursorCelPath), widths);
        var lines = new List<string> { "native_item_index\tnative_cursor_id\tpixel_width\tpixel_height\tinventory_width\tinventory_height" };
        // Every item the native generator can create; Hellfire-only cursors (objcurs2) are never selected.
        foreach (var item in items.Items.Where(item => item.Available && item.ItemType >= 0 && item.CursorGraphic != byte.MaxValue)) {
            var cursor = FirstItemCursor + item.CursorGraphic;
            if (cursor > sizes.Length)
                throw new InvalidDataException($"Item {item.NativeItemIndex} uses cursor {cursor}, which objcurs.cel lacks.");
            var (width, height) = sizes[cursor - 1]; // GetInvItemSprite
            lines.Add(string.Join('\t', new[] { item.NativeItemIndex, cursor, width, height,
                width / InventorySlotPixels, height / InventorySlotPixels }.Select(value => value.ToString(CultureInfo.InvariantCulture))));
        }
        return TsvTable.Parse(TableName, string.Join('\n', lines) + "\n");
    }

    /** cursor.cpp ReadWidths: one unsigned 16-bit width per line. */
    public static ushort[] ReadWidths(string text)
        => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => ushort.Parse(line, NumberStyles.None, CultureInfo.InvariantCulture)).ToArray();

    /**
     * cel_to_clx.cpp CelToClx for a single-group CEL: each frame is rows of
     * frameWidth pixels (control >= 0x80 is a transparent run of -control,
     * otherwise that many literal pixels), after an optional 10-byte header.
     */
    public static (int Width, int Height)[] CelFrameSizes(ReadOnlySpan<byte> cel, IReadOnlyList<ushort> widths)
    {
        var count = Read32(cel, 0);
        if (count == 0 || count > (cel.Length - 8) / 4 || Read32(cel, 4 + count * 4) != cel.Length)
            throw new InvalidDataException("objcurs.cel must be a single-group CEL.");
        if (widths.Count < count)
            throw new InvalidDataException("objcurs-widths.txt has fewer widths than objcurs.cel has frames.");
        var sizes = new (int, int)[count];
        for (var frame = 0; frame < count; frame++) {
            var position = Read32(cel, 4 + frame * 4);
            var end = Read32(cel, 8 + frame * 4);
            if (position > end || end > cel.Length)
                throw new InvalidDataException("objcurs.cel has an invalid frame offset.");
            if (end - position >= 2 && BitConverter.ToUInt16(cel.Slice(position, 2)) == 10)
                position += 10;
            var width = widths[frame];
            var height = 0;
            while (position < end) {
                for (var remaining = (int)width; remaining > 0;) {
                    if (position >= end)
                        throw new InvalidDataException("objcurs.cel frame row is truncated.");
                    var control = cel[position++];
                    var run = control >= 0x80 ? 256 - control : control;
                    if (control < 0x80)
                        position += run;
                    remaining -= run;
                }
                height++;
            }
            sizes[frame] = (width, height);
        }
        return sizes;
    }

    private static int Read32(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset > data.Length - 4)
            throw new InvalidDataException("CEL data is truncated.");
        return checked((int)BitConverter.ToUInt32(data.Slice(offset, 4)));
    }
}
