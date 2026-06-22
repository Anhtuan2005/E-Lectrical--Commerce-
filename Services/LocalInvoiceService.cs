using System.Globalization;
using System.Buffers.Binary;
using System.Text;
using EcommerceApp.Data;
using EcommerceApp.Models;
using Microsoft.EntityFrameworkCore;

namespace EcommerceApp.Services;

public interface IInvoiceService
{
    Task<InvoiceResult> IssueInvoiceAsync(int orderId);
    Task<InvoiceResult> CancelInvoiceAsync(int orderId);
    Task<InvoiceFileResult> DownloadPdfAsync(int orderId);
}

public sealed class LocalInvoiceService : IInvoiceService
{
    private const string ProviderName = "Techvora Local Invoice";
    private readonly AppDbContext _db;

    public LocalInvoiceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<InvoiceResult> IssueInvoiceAsync(int orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null)
        {
            return InvoiceResult.Fail("Không tìm thấy đơn hàng.");
        }

        if (!CanIssue(order, out var reason))
        {
            return InvoiceResult.Fail(reason);
        }

        var issuedAt = DateTime.UtcNow;
        order.InvoiceProvider = ProviderName;
        order.InvoiceStatus = InvoiceStatuses.Issued;
        order.InvoiceFkey = $"LOCAL-DH{order.Id:D6}";
        order.InvoicePattern = "LOCAL";
        order.InvoiceSerial = issuedAt.Year.ToString(CultureInfo.InvariantCulture);
        order.InvoiceNumber = order.Id.ToString("D8", CultureInfo.InvariantCulture);
        order.InvoiceLookupCode = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        order.InvoiceIssuedAt = issuedAt;
        order.InvoiceSyncedAt = issuedAt;
        order.InvoiceViewUrl = null;
        order.InvoiceRawResponse = "Local invoice generated.";
        order.InvoiceErrorMessage = null;
        order.UpdatedAt = issuedAt;
        await _db.SaveChangesAsync();

        return InvoiceResult.Ok("Đã xuất hóa đơn nội bộ.");
    }

    public async Task<InvoiceResult> CancelInvoiceAsync(int orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null)
        {
            return InvoiceResult.Fail("Không tìm thấy đơn hàng.");
        }

        if (string.IsNullOrWhiteSpace(order.InvoiceFkey) || order.InvoiceStatus == InvoiceStatuses.NotIssued)
        {
            return InvoiceResult.Fail("Đơn chưa có hóa đơn để huỷ.");
        }

        order.InvoiceStatus = InvoiceStatuses.Cancelled;
        order.InvoiceSyncedAt = DateTime.UtcNow;
        order.InvoiceRawResponse = "Local invoice cancelled.";
        order.InvoiceErrorMessage = null;
        order.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return InvoiceResult.Ok("Đã huỷ hóa đơn nội bộ.");
    }

    public async Task<InvoiceFileResult> DownloadPdfAsync(int orderId)
    {
        var order = await LoadOrderAsync(orderId);
        if (order is null)
        {
            return InvoiceFileResult.Fail("Không tìm thấy đơn hàng.");
        }

        if (string.IsNullOrWhiteSpace(order.InvoiceFkey) || order.InvoiceStatus == InvoiceStatuses.NotIssued)
        {
            return InvoiceFileResult.Fail("Đơn chưa có hóa đơn. Hãy bấm Xuất hóa đơn trước.");
        }

        var pdf = BuildInvoicePdf(order);
        return InvoiceFileResult.Ok(pdf, $"hoa-don-DH{order.Id:D6}.pdf");
    }

    private async Task<Order?> LoadOrderAsync(int orderId)
    {
        return await _db.Orders
            .Include(order => order.User)
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .FirstOrDefaultAsync(order => order.Id == orderId);
    }

    private static bool CanIssue(Order order, out string reason)
    {
        if (order.Status is OrderStatuses.AwaitingPayment or OrderStatuses.Cancelled)
        {
            reason = "Không xuất hóa đơn cho đơn đang chờ thanh toán hoặc đã huỷ.";
            return false;
        }

        if (string.Equals(order.PaymentMethod, "VNPAY", StringComparison.OrdinalIgnoreCase) && !order.IsPaid)
        {
            reason = "Không xuất hóa đơn cho đơn VNPAY chưa thanh toán.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(order.InvoiceFkey) && order.InvoiceStatus != InvoiceStatuses.Error)
        {
            reason = "Đơn này đã có hóa đơn.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static byte[] BuildInvoicePdf(Order order)
    {
        var canvas = new PdfCanvas();
        var y = 800;

        canvas.AddText(48, y, 18, "F2", "TECHVORA");
        y -= 26;
        canvas.AddText(48, y, 16, "F2", "HÓA ĐƠN BÁN HÀNG (MÔ PHỎNG)");
        y -= 18;
        canvas.AddText(48, y, 9, "F1", "File PDF nội bộ để đối soát đơn hàng, không phải hóa đơn điện tử hợp pháp.");
        y -= 26;

        canvas.AddText(48, y, 10, "F2", $"Số hóa đơn: {order.InvoiceNumber ?? order.Id.ToString("D8", CultureInfo.InvariantCulture)}");
        canvas.AddText(330, y, 10, "F1", $"Ngày xuất: {(order.InvoiceIssuedAt ?? DateTime.UtcNow).ToLocalTime():dd/MM/yyyy HH:mm}");
        y -= 16;
        canvas.AddText(48, y, 10, "F1", $"Mã đơn: DH{order.Id:D6}");
        canvas.AddText(330, y, 10, "F1", $"Mã tra cứu: {order.InvoiceLookupCode ?? "N/A"}");
        y -= 16;
        canvas.AddText(48, y, 10, "F1", $"Khách hàng: {order.RecipientName}");
        y -= 14;
        canvas.AddText(48, y, 10, "F1", $"Điện thoại: {order.RecipientPhone}");
        y -= 14;
        foreach (var line in Wrap($"Địa chỉ: {order.ShippingAddress}", 92))
        {
            canvas.AddText(48, y, 10, "F1", line);
            y -= 13;
        }

        y -= 12;
        canvas.DrawLine(48, y, 545, y);
        y -= 18;
        canvas.AddText(48, y, 10, "F2", "Sản phẩm");
        canvas.AddText(320, y, 10, "F2", "SL");
        canvas.AddText(370, y, 10, "F2", "Đơn giá");
        canvas.AddText(470, y, 10, "F2", "Thành tiền");
        y -= 10;
        canvas.DrawLine(48, y, 545, y);
        y -= 16;

        foreach (var item in order.Items)
        {
            var itemTotal = item.UnitPrice * item.Quantity;
            var wrappedName = Wrap(item.Product?.Name ?? $"Sản phẩm #{item.ProductId}", 44).ToList();
            canvas.AddText(48, y, 9, "F1", wrappedName[0]);
            canvas.AddText(320, y, 9, "F1", item.Quantity.ToString(CultureInfo.InvariantCulture));
            canvas.AddText(370, y, 9, "F1", Money(item.UnitPrice));
            canvas.AddText(470, y, 9, "F1", Money(itemTotal));
            y -= 13;
            foreach (var extraLine in wrappedName.Skip(1))
            {
                canvas.AddText(58, y, 9, "F1", extraLine);
                y -= 12;
            }
        }

        if (order.DiscountAmount > 0)
        {
            y -= 6;
            canvas.AddText(370, y, 9, "F1", "Giảm giá");
            canvas.AddText(470, y, 9, "F1", "-" + Money(order.DiscountAmount));
            y -= 14;
        }

        if (order.ShippingFee > 0)
        {
            canvas.AddText(370, y, 9, "F1", "Phí vận chuyển");
            canvas.AddText(470, y, 9, "F1", Money(order.ShippingFee));
            y -= 14;
        }

        y -= 4;
        canvas.DrawLine(330, y, 545, y);
        y -= 18;
        canvas.AddText(370, y, 12, "F2", "Tổng cộng");
        canvas.AddText(470, y, 12, "F2", Money(order.TotalAmount));
        y -= 34;
        canvas.AddText(48, y, 9, "F1", $"Thanh toán: {order.PaymentMethod} - Trạng thái đơn: {order.Status}");
        y -= 13;
        canvas.AddText(48, y, 9, "F1", "Cảm ơn quý khách đã mua hàng tại Techvora.");

        DrawSignatureBlock(canvas);
        DrawCompanyStamp(canvas, 450, 128);

        return WritePdf(canvas);
    }

    private static string Money(decimal value)
    {
        return value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " VND";
    }

    private static IEnumerable<string> Wrap(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            yield return text;
            yield break;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var line = new StringBuilder();
        foreach (var word in words)
        {
            if (line.Length > 0 && line.Length + word.Length + 1 > maxLength)
            {
                yield return line.ToString();
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            yield return line.ToString();
        }
    }

    private static void DrawSignatureBlock(PdfCanvas canvas)
    {
        canvas.AddText(365, 178, 10, "F2", "Người lập hóa đơn");
        canvas.AddText(370, 164, 8, "F1", "Ký, ghi rõ họ tên");
        canvas.AddText(384, 118, 18, "F2", "Techvora", "0.05 0.22 0.65");
        canvas.DrawLine(350, 100, 528, 100, "0.2 0.2 0.2", 0.8);
        canvas.AddText(390, 86, 8, "F1", "Đã ký và đóng dấu");
    }

    private static void DrawCompanyStamp(PdfCanvas canvas, int centerX, int centerY)
    {
        const string red = "0.72 0.04 0.04";
        canvas.DrawCircle(centerX, centerY, 54, red, 1.7);
        canvas.DrawCircle(centerX, centerY, 42, red, 0.9);
        canvas.AddText(centerX - 32, centerY + 19, 10, "F2", "TECHVORA", red);
        canvas.AddText(centerX - 39, centerY + 1, 8, "F2", "ĐÃ XUẤT HÓA ĐƠN", red);
        canvas.AddText(centerX - 18, centerY - 18, 8, "F2", "NỘI BỘ", red);
    }

    private static byte[] WritePdf(PdfCanvas canvas)
    {
        var font = TrueTypeFont.LoadDefault();
        var usedCodes = canvas.UsedCodes.Where(code => code > 0 && code <= 0xFFFF).OrderBy(code => code).ToList();
        var contentBytes = Encoding.ASCII.GetBytes(canvas.Content.ToString());
        var fontFileBytes = font.Bytes;
        var cidMapBytes = BuildCidToGidMap(font, usedCodes);
        var toUnicodeBytes = Encoding.ASCII.GetBytes(BuildToUnicodeCMap(usedCodes));
        var widthArray = BuildWidthArray(font, usedCodes);

        var objects = new List<byte[]>
        {
            PdfObject(1, "<< /Type /Catalog /Pages 2 0 R >>"),
            PdfObject(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            PdfObject(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 9 0 R /F2 9 0 R >> >> /Contents 10 0 R >>"),
            PdfStreamObject(4, $"/Length1 {fontFileBytes.Length}", fontFileBytes),
            PdfStreamObject(5, string.Empty, cidMapBytes),
            PdfStreamObject(6, string.Empty, toUnicodeBytes),
            PdfObject(7, $"<< /Type /FontDescriptor /FontName /TechvoraInvoiceFont /Flags 32 /FontBBox [{font.Scale(font.XMin)} {font.Scale(font.YMin)} {font.Scale(font.XMax)} {font.Scale(font.YMax)}] /ItalicAngle 0 /Ascent {font.Scale(font.Ascender)} /Descent {font.Scale(font.Descender)} /CapHeight {font.Scale(font.Ascender)} /StemV 80 /FontFile2 4 0 R >>"),
            PdfObject(8, $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /TechvoraInvoiceFont /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor 7 0 R /W {widthArray} /CIDToGIDMap 5 0 R >>"),
            PdfObject(9, "<< /Type /Font /Subtype /Type0 /BaseFont /TechvoraInvoiceFont /Encoding /Identity-H /DescendantFonts [8 0 R] /ToUnicode 6 0 R >>"),
            PdfStreamObject(10, string.Empty, contentBytes)
        };

        using var stream = new MemoryStream();
        WriteAscii(stream, "%PDF-1.7\n");
        stream.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        var offsets = new List<long> { 0 };
        foreach (var obj in objects)
        {
            offsets.Add(stream.Position);
            stream.Write(obj, 0, obj.Length);
        }

        var xrefOffset = stream.Position;
        WriteAscii(stream, $"xref\n0 {objects.Count + 1}\n");
        WriteAscii(stream, "0000000000 65535 f \n");
        for (var i = 1; i < offsets.Count; i++)
        {
            WriteAscii(stream, offsets[i].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        WriteAscii(stream, $"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return stream.ToArray();
    }

    private static byte[] PdfObject(int number, string body)
    {
        return Encoding.ASCII.GetBytes($"{number} 0 obj\n{body}\nendobj\n");
    }

    private static byte[] PdfStreamObject(int number, string extraDictionary, byte[] data)
    {
        using var stream = new MemoryStream();
        var extra = string.IsNullOrWhiteSpace(extraDictionary) ? string.Empty : " " + extraDictionary;
        WriteAscii(stream, $"{number} 0 obj\n<< /Length {data.Length}{extra} >>\nstream\n");
        stream.Write(data, 0, data.Length);
        WriteAscii(stream, "\nendstream\nendobj\n");
        return stream.ToArray();
    }

    private static void WriteAscii(Stream stream, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] BuildCidToGidMap(TrueTypeFont font, IReadOnlyList<int> usedCodes)
    {
        var maxCode = usedCodes.Count == 0 ? 0 : usedCodes.Max();
        var bytes = new byte[(maxCode + 1) * 2];
        foreach (var code in usedCodes)
        {
            BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(code * 2, 2), (ushort)font.GetGlyphId(code));
        }

        return bytes;
    }

    private static string BuildWidthArray(TrueTypeFont font, IReadOnlyList<int> usedCodes)
    {
        if (usedCodes.Count == 0)
        {
            return "[]";
        }

        var sb = new StringBuilder("[ ");
        var index = 0;
        while (index < usedCodes.Count)
        {
            var start = usedCodes[index];
            var widths = new List<int> { font.GetWidth(start) };
            var next = index + 1;
            while (next < usedCodes.Count && usedCodes[next] == usedCodes[next - 1] + 1)
            {
                widths.Add(font.GetWidth(usedCodes[next]));
                next++;
            }

            sb.Append(start).Append(" [");
            foreach (var width in widths)
            {
                sb.Append(width).Append(' ');
            }

            sb.Append("] ");
            index = next;
        }

        sb.Append(']');
        return sb.ToString();
    }

    private static string BuildToUnicodeCMap(IReadOnlyList<int> usedCodes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("/CIDInit /ProcSet findresource begin");
        sb.AppendLine("12 dict begin");
        sb.AppendLine("begincmap");
        sb.AppendLine("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def");
        sb.AppendLine("/CMapName /Adobe-Identity-UCS def");
        sb.AppendLine("/CMapType 2 def");
        sb.AppendLine("1 begincodespacerange");
        sb.AppendLine("<0000> <FFFF>");
        sb.AppendLine("endcodespacerange");

        foreach (var chunk in usedCodes.Chunk(100))
        {
            sb.AppendLine($"{chunk.Length} beginbfchar");
            foreach (var code in chunk)
            {
                var hex = code.ToString("X4", CultureInfo.InvariantCulture);
                sb.Append('<').Append(hex).Append("> <").Append(hex).AppendLine(">");
            }

            sb.AppendLine("endbfchar");
        }

        sb.AppendLine("endcmap");
        sb.AppendLine("CMapName currentdict /CMap defineresource pop");
        sb.AppendLine("end");
        sb.AppendLine("end");
        return sb.ToString();
    }

    private sealed class PdfCanvas
    {
        public StringBuilder Content { get; } = new();
        public HashSet<int> UsedCodes { get; } = new();

        public void AddText(int x, int y, int size, string font, string text, string fillColor = "0 0 0")
        {
            Content.Append("q ")
                .Append(fillColor)
                .Append(" rg BT /")
                .Append(font)
                .Append(' ')
                .Append(size)
                .Append(" Tf ")
                .Append(x)
                .Append(' ')
                .Append(y)
                .Append(" Td <")
                .Append(EncodeText(text))
                .AppendLine("> Tj ET Q");
        }

        public void DrawLine(int x1, int y1, int x2, int y2, string strokeColor = "0 0 0", double width = 0.6)
        {
            Content.Append("q ")
                .Append(strokeColor)
                .Append(" RG ")
                .Append(Num(width))
                .Append(" w ")
                .Append(x1)
                .Append(' ')
                .Append(y1)
                .Append(" m ")
                .Append(x2)
                .Append(' ')
                .Append(y2)
                .AppendLine(" l S Q");
        }

        public void DrawCircle(int centerX, int centerY, int radius, string strokeColor, double width)
        {
            var c = radius * 0.552284749831;
            Content.Append("q ")
                .Append(strokeColor)
                .Append(" RG ")
                .Append(strokeColor)
                .Append(" rg ")
                .Append(Num(width))
                .Append(" w ")
                .Append(Num(centerX + radius)).Append(' ').Append(Num(centerY)).Append(" m ")
                .Append(Num(centerX + radius)).Append(' ').Append(Num(centerY + c)).Append(' ')
                .Append(Num(centerX + c)).Append(' ').Append(Num(centerY + radius)).Append(' ')
                .Append(Num(centerX)).Append(' ').Append(Num(centerY + radius)).Append(" c ")
                .Append(Num(centerX - c)).Append(' ').Append(Num(centerY + radius)).Append(' ')
                .Append(Num(centerX - radius)).Append(' ').Append(Num(centerY + c)).Append(' ')
                .Append(Num(centerX - radius)).Append(' ').Append(Num(centerY)).Append(" c ")
                .Append(Num(centerX - radius)).Append(' ').Append(Num(centerY - c)).Append(' ')
                .Append(Num(centerX - c)).Append(' ').Append(Num(centerY - radius)).Append(' ')
                .Append(Num(centerX)).Append(' ').Append(Num(centerY - radius)).Append(" c ")
                .Append(Num(centerX + c)).Append(' ').Append(Num(centerY - radius)).Append(' ')
                .Append(Num(centerX + radius)).Append(' ').Append(Num(centerY - c)).Append(' ')
                .Append(Num(centerX + radius)).Append(' ').Append(Num(centerY)).AppendLine(" c S Q");
        }

        private string EncodeText(string text)
        {
            var sb = new StringBuilder(text.Length * 4);
            foreach (var ch in text)
            {
                var code = char.IsSurrogate(ch) ? '?' : ch;
                UsedCodes.Add(code);
                sb.Append(((int)code).ToString("X4", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private sealed class TrueTypeFont
    {
        private readonly ushort[] _advanceWidths;
        private readonly Dictionary<int, int> _cmap;

        private TrueTypeFont(byte[] bytes)
        {
            Bytes = bytes;
            var tables = ReadTableDirectory(bytes);
            var head = tables["head"];
            var hhea = tables["hhea"];
            var maxp = tables["maxp"];
            var hmtx = tables["hmtx"];
            var cmap = tables["cmap"];

            UnitsPerEm = ReadUShort(bytes, head + 18);
            XMin = ReadShort(bytes, head + 36);
            YMin = ReadShort(bytes, head + 38);
            XMax = ReadShort(bytes, head + 40);
            YMax = ReadShort(bytes, head + 42);
            Ascender = ReadShort(bytes, hhea + 4);
            Descender = ReadShort(bytes, hhea + 6);

            var numGlyphs = ReadUShort(bytes, maxp + 4);
            var numberOfHMetrics = ReadUShort(bytes, hhea + 34);
            _advanceWidths = new ushort[numGlyphs];
            ushort lastAdvance = 500;
            for (var i = 0; i < numGlyphs; i++)
            {
                if (i < numberOfHMetrics)
                {
                    lastAdvance = ReadUShort(bytes, hmtx + i * 4);
                }

                _advanceWidths[i] = lastAdvance;
            }

            _cmap = ReadCmap(bytes, cmap);
        }

        public byte[] Bytes { get; }
        public ushort UnitsPerEm { get; }
        public short XMin { get; }
        public short YMin { get; }
        public short XMax { get; }
        public short YMax { get; }
        public short Ascender { get; }
        public short Descender { get; }

        public static TrueTypeFont LoadDefault()
        {
            var fontDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var candidates = new[]
            {
                Path.Combine(fontDir, "arial.ttf"),
                Path.Combine(fontDir, "segoeui.ttf"),
                Path.Combine(fontDir, "times.ttf")
            };

            var path = candidates.FirstOrDefault(File.Exists)
                ?? throw new InvalidOperationException("Không tìm thấy font Unicode để xuất hóa đơn PDF.");
            return new TrueTypeFont(File.ReadAllBytes(path));
        }

        public int GetGlyphId(int unicodeCode)
        {
            return _cmap.TryGetValue(unicodeCode, out var glyphId) ? glyphId : 0;
        }

        public int GetWidth(int unicodeCode)
        {
            var glyphId = GetGlyphId(unicodeCode);
            var width = glyphId >= 0 && glyphId < _advanceWidths.Length
                ? _advanceWidths[glyphId]
                : _advanceWidths.LastOrDefault();
            return Math.Max(1, (int)Math.Round(width * 1000d / UnitsPerEm));
        }

        public int Scale(short value)
        {
            return (int)Math.Round(value * 1000d / UnitsPerEm);
        }

        private static Dictionary<string, int> ReadTableDirectory(byte[] bytes)
        {
            var tables = new Dictionary<string, int>(StringComparer.Ordinal);
            var numTables = ReadUShort(bytes, 4);
            for (var i = 0; i < numTables; i++)
            {
                var offset = 12 + i * 16;
                var tag = Encoding.ASCII.GetString(bytes, offset, 4);
                tables[tag] = (int)ReadUInt(bytes, offset + 8);
            }

            return tables;
        }

        private static Dictionary<int, int> ReadCmap(byte[] bytes, int cmapOffset)
        {
            var numTables = ReadUShort(bytes, cmapOffset + 2);
            Dictionary<int, int>? format4 = null;
            Dictionary<int, int>? format12 = null;

            for (var i = 0; i < numTables; i++)
            {
                var record = cmapOffset + 4 + i * 8;
                var subtableOffset = cmapOffset + (int)ReadUInt(bytes, record + 4);
                var format = ReadUShort(bytes, subtableOffset);
                if (format == 12)
                {
                    format12 = ReadCmapFormat12(bytes, subtableOffset);
                }
                else if (format == 4)
                {
                    format4 ??= ReadCmapFormat4(bytes, subtableOffset);
                }
            }

            return format12 ?? format4 ?? new Dictionary<int, int>();
        }

        private static Dictionary<int, int> ReadCmapFormat12(byte[] bytes, int offset)
        {
            var result = new Dictionary<int, int>();
            var groups = ReadUInt(bytes, offset + 12);
            var groupOffset = offset + 16;
            for (var i = 0; i < groups; i++)
            {
                var startChar = ReadUInt(bytes, groupOffset + i * 12);
                var endChar = ReadUInt(bytes, groupOffset + i * 12 + 4);
                var startGlyph = ReadUInt(bytes, groupOffset + i * 12 + 8);
                for (var code = startChar; code <= endChar && code <= 0xFFFF; code++)
                {
                    result[(int)code] = (int)(startGlyph + code - startChar);
                }
            }

            return result;
        }

        private static Dictionary<int, int> ReadCmapFormat4(byte[] bytes, int offset)
        {
            var result = new Dictionary<int, int>();
            var segCount = ReadUShort(bytes, offset + 6) / 2;
            var endCodeOffset = offset + 14;
            var startCodeOffset = endCodeOffset + segCount * 2 + 2;
            var idDeltaOffset = startCodeOffset + segCount * 2;
            var idRangeOffsetOffset = idDeltaOffset + segCount * 2;

            for (var i = 0; i < segCount; i++)
            {
                var endCode = ReadUShort(bytes, endCodeOffset + i * 2);
                var startCode = ReadUShort(bytes, startCodeOffset + i * 2);
                var idDelta = ReadShort(bytes, idDeltaOffset + i * 2);
                var idRangeOffset = ReadUShort(bytes, idRangeOffsetOffset + i * 2);
                if (startCode == 0xFFFF && endCode == 0xFFFF)
                {
                    continue;
                }

                for (var code = (int)startCode; code <= endCode; code++)
                {
                    int glyphId;
                    if (idRangeOffset == 0)
                    {
                        glyphId = (code + idDelta) & 0xFFFF;
                    }
                    else
                    {
                        var glyphOffset = idRangeOffsetOffset + i * 2 + idRangeOffset + (code - startCode) * 2;
                        glyphId = ReadUShort(bytes, glyphOffset);
                        if (glyphId != 0)
                        {
                            glyphId = (glyphId + idDelta) & 0xFFFF;
                        }
                    }

                    result[code] = glyphId;
                }
            }

            return result;
        }

        private static ushort ReadUShort(byte[] bytes, int offset)
        {
            return BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
        }

        private static short ReadShort(byte[] bytes, int offset)
        {
            return BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(offset, 2));
        }

        private static uint ReadUInt(byte[] bytes, int offset)
        {
            return BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
        }
    }
}

public sealed record InvoiceResult(bool Success, string Message)
{
    public static InvoiceResult Ok(string message) => new(true, message);
    public static InvoiceResult Fail(string message) => new(false, message);
}

public sealed record InvoiceFileResult(bool Success, string Message, byte[]? Content = null, string? FileName = null)
{
    public static InvoiceFileResult Ok(byte[] content, string fileName) => new(true, string.Empty, content, fileName);
    public static InvoiceFileResult Fail(string message) => new(false, message);
}
