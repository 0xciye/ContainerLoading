# Container Loading App

Ứng dụng Unity 6 chạy trên Android để lập, kiểm tra và báo cáo phương án xếp container trên lưới 3D.

## Version 4

- Tạo tối đa 3 `Optimized Candidate` bằng heuristic deterministic: ưu tiên tiết kiệm container, cân bằng tải hoặc dễ đóng hàng; không tuyên bố nghiệm tối ưu tuyệt đối.
- So sánh candidate theo completion, số container, utilization trung bình/thấp nhất, trọng lượng, cảnh báo và tính khả thi của trình tự; recommendation có giải thích rule-based.
- Áp dụng candidate theo transaction: luôn tạo snapshot khôi phục trước, không ghi đè âm thầm sơ đồ thủ công.
- Placement có thể khóa; optimizer giữ nguyên kiện đã khóa và xử lý phần còn lại.
- Cửa container được quy ước tại `X=0`/Cột 1; dependency graph tạo thứ tự đóng hàng từ sâu ra cửa và từ dưới lên trên, có Previous/Next để highlight kiện trên 3D.
- Tính chỉ báo phân bố tải trái/phải, cửa/cuối và center-of-mass tương đối; chỉ phục vụ lập kế hoạch, không phải chứng nhận an toàn.
- PDF cấp Shipment có executive recommendation, bảng so sánh scenario, weight indicator và hướng dẫn đóng hàng theo nhóm.
- Shipment schema 4 đọc trực tiếp dữ liệu schema 3; backup giữ scenario, lock, sequence, settings và lịch sử nhẹ.

### Baseline V3 được bảo toàn

- Home Dashboard hiển thị chuyến hàng thay vì từng container rời; một Shipment chứa danh mục cargo dùng chung và nhiều Container Plan.
- Shipment Detail tổng hợp container, tổng/đã xếp/còn lại, tiến độ, trọng lượng và card trạng thái của từng container.
- Quantity được khóa ở cấp Shipment; đặt/xóa/nhân bản container luôn cộng đúng trên toàn chuyến và không thể vượt yêu cầu.
- Tìm kiếm dữ liệu thật và xóa container có xác nhận.
- Nhập tên và kích thước container theo ô lưới.
- Thêm, sửa, xóa nhiều loại hàng; quản lý số lượng, trọng lượng, màu, quyền xoay và khả năng chịu xếp chồng.
- Chạm để chọn cell, xem ghost, kiểm tra rồi xác nhận đặt; chọn thùng đã đặt, xoay, di chuyển và xóa.
- Hai chế độ đặt: chạm trực tiếp trên 3D hoặc nút điều hướng Cột/Hàng/Tầng; thao tác giữ nguyên vị trí cuộn và luôn có preview cùng viền ô trắng.
- Đặt tiếp nhanh, đề xuất vị trí và xếp tự động phần còn lại theo heuristic deterministic bottom-first.
- Đổi tầng, góc nhìn Top/Front/Side, orbit và zoom camera.
- Kiểm tra vượt biên, va chạm, số lượng, hướng xoay, bề mặt đỡ và quy tắc xếp chồng; undo/redo và autosave có debounce.
- Lưu, mở và xóa nhiều phương án dưới dạng JSON.
- Quantity dùng một nguồn dữ liệu duy nhất: tổng, đã xếp, còn lại; có search/filter cargo và không cho đặt vượt số lượng.
- Xếp tự động theo heuristic deterministic có màn preview, Accept/Cancel và rollback cả batch bằng một lần Undo.
- Xuất PDF mặc định cho toàn Shipment: KPI, container summary, cargo distribution, Isometric/Top/Front/Side và section riêng từng container; vẫn xuất được PDF container độc lập.
- Backup/restore toàn bộ dữ liệu và CSV cargo có validate trước khi ghi, chống ghi đè và cô lập file hỏng.
- Health check toàn phương án, completeness, dung tích, tổng trọng lượng; dashboard tìm kiếm và sắp xếp.
- Nhân bản phương án an toàn, tự xóa các định danh container/niêm phong/đơn hàng cần nhập mới.
- Kiểm tra cập nhật bất đồng bộ qua GitHub Releases `0xciye/ContainerLoading`; không lộ owner/repo hay token cho người dùng, lỗi mạng không chặn ứng dụng.
- Icon logistics riêng; Unity splash và logo “Made with Unity” đã tắt.

Placement dùng grid cell làm source of truth. Khi di chuyển hoặc đặt hàng, object không commit ngay: người dùng có thể chỉnh vị trí, xem trạng thái hợp lệ/va chạm/vượt biên rồi nhấn “ĐẶT HÀNG”. Màu category được giữ trên cargo và được lưu cùng plan JSON.

## Chạy và kiểm thử

Trong Unity Hub, mở project bằng Unity `6000.5.10f1`, mở `Assets/Scenes/SampleScene.unity` và nhấn Play.

APK ARM64 V4.0 đã ký release tại `Builds/Android/ContainerLoadingApp-v4.0.0.apk`. Yêu cầu Android 8.0 (API 26) trở lên. Xem hướng dẫn tại `docs/ANDROID_BUILD.md`.

56 EditMode tests bao phủ thêm optimizer, determinism, scenario apply/restore, lock, loading sequence, reason code, weight/CoM, migration V3 và workflow 5 container/240 kiện qua save–restart–backup–PDF. Kiến trúc và giới hạn hiện tại được ghi tại `docs/PROJECT_DETAILS.md`.

## Dữ liệu

Shipment schema 4 được lưu nguyên tử trong vùng dữ liệu ứng dụng; quan hệ Cargo–Container–Placement–Scenario được giữ nguyên khi backup/import. Shipment V3 và Plan V2 được migrate idempotent, file nguồn không bị xóa. PDF ưu tiên thư mục `Documents` công khai trên Android và tự fallback về vùng ứng dụng nếu thiết bị từ chối quyền ghi.

## Nhận diện

Icon nguồn 1024 × 1024 nằm tại `Assets/Resources/AppIcon.png`. Script build tự áp dụng icon Android và tắt Unity splash trong mọi lần build.

PDF nhúng Noto Sans từ `Assets/Resources/NotoSans-Regular.bytes` để giữ Unicode tiếng Việt; file license đi kèm trong Resources.
