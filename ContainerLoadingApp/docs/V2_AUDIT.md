# Container Loading V2 - Audit và QA

## Kết quả ưu tiên

- P0: chặn out-of-bounds, overlap, floating placement, vượt quantity, rotation bị cấm và xếp lên cargo không stackable.
- P0: JSON validate-before-write, atomic replace, backup recovery, import preview và file hỏng không chặn startup.
- P0: PDF A4 Unicode nhiều trang, tên tệp theo container/phương án, summary số lượng/trọng lượng/dung tích, ba hình chiếu, legend và placement appendix.
- P1: UI mobile mới, touch target lớn, safe area, hierarchy rõ, empty/error/success states và xác nhận thao tác nguy hiểm.
- P1: hai chế độ placement, preview 3D + viền ô trắng, nút điều hướng không nhập tọa độ, giữ scroll theo anchor khi dựng lại UI.
- P1: repeat placement, đề xuất vị trí và auto-fill deterministic bottom-first; không mô tả heuristic là tối ưu.
- P1: quantity/remaining, weight, rotation rule, stackability, health check, duplicate plan và debounced autosave.
- P1: GitHub Releases update check không token; lỗi mạng/timeout/response lỗi không chặn app.

## Android QA

- Cài đè APK `2.0.0` / version code `3` vào LDPlayer bằng đúng package `com.ciye.containerloading`.
- Xác nhận dữ liệu schema cũ còn nguyên sau nâng cấp: phương án `TEST_FULL_CONTAINER` có 144/144 kiện và 100% dung tích.
- Chạy Home, Plan Detail, Health Check, Editor, touch/control mode, preview, move-by-button, rotate và PDF workflow.
- Xác nhận bấm điều hướng giữ nguyên vùng control đang xem; không quay về đầu panel.
- Render và kiểm tra trực quan đủ 5 trang PDF test ở A4; Unicode, page number, margins, views và placement rows không overlap.
- Kiểm tra giao diện ở 720x1280, 1080x1920 và 1080x2400.
- EditMode release suite: 35/35 test đạt.

## Before / After

V1 yêu cầu chọn lại/scroll lại nhiều lần, không quản lý completeness, chưa có pre-flight check và report thiên về demo. V2 giữ user trong context, tự chuẩn bị vị trí tiếp theo, cho phép auto-fill có kiểm soát, giải thích invalid placement và đưa quantity/remaining/health/report vào cùng workflow.

## Chủ động không thêm

- Không thêm cloud, login, ERP, tracking, approval workflow hoặc thuật toán tự nhận là tối ưu.
- Chưa thêm Excel/XLSX, pinning, ảnh PNG và saved preset riêng vì không thuộc P0 và sẽ tăng dependency/state đáng kể. JSON backup/import và duplicate plan đáp ứng workflow tái sử dụng hiện tại.
- Repository GitHub update được cấu hình tại Settings vì source hiện tại không có remote/repository phát hành đáng tin để hard-code.
