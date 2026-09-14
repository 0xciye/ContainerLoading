# HOW TO RELEASE

Quy trình phát hành thủ công:

1. Chạy EditMode tests trên máy phát hành.
2. Build APK release đã ký bằng keystore hiện tại.
3. Kiểm tra `versionName`, `versionCode`, kiến trúc ARM64 và chữ ký APK.
4. Tạo GitHub Release thủ công rồi tải APK lên.
5. Ứng dụng kiểm tra repository `0xciye/ContainerLoading` và thông báo khi có phiên bản mới.

Không commit keystore, mật khẩu, GitHub token hoặc file APK. Không thay đổi signing key sau khi đã phát hành vì Android sẽ từ chối cập nhật APK có chữ ký khác.
