# Android build và cài đặt

## Bản APK có sẵn

File: `Builds/Android/ContainerLoadingApp-v3.0.0.apk`

- Package: `com.ciye.containerloading`
- Kiến trúc: ARM64
- Android tối thiểu: Android 8.0 / API 26
- Phiên bản ứng dụng: 3.0.0, version code 4
- Chữ ký local QA: keystore release ổn định, RSA 4096
- Splash Unity: tắt
- Launcher icon: `Assets/Resources/AppIcon.png`

Chép APK sang điện thoại Android, mở file và cho phép trình quản lý file cài ứng dụng không rõ nguồn gốc khi hệ thống yêu cầu.

## Build lại

Android Build Support, SDK, NDK và OpenJDK đã được cài cho Unity `6000.5.10f1`.

Trong Unity Editor, chọn Android rồi Build, hoặc chạy:

```powershell
unity build "C:\\Users\\CiyE\\Desktop\\3d container\\ContainerLoadingApp" --target Android --execute-method AndroidBuilder.Build --output-path "C:\\Users\\CiyE\\Desktop\\3d container\\ContainerLoadingApp\\Builds\\Android\\ContainerLoadingApp-v3.0.0.apk" --allow-dirty-build --timeout 900
```

Script build nằm tại `Assets/Editor/AndroidBuilder.cs`. Scene phát hành là `Assets/Scenes/SampleScene.unity`.

## Kiểm tra trên thiết bị

Khi điện thoại đã bật USB debugging và xuất hiện trong `adb devices`:

```powershell
adb install -r "Builds\\Android\\ContainerLoadingApp-v3.0.0.apk"
adb shell am start -n com.ciye.containerloading/com.unity3d.player.UnityPlayerGameActivity
```

Kiểm tra lần lượt: nhập container, thêm loại thùng, đặt/xoay/di chuyển, undo/redo, lưu/mở phương án, xuất/xem/chia sẻ PDF và nút Back của Android.
