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
        SectionHeader(content, "CONTAINERS", "Mỗi container có sơ đồ, utilization và validation riêng");
        foreach (var container in shipment.containers)
        {
            var cs = PlanIntelligence.Statistics(container); var card = Vertical(content, "ContainerCard", UiSurface, 8, 18); MobileText(card, ShipmentIntelligence.containerNumberOrName(container), 28, UiText, FontStyle.Bold); MobileText(card, $"{container.containerType} · {container.container?.length} × {container.container?.width} × {container.container?.height} · {cs.PlacedQuantity} kiện · {cs.UtilizationPercent:0.#}% · {cs.PlacedWeight:0.##} kg", 23, UiMuted); var row = Horizontal(card, "ContainerActions"); MobileButton(row, "MỞ SƠ ĐỒ", () => OpenShipmentContainer(container), true); MobileButton(row, "PDF", () => ExportContainerPdf(container)); var more = Horizontal(card, "ContainerMore"); MobileButton(more, "NHÂN BẢN CẤU HÌNH", () => DuplicateShipmentContainer(container, false)); MobileButton(more, "NHÂN BẢN KÈM HÀNG", () => DuplicateShipmentContainer(container, true)); MobileButton(more, "XÓA", () => RequestDeleteShipmentContainer(container), false, true);
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
