# HOW TO RELEASE

Quy trình production:

1. Commit code và push nhánh `main`.
2. GitHub Actions chạy EditMode tests.
3. Workflow build APK đã ký, tự tăng `versionCode` và đặt `versionName` dạng `3.0.<run_number>`.
4. GitHub Release được tạo với asset cố định `ContainerLoading.apk`.
5. Ứng dụng kiểm tra repository `0xciye/ContainerLoading` sau khi Home đã sẵn sàng và chỉ hiện thông báo khi có bản mới.

## Secrets bắt buộc

- `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`: license GameCI theo hướng dẫn chính thức.
- `ANDROID_KEYSTORE_BASE64`: nội dung keystore release mã hóa base64.
- `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS`: thông tin signing.

Không commit keystore, password, GitHub PAT hoặc file APK. GitHub token mặc định của workflow chỉ dùng để tạo Release và không được đưa vào ứng dụng.

## Kiểm tra workflow

Có thể chạy thủ công từ tab Actions bằng `workflow_dispatch`. Nếu job dừng ở bước license, cấu hình lại ba secret Unity; không thay đổi signing key sau khi đã phát hành vì Android sẽ từ chối cập nhật APK có chữ ký khác.
