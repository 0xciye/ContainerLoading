using System.Collections.Generic;

public static class ReportLocalization
{
    static readonly Dictionary<string, string> Labels = new()
    {
        ["ReportTitle"] = "BÁO CÁO PHƯƠNG ÁN XẾP HÀNG\n集装箱装载方案报告",
        ["ShipmentOverview"] = "TỔNG QUAN CHUYẾN HÀNG\n货运概览",
        ["Shipment"] = "Chuyến hàng\n货运批次",
        ["Order"] = "Mã đơn\n订单号",
        ["Booking"] = "Booking\n订舱号",
        ["Customer"] = "Khách hàng\n客户",
        ["Destination"] = "Điểm đến\n目的地",
        ["LoadingDate"] = "Ngày đóng\n装箱日期",
        ["GeneratedAt"] = "Ngày xuất báo cáo\n报告生成时间",
        ["Container"] = "Container\n集装箱",
        ["CargoTypes"] = "Loại hàng\n货物种类",
        ["TotalCargo"] = "Tổng kiện\n总件数",
        ["Placed"] = "Đã xếp\n已装",
        ["Remaining"] = "Còn lại\n剩余",
        ["Completion"] = "Hoàn thành\n完成率",
        ["Utilization"] = "Mức sử dụng\n空间利用率",
        ["TotalWeight"] = "Tổng trọng lượng\n总重量",
        ["Status"] = "Trạng thái\n状态",
        ["ContainerSummary"] = "TỔNG HỢP CONTAINER\n集装箱汇总",
        ["CargoSummary"] = "TỔNG HỢP HÀNG HÓA\n货物汇总",
        ["Warnings"] = "CẦN LƯU Ý\n注意事项",
        ["Notes"] = "GHI CHÚ\n备注",
        ["CurrentPlan"] = "PHƯƠNG ÁN HIỆN TẠI\n当前装载方案",
        ["LoadDistribution"] = "Phân bố tải\n载荷分布",
        ["OperationalTitle"] = "HƯỚNG DẪN ĐÓNG HÀNG\n装箱作业指导",
        ["LoadingOrder"] = "THỨ TỰ ĐÓNG HÀNG\n装箱顺序",
        ["Diagram"] = "SƠ ĐỒ ĐÓNG HÀNG\n装箱示意图",
        ["CargoCode"] = "Mã hàng\n货号",
        ["CargoName"] = "Tên hàng\n货物名称",
        ["Distribution"] = "Phân bổ container\n集装箱分配",
        ["TopView"] = "Mặt trên\n俯视图",
        ["Perspective"] = "Phối cảnh 3D\n3D透视图",
        ["SideView"] = "Mặt bên\n侧视图",
        ["FrontView"] = "Mặt trước\n正视图",
        ["Legend"] = "Chú giải\n图例"
    };

    public static string Get(string key) => Labels.TryGetValue(key, out var value) ? value : key;
    public static string Inline(string key) => Get(key).Replace("\n", " / ");
}
