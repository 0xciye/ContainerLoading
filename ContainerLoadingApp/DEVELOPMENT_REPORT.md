# Development report

## Kết quả

- Tạo ứng dụng Unity `6000.0.65f1` từ workspace ban đầu.
- Hoàn thiện luồng nhập container -> nhập danh mục thùng -> xếp hàng 3D -> lưu phương án -> xuất PDF.
- Kích thước container và từng loại thùng đều do người dùng nhập; không còn phụ thuộc các bội số demo cố định.
- Tích hợp xem/chia sẻ PDF trên Android bằng content provider nội bộ, chỉ cấp quyền đọc file trong vùng dữ liệu của ứng dụng.
- Thêm script build Android và tạo APK ARM64.

## Kiểm chứng

- EditMode tests: 10/10 đạt.
- Unity batch build Android: thành công, không có compiler/build error.
- Package id: `com.ciye.containerloading`; min SDK: 26; target SDK theo Unity 6.
- APK: `Builds/Android/ContainerLoadingApp.apk`.

## UI/UX redesign và branding

- App mở vào Home Dashboard thay vì vào thẳng editor 3D.
- Bổ sung container cards, tìm kiếm/sắp xếp, trạng thái, thống kê, nhân bản và xác nhận xóa.
- Tách luồng tạo/sửa container, chi tiết, loại hàng, editor 3D và cài đặt.
- Bảo vệ dữ liệu khi đổi kích thước container hoặc loại hàng đang được sử dụng.
- Tạo icon logistics riêng và áp dụng vào launcher Android.
- Tắt Unity splash screen và Unity logo trong PlayerSettings lẫn script build.

## Manual QA

- Đã cài APK trực tiếp trên LDPlayer và chạy ở portrait 900 × 1600.
- Đã kiểm tra empty state, nút +, form, validation, tạo/mở dữ liệu, danh mục hàng, editor 3D, Android Back, dialog xác nhận xóa và Settings navigation.
- Đã force-stop/mở lại app và xác nhận phương án cùng thùng đã đặt vẫn tồn tại.
- Runtime log không còn lỗi Input System và IMGUI làm màn hình trắng.

## Giới hạn đã biết

Xem `docs/PROJECT_DETAILS.md`. Dark mode và filter trạng thái chưa được tạo vì model hiện tại chưa có theme/status thật; ứng dụng không hiển thị setting hoặc filter giả.
