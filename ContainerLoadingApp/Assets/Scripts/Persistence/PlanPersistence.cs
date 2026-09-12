using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class AppBackupDocument
{
    public int schemaVersion = 1;
    public string createdAt;
    public string containerPresetJson;
    public string cargoPresetJson;
    public List<LoadingPlan> plans = new();
}

public static class PlanPersistence
{
    const long MaxImportBytes = 5 * 1024 * 1024;
    public static string RootPathOverride { get; set; }
    static string RootPath => string.IsNullOrEmpty(RootPathOverride) ? Application.persistentDataPath : RootPathOverride;
    public static string DirectoryPath => System.IO.Path.Combine(RootPath, "Plans");
    public static string ExportDirectoryPath => System.IO.Path.Combine(RootPath, "Exports");
    public static string Path => GetPath("loading-plan");

    public static string Save(LoadingPlan plan, string previousPath = null)
    {
        if (!PlanValidation.TryValidate(plan, out var error)) throw new InvalidDataException(error);
        Directory.CreateDirectory(DirectoryPath);
        if (string.IsNullOrWhiteSpace(plan.createdAt)) plan.createdAt = DateTime.UtcNow.ToString("O");
        plan.updatedAt = DateTime.UtcNow.ToString("O");
        var destination = GetPath(plan.name);
        AtomicWrite(destination, JsonUtility.ToJson(plan, true));
        if (!string.IsNullOrEmpty(previousPath) && !SamePath(previousPath, destination) && IsInside(previousPath, DirectoryPath) && File.Exists(previousPath)) File.Delete(previousPath);
        return destination;
    }

    public static LoadingPlan Load()
    {
        foreach (var path in ListFiles()) if (TryLoad(path, out var plan, out _)) return plan;
        return null;
    }

    public static LoadingPlan Load(string path) => TryLoad(path, out var plan, out _) ? plan : null;

    public static bool TryLoad(string path, out LoadingPlan plan, out string error)
    {
        plan = null;
        error = null;
        try
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { error = "Không tìm thấy tệp dữ liệu."; return false; }
            if (!IsInside(path, DirectoryPath)) { error = "Tệp dữ liệu nằm ngoài vùng lưu trữ của ứng dụng."; return false; }
            var info = new FileInfo(path);
            if (info.Length == 0) { error = "Tệp dữ liệu bị rỗng."; return false; }
            if (info.Length > MaxImportBytes) { error = "Tệp dữ liệu vượt quá giới hạn 5 MB."; return false; }
            plan = JsonUtility.FromJson<LoadingPlan>(File.ReadAllText(path, Encoding.UTF8));
            if (!PlanValidation.TryValidate(plan, out error)) { plan = null; return false; }
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            error = "Không thể đọc tệp dữ liệu: " + exception.Message;
            plan = null;
            return false;
        }
    }

    public static IReadOnlyList<LoadingPlan> ListValid(out string[] invalidPaths)
    {
        var valid = new List<LoadingPlan>();
        var invalid = new List<string>();
        foreach (var path in ListFiles())
        {
            if (TryLoad(path, out var plan, out _)) valid.Add(plan);
            else invalid.Add(path);
        }
        invalidPaths = invalid.ToArray();
        return valid;
    }

    public static string[] ListFiles()
    {
        RecoverBackups();
        return Directory.Exists(DirectoryPath) ? Directory.GetFiles(DirectoryPath, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).ToArray() : Array.Empty<string>();
    }

    public static bool Delete(string path)
    {
        if (!IsInside(path, DirectoryPath) || !File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public static string ExportJson(LoadingPlan plan)
    {
        if (!PlanValidation.TryValidate(plan, out var error)) throw new InvalidDataException(error);
        Directory.CreateDirectory(ExportDirectoryPath);
        var reference = string.IsNullOrWhiteSpace(plan.orderReference) ? plan.name : plan.orderReference;
        var path = System.IO.Path.Combine(ExportDirectoryPath, $"Container_{SafeName(reference)}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
        AtomicWrite(path, JsonUtility.ToJson(plan, true));
        return path;
    }

    public static string ExportBackup(string containerPresetJson = "", string cargoPresetJson = "")
    {
        var document = new AppBackupDocument { createdAt = DateTime.UtcNow.ToString("O"), containerPresetJson = containerPresetJson ?? "", cargoPresetJson = cargoPresetJson ?? "" };
        document.plans.AddRange(ListValid(out _));
        Directory.CreateDirectory(ExportDirectoryPath);
        var path = System.IO.Path.Combine(ExportDirectoryPath, $"ContainerLoading_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        AtomicWrite(path, JsonUtility.ToJson(document, true));
        return path;
    }

    public static bool TryReadBackup(string sourcePath, out AppBackupDocument document, out string error)
    {
        document = null; error = null;
        try
        {
            if (!TryReadText(sourcePath, out var json, out error)) return false;
            document = JsonUtility.FromJson<AppBackupDocument>(json);
            if (document == null || document.schemaVersion != 1 || document.plans == null) { error = "Tệp không phải bản sao lưu hợp lệ."; document = null; return false; }
            foreach (var plan in document.plans)
                if (!PlanValidation.TryValidate(plan, out error)) { document = null; return false; }
            return true;
        }
        catch (Exception exception) { error = "Không thể đọc bản sao lưu: " + exception.Message; document = null; return false; }
    }

    public static int RestoreBackup(AppBackupDocument document)
    {
        if (document?.plans == null) throw new InvalidDataException("Bản sao lưu bị rỗng.");
        foreach (var plan in document.plans)
            if (!PlanValidation.TryValidate(plan, out var error)) throw new InvalidDataException(error);
        var restored = 0;
        foreach (var source in document.plans)
        {
            var copy = JsonUtility.FromJson<LoadingPlan>(JsonUtility.ToJson(source));
            var original = copy.name; var suffix = 1;
            while (File.Exists(GetPath(copy.name))) copy.name = original + "_RESTORE_" + suffix++;
            Save(copy); restored++;
        }
        return restored;
    }

    public static string ExportCargoCsv(LoadingPlan plan)
    {
        if (!PlanValidation.TryValidate(plan, out var error)) throw new InvalidDataException(error);
        var csv = new StringBuilder("code,name,length,width,height,quantity,weightKg,color,allowRotation,stackable\r\n");
        foreach (var type in plan.cargoTypes)
            csv.AppendLine(string.Join(",", Csv(type.code), Csv(type.name), type.length, type.width, type.height, type.quantity, type.weightPerUnit.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), "#" + ColorUtility.ToHtmlStringRGB(type.color), type.allowRotation, type.stackable));
        Directory.CreateDirectory(ExportDirectoryPath);
        var path = System.IO.Path.Combine(ExportDirectoryPath, $"Cargo_{SafeName(string.IsNullOrWhiteSpace(plan.orderReference) ? plan.name : plan.orderReference)}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        AtomicWrite(path, csv.ToString()); return path;
    }

    public static bool TryReadCargoCsv(string sourcePath, out List<CargoType> cargo, out string error)
    {
        cargo = new List<CargoType>(); error = null;
        if (!TryReadText(sourcePath, out var text, out error)) return false;
        var lines = text.Replace("\r", "").Split('\n');
        if (lines.Length < 2) { error = "CSV không có dữ liệu."; return false; }
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var row = 1; row < lines.Length; row++)
        {
            if (string.IsNullOrWhiteSpace(lines[row])) continue;
            var f = ParseCsvLine(lines[row]);
            if (f.Count != 10 || !int.TryParse(f[2], out var l) || !int.TryParse(f[3], out var w) || !int.TryParse(f[4], out var h) || !int.TryParse(f[5], out var quantity) || !float.TryParse(f[6], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var weight) || !ColorUtility.TryParseHtmlString(f[7], out var color) || !bool.TryParse(f[8], out var rotate) || !bool.TryParse(f[9], out var stackable)) { error = $"Dòng {row + 1}: sai định dạng."; return false; }
            var type = new CargoType { id = Guid.NewGuid().ToString("N"), code = f[0].Trim(), name = f[1].Trim(), length = l, width = w, height = h, quantity = quantity, weightPerUnit = weight, color = color, allowRotation = rotate, stackable = stackable };
            var probe = new LoadingPlan { name = "CSV", container = new ContainerConfig { length = PlanValidation.MaxDimension, width = PlanValidation.MaxDimension, height = PlanValidation.MaxDimension }, cargoTypes = new List<CargoType> { type } };
            if (!codes.Add(type.code)) { error = $"Dòng {row + 1}: mã hàng bị trùng."; return false; }
            if (!PlanValidation.TryValidate(probe, out var rowError)) { error = $"Dòng {row + 1}: " + rowError; return false; }
            cargo.Add(type);
        }
        if (cargo.Count == 0) { error = "CSV không có loại hàng hợp lệ."; return false; }
        return true;
    }

    public static bool TryImport(string sourcePath, out LoadingPlan imported, out string savedPath, out string error)
    {
        imported = null;
        savedPath = null;
        error = null;
        try
        {
            if (!TryReadExternal(sourcePath, out imported, out error)) return false;
            var originalName = imported.name;
            var suffix = 1;
            while (File.Exists(GetPath(imported.name))) imported.name = $"{originalName}_IMPORT_{suffix++}";
            imported.createdAt = DateTime.UtcNow.ToString("O");
            imported.updatedAt = imported.createdAt;
            savedPath = Save(imported);
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            imported = null;
            error = "Không thể nhập tệp: " + exception.Message;
            return false;
        }
    }

    public static bool TryReadExternal(string sourcePath, out LoadingPlan plan, out string error)
    {
        plan = null;
        error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) { error = "Không tìm thấy tệp đã chọn."; return false; }
            var info = new FileInfo(sourcePath);
            if (info.Length == 0) { error = "Tệp nhập bị rỗng."; return false; }
            if (info.Length > MaxImportBytes) { error = "Tệp nhập vượt quá giới hạn 5 MB."; return false; }
            plan = JsonUtility.FromJson<LoadingPlan>(File.ReadAllText(sourcePath, Encoding.UTF8));
            if (!PlanValidation.TryValidate(plan, out error)) { plan = null; return false; }
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            plan = null;
            error = "Không thể đọc tệp: " + exception.Message;
            return false;
        }
    }

    static bool TryReadText(string sourcePath, out string text, out string error)
    {
        text = null; error = null;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) { error = "Không tìm thấy tệp đã chọn."; return false; }
        var info = new FileInfo(sourcePath);
        if (info.Length == 0 || info.Length > MaxImportBytes) { error = info.Length == 0 ? "Tệp nhập bị rỗng." : "Tệp nhập vượt quá giới hạn 5 MB."; return false; }
        text = File.ReadAllText(sourcePath, Encoding.UTF8); return true;
    }

    static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
    static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>(); var value = new StringBuilder(); var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted) { fields.Add(value.ToString()); value.Clear(); }
            else value.Append(c);
        }
        fields.Add(value.ToString()); return fields;
    }

    public static bool MatchesSearch(LoadingPlan plan, string query)
    {
        if (plan == null) return false;
        query = (query ?? "").Trim();
        return query.Length == 0 || Contains(plan.name, query) || Contains(plan.container?.name, query) || Contains(plan.orderReference, query) || Contains(plan.customerName, query);
    }

    static bool Contains(string value, string query) => (value ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

    static void AtomicWrite(string destination, string content)
    {
        var temp = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        var backup = destination + ".bak";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(destination))
            {
                try { File.Replace(temp, destination, backup, true); }
                catch (PlatformNotSupportedException) { SafeReplaceFallback(temp, destination, backup); }
                if (File.Exists(backup)) File.Delete(backup);
            }
            else File.Move(temp, destination);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    static void SafeReplaceFallback(string temp, string destination, string backup)
    {
        if (File.Exists(backup)) File.Delete(backup);
        File.Move(destination, backup);
        try { File.Move(temp, destination); }
        catch { if (!File.Exists(destination) && File.Exists(backup)) File.Move(backup, destination); throw; }
    }

    static void RecoverBackups()
    {
        if (!Directory.Exists(DirectoryPath)) return;
        foreach (var backup in Directory.GetFiles(DirectoryPath, "*.json.bak"))
        {
            var destination = backup.Substring(0, backup.Length - 4);
            if (!File.Exists(destination)) File.Move(backup, destination);
        }
    }

    static string GetPath(string name) => System.IO.Path.Combine(DirectoryPath, SafeName(name) + ".json");

    public static string SafeName(string value)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var safe = new string((string.IsNullOrWhiteSpace(value) ? "loading-plan" : value.Trim()).Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim('.', ' ');
        if (safe.Length > 100) safe = safe.Substring(0, 100);
        return string.IsNullOrWhiteSpace(safe) ? "loading-plan" : safe;
    }

    static bool IsInside(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var full = System.IO.Path.GetFullPath(path);
        var fullRoot = System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        return full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
    }

    static bool SamePath(string a, string b) => string.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
