using System;
using UnityEngine;

public static class DataTransferPlatform
{
    public static bool PickJson(string receiver, string callback, out string error) => Pick(receiver, callback, "application/json", ".json", out error);
    public static bool PickCsv(string receiver, string callback, out string error) => Pick(receiver, callback, "text/csv", ".csv", out error);

    static bool Pick(string receiver, string callback, string mime, string extension, out string error)
    {
        error = null;
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var picker = new AndroidJavaClass("com.ciye.containerloading.JsonPickerActivity");
            picker.CallStatic("open", activity, receiver, callback, mime, extension);
            return true;
#elif UNITY_EDITOR
            var path = UnityEditor.EditorUtility.OpenFilePanel("Nhập dữ liệu container", "", extension.TrimStart('.'));
            var target = UnityEngine.Object.FindAnyObjectByType<ContainerLoadingApp>();
            if (target != null) target.OnJsonImportPicked(path ?? "");
            return true;
#else
            error = "Thiết bị này chưa hỗ trợ trình chọn tệp.";
            return false;
#endif
        }
        catch (Exception exception)
        {
            Debug.LogError("Không thể mở trình chọn tệp: " + exception.Message);
            error = "Không thể mở trình chọn tệp trên thiết bị này.";
            return false;
        }
    }
}
