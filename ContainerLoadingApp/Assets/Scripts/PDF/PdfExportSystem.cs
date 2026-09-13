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

    public static string Export(Shipment shipment)
    {
        if (!ShipmentPersistence.TryValidate(shipment, out var error)) throw new InvalidDataException(error);
        var fontAsset = Resources.Load<TextAsset>("NotoSansSC-VF");
        if (fontAsset == null || fontAsset.bytes.Length == 0) throw new FileNotFoundException("Thiếu font Unicode Noto Sans.");
        var pages = BuildShipmentPages(shipment);
        var reference = string.IsNullOrWhiteSpace(shipment.orderReference) ? shipment.shipmentName : shipment.orderReference;
        AddFooters(pages, reference);
        var fileName = $"Shipment_{PlanPersistence.SafeName(reference)}_{DateTime.Now:yyyyMMdd}.pdf";
        var preferredPath = string.IsNullOrWhiteSpace(OutputDirectoryOverride) ? AndroidDocumentsPath(fileName) : Path.Combine(OutputDirectoryOverride, fileName);
        try { WritePdf(preferredPath, pages, fontAsset.bytes); return preferredPath; }
        catch (Exception exception) { Debug.LogWarning("Không ghi được PDF chuyến hàng vào Documents: " + exception.Message); var fallback = Path.Combine(Application.persistentDataPath, fileName); WritePdf(fallback, pages, fontAsset.bytes); return fallback; }
    }

    public static string ExportOperational(Shipment shipment)
    {
        if (!ShipmentPersistence.TryValidate(shipment, out var error)) throw new InvalidDataException(error);
        var fontAsset = Resources.Load<TextAsset>("NotoSansSC-VF");
        if (fontAsset == null || fontAsset.bytes.Length == 0) throw new FileNotFoundException("Thiếu font Unicode Noto Sans.");
        var pages = new List<StringBuilder>();
        var anchor = shipment.containers.FirstOrDefault() ?? new LoadingPlan { name = shipment.shipmentName };
        var cover = NewPage(ReportLocalization.Get("OperationalTitle"), anchor);
        Text(cover, 40, 704, 10, ReportLocalization.Inline("Shipment") + ": " + shipment.shipmentName);
        if (!string.IsNullOrWhiteSpace(shipment.orderReference)) Text(cover, 40, 680, 9, ReportLocalization.Inline("Order") + ": " + shipment.orderReference);
        if (!string.IsNullOrWhiteSpace(shipment.customerName)) Text(cover, 40, 656, 9, ReportLocalization.Inline("Customer") + ": " + shipment.customerName);
        if (!string.IsNullOrWhiteSpace(shipment.destination)) Text(cover, 40, 632, 9, ReportLocalization.Inline("Destination") + ": " + shipment.destination);
        Text(cover, 40, 584, 12, ReportLocalization.Get("ContainerSummary"));
        var coverY = 548f;
        foreach (var container in shipment.containers)
        {
            var stats = PlanIntelligence.Statistics(container);
            Text(cover, 46, coverY, 9, $"{ShipmentIntelligence.containerNumberOrName(container)} · {stats.PlacedQuantity} kiện / 件 · {stats.PlacedWeight:0.##} kg");
            coverY -= 24;
        }
        if (!string.IsNullOrWhiteSpace(shipment.notes))
        {
            Text(cover, 40, coverY - 10, 12, ReportLocalization.Get("Notes"));
            coverY -= 48;
            foreach (var line in Wrap(shipment.notes, 88)) { Text(cover, 46, coverY, 9, line); coverY -= 15; }
        }
        pages.Add(cover);
        foreach (var container in shipment.containers)
        {
            LoadingSequencePlanner.Generate(container, out var sequenceWarning);
            var diagram = NewPage(ReportLocalization.Get("Diagram") + " - " + ShipmentIntelligence.containerNumberOrName(container), container);
            DrawViews(diagram, container);
            pages.Add(diagram);
            var page = NewPage(ReportLocalization.Get("OperationalTitle") + " - " + ShipmentIntelligence.containerNumberOrName(container), container);
            var y = 720f;
            Text(page, 40, y, 9, "Cửa container ở phía Cột 1 (X=0). Hàng sâu phía cuối được xếp trước。\n集装箱门位于第1列（X=0），先装靠近箱体深处的货物。"); y -= 34;
            AddSectionRows(pages, ref page, ref y, ReportLocalization.Get("LoadingOrder"), container, BuildLoadingBatches(container.loadingSequence));
            if (!string.IsNullOrWhiteSpace(sequenceWarning)) AddSectionRows(pages, ref page, ref y, ReportLocalization.Get("Warnings"), container, new[] { sequenceWarning });
            pages.Add(page);
        }
        var reference = string.IsNullOrWhiteSpace(shipment.orderReference) ? shipment.shipmentName : shipment.orderReference;
        AddFooters(pages, reference);
        var fileName = $"Operational_{PlanPersistence.SafeName(reference)}_{DateTime.Now:yyyyMMdd}.pdf";
        var path = string.IsNullOrWhiteSpace(OutputDirectoryOverride) ? AndroidDocumentsPath(fileName) : Path.Combine(OutputDirectoryOverride, fileName);
        try { WritePdf(path, pages, fontAsset.bytes); return path; } catch { var fallback = Path.Combine(Application.persistentDataPath, fileName); WritePdf(fallback, pages, fontAsset.bytes); return fallback; }
    }

    public static string Export(LoadingPlan plan)
    {
        if (!PlanValidation.TryValidate(plan, out var error)) throw new InvalidDataException(error);
        var fontAsset = Resources.Load<TextAsset>("NotoSansSC-VF");
        if (fontAsset == null || fontAsset.bytes.Length == 0) throw new FileNotFoundException("Thiếu font Unicode Noto Sans.");
        var pages = BuildShipmentPages(ShipmentIntelligence.FromV2Plan(plan));
        var footerReference = string.IsNullOrWhiteSpace(plan.orderReference) ? plan.name : plan.orderReference;
        AddFooters(pages, footerReference);
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

    static List<StringBuilder> BuildShipmentPages(Shipment shipment)
    {
        ShipmentIntelligence.Normalize(shipment); var stats = ShipmentIntelligence.Statistics(shipment); var health = ShipmentIntelligence.Check(shipment);
        var anchor = shipment.containers.FirstOrDefault() ?? new LoadingPlan { name = shipment.shipmentName };
        var pages = new List<StringBuilder>(); var summary = NewPage(ReportLocalization.Get("ReportTitle"), anchor);
        Text(summary, 40, 704, 10, ReportLocalization.Inline("Shipment") + ": " + shipment.shipmentName);
        if (!string.IsNullOrWhiteSpace(shipment.orderReference)) Text(summary, 40, 682, 9, ReportLocalization.Inline("Order") + ": " + shipment.orderReference);
        if (!string.IsNullOrWhiteSpace(shipment.bookingReference)) Text(summary, 315, 682, 9, ReportLocalization.Inline("Booking") + ": " + shipment.bookingReference);
        if (!string.IsNullOrWhiteSpace(shipment.customerName)) Text(summary, 40, 660, 9, ReportLocalization.Inline("Customer") + ": " + shipment.customerName);
        if (!string.IsNullOrWhiteSpace(shipment.destination)) Text(summary, 40, 638, 9, ReportLocalization.Inline("Destination") + ": " + shipment.destination);
        if (!string.IsNullOrWhiteSpace(shipment.loadingDate)) Text(summary, 315, 638, 9, ReportLocalization.Inline("LoadingDate") + ": " + shipment.loadingDate);
        Kpi(summary, 40, 552, 118, stats.ContainerCount.ToString(), ReportLocalization.Get("Container")); Kpi(summary, 170, 552, 118, stats.CargoTypeCount.ToString(), ReportLocalization.Get("CargoTypes")); Kpi(summary, 300, 552, 118, stats.TotalUnits.ToString(), ReportLocalization.Get("TotalCargo")); Kpi(summary, 430, 552, 125, stats.PlacedUnits.ToString(), ReportLocalization.Get("Placed"));
        Kpi(summary, 40, 478, 118, stats.RemainingUnits.ToString(), ReportLocalization.Get("Remaining")); Kpi(summary, 170, 478, 118, $"{stats.CompletionPercent:0.#}%", ReportLocalization.Get("Completion")); Kpi(summary, 300, 478, 118, $"{stats.UtilizationPercent:0.#}%", ReportLocalization.Get("Utilization")); Kpi(summary, 430, 478, 125, $"{stats.TotalWeight:0.##} kg", ReportLocalization.Get("TotalWeight"));
        Text(summary, 40, 444, 12, ReportLocalization.Get("CurrentPlan"));
        var summaryText = stats.RemainingUnits == 0 ? $"Toàn bộ {stats.TotalUnits} kiện đã được bố trí vào {stats.ContainerCount} container.\n全部{stats.TotalUnits}件货物已分配至{stats.ContainerCount}个集装箱。" : $"Còn {stats.RemainingUnits} kiện chưa được bố trí.\n仍有{stats.RemainingUnits}件货物尚未装载。";
        var sy = 410f; foreach (var line in Wrap(summaryText, 82)) { Text(summary, 40, sy, 9, line); sy -= 15; }
        Text(summary, 40, 360, 12, ReportLocalization.Get("ContainerSummary")); var summaryY = 322f;
        foreach (var container in shipment.containers)
        {
            var cs = PlanIntelligence.Statistics(container); var state = PlanIntelligence.Check(container).IsValid ? "Sẵn sàng / 已就绪" : "Cần kiểm tra / 建议检查";
            Text(summary, 46, summaryY, 8, $"{ShipmentIntelligence.containerNumberOrName(container)}  |  {container.containerType}  |  {cs.PlacedQuantity} kiện / 件  |  {cs.UtilizationPercent:0.#}%  |  {cs.PlacedWeight:0.##} kg  |  {state}"); summaryY -= 22;
        }
        Text(summary, 40, summaryY - 8, 11, ReportLocalization.Get("Warnings")); summaryY -= 30;
        var warnings = health.errors.Concat(health.warnings).ToList();
        foreach (var container in shipment.containers)
        {
            var wd = WeightDistributionCalculator.Calculate(container);
            if (wd.classification == "Lệch tải" || wd.classification == "Nên kiểm tra") warnings.Add($"{ShipmentIntelligence.containerNumberOrName(container)} có phân bố tải cần kiểm tra trước khi đóng hàng.");
        }
        var warningText = warnings.Count == 0 ? "Không phát hiện vấn đề cần chú ý.\n未发现需要特别注意的问题。" : string.Join("\n", warnings.Select(x => x + "\n建议在实际装箱前再次检查。"));
        foreach (var line in Wrap(warningText, 88)) { Text(summary, 46, summaryY, 8, line); summaryY -= 14; }
        pages.Add(summary);
        var cargoPage = NewPage(ReportLocalization.Get("CargoSummary"), anchor); float cargoY = 704;
        DrawCargoTable(cargoPage, ref cargoY, shipment, stats.Cargo);
        if (!string.IsNullOrWhiteSpace(shipment.notes)) AddSectionRows(pages, ref cargoPage, ref cargoY, "GHI CHÚ ĐƠN HÀNG\n订单备注", anchor, Wrap(shipment.notes, 88));
        pages.Add(cargoPage);
        foreach (var container in shipment.containers)
        {
            var page = NewPage("CONTAINER / 集装箱 - " + ShipmentIntelligence.containerNumberOrName(container), container); DrawViews(page, container); pages.Add(page);
            var detail = NewPage("TỔNG QUAN / 概览 - " + ShipmentIntelligence.containerNumberOrName(container), container); float detailY = 720; var cs = PlanIntelligence.Statistics(container); var wd = WeightDistributionCalculator.Calculate(container); Text(detail, 40, detailY, 10, $"{ReportLocalization.Inline("Placed")}: {cs.PlacedQuantity} kiện / 件 · {ReportLocalization.Inline("Utilization")}: {cs.UtilizationPercent:0.#}% · {ReportLocalization.Inline("TotalWeight")}: {cs.PlacedWeight:0.##} kg"); detailY -= 24;
            if (wd.totalWeight > 0) { Text(detail, 40, detailY, 9, $"{ReportLocalization.Inline("LoadDistribution")}: trái/左 {wd.leftPercent:0.#}% · phải/右 {wd.rightPercent:0.#}% · cửa/门端 {wd.frontPercent:0.#}% · cuối/箱尾 {wd.rearPercent:0.#}% · {BilingualLoadClassification(wd.classification)}"); detailY -= 24; }
            AddSectionRows(pages, ref detail, ref detailY, ReportLocalization.Get("CargoSummary"), container, container.cargoTypes.Select(type => $"{type.code} | {type.name} | {container.placedCargo.Count(x => x.cargoTypeId == type.id)} kiện / 件")); pages.Add(detail);
        }
        return pages;
    }

    static IEnumerable<string> BuildLoadingBatches(IEnumerable<LoadingStep> steps)
    {
        var ordered = steps.OrderBy(x => x.step).ToList();
        for (var index = 0; index < ordered.Count;)
        {
            var first = ordered[index];
            var last = first;
            var count = 1;
            index++;
            while (index < ordered.Count && ordered[index].step == last.step + 1 &&
                   ordered[index].cargoCode == first.cargoCode && ordered[index].zone == first.zone)
            {
                last = ordered[index++];
                count++;
            }
            var range = first.step == last.step ? first.step.ToString() : $"{first.step}-{last.step}";
            yield return $"Bước / 步骤 {range} · {first.cargoCode} · {count} kiện / 件 · {BilingualZone(first.zone)}";
        }
    }

    static string BilingualZone(string zone) => zone == "Cuối container" ? "Cuối container / 箱尾区域" : zone == "Giữa container" ? "Giữa container / 中部区域" : "Gần cửa / 靠门区域";

    static string BilingualLoadClassification(string value) => value == "Lệch tải" ? "Lệch tải / 载荷偏移" : value == "Nên kiểm tra" ? "Nên kiểm tra / 建议检查" : value + " / 正常";

    static void DrawCargoTable(StringBuilder page, ref float y, Shipment shipment, IEnumerable<ShipmentCargoStatistics> items)
    {
        var widths = new[] { 62f, 128f, 46f, 48f, 46f, 185f };
        var labels = new[] { ReportLocalization.Get("CargoCode"), ReportLocalization.Get("CargoName"), ReportLocalization.Get("TotalCargo"), ReportLocalization.Get("Placed"), ReportLocalization.Get("Remaining"), ReportLocalization.Get("Distribution") };
        DrawTableRow(page, y, 34, widths, labels, true); y -= 34;
        foreach (var item in items)
        {
            var distribution = string.Join(", ", item.Distribution.Select(d => $"{shipment.containers.Find(c => c.id == d.Key)?.containerNumber ?? d.Key.Substring(0, Math.Min(6, d.Key.Length))}: {d.Value}"));
            var values = new[] { Shorten(item.Type.code, 12), Shorten(item.Type.name, 24), item.Required.ToString(), item.Placed.ToString(), item.Remaining.ToString(), Shorten(distribution, 38) };
            DrawTableRow(page, y, 27, widths, values, false); y -= 27;
        }
        y -= 18;
    }

    static void DrawTableRow(StringBuilder page, float y, float height, float[] widths, string[] values, bool header)
    {
        var x = 40f;
        if (header) page.AppendFormat(CultureInfo.InvariantCulture, "0.91 0.95 0.98 rg 40 {0:0.##} 515 {1:0.##} re f\n", y - height, height);
        for (var i = 0; i < widths.Length; i++)
        {
            page.AppendFormat(CultureInfo.InvariantCulture, "0.76 0.81 0.86 RG {0:0.##} {1:0.##} {2:0.##} {3:0.##} re S\n", x, y - height, widths[i], height);
            Text(page, x + 4, y - (header ? 10 : 17), header ? 6.2f : 7.2f, values[i]);
            x += widths[i];
        }
    }

    static string Shorten(string value, int maximum) => string.IsNullOrEmpty(value) || value.Length <= maximum ? value ?? "" : value.Substring(0, maximum - 1) + "…";

    static void AddFooters(List<StringBuilder> pages, string reference)
    {
        var generated = DateTime.Now;
        for (var i = 0; i < pages.Count; i++)
        {
            Text(pages[i], 40, 22, 7, $"{reference} · Xuất lúc / 生成时间 {generated:dd/MM/yyyy HH:mm} · ContainerLoading v{Application.version}");
            Text(pages[i], 470, 22, 7, $"Trang {i + 1}/{pages.Count} · 第{i + 1}/{pages.Count}页");
        }
    }

    static void Kpi(StringBuilder page,float x,float y,float width,string value,string label)
    {
        page.AppendFormat(CultureInfo.InvariantCulture,"0.95 0.97 0.99 rg {0} {1} {2} 58 re f 0.86 0.9 0.94 RG {0} {1} {2} 58 re S\n",x,y,width);
        Text(page,x+10,y+34,15,value);Text(page,x+10,y+14,7,label);
    }

    static StringBuilder NewPage(string title, LoadingPlan plan)
    {
        var page = new StringBuilder();
        Text(page, 40, 804, 17, title);
        Text(page, 40, 746, 8, $"Phương án hiện tại / 当前方案: {plan.name}");
        Text(page, 360, 746, 8, $"Ngày xuất / 生成时间: {DateTime.Now:dd/MM/yyyy HH:mm}");
        page.Append("0.75 0.8 0.84 RG 40 730 515 0 re S\n");
        return page;
    }

    static void AddSectionRows(List<StringBuilder> pages, ref StringBuilder page, ref float y, string title, LoadingPlan plan, IEnumerable<string> rows)
    {
        if (y < 100) { pages.Add(page); page = NewPage(title, plan); y = 700; }
        Text(page, 40, y, 12, title); y -= 18 * Mathf.Max(1, title.Count(c => c == '\n') + 1) + 8;
        foreach (var row in rows)
            foreach (var line in Wrap(row, 94))
            {
                if (y < 55) { pages.Add(page); page = NewPage(title + " (TIẾP / 续)", plan); y = 700; }
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
        Text(stream,395,704,9,ReportLocalization.Get("Perspective"));DrawIsometric(stream,plan,465,605,Mathf.Min(10f,90f/Mathf.Max(c.length,c.width)));
        Text(stream,40,704,9,ReportLocalization.Get("TopView"));DrawGrid(stream,40,555,c.length,c.width,Mathf.Min(26f,340f/Mathf.Max(c.length,c.width)),0,plan);
        Text(stream,40,520,9,ReportLocalization.Get("SideView"));DrawGrid(stream,40,365,c.length,c.height,Mathf.Min(22f,500f/Mathf.Max(c.length,c.height)),1,plan);
        Text(stream,40,330,9,ReportLocalization.Get("FrontView"));DrawGrid(stream,40,195,c.width,c.height,Mathf.Min(24f,360f/Mathf.Max(c.width,c.height)),2,plan);
        DrawLegend(stream,plan,40,155,18);
    }

    static void DrawIsometric(StringBuilder stream,LoadingPlan plan,float ox,float oy,float scale)
    {
        foreach(var box in plan.placedCargo.OrderBy(x=>x.position.z).ThenBy(x=>x.position.x+x.position.y))
        {
            var type=plan.cargoTypes.Find(x=>x.id==box.cargoTypeId);if(type==null)continue;
            float x=box.position.x,y=box.position.y,z=box.position.z+box.size.z;
            var a=Iso(ox,oy,scale,x,y,z);var b=Iso(ox,oy,scale,x+box.size.x,y,z);var c=Iso(ox,oy,scale,x+box.size.x,y+box.size.y,z);var d=Iso(ox,oy,scale,x,y+box.size.y,z);
            stream.AppendFormat(CultureInfo.InvariantCulture,"{0:0.###} {1:0.###} {2:0.###} rg {3:0.##} {4:0.##} m {5:0.##} {6:0.##} l {7:0.##} {8:0.##} l {9:0.##} {10:0.##} l h f\n",type.color.r,type.color.g,type.color.b,a.x,a.y,b.x,b.y,c.x,c.y,d.x,d.y);
        }
        var cfg=plan.container;var corners=new[]{Iso(ox,oy,scale,0,0,0),Iso(ox,oy,scale,cfg.length,0,0),Iso(ox,oy,scale,cfg.length,cfg.width,0),Iso(ox,oy,scale,0,cfg.width,0),Iso(ox,oy,scale,0,0,cfg.height),Iso(ox,oy,scale,cfg.length,0,cfg.height),Iso(ox,oy,scale,cfg.length,cfg.width,cfg.height),Iso(ox,oy,scale,0,cfg.width,cfg.height)};int[,] edges={{0,1},{1,2},{2,3},{3,0},{4,5},{5,6},{6,7},{7,4},{0,4},{1,5},{2,6},{3,7}};stream.Append("0.25 0.45 0.7 RG 0.8 w\n");for(var i=0;i<edges.GetLength(0);i++){var a=corners[edges[i,0]];var b=corners[edges[i,1]];stream.AppendFormat(CultureInfo.InvariantCulture,"{0:0.##} {1:0.##} m {2:0.##} {3:0.##} l S\n",a.x,a.y,b.x,b.y);}
    }
    static Vector2 Iso(float ox,float oy,float scale,float x,float y,float z)=>new(ox+(x-y)*scale,oy+(x+y)*scale*.45f+z*scale);

    static void DrawLegend(StringBuilder stream,LoadingPlan plan,float x,float y,int maximum)
    {
        Text(stream,x,y,9,ReportLocalization.Get("Legend"));y-=30;var column=0;
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

    static void Text(StringBuilder stream, float x, float y, float size, string value)
    {
        var lines = (value ?? "").Replace("\r", "").Split('\n');
        for (var i = 0; i < lines.Length; i++)
            stream.AppendFormat(CultureInfo.InvariantCulture, "0.07 0.1 0.15 rg BT /F1 {0:0.##} Tf {1:0.##} {2:0.##} Td <{3}> Tj ET\n", size, x, y - i * size * 1.35f, Utf16Hex(lines[i]));
    }
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
        var cidMap = BuildCidMap(fontBytes);
        objects[cidFontId] = Ascii($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /NotoSans /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /FontDescriptor {descriptorId} 0 R /DW 1000 /W {BuildWidths(fontBytes, cidMap)} /CIDToGIDMap {cidMapId} 0 R >>");
        objects[descriptorId] = Ascii($"<< /Type /FontDescriptor /FontName /NotoSans /Flags 32 /FontBBox [-621 -389 2800 1067] /ItalicAngle 0 /Ascent 1069 /Descent -293 /CapHeight 714 /StemV 80 /FontFile2 {fontFileId} 0 R >>");
        objects[fontFileId] = Stream(fontBytes);
        objects[cidMapId] = Stream(cidMap);
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
        var format4 = -1; var format12 = -1;
        for (var i = 0; i < tables; i++)
        {
            var record = cmapOffset + 4 + i * 8;
            var subtable = cmapOffset + (int)U32(font, record + 4);
            if (U16(font, subtable) == 4) { format4 = subtable; if (U16(font, record) == 3 && U16(font, record + 2) == 1) continue; }
            if (U16(font, subtable) == 12) format12 = subtable;
        }
        if (format12 >= 0)
        {
            var groups = (int)U32(font, format12 + 12);
            for (var group = 0; group < groups; group++)
            {
                var offset = format12 + 16 + group * 12; var start = U32(font, offset); var end = U32(font, offset + 4); var glyph = U32(font, offset + 8);
                for (var code = start; code <= end && code <= 0xFFFF; code++) { var gid = glyph + code - start; map[code * 2] = (byte)(gid >> 8); map[code * 2 + 1] = (byte)gid; }
            }
            return map;
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

    static string BuildWidths(byte[] font, byte[] cidMap)
    {
        var hmtx = TableOffset(font, "hmtx"); var hhea = TableOffset(font, "hhea"); var head = TableOffset(font, "head");
        if (hmtx < 0 || hhea < 0 || head < 0) return "[]";
        var metrics = U16(font, hhea + 34); var units = Math.Max(1, (int)U16(font, head + 18));
        var result = new StringBuilder("["); var open = false; var previous = -2;
        for (var code = 0; code < 65536; code++)
        {
            var glyph = (cidMap[code * 2] << 8) | cidMap[code * 2 + 1]; if (glyph == 0) continue;
            if (code != previous + 1) { if (open) result.Append("] "); result.Append(code).Append(" ["); open = true; }
            var metricIndex = Math.Min(glyph, metrics - 1); var advance = U16(font, hmtx + metricIndex * 4); result.Append(Mathf.RoundToInt(advance * 1000f / units)).Append(' '); previous = code;
        }
        if (open) result.Append("] "); return result.Append(']').ToString();
    }

    static int TableOffset(byte[] data, string tag) { var count = U16(data, 4); for (var i = 0; i < count; i++) { var offset = 12 + i * 16; if (Encoding.ASCII.GetString(data, offset, 4) == tag) return (int)U32(data, offset + 8); } return -1; }
    static ushort U16(byte[] data, int offset) => (ushort)((data[offset] << 8) | data[offset + 1]);
    static uint U32(byte[] data, int offset) => ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16) | ((uint)data[offset + 2] << 8) | data[offset + 3];
    static byte[] Ascii(string value) => Encoding.GetEncoding(28591).GetBytes(value);
    static byte[] Stream(byte[] data) { using var output = new MemoryStream(); Write(output, Ascii($"<< /Length {data.Length} >>\nstream\n")); Write(output, data); Write(output, Ascii("\nendstream")); return output.ToArray(); }
    static void Write(Stream stream, byte[] bytes) => stream.Write(bytes, 0, bytes.Length);
}
