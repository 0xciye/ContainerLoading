# Project Details

## Application Overview

Container Loading là ứng dụng Android/Unity 6 để tạo container theo kích thước người dùng nhập, khai báo các loại thùng và bố trí chúng trên lưới 3D. Dữ liệu hiển thị trong danh sách luôn được đọc từ storage thật; khi chưa có file dữ liệu, ứng dụng hiển thị empty state.

## Mobile UI Architecture

UI runtime dùng Unity uGUI (`Canvas`, `CanvasScaler`, `ScrollRect`, `RectMask2D`, layout groups và `EventSystem`). `ContainerLoadingMobileUI` chịu trách nhiệm dựng màn hình và nối sự kiện; `ContainerLoadingApp` giữ trạng thái/workflow và điều khiển mô hình 3D. Reference resolution là 1080 × 1920, scale theo kích thước màn hình.

## Screens

- Home: tìm kiếm dữ liệu thật, empty state, card container và thao tác mở/xóa.
- Create/Edit Container: preset tùy chọn và form kích thước do người dùng nhập.
- Detail: thống kê dung tích thật, mở danh mục hàng, chỉnh sửa, xếp 3D và xuất/chia sẻ PDF.
- Cargo Types: thêm, sửa, xóa loại thùng với kích thước do người dùng nhập.
- 3D Editor: touch hoặc bảng nút điều hướng, preview viền trắng, đặt/chọn/xoay/di chuyển/xóa, đổi tầng, camera, auto-place transaction, undo/redo và autosave.
- Settings: chỉ hiển thị cấu hình/khả năng thực sự có trong ứng dụng.

## Navigation

Bottom navigation chuyển thật giữa Home và Settings. Màn hình con có nút Back; Android Back đi Editor/Cargo/Edit về Detail, màn hình tạo và Settings về Home, và chỉ thoát khi đang ở Home.

## Container Management

Home đọc Shipment schema 3 trong thư mục `Shipments` và hiển thị mỗi chuyến hàng thành một card tổng hợp. Shipment Detail là hub KPI và danh sách container; editor vẫn mở độc lập theo từng container nhưng cargo remaining luôn lấy từ toàn Shipment.

## Create/Edit/Delete Workflow

Create kiểm tra mã bắt buộc và ba kích thước nguyên dương trước khi lưu. Edit nạp dữ liệu cũ và chặn thu nhỏ nếu làm thùng đã đặt vượt biên. Delete luôn mở dialog xác nhận. Danh mục thùng kiểm tra mã trùng, kích thước, khả năng nằm trong container và chặn xóa/đổi kích thước khi loại đó đang được sử dụng.

## Data Persistence

`ShipmentPersistence` lưu toàn bộ metadata, cargo, container và placement trong một JSON schema 3 dưới `Application.persistentDataPath/Shipments`. Save dùng file tạm + replace; backup/import bảo toàn quan hệ. Nếu chỉ có dữ liệu V2 trong `Plans`, migration tạo Shipment tương ứng mà không xóa source và không lặp lại khi chạy sau.

## Design System

Giao diện dùng navy doanh nghiệp, nền xanh-xám nhạt, surface trắng và đỏ cho thao tác nguy hiểm. Hệ thống dùng khoảng cách nhất quán, chữ phân cấp rõ, button/input cao tối thiểu khoảng 44dp sau scaling, trạng thái pressed/selected/disabled và vùng chạm lớn.

## Dark Mode

Chưa có dark mode trong model/cấu hình hiện tại. Không hiển thị công tắc giả trong Settings.

## Responsive Design

Ứng dụng ưu tiên portrait phone/LDPlayer và dùng CanvasScaler cùng layout co giãn cho các độ phân giải khác. Nội dung dài cuộn trong vùng giữa; header và bottom navigation giữ cố định. Build hiện khóa portrait.

## Android Interaction

Trường kích thước dùng numeric keyboard; text field dùng keyboard chuẩn. Android Back tuân theo cấp màn hình. Splash Unity bị tắt, launcher dùng icon riêng. PDF được mở/chia sẻ qua Android content provider với quyền đọc tạm thời.

## V3 reliability và testing

47/47 EditMode tests đạt, gồm placement nhiều tầng, bounds/collision, quota xuyên 3 container, remove/delete/duplicate, migration V2, Shipment persistence/backup, auto-fill, 500 placements, update parsing và PDF. QA report production tạo Shipment A=100/B=50/C=25 phân bổ đủ trên 3 container; PDF 8 trang đã render và kiểm tra trực quan. APK V3.1 đã cài/mở qua LDPlayer CLI; ADB của instance vẫn báo `offline`, nên không thể ghi nhận thao tác UI tự động bằng bridge.

## Known Limitations

- Chưa có dark mode vì ứng dụng chưa có cơ chế lưu theme.
- Status được derive từ validation/statistics (`Chưa hoàn tất`, `Cần kiểm tra`, `Sẵn sàng`), không lưu một trạng thái có thể lệch dữ liệu.
- Storage là JSON cục bộ và được đọc đồng bộ, phù hợp quy mô dữ liệu hiện tại nhưng chưa tối ưu cho hàng nghìn phương án.
- PDF open/share phụ thuộc ứng dụng nhận intent có sẵn trên thiết bị. CI cần Unity license secrets trước khi workflow có thể build trên GitHub-hosted runner.

## Future Improvements

- Thêm theme persistence và dark palette hoàn chỉnh.
- Thêm trạng thái nghiệp vụ chính thức vào model rồi triển khai filter.
- Chuyển danh sách lớn sang virtualized list và repository bất đồng bộ.
- Thêm PlayMode UI tests và tự động cuộn chính xác tới field đang focus khi dùng bàn phím mềm trên nhiều thiết bị hơn.

## 3D Container Loading

### Grid System

`Vector3Int` là source of truth theo Cột/Hàng/Tầng; kích thước và vị trí nghiệp vụ đều tính theo ô. World coordinates chỉ được suy ra khi dựng object 3D.

### Placement System

Đặt nhanh bằng tap vào mặt phẳng layer hoặc đặt chính xác bằng Position Controller. Các nút +/- dịch chuyển đúng một ô và input số cho phép nhập trực tiếp Cột, Hàng, Tầng.

### Position Controller

Controller dùng nút Cột/Hàng/Tầng thay vì nhập số, hiển thị footprint, preview viền trắng và trạng thái hợp lệ/lỗi. Scroll anchor được giữ khi dịch chuyển hoặc xoay nên panel không nhảy về đầu.

### Layer System

Layer hiện tại có guide grid ngang trong container và giới hạn theo chiều cao thật của container.

### Top / Front / Side Views

Các view dùng chung placement state; nút camera hiện có giữ nguyên và layer guide được dựng theo kích thước container.

### Ghost Preview

Tap cell hoặc bắt đầu đặt hàng tạo ghost theo kích thước đã xoay và category color. Ghost chỉ commit sau khi người dùng xác nhận.

### Snap

Tap được snap xuống cell nguyên; không có vị trí pixel/world tự do trong business model.

### Collision / Boundary Validation

Validation xét toàn bộ footprint và có thể bỏ qua chính object đang di chuyển, nên không tự va chạm với bản thân.

### Cargo Categories / Category Colors

Màu nằm trong `CargoType.color`, được lưu cùng JSON và renderer lấy trực tiếp từ category. Card danh mục hiển thị swatch/HEX; palette có selected checkmark.

### Color Picker

Màn hình mobile hiện cung cấp palette chọn màu, màu hiện tại và mã HEX ngay trong form category; thay đổi được lưu khi lưu loại hàng.

### 3D Rendering

Object và ghost giữ category color; selection/validation chỉ là trạng thái UI, không thay màu identity của hàng.

### Persistence / PDF Export

Metadata đơn hàng, quantity, placement, rotation, category và color đi qua JSON/PDF pipeline. PDF dùng Noto Sans nhúng để hiển thị tiếng Việt và có trang ghi chú dài.

### Mobile UX / Performance

Editor dùng bottom control panel, touch target lớn và form số. Rebuild visual chỉ xảy ra khi state placement thay đổi, không trong mỗi frame render.

### Testing

Đã bổ sung test footprint occupied-cells và move-ignore-self bên cạnh bộ test grid/collision/rotation/JSON/PDF hiện có.

### Known Limitations

- Ghost hiện là preview cube trong runtime; outline riêng cho collision chưa có shader chuyên dụng.
- Color picker dùng bảng palette trực quan; màu chọn là cùng giá trị mà renderer 3D và PDF sử dụng.
- Bộ test UI PlayMode tự động và xác minh PDF trên thiết bị Android vẫn cần bổ sung.
