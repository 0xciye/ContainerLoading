using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public static class PdfExportSystem
{
    const float PageWidth = 595f, PageHeight = 842f;
    public static string OutputDirectoryOverride { get; set; }

    public static string Export(LoadingPlan plan)
    {
        if (!PlanValidation.TryValidate(plan, out var error)) throw new InvalidDataException(error);
        var fontAsset = Resources.Load<TextAsset>("NotoSans-Regular");
        if (fontAsset == null || fontAsset.bytes.Length == 0) throw new FileNotFoundException("Thiếu font Unicode Noto Sans.");
        var pages = BuildPages(plan);
        var footerReference = string.IsNullOrWhiteSpace(plan.orderReference) ? plan.name : plan.orderReference;
        for (var i = 0; i < pages.Count; i++)
        {
            Text(pages[i], 40, 22, 7, $"Đơn hàng: {footerReference}  ·  Tạo lúc {DateTime.Now:dd/MM/yyyy HH:mm}  ·  ContainerLoading v{Application.version}");
            Text(pages[i], 505, 22, 7, $"{i + 1}/{pages.Count}");
        }
        var orderName = string.IsNullOrWhiteSpace(plan.orderReference) ? plan.name : plan.orderReference;
        var containerName = string.IsNullOrWhiteSpace(plan.containerNumber) ? plan.container.name : plan.containerNumber;
        var fileName = $"LoadingPlan_{PlanPersistence.SafeName(orderName)}_{PlanPersistence.SafeName(containerName)}_{DateTime.Now:yyyyMMdd}.pdf";
        var preferredPath = string.IsNullOrWhiteSpace(OutputDirectoryOverride) ? AndroidDocumentsPath(fileName) : Path.Combine(OutputDirectoryOverride, fileName);
        try
        {
            WritePdf(preferredPath, pages, fontAsset.bytes);
            return preferredPath;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Không ghi được vào Documents, chuyển sang thư mục dữ liệu ứng dụng: " + exception.Message);
            var fallback = Path.Combine(Application.persistentDataPath, fileName);
            WritePdf(fallback, pages, fontAsset.bytes);
            return fallback;
        }
    }

    static string AndroidDocumentsPath(string fileName)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var environment = new AndroidJavaClass("android.os.Environment");
            using var directory = environment.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", "Documents");
            var path = directory.Call<string>("getAbsolutePath");
            Directory.CreateDirectory(path);
            return Path.Combine(path, fileName);
        }
        catch { }
#endif
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    static List<StringBuilder> BuildPages(LoadingPlan plan)
    {
        var pages = new List<StringBuilder>();
        var stats = PlanIntelligence.Statistics(plan);
        var health = PlanIntelligence.Check(plan);
        var cover = NewPage("BÁO CÁO QUẢN TRỊ XẾP CONTAINER", plan);
        var ready = health.Readiness == PlanReadiness.Ready;
        cover.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} rg 430 704 125 24 re f\n", ready ? ".08" : ".85", ready ? ".50" : ".36", ready ? ".24" : ".04");
        Text(cover, 447, 712, 10, ready ? "READY" : "DRAFT");
        Text(cover, 40, 720, 10, $"Container: {plan.container.name} · {plan.container.length} × {plan.container.width} × {plan.container.height} ô");
        Kpi(cover, 40, 652, 118, $"{stats.CargoTypeCount}", "LOẠI HÀNG"); Kpi(cover, 170, 652, 118, $"{stats.PlacedQuantity}/{stats.TotalQuantity}", "ĐÃ XẾP"); Kpi(cover, 300, 652, 118, $"{stats.RemainingQuantity}", "CÒN LẠI"); Kpi(cover, 430, 652, 125, $"{stats.UtilizationPercent:0.#}%", "SỬ DỤNG");
        Kpi(cover, 40, 580, 160, $"{stats.CompletionPercent:0.#}%", "TIẾN ĐỘ"); Kpi(cover, 215, 580, 165, $"{stats.PlacedWeight:0.##} kg", "TRỌNG LƯỢNG"); Kpi(cover, 395, 580, 160, health.StatusLabel, "TRẠNG THÁI");
        Text(cover, 40, 548, 12, "TÓM TẮT ĐIỀU HÀNH");
        var summary = ready ? $"Phương án đã xếp đủ {stats.TotalQuantity} kiện, không có lỗi chặn và sẵn sàng triển khai." : health.errors.Count > 0 ? $"Phương án có {health.errors.Count} lỗi cần xử lý trước khi triển khai." : $"Phương án đang thực hiện: còn {stats.RemainingQuantity} kiện, đạt {stats.CompletionPercent:0.#}% tiến độ.";
        var y = 526f; foreach (var line in Wrap(summary, 90)) { Text(cover, 40, y, 10, line); y -= 16; }
        Text(cover, 40, 472, 12, "TỔNG QUAN MẶT TRÊN");
        DrawGrid(cover, 40, 300, plan.container.length, plan.container.width, Mathf.Min(28f, 500f / Mathf.Max(plan.container.length, plan.container.width)), 0, plan);
        DrawLegend(cover, plan, 40, 270, 10);
        pages.Add(cover);

        var views = NewPage("SƠ ĐỒ BỐ TRÍ", plan); DrawViews(views, plan); pages.Add(views);

        var cargo = NewPage("TỔNG HỢP HÀNG HÓA", plan); y = 720;
        Text(cargo,40,y,8,"STT · Mã / Tên · Kích thước · Tổng / Đã xếp / Còn · kg/kiện · Tổng kg · Quy tắc · Màu");y-=22;
        AddSectionRows(pages, ref cargo, ref y, "CHI TIẾT", plan, plan.cargoTypes.Select((type,index)=>{var placed=plan.placedCargo.Count(x=>x.cargoTypeId==type.id);return $"{index+1}. {type.code} / {type.name} · {type.length}×{type.width}×{type.height} · {type.quantity} / {placed} / {Math.Max(0,type.quantity-placed)} · {type.weightPerUnit:0.##} · {type.quantity*type.weightPerUnit:0.##} kg · {(type.allowRotation?"xoay":"giữ hướng")}, {(type.stackable?"xếp chồng":"không chồng")} · #{ColorUtility.ToHtmlStringRGB(type.color)}";}));
        pages.Add(cargo);

        var validation = NewPage("KIỂM TRA VÀ CẢNH BÁO", plan); y = 720;
        Text(validation,40,y,12,$"Trạng thái: {health.StatusLabel} · {health.Summary}");y-=28;
        AddSectionRows(pages,ref validation,ref y,"LỖI",plan,health.errors.Count==0?new[]{"Không có lỗi chặn."}:health.errors.Select(x=>"Lỗi · "+x));
        AddSectionRows(pages,ref validation,ref y,"CẢNH BÁO",plan,health.warnings.Count==0?new[]{"Không có cảnh báo."}:health.warnings.Select(x=>"Cảnh báo · "+x));
        AddSectionRows(pages,ref validation,ref y,"THÔNG TIN",plan,health.information);
        if(!string.IsNullOrWhiteSpace(plan.orderNotes))AddSectionRows(pages,ref validation,ref y,"GHI CHÚ ĐƠN HÀNG",plan,Wrap(plan.orderNotes,90));
        pages.Add(validation);
        return pages;
    }

    static void Kpi(StringBuilder page,float x,float y,float width,string value,string label)
    {
        page.AppendFormat(CultureInfo.InvariantCulture,"0.95 0.97 0.99 rg {0} {1} {2} 58 re f 0.86 0.9 0.94 RG {0} {1} {2} 58 re S\n",x,y,width);
        Text(page,x+10,y+34,15,value);Text(page,x+10,y+14,7,label);
    }

    static StringBuilder NewPage(string title, LoadingPlan plan)
    {
        var page = new StringBuilder();
        Text(page, 40, 800, 17, title);
        Text(page, 40, 778, 9, $"Phương án: {plan.name}");
        Text(page, 300, 778, 9, $"Ngày xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
        if(!string.IsNullOrWhiteSpace(plan.orderReference))Text(page, 40, 762, 9, $"Mã đơn hàng: {plan.orderReference}");
        if(!string.IsNullOrWhiteSpace(plan.customerName))Text(page, 300, 762, 9, $"Khách hàng: {plan.customerName}");
        var metadata = new List<string>();
        if(!string.IsNullOrWhiteSpace(plan.containerNumber))metadata.Add("Số container: "+plan.containerNumber);
        if(!string.IsNullOrWhiteSpace(plan.sealNumber))metadata.Add("Niêm phong: "+plan.sealNumber);
        if(!string.IsNullOrWhiteSpace(plan.destination))metadata.Add("Điểm đến: "+plan.destination);
        if(!string.IsNullOrWhiteSpace(plan.loadingDate))metadata.Add("Ngày đóng hàng: "+plan.loadingDate);
        if(metadata.Count>0)Text(page,40,746,8,string.Join("    ",metadata));
        page.Append("0.75 0.8 0.84 RG 40 738 515 0 re S\n");
        return page;
    }

    static void AddSectionRows(List<StringBuilder> pages, ref StringBuilder page, ref float y, string title, LoadingPlan plan, IEnumerable<string> rows)
    {
        if (y < 100) { pages.Add(page); page = NewPage(title, plan); y = 735; }
        Text(page, 40, y, 12, title); y -= 22;
        foreach (var row in rows)
            foreach (var line in Wrap(row, 94))
            {
                if (y < 55) { pages.Add(page); page = NewPage(title + " (TIẾP)", plan); y = 735; }
                Text(page, 45, y, 9, line); y -= 14;
            }
        y -= 12;
    }

    static IEnumerable<string> Wrap(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value)) yield break;
        foreach (var paragraph in value.Replace("\r", "").Split('\n'))
        {
            if (paragraph.Length == 0) { yield return ""; continue; }
            var current = new StringBuilder();
            foreach (var word in paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var remaining = word;
                while (remaining.Length > maxChars)
                {
                    if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
                    yield return remaining.Substring(0, maxChars);
                    remaining = remaining.Substring(maxChars);
                }
                if (current.Length > 0 && current.Length + remaining.Length + 1 > maxChars) { yield return current.ToString(); current.Clear(); }
                if (current.Length > 0) current.Append(' ');
                current.Append(remaining);
            }
            if (current.Length > 0) yield return current.ToString();
        }
    }

    static void DrawViews(StringBuilder stream, LoadingPlan plan)
    {
        var c = plan.container;
        Text(stream,40,720,9,"MẶT TRÊN · CỘT/HÀNG");DrawGrid(stream,40,590,c.length,c.width,Mathf.Min(22f,500f/Mathf.Max(c.length,c.width)),0,plan);
        Text(stream,40,550,9,"MẶT BÊN · CỘT/TẦNG");DrawGrid(stream,40,395,c.length,c.height,Mathf.Min(20f,500f/Mathf.Max(c.length,c.height)),1,plan);
        Text(stream,40,355,9,"MẶT TRƯỚC · HÀNG/TẦNG");DrawGrid(stream,40,210,c.width,c.height,Mathf.Min(22f,360f/Mathf.Max(c.width,c.height)),2,plan);
        DrawLegend(stream,plan,40,175,18);
    }

    static void DrawLegend(StringBuilder stream,LoadingPlan plan,float x,float y,int maximum)
    {
        Text(stream,x,y,9,"CHÚ GIẢ MÀU");y-=16;var column=0;
        foreach(var type in plan.cargoTypes.Take(maximum))
        {
            var cx=x+column*255;stream.AppendFormat(CultureInfo.InvariantCulture,"{0:0.###} {1:0.###} {2:0.###} rg {3:0.##} {4:0.##} 10 10 re f\n",type.color.r,type.color.g,type.color.b,cx,y-2);Text(stream,cx+16,y,8,$"{type.code} · {type.name}");y-=15;if(y<55&&column==0){column=1;y=159;}
        }
    }

    static void DrawGrid(StringBuilder stream, float ox, float oy, int width, int height, float scale, int projection, LoadingPlan plan)
    {
        for (var x = 0; x < width; x++) for (var y = 0; y < height; y++)
        {
            foreach (var box in plan.placedCargo)
            {
                var hit = projection == 0
                    ? x >= box.position.x && x < box.position.x + box.size.x && y >= box.position.y && y < box.position.y + box.size.y
                    : projection == 1
                        ? x >= box.position.x && x < box.position.x + box.size.x && y >= box.position.z && y < box.position.z + box.size.z
                        : x >= box.position.y && x < box.position.y + box.size.y && y >= box.position.z && y < box.position.z + box.size.z;
                if (!hit) continue;
                var type = plan.cargoTypes.Find(t => t.id == box.cargoTypeId);
                if (type != null) stream.AppendFormat(CultureInfo.InvariantCulture, "{0:0.###} {1:0.###} {2:0.###} rg {3:0.##} {4:0.##} {5:0.##} {5:0.##} re f\n", type.color.r, type.color.g, type.color.b, ox + x * scale, oy + y * scale, scale);
                break;
            }
            stream.AppendFormat(CultureInfo.InvariantCulture, "0.72 0.76 0.8 RG {0:0.##} {1:0.##} {2:0.##} {2:0.##} re S\n", ox + x * scale, oy + y * scale, scale);
        }
    }

    static void Text(StringBuilder stream, float x, float y, float size, string value) => stream.AppendFormat(CultureInfo.InvariantCulture, "0.07 0.1 0.15 rg BT /F1 {0:0.##} Tf {1:0.##} {2:0.##} Td <{3}> Tj ET\n", size, x, y, Utf16Hex(value));
    static string Utf16Hex(string value) { var bytes = Encoding.BigEndianUnicode.GetBytes(value ?? ""); var hex = new StringBuilder(bytes.Length * 2); foreach (var b in bytes) hex.Append(b.ToString("X2")); return hex.ToString(); }

    static void WritePdf(string path, List<StringBuilder> pages, byte[] fontBytes)
    {
        var pageCount = pages.Count;
        var fontId = 3 + pageCount;
        var cidFontId = fontId + 1;
        var descriptorId = fontId + 2;
        var fontFileId = fontId + 3;
        var cidMapId = fontId + 4;
        var toUnicodeId = fontId + 5;
        var contentBase = fontId + 6;
        var objectCount = contentBase + pageCount - 1;
        var objects = new byte[objectCount + 1][];
        objects[1] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");
        objects[2] = Ascii($"<< /Type /Pages /Kids [{string.Join(" ", Enumerable.Range(0, pageCount).Select(i => $"{3 + i} 0 R"))}] /Count {pageCount} >>");
        for (var i = 0; i < pageCount; i++) objects[3 + i] = Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 {fontId} 0 R >> >> /Contents {contentBase + i} 0 R >>");
        objects[fontId] = Ascii($"<< /Type /Font /Subtype /Type0 /BaseFont /NotoSans /Encoding /Identity-H /DescendantFonts [{cidFontId} 0 R] /ToUnicode {toUnicodeId} 0 R >>");
        objects[cidFontId] = Ascii($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /NotoSans /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {descriptorId} 0 R /DW 600 /CIDToGIDMap {cidMapId} 0 R >>");
        objects[descriptorId] = Ascii($"<< /Type /FontDescriptor /FontName /NotoSans /Flags 32 /FontBBox [-621 -389 2800 1067] /ItalicAngle 0 /Ascent 1069 /Descent -293 /CapHeight 714 /StemV 80 /FontFile2 {fontFileId} 0 R >>");
        objects[fontFileId] = Stream(fontBytes);
        objects[cidMapId] = Stream(BuildCidMap(fontBytes));
        objects[toUnicodeId] = Stream(Ascii("/CIDInit /ProcSet findresource begin\n12 dict begin\nbegincmap\n/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n/CMapName /Adobe-Identity-UCS def\n/CMapType 2 def\n1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n1 beginbfrange\n<0000> <FFFF> <0000>\nendbfrange\nendcmap\nCMapName currentdict /CMap defineresource pop\nend\nend"));
        for (var i = 0; i < pageCount; i++) objects[contentBase + i] = Stream(Ascii(pages[i].ToString()));

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(output, Ascii("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n"));
        var offsets = new long[objectCount + 1];
        for (var id = 1; id <= objectCount; id++) { offsets[id] = output.Position; Write(output, Ascii($"{id} 0 obj\n")); Write(output, objects[id]); Write(output, Ascii("\nendobj\n")); }
        var xref = output.Position;
        Write(output, Ascii($"xref\n0 {objectCount + 1}\n0000000000 65535 f \n"));
        for (var id = 1; id <= objectCount; id++) Write(output, Ascii($"{offsets[id]:0000000000} 00000 n \n"));
        Write(output, Ascii($"trailer << /Size {objectCount + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
        output.Flush(true);
    }

    static byte[] BuildCidMap(byte[] font)
    {
        var map = new byte[65536 * 2];
        var cmapOffset = TableOffset(font, "cmap");
        if (cmapOffset < 0) return map;
        var tables = U16(font, cmapOffset + 2);
        var format4 = -1;
        for (var i = 0; i < tables; i++)
        {
            var record = cmapOffset + 4 + i * 8;
            var subtable = cmapOffset + (int)U32(font, record + 4);
            if (U16(font, subtable) == 4) { format4 = subtable; if (U16(font, record) == 3 && U16(font, record + 2) == 1) break; }
        }
        if (format4 < 0) return map;
        var segCount = U16(font, format4 + 6) / 2;
        var endCodes = format4 + 14;
        var startCodes = endCodes + segCount * 2 + 2;
        var deltas = startCodes + segCount * 2;
        var ranges = deltas + segCount * 2;
        for (var segment = 0; segment < segCount; segment++)
        {
            int start = U16(font, startCodes + segment * 2); int end = U16(font, endCodes + segment * 2); int delta = U16(font, deltas + segment * 2); int range = U16(font, ranges + segment * 2);
            for (var code = start; code <= end; code++)
            {
                int glyph;
                if (range == 0) glyph = (code + delta) & 0xFFFF;
                else { var glyphOffset = ranges + segment * 2 + range + (code - start) * 2; glyph = glyphOffset + 1 < font.Length ? U16(font, glyphOffset) : 0; if (glyph != 0) glyph = (glyph + delta) & 0xFFFF; }
                map[code * 2] = (byte)(glyph >> 8); map[code * 2 + 1] = (byte)glyph;
            }
        }
        return map;
    }

    static int TableOffset(byte[] data, string tag) { var count = U16(data, 4); for (var i = 0; i < count; i++) { var offset = 12 + i * 16; if (Encoding.ASCII.GetString(data, offset, 4) == tag) return (int)U32(data, offset + 8); } return -1; }
    static ushort U16(byte[] data, int offset) => (ushort)((data[offset] << 8) | data[offset + 1]);
    static uint U32(byte[] data, int offset) => ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
    static byte[] Ascii(string value) => Encoding.GetEncoding(28591).GetBytes(value);
    static byte[] Stream(byte[] data) { using var output = new MemoryStream(); Write(output, Ascii($"<< /Length {data.Length} >>\nstream\n")); Write(output, data); Write(output, Ascii("\nendstream")); return output.ToArray(); }
    static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);
}
