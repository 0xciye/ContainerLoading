# Data model

`LoadingPlan` schema 2 là nguồn dữ liệu chính, có tên phương án, tham chiếu đơn, khách hàng, số container, số niêm phong, điểm đến, ngày đóng hàng, ghi chú, timestamps, `ContainerConfig`, danh sách `CargoType` và `PlacedCargo`.

`CargoType` lưu mã, tên, kích thước nguyên, quantity, trọng lượng mỗi kiện, màu, quyền xoay và stackability. `PlacedCargo` lưu loại hàng, vị trí Cột/Hàng/Tầng, kích thước sau xoay và góc xoay.

`PlanValidation` kiểm tra schema, độ dài, dimensions, quantity mismatch, weight, mã trùng, cargo reference, orientation, stackability, bounds và overlap trước khi lưu/import. Schema 1 được nâng cấp xác định, giữ nguyên placement và tự điều chỉnh quantity tối thiểu bằng số đã xếp.
