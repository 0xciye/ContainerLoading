using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ContainerConfig { public string id = "CUSTOM"; public string name = "Custom"; public int length = 10, width = 3, height = 6; }
[Serializable]
public class CargoType
{
    public string id, code, name;
    public int length = 1, width = 1, height = 1, quantity = 1;
    public float weightPerUnit;
    public Color color = Color.white;
    public bool allowRotation = true;
    public bool stackable = true;
}
[Serializable]
public class PlacedCargo { public string id, cargoTypeId; public Vector3Int position; public Vector3Int size; public int rotation; public bool locked; }
[Serializable]
public class LoadingPlan
{
    public const int CurrentSchemaVersion = 2;
    public string id = Guid.NewGuid().ToString("N");
    public string shipmentId = "";
    public int schemaVersion = CurrentSchemaVersion;
    public string name = "LoadingPlan";
    public string orderReference = "";
    public string customerName = "";
    public string containerNumber = "";
    public string sealNumber = "";
    public string destination = "";
    public string loadingDate = "";
    public string orderNotes = "";
    public string notes = "";
    public string containerType = "";
    public ContainerConfig container = new();
    public List<CargoType> cargoTypes = new();
    public List<PlacedCargo> placedCargo = new();
    public List<LoadingStep> loadingSequence = new();
    public string doorSide = "X0";
    public string createdAt = DateTime.UtcNow.ToString("O");
    public string updatedAt = DateTime.UtcNow.ToString("O");
}

public static class PlanValidation
{
    public const int MaxDimension = 500;
    public const int MaxTextLength = 200;
    public const int MaxNotesLength = 20000;
    public const int MaxCargoTypes = 500;
    public const int MaxPlacedCargo = 5000;

    public static bool TryValidate(LoadingPlan plan, out string error)
    {
        if (plan == null) return Fail("Dữ liệu phương án bị rỗng.", out error);
        if (plan.schemaVersion > LoadingPlan.CurrentSchemaVersion) return Fail($"Phiên bản dữ liệu {plan.schemaVersion} chưa được hỗ trợ.", out error);
        Normalize(plan);
        if (!ValidText(plan.name, true, MaxTextLength)) return Fail("Mã/tên phương án không hợp lệ hoặc quá dài.", out error);
        if (!ValidText(plan.orderReference, false, MaxTextLength)) return Fail("Mã đơn hàng quá dài.", out error);
        if (!ValidText(plan.customerName, false, MaxTextLength)) return Fail("Tên khách hàng quá dài.", out error);
        if (!ValidText(plan.containerNumber, false, MaxTextLength)) return Fail("Số container quá dài.", out error);
        if (!ValidText(plan.sealNumber, false, MaxTextLength)) return Fail("Số niêm phong quá dài.", out error);
        if (!ValidText(plan.destination, false, MaxTextLength)) return Fail("Điểm đến quá dài.", out error);
        if (!ValidText(plan.loadingDate, false, MaxTextLength)) return Fail("Ngày đóng hàng quá dài.", out error);
        if ((plan.orderNotes?.Length ?? 0) > MaxNotesLength) return Fail("Ghi chú đơn hàng vượt quá 20.000 ký tự.", out error);
        if (plan.container == null) return Fail("Thiếu thông tin container.", out error);
        if (!ValidDimension(plan.container.length) || !ValidDimension(plan.container.width) || !ValidDimension(plan.container.height)) return Fail($"Kích thước container phải từ 1 đến {MaxDimension} ô.", out error);
        if (plan.cargoTypes.Count > MaxCargoTypes) return Fail($"Tối đa {MaxCargoTypes} loại hàng.", out error);
        if (plan.placedCargo.Count > MaxPlacedCargo) return Fail($"Tối đa {MaxPlacedCargo} kiện hàng.", out error);

        var typeIds = new HashSet<string>(StringComparer.Ordinal);
        var typeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in plan.cargoTypes)
        {
            if (type == null || !ValidText(type.id, true, MaxTextLength)) return Fail("Loại hàng có ID không hợp lệ.", out error);
            if (!typeIds.Add(type.id)) return Fail($"ID loại hàng bị trùng: {type.id}.", out error);
            if (!ValidText(type.code, true, MaxTextLength) || !typeCodes.Add(type.code)) return Fail($"Mã loại hàng bị trống, quá dài hoặc trùng: {type.code}.", out error);
            if (!ValidText(type.name, true, MaxTextLength)) return Fail($"Tên loại hàng {type.code} không hợp lệ.", out error);
            if (!ValidDimension(type.length) || !ValidDimension(type.width) || !ValidDimension(type.height)) return Fail($"Kích thước loại hàng {type.code} phải từ 1 đến {MaxDimension} ô.", out error);
            if (type.quantity <= 0 || type.quantity > MaxPlacedCargo) return Fail($"Số lượng loại hàng {type.code} phải từ 1 đến {MaxPlacedCargo}.", out error);
            if (float.IsNaN(type.weightPerUnit) || float.IsInfinity(type.weightPerUnit) || type.weightPerUnit < 0f || type.weightPerUnit > 10000000f) return Fail($"Trọng lượng loại hàng {type.code} không hợp lệ.", out error);
            if (type.color.a <= 0f) return Fail($"Loại hàng {type.code} chưa có màu hợp lệ.", out error);
        }

        var placedIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < plan.placedCargo.Count; i++)
        {
            var box = plan.placedCargo[i];
            if (box == null || !ValidText(box.id, true, MaxTextLength) || !placedIds.Add(box.id)) return Fail("Kiện hàng có ID rỗng hoặc trùng.", out error);
            var type = plan.cargoTypes.Find(x => x.id == box.cargoTypeId);
            if (type == null) return Fail($"Kiện {box.id} tham chiếu loại hàng không tồn tại.", out error);
            if (box.rotation < 0 || box.rotation >= 360 || box.rotation % 90 != 0) return Fail($"Góc xoay của kiện {box.id} không hợp lệ.", out error);
            if (!type.allowRotation && box.rotation != 0) return Fail($"Kiện {box.id} vi phạm quy tắc không được xoay.", out error);
            var expectedSize = GridPlacement.RotatedSize(new Vector3Int(type.length, type.width, type.height), box.rotation);
            if (box.size != expectedSize) return Fail($"Kích thước kiện {box.id} không khớp loại hàng và góc xoay.", out error);
            if (!GridPlacement.WithinBounds(plan.container, box.position, box.size)) return Fail($"Kiện {box.id} nằm ngoài container.", out error);
            for (var j = 0; j < i; j++)
                if (GridPlacement.Overlaps(box.position, box.size, plan.placedCargo[j].position, plan.placedCargo[j].size)) return Fail($"Kiện {box.id} bị chồng lấn với kiện {plan.placedCargo[j].id}.", out error);
        }
        foreach (var type in plan.cargoTypes)
            if (plan.placedCargo.Count(x => x.cargoTypeId == type.id) > type.quantity) return Fail($"Loại hàng {type.code} đã xếp vượt số lượng khai báo.", out error);
        foreach (var lower in plan.placedCargo)
        {
            var type = plan.cargoTypes.Find(x => x.id == lower.cargoTypeId);
            if (type == null || type.stackable) continue;
            if (plan.placedCargo.Any(upper => upper.id != lower.id && upper.position.z >= lower.position.z + lower.size.z && GridPlacement.FootprintsOverlap(lower.position, lower.size, upper.position, upper.size)))
                return Fail($"Không được xếp hàng phía trên loại không chịu xếp chồng {type.code}.", out error);
        }
        error = null;
        return true;
    }

    public static void Normalize(LoadingPlan plan)
    {
        if (plan == null) return;
        var previousSchema = plan.schemaVersion;
        plan.name = Clean(plan.name);
        plan.orderReference = Clean(plan.orderReference);
        plan.customerName = Clean(plan.customerName);
        plan.containerNumber = Clean(plan.containerNumber);
        plan.sealNumber = Clean(plan.sealNumber);
        plan.destination = Clean(plan.destination);
        plan.loadingDate = Clean(plan.loadingDate);
        plan.orderNotes = (plan.orderNotes ?? "").Trim();
        plan.container ??= new ContainerConfig();
        plan.container.id = Clean(plan.container.id);
        plan.container.name = Clean(plan.container.name);
        plan.cargoTypes ??= new List<CargoType>();
        plan.placedCargo ??= new List<PlacedCargo>();
        plan.loadingSequence ??= new List<LoadingStep>();
        if (string.IsNullOrWhiteSpace(plan.doorSide)) plan.doorSide = "X0";
        foreach (var type in plan.cargoTypes.Where(x => x != null))
        {
            type.id = Clean(type.id); type.code = Clean(type.code); type.name = Clean(type.name);
            var placed = plan.placedCargo.Count(x => x != null && x.cargoTypeId == type.id);
            if (previousSchema < 2) { type.quantity = Math.Max(Math.Max(1, type.quantity), placed); type.stackable = true; }
            else if (type.quantity <= 0) type.quantity = Math.Max(1, placed);
        }
        foreach (var box in plan.placedCargo.Where(x => x != null)) { box.id = Clean(box.id); box.cargoTypeId = Clean(box.cargoTypeId); }
        plan.schemaVersion = LoadingPlan.CurrentSchemaVersion;
    }

    public static bool ValidDimension(int value) => value > 0 && value <= MaxDimension;
    public static bool CanResize(LoadingPlan plan, Vector3Int dimensions, out string error)
    {
        if (plan == null || !ValidDimension(dimensions.x) || !ValidDimension(dimensions.y) || !ValidDimension(dimensions.z)) return Fail("Kích thước container không hợp lệ.", out error);
        var resized = new ContainerConfig { length = dimensions.x, width = dimensions.y, height = dimensions.z };
        var invalid = plan.placedCargo?.FirstOrDefault(box => box != null && !GridPlacement.WithinBounds(resized, box.position, box.size));
        if (invalid != null) return Fail($"Kiện {invalid.id} sẽ nằm ngoài container sau khi thu nhỏ.", out error);
        error = null;
        return true;
    }
    static bool ValidText(string value, bool required, int max) => (!required || !string.IsNullOrWhiteSpace(value)) && (value?.Length ?? 0) <= max && !(value ?? "").Contains('\n') && !(value ?? "").Contains('\r');
    static string Clean(string value) => (value ?? "").Trim();
    static bool Fail(string message, out string error) { error = message; return false; }
}

public static class GridPlacement
{
    public static Vector3Int RotatedSize(Vector3Int size, int rotation) => rotation % 180 == 0 ? size : new Vector3Int(size.y, size.x, size.z);

    public static int LowestSupportedLayer(LoadingPlan plan, CargoType type, Vector3Int position, int rotation, string ignoreId = null)
    {
        var size = RotatedSize(new Vector3Int(type.length, type.width, type.height), rotation);
        var highestLayer = Math.Max(0, plan.container.height - size.z);
        for (var z = 0; z <= highestLayer; z++)
        {
            var candidate = new Vector3Int(position.x, position.y, z);
            if (!TryPlace(plan, type, candidate, rotation, ignoreId, out _)) continue;
            if (z == 0 || FootprintIsSupported(plan, candidate, size, ignoreId)) return z;
        }
        return Mathf.Clamp(position.z, 0, highestLayer);
    }

    static bool FootprintIsSupported(LoadingPlan plan, Vector3Int position, Vector3Int size, string ignoreId)
    {
        for (var x = position.x; x < position.x + size.x; x++)
            for (var y = position.y; y < position.y + size.y; y++)
            {
                var support = plan.placedCargo.FirstOrDefault(box => box.id != ignoreId && box.position.z + box.size.z == position.z && x >= box.position.x && x < box.position.x + box.size.x && y >= box.position.y && y < box.position.y + box.size.y);
                if (support == null) return false;
                var supportType = plan.cargoTypes.Find(type => type.id == support.cargoTypeId);
                if (supportType != null && !supportType.stackable) return false;
            }
        return true;
    }

    public static IEnumerable<Vector3Int> OccupiedCells(Vector3Int position, Vector3Int size)
    {
        for (var x = 0; x < size.x; x++)
            for (var y = 0; y < size.y; y++)
                for (var z = 0; z < size.z; z++)
                    yield return position + new Vector3Int(x, y, z);
    }

    public static bool TryPlace(LoadingPlan plan, CargoType type, Vector3Int position, int rotation, out string error)
        => TryPlace(plan, type, position, rotation, null, out error);

    public static bool TryPlace(LoadingPlan plan, CargoType type, Vector3Int position, int rotation, string ignoreId, out string error)
    {
        if (plan?.container == null || type == null) { error = "Thiếu dữ liệu container hoặc loại hàng."; return false; }
        if (rotation < 0 || rotation >= 360 || rotation % 90 != 0) { error = "Góc xoay phải là 0°, 90°, 180° hoặc 270°."; return false; }
        if (!type.allowRotation && rotation != 0) { error = "Loại hàng này phải giữ nguyên hướng."; return false; }
        if (!PlanValidation.ValidDimension(type.length) || !PlanValidation.ValidDimension(type.width) || !PlanValidation.ValidDimension(type.height)) { error = "Kích thước loại hàng không hợp lệ."; return false; }
        var size = RotatedSize(new Vector3Int(type.length, type.width, type.height), rotation);
        var c = plan.container;
        if (!WithinBounds(c, position, size)) { error = "Vị trí vượt biên container."; return false; }
        var placedCount = plan.placedCargo.Count(box => box.cargoTypeId == type.id && box.id != ignoreId);
        if (placedCount >= type.quantity) { error = $"Đã xếp đủ {type.quantity} kiện {type.code}."; return false; }
        foreach (var box in plan.placedCargo)
        {
            if (!string.IsNullOrEmpty(ignoreId) && box.id == ignoreId) continue;
            if (Overlaps(position, size, box.position, box.size))
            {
                var occupiedBy = plan.cargoTypes.Find(x => x.id == box.cargoTypeId)?.code ?? box.id;
                error = $"Không thể đặt: chồng lên hàng {occupiedBy}.";
                return false;
            }
        }
        if (position.z > 0 && !FootprintIsSupported(plan, position, size, ignoreId)) { error = "Hàng chưa được đỡ kín bởi tầng bên dưới."; return false; }
        error = null; return true;
    }

    public static bool Overlaps(Vector3Int a, Vector3Int asize, Vector3Int b, Vector3Int bsize) =>
        a.x < b.x + bsize.x && a.x + asize.x > b.x && a.y < b.y + bsize.y && a.y + asize.y > b.y && a.z < b.z + bsize.z && a.z + asize.z > b.z;

    public static bool FootprintsOverlap(Vector3Int a, Vector3Int asize, Vector3Int b, Vector3Int bsize) =>
        a.x < b.x + bsize.x && a.x + asize.x > b.x && a.y < b.y + bsize.y && a.y + asize.y > b.y;

    public static bool WithinBounds(ContainerConfig container, Vector3Int position, Vector3Int size) => container != null
        && position.x >= 0 && position.y >= 0 && position.z >= 0
        && size.x > 0 && size.y > 0 && size.z > 0
        && position.x + size.x <= container.length && position.y + size.y <= container.width && position.z + size.z <= container.height;
}
