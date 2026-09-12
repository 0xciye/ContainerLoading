using System;
using UnityEngine;

public static class PdfPlatform
{
    public static bool Open(string path) => Launch(path, false, "application/pdf");
    public static bool Share(string path) => Launch(path, true, "application/pdf");
    public static bool ShareJson(string path) => Launch(path, true, "application/json");
    public static bool ShareCsv(string path) => Launch(path, true, "text/csv");

    static bool Launch(string path, bool share, string mimeType)
    {
        if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path)) return false;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var uriClass = new AndroidJavaClass("android.net.Uri");
            var encoded = uriClass.CallStatic<string>("encode", path);
            var authority = activity.Call<string>("getPackageName") + ".pdfprovider";
            var encodedMime = uriClass.CallStatic<string>("encode", mimeType);
            using var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "content://" + authority + "/file?path=" + encoded + "&mime=" + encodedMime);
            using var intent = new AndroidJavaObject("android.content.Intent", share ? "android.intent.action.SEND" : "android.intent.action.VIEW");
            if (share) { intent.Call<AndroidJavaObject>("setType", mimeType); intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.STREAM", uri); }
            else intent.Call<AndroidJavaObject>("setDataAndType", uri, mimeType);
            intent.Call<AndroidJavaObject>("addFlags", 1);
            if (share) { using var intentClass = new AndroidJavaClass("android.content.Intent"); using var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, "Chia sẻ tệp"); activity.Call("startActivity", chooser); }
            else activity.Call("startActivity", intent);
            return true;
        }
        catch (Exception exception) { Debug.LogError("Không thể mở/chia sẻ tệp: " + exception.Message); return false; }
#else
        try { Application.OpenURL(new Uri(path).AbsoluteUri); return true; }
        catch (Exception exception) { Debug.LogError("Không thể mở/chia sẻ tệp: " + exception.Message); return false; }
#endif
    }
}
