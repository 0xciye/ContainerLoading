using System.Collections;
using UnityEngine;

public sealed partial class ContainerLoadingApp
{
    public const string UpdateRepository = "0xciye/ContainerLoading";
    string updateStatus = "Tự động kiểm tra khi mở ứng dụng.";
    bool updateChecking;
    UpdateInfo latestUpdate;

    void SetupUpdates()
    {
        StartCoroutine(CheckForUpdatesAfterStartup());
    }

    IEnumerator CheckForUpdatesAfterStartup(){yield return null;yield return new WaitForSecondsRealtime(1.2f);yield return CheckForUpdates(true);}

    IEnumerator CheckForUpdates(bool automatic)
    {
        if (updateChecking) yield break;
        updateChecking = true;
        if (!automatic) { updateStatus = "Đang kiểm tra cập nhật…"; RefreshMobileUI(true); }
        yield return AppUpdateService.CheckLatest(UpdateRepository, Application.version,
            info =>
            {
                latestUpdate = info;
                updateStatus = info.IsUpdateAvailable ? "Có phiên bản mới " + info.VersionName + "." : "Bạn đang sử dụng phiên bản mới nhất.";
            },
            error => updateStatus = automatic ? "" : error);
        updateChecking = false;
        if (mobileCanvas) RefreshMobileUI(true);
        if (automatic && latestUpdate != null && latestUpdate.IsUpdateAvailable) ShowUpdateDialog();
    }

    void ShowUpdateDialog()
    {
        if (latestUpdate == null || !latestUpdate.IsUpdateAvailable) return;
        var notes = string.IsNullOrWhiteSpace(latestUpdate.ReleaseNotes) ? "Không có ghi chú phát hành." : latestUpdate.ReleaseNotes;
        Confirm("Có phiên bản mới", "Hiện tại: " + Application.version + "\nPhiên bản mới: " + latestUpdate.VersionName + "\n\n" + notes,
            OpenLatestUpdate, "Cập nhật", "Để sau");
    }

    void OpenLatestUpdate()
    {
        var url = latestUpdate?.DownloadUrl;
        if (string.IsNullOrWhiteSpace(url)) url = latestUpdate?.ReleasePageUrl;
        if (!string.IsNullOrWhiteSpace(url)) Application.OpenURL(url);
    }
}
