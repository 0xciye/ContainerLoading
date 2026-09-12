# Architecture

`LoadingPlanModels` là domain model serializable; `PlanValidation` chịu trách nhiệm migration schema và integrity. `GridPlacement` kiểm tra bounds, collision, quantity, rotation, support và stackability bằng tọa độ nguyên Cột/Hàng/Tầng. `PlanIntelligence` giữ completeness/weight/health và heuristic placement V3.

`V4Planning` là domain thuần tách khỏi UI: `MultiContainerOptimizer` tạo candidate deterministic có time budget/cancellation, `ConstraintEvaluator` trả severity + reason code, `LoadingSequencePlanner` dựng dependency DAG với cửa tại X=0, `WeightDistributionCalculator` tính centroid/phân bố tải, và `ScenarioService` quản lý apply/snapshot/restore. Đây là heuristic best-found, không chứng minh optimality.

`ShipmentPersistence` validate-before-write và nâng Shipment schema 3 lên 4; `PlanPersistence` tiếp tục bảo toàn Plan V2. `ContainerLoadingApp` điều phối workflow, undo/redo, debounced autosave, camera và visualizer; `ContainerLoadingMobileUI` dựng uGUI responsive, còn `ContainerLoadingShipmentUI` hiển thị scenario comparison. `PdfExportSystem` tạo báo cáo A4 Unicode nhiều trang cho manager và operator. `AppUpdateService` kiểm tra GitHub Releases không token và không chặn startup. Android bridge xử lý picker, content URI và share sheet.
