# Testing

EditMode tests nằm trong `Assets/Editor/GridPlacementTests.cs`, bao phủ grid/bounds/collision, quantity, weight, orientation, stackability, assisted placement, health check, persistence, corrupt JSON, import/export, update parsing và PDF Unicode nhiều trang.

Chạy: `unity test . --editor-version 6000.5.10f1 --mode EditMode --output TestResults/editmode.xml --timeout 600`.

Build Android release phải cung cấp bốn biến signing (`ANDROID_KEYSTORE_PATH`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_ALIAS_PASS`) và thêm `-requireSigning true`. CI sẽ fail nếu thiếu một giá trị thay vì xuất APK debug.

QA V3 hiện có 41/41 EditMode tests đạt. Bộ performance deterministic kiểm tra 500 kiện; QA report tạo 10 loại × 30 kiện và lấp đủ 300/300 ô trong container 12 × 5 × 5.

Manual QA: startup → tạo container → thêm cargo với quantity → palette màu → đặt/xoay/di chuyển → auto-place preview/accept/cancel → lưu → sửa/xóa → backup/CSV → validation → xuất/mở/chia sẻ PDF → đóng/mở lại app.
