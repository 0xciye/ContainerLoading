# Container Loading App

Ứng dụng Unity 6 chạy trên Android để lập, kiểm tra và báo cáo phương án xếp container trên lưới 3D.

## Version 3

- Home Dashboard là màn hình khởi động, hiển thị container dạng card với trạng thái, thời gian cập nhật và mức sử dụng.
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
- Xuất báo cáo quản trị PDF nhiều trang: KPI, trạng thái READY/DRAFT, executive summary, sơ đồ Top/Front/Side, legend, bảng cargo, validation và ghi chú Unicode.
- Backup/restore toàn bộ dữ liệu và CSV cargo có validate trước khi ghi, chống ghi đè và cô lập file hỏng.
- Health check toàn phương án, completeness, dung tích, tổng trọng lượng; dashboard tìm kiếm và sắp xếp.
- Nhân bản phương án an toàn, tự xóa các định danh container/niêm phong/đơn hàng cần nhập mới.
- Kiểm tra cập nhật bất đồng bộ qua GitHub Releases `0xciye/ContainerLoading`; không lộ owner/repo hay token cho người dùng, lỗi mạng không chặn ứng dụng.
- Icon logistics riêng; Unity splash và logo “Made with Unity” đã tắt.

Placement dùng grid cell làm source of truth. Khi di chuyển hoặc đặt hàng, object không commit ngay: người dùng có thể chỉnh vị trí, xem trạng thái hợp lệ/va chạm/vượt biên rồi nhấn “ĐẶT HÀNG”. Màu category được giữ trên cargo và được lưu cùng plan JSON.

## Chạy và kiểm thử

Trong Unity Hub, mở project bằng Unity `6000.5.10f1`, mở `Assets/Scenes/SampleScene.unity` và nhấn Play.

APK ARM64 V3 đã ký release tại `Builds/Android/ContainerLoadingApp-v3.0.0.apk`. Yêu cầu Android 8.0 (API 26) trở lên. Xem hướng dẫn tại `docs/ANDROID_BUILD.md`.

41 EditMode tests bao phủ placement, quantity, auto-fill, validation, 500 placements, backup/CSV, update parsing và PDF Unicode nhiều trang. Kiến trúc và giới hạn hiện tại được ghi tại `docs/PROJECT_DETAILS.md`.

## Dữ liệu

Phương án được lưu nguyên tử trong vùng dữ liệu ứng dụng. PDF ưu tiên thư mục `Documents` công khai trên Android và tự fallback về vùng ứng dụng nếu thiết bị từ chối quyền ghi.

## Nhận diện

Icon nguồn 1024 × 1024 nằm tại `Assets/Resources/AppIcon.png`. Script build tự áp dụng icon Android và tắt Unity splash trong mọi lần build.

PDF nhúng Noto Sans từ `Assets/Resources/NotoSans-Regular.bytes` để giữ Unicode tiếng Việt; file license đi kèm trong Resources.
