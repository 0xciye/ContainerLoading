# Data model

`Shipment` schema 4 là aggregate root: metadata chuyến hàng, danh sách `CargoType` dùng chung, 1..N `LoadingPlan`, tối đa 3 candidate Scenario và 1 snapshot khôi phục. Mỗi Scenario lưu containers/placements, metrics, recommendation, warnings và optimization metadata; quantity vẫn được cộng trên toàn Shipment.

Shipment schema 3 mở trực tiếp trong V4 và nhận mặc định an toàn: chưa có scenario, chưa khóa placement, settings optimizer mặc định, sequence chỉ sinh khi user yêu cầu. Mỗi file `LoadingPlan` schema 2 vẫn được migrate idempotent thành một Shipment có một container. File nguồn được giữ nguyên; Shipment sau khi lưu mang `schemaVersion = 4`.

`CargoType` lưu mã, tên, kích thước nguyên, quantity, trọng lượng mỗi kiện, màu, quyền xoay và stackability. `PlacedCargo` lưu loại hàng, vị trí Cột/Hàng/Tầng, kích thước sau xoay, góc xoay và trạng thái khóa. `LoadingPlan` lưu thêm loading sequence và quy ước cửa `X0`.

`PlanValidation` kiểm tra schema, độ dài, dimensions, quantity mismatch, weight, mã trùng, cargo reference, orientation, stackability, bounds và overlap trước khi lưu/import. Schema 1 được nâng cấp xác định, giữ nguyên placement và tự điều chỉnh quantity tối thiểu bằng số đã xếp.
