# PDF format

`PdfExportSystem` tạo **CONTAINER LOADING MANAGEMENT REPORT** nhiều trang từ `LoadingPlan`, dùng chung `PlanIntelligence` với UI. Trang 1 có KPI, READY/DRAFT, executive summary, metadata, Top view và legend; các trang sau có Top/Side/Front, cargo summary, validation và ghi chú.

Management Report mặc định ở cấp Shipment, gồm KPI toàn chuyến, bảng container, Cargo Required/Loaded/Remaining, phân bổ cargo theo container và section Isometric/Top/Side/Front cho từng container. Font Noto Sans được nhúng dạng Type0/CID. Tên file là `Shipment_<OrderRef>_<yyyyMMdd>.pdf`; báo cáo container riêng vẫn dùng `LoadingPlan_<OrderRef>_<ContainerNo>_<yyyyMMdd>.pdf`.
