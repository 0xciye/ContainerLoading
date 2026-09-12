using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class UpdateInfo
{
    public string VersionName;
    public int VersionCode;
    public string ReleaseNotes;
    public string DownloadUrl;
    public string ReleasePageUrl;
    public bool IsUpdateAvailable;
}

public static class AppUpdateService
{
    [Serializable] sealed class GitHubAsset { public string name; public string browser_download_url; public string content_type; }
    [Serializable] sealed class GitHubRelease { public string tag_name; public string body; public string html_url; public GitHubAsset[] assets; }

    public static string NormalizeRepository(string value)
    {
        var repository = (value ?? "").Trim().TrimEnd('/');
        const string github = "github.com/";
        var githubIndex = repository.IndexOf(github, StringComparison.OrdinalIgnoreCase);
        if (githubIndex >= 0) repository = repository.Substring(githubIndex + github.Length);
        if (repository.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) repository = repository.Substring(0, repository.Length - 4);
        var parts = repository.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? parts[0] + "/" + parts[1] : "";
    }

    public static IEnumerator CheckLatest(string repository, string currentVersion, Action<UpdateInfo> completed, Action<string> failed)
    {
        repository = NormalizeRepository(repository);
        if (string.IsNullOrEmpty(repository)) { failed?.Invoke("Chưa cấu hình GitHub repository."); yield break; }
        using var request = UnityWebRequest.Get("https://api.github.com/repos/" + repository + "/releases/latest");
        request.timeout = 8;
        request.SetRequestHeader("Accept", "application/vnd.github+json");
        request.SetRequestHeader("User-Agent", "ContainerLoadingApp/" + currentVersion);
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success)
        {
            failed?.Invoke("Không thể kiểm tra cập nhật. Vui lòng kiểm tra kết nối Internet.");
            yield break;
        }
        if (TryParseLatestRelease(request.downloadHandler.text, currentVersion, out var info, out var error)) completed?.Invoke(info);
        else failed?.Invoke(error);
    }

    public static bool TryParseLatestRelease(string json, string currentVersion, out UpdateInfo info, out string error)
    {
        info = null;
        error = null;
        GitHubRelease release;
        try { release = JsonUtility.FromJson<GitHubRelease>(json); }
        catch { release = null; }
        if (release == null || !TryCompareVersions(currentVersion, release.tag_name, out var comparison))
        {
            error = "Thông tin phiên bản trên GitHub không hợp lệ.";
            return false;
        }
        var apk = release.assets?.FirstOrDefault(x => string.Equals(x.name, "ContainerLoading.apk", StringComparison.OrdinalIgnoreCase))
                  ?? release.assets?.FirstOrDefault(x => (x.name ?? "").EndsWith(".apk", StringComparison.OrdinalIgnoreCase));
        if (apk == null || string.IsNullOrWhiteSpace(apk.browser_download_url))
        {
            error = "Bản phát hành mới nhất chưa có tệp APK.";
            return false;
        }
        info = new UpdateInfo
        {
            VersionName = CleanVersion(release.tag_name),
            VersionCode = 0,
            ReleaseNotes = SanitizeNotes(release.body),
            DownloadUrl = apk.browser_download_url,
            ReleasePageUrl = release.html_url,
            IsUpdateAvailable = comparison < 0
        };
        return true;
    }

    public static bool TryCompareVersions(string current, string latest, out int comparison)
    {
        comparison = 0;
        if (!TryParseVersion(current, out var left) || !TryParseVersion(latest, out var right)) return false;
        var length = Math.Max(left.Length, right.Length);
        for (var i = 0; i < length; i++)
        {
            var a = i < left.Length ? left[i] : 0;
            var b = i < right.Length ? right[i] : 0;
            if (a == b) continue;
            comparison = a < b ? -1 : 1;
            return true;
        }
        return true;
    }

    static bool TryParseVersion(string value, out int[] parts)
    {
        parts = null;
        var clean = CleanVersion(value);
        var core = clean.Split(new[] { '-', '+' }, 2)[0];
        var tokens = core.Split('.');
        if (tokens.Length == 0 || tokens.Any(x => !int.TryParse(x, out _))) return false;
        parts = tokens.Select(int.Parse).ToArray();
        return true;
    }

    static string CleanVersion(string value)
    {
        var clean = (value ?? "").Trim();
        return clean.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? clean.Substring(1) : clean;
    }

    static string SanitizeNotes(string value)
    {
        var notes = (value ?? "").Replace("\r", "").Replace("#", "").Replace("**", "").Trim();
        return notes.Length > 600 ? notes.Substring(0, 600).TrimEnd() + "…" : notes;
    }
}
