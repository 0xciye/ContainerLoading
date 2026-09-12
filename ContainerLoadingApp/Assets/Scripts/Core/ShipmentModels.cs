using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class Shipment
{
    public const int CurrentSchemaVersion = 3;
    public int schemaVersion = CurrentSchemaVersion;
    public string id = Guid.NewGuid().ToString("N");
    public string shipmentName = "Chuyến hàng mới";
    public string orderReference = "";
    public string bookingReference = "";
    public string customerName = "";
    public string destination = "";
    public string loadingDate = "";
    public string notes = "";
    public string createdAt = DateTime.UtcNow.ToString("O");
    public string updatedAt = DateTime.UtcNow.ToString("O");
    public List<CargoType> cargoTypes = new();
    public List<LoadingPlan> containers = new();
}

public sealed class ShipmentCargoStatistics
{
    public CargoType Type;
    public int Required;
    public int Placed;
    public int Remaining => Math.Max(0, Required - Placed);
    public Dictionary<string, int> Distribution = new();
}

public sealed class ShipmentStatistics
{
    public int ContainerCount, CargoTypeCount, TotalUnits, PlacedUnits, RemainingUnits, UsedCells, CapacityCells;
    public float CompletionPercent, UtilizationPercent, TotalWeight;
    public List<ShipmentCargoStatistics> Cargo = new();
}

public sealed class ShipmentHealthReport
{
    public readonly List<string> errors = new();
    public readonly List<string> warnings = new();
    public bool IsReady => errors.Count == 0 && warnings.Count == 0;
    public string StatusLabel => IsReady ? "Sẵn sàng" : errors.Count > 0 ? "Cần xử lý" : "Chưa hoàn tất";
}

public static class ShipmentIntelligence
{
    public static void Normalize(Shipment shipment)
    {
        if (shipment == null) return;
        shipment.schemaVersion = Shipment.CurrentSchemaVersion;
        shipment.id = string.IsNullOrWhiteSpace(shipment.id) ? Guid.NewGuid().ToString("N") : shipment.id.Trim();
        shipment.shipmentName = string.IsNullOrWhiteSpace(shipment.shipmentName) ? "Chuyến hàng mới" : shipment.shipmentName.Trim();
        shipment.cargoTypes ??= new List<CargoType>();
        shipment.containers ??= new List<LoadingPlan>();
        foreach (var container in shipment.containers.Where(x => x != null))
        {
            container.id = string.IsNullOrWhiteSpace(container.id) ? Guid.NewGuid().ToString("N") : container.id.Trim();
            container.shipmentId = shipment.id;
            PlanValidation.Normalize(container);
            container.cargoTypes = shipment.cargoTypes;
            if (string.IsNullOrWhiteSpace(container.containerType)) container.containerType = container.container?.name ?? "Custom";
        }
    }

    public static Shipment FromV2Plan(LoadingPlan plan)
    {
        if (plan == null) throw new ArgumentNullException(nameof(plan));
        var shipment = new Shipment
        {
            id = Guid.NewGuid().ToString("N"), shipmentName = string.IsNullOrWhiteSpace(plan.name) ? "Chuyến hàng" : plan.name,
            orderReference = plan.orderReference, customerName = plan.customerName, destination = plan.destination,
            loadingDate = plan.loadingDate, notes = plan.orderNotes, cargoTypes = plan.cargoTypes ?? new List<CargoType>()
        };
        var container = JsonUtility.FromJson<LoadingPlan>(JsonUtility.ToJson(plan));
        container.id = Guid.NewGuid().ToString("N"); container.shipmentId = shipment.id; container.cargoTypes = shipment.cargoTypes;
        shipment.containers.Add(container); Normalize(shipment); return shipment;
    }

    public static ShipmentStatistics Statistics(Shipment shipment)
    {
        var result = new ShipmentStatistics(); if (shipment == null) return result;
        Normalize(shipment); result.ContainerCount = shipment.containers.Count; result.CargoTypeCount = shipment.cargoTypes.Count;
        result.TotalUnits = shipment.cargoTypes.Sum(x => Math.Max(0, x.quantity));
        result.CapacityCells = shipment.containers.Sum(x => Math.Max(0, (x.container?.length ?? 0) * (x.container?.width ?? 0) * (x.container?.height ?? 0)));
        result.UsedCells = shipment.containers.Sum(x => x.placedCargo?.Sum(b => b.size.x * b.size.y * b.size.z) ?? 0);
        result.TotalWeight = shipment.containers.Sum(PlanIntelligence.TotalWeight);
        foreach (var type in shipment.cargoTypes)
        {
            var item = new ShipmentCargoStatistics { Type = type, Required = Math.Max(0, type.quantity) };
            foreach (var container in shipment.containers)
            {
                var count = container.placedCargo?.Count(x => x.cargoTypeId == type.id) ?? 0;
                item.Placed += count; item.Distribution[container.id] = count;
            }
            result.Cargo.Add(item);
        }
        result.PlacedUnits = result.Cargo.Sum(x => x.Placed); result.RemainingUnits = Math.Max(0, result.TotalUnits - result.PlacedUnits);
        result.CompletionPercent = result.TotalUnits == 0 ? 0 : result.PlacedUnits * 100f / result.TotalUnits;
        result.UtilizationPercent = result.CapacityCells == 0 ? 0 : result.UsedCells * 100f / result.CapacityCells;
        return result;
    }

    public static int Placed(Shipment shipment, string cargoTypeId, string ignoreContainerId = null, string ignorePlacementId = null)
        => shipment?.containers?.Where(x => x != null).Sum(x => x.placedCargo?.Count(b => b.cargoTypeId == cargoTypeId && b.id != ignorePlacementId) ?? 0) ?? 0;

    public static int Remaining(Shipment shipment, CargoType type, string ignoreContainerId = null, string ignorePlacementId = null)
        => Math.Max(0, (type?.quantity ?? 0) - Placed(shipment, type?.id, ignoreContainerId, ignorePlacementId));

    public static bool TryPlace(Shipment shipment, LoadingPlan container, CargoType type, Vector3Int position, int rotation, string ignorePlacementId, out string error)
    {
        if (shipment == null || container == null || type == null) { error = "Thiếu dữ liệu chuyến hàng, container hoặc loại hàng."; return false; }
        if (Placed(shipment, type.id, null, ignorePlacementId) >= type.quantity) { error = $"Chuyến hàng đã đủ {type.quantity} kiện {type.code}."; return false; }
        return GridPlacement.TryPlace(container, type, position, rotation, ignorePlacementId, out error);
    }

    public static ShipmentHealthReport Check(Shipment shipment)
    {
        var report = new ShipmentHealthReport(); if (shipment == null) { report.errors.Add("Dữ liệu chuyến hàng bị rỗng."); return report; }
        Normalize(shipment); var stats = Statistics(shipment);
        if (string.IsNullOrWhiteSpace(shipment.orderReference)) report.warnings.Add("Chưa nhập mã đơn hàng.");
        if (shipment.containers.Count == 0) report.errors.Add("Chuyến hàng phải có ít nhất một container.");
        var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var container in shipment.containers)
        {
            if (!PlanValidation.TryValidate(container, out var error)) report.errors.Add($"{container.containerNumberOrName()}: {error}");
            if (!string.IsNullOrWhiteSpace(container.containerNumber) && !numbers.Add(container.containerNumber)) report.errors.Add($"Trùng số container: {container.containerNumber}.");
            if (string.IsNullOrWhiteSpace(container.containerNumber)) report.warnings.Add($"Container {container.container?.name ?? "Custom"} chưa có số container.");
        }
        foreach (var item in stats.Cargo)
            if (item.Placed > item.Required) report.errors.Add($"Loại hàng {item.Type.code} đã vượt số lượng chuyến hàng.");
            else if (item.Remaining > 0) report.warnings.Add($"Còn {item.Remaining} kiện {item.Type.code} chưa được xếp.");
        return report;
    }

    public static LoadingPlan DuplicateContainer(Shipment shipment, LoadingPlan source, bool includePlacements, out bool placementsCopied)
    {
        if (shipment == null || source == null) throw new ArgumentNullException();
        Normalize(shipment); var copy = JsonUtility.FromJson<LoadingPlan>(JsonUtility.ToJson(source));
        copy.id = Guid.NewGuid().ToString("N"); copy.shipmentId = shipment.id; copy.name = "Container " + (shipment.containers.Count + 1).ToString("00"); copy.containerNumber = ""; copy.sealNumber = ""; copy.cargoTypes = shipment.cargoTypes;
        placementsCopied = includePlacements && shipment.cargoTypes.All(type => copy.placedCargo.Count(x => x.cargoTypeId == type.id) <= Remaining(shipment, type));
        if (!placementsCopied) copy.placedCargo.Clear();
        shipment.containers.Add(copy); return copy;
    }

    public static bool DeleteContainer(Shipment shipment, LoadingPlan target)
    {
        if (shipment?.containers == null || target == null || shipment.containers.Count <= 1) return false;
        return shipment.containers.Remove(target);
    }

    public static string containerNumberOrName(this LoadingPlan plan) => string.IsNullOrWhiteSpace(plan.containerNumber) ? plan.container?.name ?? "Container" : plan.containerNumber;
}
