# Data model

`Shipment` schema 3 là aggregate root: metadata chuyến hàng, danh sách `CargoType` dùng chung và 1..N `LoadingPlan` con. Mỗi `LoadingPlan` là Container Plan tương thích V2, chỉ sở hữu cấu hình/số container/niêm phong/ghi chú/timestamps và `PlacedCargo`; quantity được cộng trên toàn Shipment.

Khi lần đầu mở V3.1, mỗi file `LoadingPlan` schema 2 được migrate idempotent thành một Shipment có một container. File V2 nguồn được giữ nguyên; Shipment sau khi lưu luôn mang `schemaVersion = 3`.

`CargoType` lưu mã, tên, kích thước nguyên, quantity, trọng lượng mỗi kiện, màu, quyền xoay và stackability. `PlacedCargo` lưu loại hàng, vị trí Cột/Hàng/Tầng, kích thước sau xoay và góc xoay.

`PlanValidation` kiểm tra schema, độ dài, dimensions, quantity mismatch, weight, mã trùng, cargo reference, orientation, stackability, bounds và overlap trước khi lưu/import. Schema 1 được nâng cấp xác định, giữ nguyên placement và tự điều chỉnh quantity tối thiểu bằng số đã xếp.
