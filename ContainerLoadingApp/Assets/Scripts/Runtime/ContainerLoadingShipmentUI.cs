using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class ContainerLoadingApp
{
    bool BuildMobileShipmentHome()
    {
        var content = BuildPage("Chuyến hàng", "Quản lý theo Shipment / Order", true); StatusBanner(content, status);
        var action = Horizontal(content, "ShipmentActions", 10); MobileButton(action, "+ TẠO CHUYẾN HÀNG", BeginNewContainer, true);
        var files = ShipmentPersistence.ListFiles(); var entries = files.Select(path => new { path, data = ShipmentPersistence.Load(path) }).Where(x => x.data != null && (string.IsNullOrWhiteSpace(search) || (x.data.shipmentName ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || (x.data.orderReference ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || (x.data.customerName ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
        var dashboard = Horizontal(content, "ShipmentDashboard", 10); StatCard(dashboard, entries.Count.ToString(), "Chuyến hàng"); StatCard(dashboard, entries.Sum(x => x.data.containers.Count).ToString(), "Container"); StatCard(dashboard, entries.Sum(x => ShipmentIntelligence.Statistics(x.data).RemainingUnits).ToString(), "Còn lại");
        if (entries.Count == 0) { var empty = Vertical(content, "EmptyShipment", UiSurface, 8, 28); MobileText(empty, "Chưa có chuyến hàng", 31, UiText, FontStyle.Bold, TextAnchor.MiddleCenter); MobileText(empty, "Tạo chuyến hàng đầu tiên để bắt đầu phân bổ container.", 23, UiMuted, FontStyle.Normal, TextAnchor.MiddleCenter); return true; }
        foreach (var item in entries)
        {
            var data = item.data; var stats = ShipmentIntelligence.Statistics(data); var card = Vertical(content, "ShipmentCard", UiSurface, 8, 24); card.gameObject.AddComponent<LayoutElement>().preferredHeight = 300;
            var top = Horizontal(card, "Header"); var titles = Vertical(top, "Titles", null, 2); MobileText(titles, string.IsNullOrWhiteSpace(data.orderReference) ? data.shipmentName : data.orderReference, 32, UiText, FontStyle.Bold); MobileText(titles, data.customerName ?? "Chưa có khách hàng", 23, UiMuted); MobileText(top, stats.CompletionPercent >= 100 && stats.RemainingUnits == 0 ? "Sẵn sàng" : "Đang làm", 21, stats.RemainingUnits == 0 ? UiSuccess : UiWarning, FontStyle.Bold, TextAnchor.MiddleRight);
            MobileText(card, $"{stats.ContainerCount} container · {stats.PlacedUnits}/{stats.TotalUnits} kiện · {stats.CompletionPercent:0.#}% hoàn thành", 26, UiText); MobileText(card, $"Điểm đến: {data.destination ?? "Chưa có"}", 22, UiMuted); MobileText(card, "Cập nhật: " + FormatUpdatedAt(data.updatedAt), 22, UiMuted); MobileButton(card, "MỞ CHI TIẾT CHUYẾN HÀNG", () => OpenShipment(item.path), true);
        }
        return true;
    }

    bool BuildMobileShipmentDetail()
    {
        if (shipment == null) return false;
        var stats = ShipmentIntelligence.Statistics(shipment); var content = BuildPage(string.IsNullOrWhiteSpace(shipment.orderReference) ? shipment.shipmentName : shipment.orderReference, "Shipment / Chuyến hàng", false, OpenHome); StatusBanner(content, status);
        var summary = Horizontal(content, "ShipmentSummary", 10); StatCard(summary, stats.ContainerCount.ToString(), "Container"); StatCard(summary, stats.TotalUnits.ToString(), "Tổng kiện"); StatCard(summary, stats.PlacedUnits.ToString(), "Đã xếp"); StatCard(summary, stats.RemainingUnits.ToString(), "Còn lại");
        var info = Vertical(content, "ShipmentInfo", UiSurface, 8, 20); MobileText(info, "Khách hàng: " + (string.IsNullOrWhiteSpace(shipment.customerName) ? "Chưa có" : shipment.customerName), 23, UiText); MobileText(info, "Điểm đến: " + (string.IsNullOrWhiteSpace(shipment.destination) ? "Chưa có" : shipment.destination), 23, UiText); MobileText(info, $"Tiến độ: {stats.CompletionPercent:0.#}% · Trọng lượng: {stats.TotalWeight:0.##} kg", 23, UiText, FontStyle.Bold); ProgressBar(info, stats.TotalUnits == 0 ? 0 : stats.PlacedUnits / (float)stats.TotalUnits);
        SectionHeader(content, "XẾP HÀNG TỰ ĐỘNG", "Tạo các cách xếp thử để bạn so sánh trước khi áp dụng");
        if (optimizationRunning)
        {
            var running = Vertical(content, "OptimizationRunning", UiPrimarySoft, 8, 22); MobileText(running, "Đang tính phương án…", 27, UiPrimary, FontStyle.Bold); MobileText(running, "Sơ đồ hiện tại không bị thay đổi. Tiến trình không hiển thị phần trăm giả.", 22, UiMuted); MobileButton(running, "HỦY", CancelOptimization, false, true);
        }
        else
        {
            var hasContainer = shipment.containers.Count > 0;
            var hasCargo = shipment.cargoTypes.Any(x => x.quantity > 0);
            var optimize = MobileButton(content, "TẠO 3 CÁCH XẾP ĐỂ SO SÁNH", GenerateSuggestedScenarios, true);
            optimize.interactable = hasContainer && hasCargo;
            if (!hasContainer) MobileText(content, "Chưa thể tạo: hãy thêm ít nhất 1 container.", 22, UiWarning, FontStyle.Bold);
            else if (!hasCargo) MobileText(content, "Chưa thể tạo: chuyến hàng chưa có loại hàng nào.", 22, UiWarning, FontStyle.Bold);
            else MobileText(content, "Sơ đồ hiện tại được giữ nguyên cho đến khi bạn chọn Áp dụng.", 22, UiMuted);
            if (!string.IsNullOrWhiteSpace(shipment.restoreScenarioId)) MobileButton(content, "KHÔI PHỤC BẢN XẾP TRƯỚC KHI ÁP DỤNG", RestoreBeforeScenario);
        }
        foreach (var scenario in shipment.scenarios.Where(x => !x.workingSnapshot))
        {
            var m = scenario.metrics; var card = Vertical(content, "ScenarioCard", scenario.recommended ? UiPrimarySoft : UiSurface, 8, 20);
            MobileText(card, scenario.name + (scenario.recommended ? "  ·  ĐỀ XUẤT" : ""), 29, scenario.recommended ? UiPrimary : UiText, FontStyle.Bold);
            MobileText(card, scenario.strategy, 22, UiMuted, FontStyle.Bold);
            MobileText(card, $"{m.containerCount} container · {m.placed}/{m.totalCargo} kiện · {m.completionPercent:0.#}% hoàn thành", 23, UiText);
            MobileText(card, $"Sử dụng TB {m.averageUtilization:0.#}% · thấp nhất {m.minimumUtilization:0.#}% · tải {m.weightBalance}", 22, UiMuted);
            MobileText(card, $"Trình tự: {m.loadingFeasibility} · {m.loadingSteps} bước · {m.warnings} cảnh báo", 22, m.warnings == 0 ? UiSuccess : UiWarning);
            if (scenario.recommended) MobileText(card, scenario.explanation, 22, UiText);
            MobileButton(card, "ÁP DỤNG PHƯƠNG ÁN", () => ApplyScenario(scenario), scenario.recommended);
        }
        SectionHeader(content, "CONTAINERS", "Mỗi container có sơ đồ, utilization và validation riêng");
        foreach (var container in shipment.containers)
        {
            var cs = PlanIntelligence.Statistics(container); var wd = WeightDistributionCalculator.Calculate(container); var card = Vertical(content, "ContainerCard", UiSurface, 8, 18); MobileText(card, ShipmentIntelligence.containerNumberOrName(container), 28, UiText, FontStyle.Bold); MobileText(card, $"{container.containerType} · {container.container?.length} × {container.container?.width} × {container.container?.height} · {cs.PlacedQuantity} kiện · {cs.UtilizationPercent:0.#}% · {cs.PlacedWeight:0.##} kg", 23, UiMuted); if(wd.totalWeight>0)MobileText(card,$"Tải: trái {wd.leftPercent:0.#}% / phải {wd.rightPercent:0.#}% · cửa {wd.frontPercent:0.#}% / cuối {wd.rearPercent:0.#}% · {wd.classification}",21,UiMuted);var row = Horizontal(card, "ContainerActions"); MobileButton(row, "MỞ SƠ ĐỒ", () => OpenShipmentContainer(container), true); MobileButton(row, "PDF", () => ExportContainerPdf(container)); var more = Horizontal(card, "ContainerMore"); MobileButton(more, "NHÂN BẢN CẤU HÌNH", () => DuplicateShipmentContainer(container, false)); MobileButton(more, "NHÂN BẢN KÈM HÀNG", () => DuplicateShipmentContainer(container, true)); MobileButton(more, "XÓA", () => RequestDeleteShipmentContainer(container), false, true);
        }
        MobileButton(content, "+ THÊM CONTAINER", AddShipmentContainer, true); var reports = Horizontal(content, "Reports"); MobileButton(reports, "XUẤT PDF TOÀN CHUYẾN", ExportPdf, true); MobileButton(reports, "SAO LƯU", ExportPlanData);
        return true;
    }

    void OpenShipment(string path)
    { var loaded = ShipmentPersistence.Load(path); if (loaded == null) { status = "Không thể mở chuyến hàng."; return; } shipment = loaded; EnsureShipmentDefaults(); plan = shipment.containers.FirstOrDefault() ?? CreateContainerForShipment(); shipmentPath = path; screen = ScreenMode.Detail; selectedId = null; placementMode = false; Rebuild(); ResetCamera(); RefreshMobileUI(); }
    void OpenShipmentContainer(LoadingPlan container) { plan = container; selectedId = null; placementMode = false; screen = ScreenMode.Editor; ResetCamera(); Rebuild(); RefreshMobileUI(); }
    void AddShipmentContainer() { var index = shipment.containers.Count + 1; var container = new LoadingPlan { id = Guid.NewGuid().ToString("N"), shipmentId = shipment.id, name = "Container " + index.ToString("00"), containerType = "40FT", container = new ContainerConfig { id = Guid.NewGuid().ToString("N"), name = "40FT", length = 12, width = 3, height = 3 }, cargoTypes = shipment.cargoTypes }; shipment.containers.Add(container); plan = container; ShipmentPersistence.Save(shipment, shipmentPath); status = "Đã thêm container rỗng."; RefreshMobileUI(); }
    void DuplicateShipmentContainer(LoadingPlan source, bool includePlacements)
    {
        var copy=ShipmentIntelligence.DuplicateContainer(shipment,source,includePlacements,out var copied);if(includePlacements&&!copied)status="Không đủ số lượng toàn chuyến để sao chép hàng. Đã tạo container chỉ có cấu hình.";else status=copied?"Đã nhân bản container kèm hàng.":"Đã nhân bản cấu hình container.";plan = copy; shipmentPath = ShipmentPersistence.Save(shipment, shipmentPath); RefreshMobileUI();
    }
    void RequestDeleteShipmentContainer(LoadingPlan target)
    {
        if (shipment.containers.Count <= 1) { status = "Chuyến hàng phải có ít nhất một container."; RefreshMobileUI(); return; }
        var count = target.placedCargo.Count; Confirm("Xóa container?", $"Container này đang chứa {count} kiện. Nếu xóa container, {count} kiện sẽ trở lại danh sách chưa xếp.", () => { ShipmentIntelligence.DeleteContainer(shipment,target); plan = shipment.containers[0]; shipmentPath = ShipmentPersistence.Save(shipment, shipmentPath); status = "Đã xóa container; số lượng đã được trả lại chuyến hàng."; RefreshMobileUI(); }, "Xóa container", "Hủy");
    }
    void ExportContainerPdf(LoadingPlan container) { try { lastPdf = PdfExportSystem.Export(container); status = "Đã tạo PDF container."; } catch (Exception e) { status = "Không thể tạo PDF container: " + e.Message; } RefreshMobileUI(); }
}
