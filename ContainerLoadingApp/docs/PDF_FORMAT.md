# PDF format

`PdfExportSystem` tạo **CONTAINER LOADING MANAGEMENT REPORT** nhiều trang từ `LoadingPlan`, dùng chung `PlanIntelligence` với UI. Trang 1 có KPI, READY/DRAFT, executive summary, metadata, Top view và legend; các trang sau có Top/Side/Front, cargo summary, validation và ghi chú.

Phần `GHI CHÚ ĐƠN HÀNG` hỗ trợ nhiều dòng, Unicode tiếng Việt và wrapping/page break. Font Noto Sans được nhúng dưới dạng Type0/CID. Footer có order, thời điểm tạo, version và `Page X / Y`. Tên file được sanitize theo `LoadingPlan_<OrderRef>_<ContainerNo>_<yyyyMMdd>.pdf` và fallback sang tên phương án khi thiếu metadata.
