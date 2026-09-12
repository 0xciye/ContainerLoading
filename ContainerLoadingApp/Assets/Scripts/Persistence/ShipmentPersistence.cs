using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class ShipmentBackupDocument
{
    public int schemaVersion = Shipment.CurrentSchemaVersion;
    public string createdAt;
    public List<Shipment> shipments = new();
}

public static class ShipmentPersistence
{
    const long MaxImportBytes = 20 * 1024 * 1024;
    static string RootPath => string.IsNullOrEmpty(PlanPersistence.RootPathOverride) ? Application.persistentDataPath : PlanPersistence.RootPathOverride;
    public static string DirectoryPath => Path.Combine(RootPath, "Shipments");
    public static string ExportDirectoryPath => Path.Combine(RootPath, "Exports");

    public static string Save(Shipment shipment, string previousPath = null)
    {
        if (!TryValidate(shipment, out var error)) throw new InvalidDataException(error);
        ShipmentIntelligence.Normalize(shipment); Directory.CreateDirectory(DirectoryPath);
        if (string.IsNullOrWhiteSpace(shipment.createdAt)) shipment.createdAt = DateTime.UtcNow.ToString("O");
        shipment.updatedAt = DateTime.UtcNow.ToString("O");
        var destination = GetPath(shipment.id, shipment.shipmentName);
        AtomicWrite(destination, JsonUtility.ToJson(shipment, true));
        if (!string.IsNullOrWhiteSpace(previousPath) && !SamePath(previousPath, destination) && IsInside(previousPath, DirectoryPath) && File.Exists(previousPath)) File.Delete(previousPath);
        return destination;
    }

    public static Shipment Load()
    {
        var migrated = MigrateV2IfNeeded();
        foreach (var path in ListFiles()) if (TryLoad(path, out var shipment, out _)) return shipment;
        return migrated?.FirstOrDefault();
    }

    public static Shipment Load(string path) => TryLoad(path, out var shipment, out _) ? shipment : null;

    public static bool TryLoad(string path, out Shipment shipment, out string error)
    {
        shipment = null; error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { error = "Không tìm thấy tệp chuyến hàng."; return false; }
            if (!IsInside(path, DirectoryPath)) { error = "Tệp chuyến hàng nằm ngoài vùng lưu trữ."; return false; }
            if (new FileInfo(path).Length > MaxImportBytes) { error = "Tệp chuyến hàng vượt quá giới hạn 20 MB."; return false; }
            shipment = JsonUtility.FromJson<Shipment>(File.ReadAllText(path, Encoding.UTF8));
            if (!TryValidate(shipment, out error)) { shipment = null; return false; }
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        { error = "Không thể đọc chuyến hàng: " + exception.Message; shipment = null; return false; }
    }

    public static string[] ListFiles() => Directory.Exists(DirectoryPath) ? Directory.GetFiles(DirectoryPath, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).ToArray() : Array.Empty<string>();
    public static IReadOnlyList<Shipment> ListValid(out string[] invalidPaths)
    {
        MigrateV2IfNeeded(); var valid = new List<Shipment>(); var invalid = new List<string>();
        foreach (var path in ListFiles()) if (TryLoad(path, out var shipment, out _)) valid.Add(shipment); else invalid.Add(path);
        invalidPaths = invalid.ToArray(); return valid;
    }

    public static bool Delete(string path) => IsInside(path, DirectoryPath) && File.Exists(path) && DeleteFile(path);
    static bool DeleteFile(string path) { File.Delete(path); return true; }

    public static string ExportJson(Shipment shipment)
    {
        if (!TryValidate(shipment, out var error)) throw new InvalidDataException(error);
        Directory.CreateDirectory(ExportDirectoryPath); var reference = string.IsNullOrWhiteSpace(shipment.orderReference) ? shipment.shipmentName : shipment.orderReference;
        var path = Path.Combine(ExportDirectoryPath, $"Shipment_{PlanPersistence.SafeName(reference)}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.json");
        AtomicWrite(path, JsonUtility.ToJson(shipment, true)); return path;
    }

    public static string ExportBackup()
    {
        var document = new ShipmentBackupDocument { createdAt = DateTime.UtcNow.ToString("O") }; document.shipments.AddRange(ListValid(out _));
        Directory.CreateDirectory(ExportDirectoryPath); var path = Path.Combine(ExportDirectoryPath, $"ShipmentLoading_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        AtomicWrite(path, JsonUtility.ToJson(document, true)); return path;
    }

    public static bool TryReadBackup(string path, out ShipmentBackupDocument document, out string error)
    {
        document = null; error = null;
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxImportBytes) { error = "Tệp bản sao lưu không hợp lệ."; return false; }
            document = JsonUtility.FromJson<ShipmentBackupDocument>(File.ReadAllText(path, Encoding.UTF8));
            if (document == null || document.schemaVersion != Shipment.CurrentSchemaVersion || document.shipments == null) { error = "Bản sao lưu không đúng schema chuyến hàng."; document = null; return false; }
            foreach (var shipment in document.shipments) if (!TryValidate(shipment, out error)) { document = null; return false; }
            return true;
        }
        catch (Exception exception) { error = "Không thể đọc bản sao lưu: " + exception.Message; document = null; return false; }
    }

    public static int RestoreBackup(ShipmentBackupDocument document)
    {
        if (document?.shipments == null) throw new InvalidDataException("Bản sao lưu chuyến hàng bị rỗng.");
        var restored = 0; foreach (var source in document.shipments)
        {
            var copy = JsonUtility.FromJson<Shipment>(JsonUtility.ToJson(source)); copy.id = Guid.NewGuid().ToString("N"); copy.shipmentName += "_RESTORE_" + (restored + 1); Save(copy); restored++;
        }
        return restored;
    }

    public static bool TryImport(string sourcePath, out Shipment imported, out string savedPath, out string error)
    {
        imported = null; savedPath = null; error = null;
        try
        {
            if (!File.Exists(sourcePath) || new FileInfo(sourcePath).Length > MaxImportBytes) { error = "Tệp nhập chuyến hàng không hợp lệ."; return false; }
            imported = JsonUtility.FromJson<Shipment>(File.ReadAllText(sourcePath, Encoding.UTF8)); if (!TryValidate(imported, out error)) { imported = null; return false; }
            imported.id = Guid.NewGuid().ToString("N"); imported.shipmentName += "_IMPORT"; savedPath = Save(imported); return true;
        }
        catch (Exception exception) { error = "Không thể nhập chuyến hàng: " + exception.Message; imported = null; return false; }
    }

    public static bool TryValidate(Shipment shipment, out string error)
    {
        error = null; if (shipment == null) { error = "Dữ liệu chuyến hàng bị rỗng."; return false; }
        if (shipment.schemaVersion > Shipment.CurrentSchemaVersion) { error = $"Phiên bản dữ liệu {shipment.schemaVersion} chưa được hỗ trợ."; return false; }
        ShipmentIntelligence.Normalize(shipment); var health = ShipmentIntelligence.Check(shipment);
        if (health.errors.Count > 0) { error = health.errors[0]; return false; }
        return true;
    }

    static List<Shipment> MigrateV2IfNeeded()
    {
        if (ListFiles().Length > 0) return new List<Shipment>();
        var migrated = new List<Shipment>();
        foreach (var path in PlanPersistence.ListFiles())
        {
            if (!PlanPersistence.TryLoad(path, out var plan, out _)) continue;
            var shipment = ShipmentIntelligence.FromV2Plan(plan); Save(shipment); migrated.Add(shipment);
        }
        return migrated;
    }

    static string GetPath(string id, string name) => Path.Combine(DirectoryPath, PlanPersistence.SafeName(string.IsNullOrWhiteSpace(name) ? id : name) + "_" + PlanPersistence.SafeName(id.Substring(0, Math.Min(8, id.Length))) + ".json");
    static void AtomicWrite(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); var temp = path + ".tmp"; File.WriteAllText(temp, content, Encoding.UTF8); if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
    }
    static bool IsInside(string path, string root) { var full = Path.GetFullPath(path); var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; return full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase); }
    static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
