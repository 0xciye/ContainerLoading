using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PlanHealthReport
{
    public readonly List<string> errors = new();
    public readonly List<string> warnings = new();
    public readonly List<string> information = new();
    public bool IsValid => errors.Count == 0;
    public PlanReadiness Readiness { get; internal set; }
    public string StatusLabel => Readiness == PlanReadiness.Ready ? "Sẵn sàng" : Readiness == PlanReadiness.NeedsCheck ? "Cần kiểm tra" : "Chưa hoàn tất";
    public string Summary => IsValid ? (warnings.Count == 0 ? "Phương án hợp lệ và đã sẵn sàng." : $"Phương án hợp lệ, có {warnings.Count} cảnh báo.") : $"Phương án có {errors.Count} lỗi cần xử lý.";
}

public enum PlanReadiness { Incomplete, NeedsCheck, Ready }

public sealed class PlanStatistics
{
    public int CargoTypeCount, TotalQuantity, PlacedQuantity, RemainingQuantity, CapacityCells, UsedCells;
    public float CompletionPercent, UtilizationPercent, PlacedWeight, PlannedWeight;
}

public static class PlanIntelligence
{
    public static int TotalQuantity(LoadingPlan plan) => plan?.cargoTypes?.Sum(x => Math.Max(0, x.quantity)) ?? 0;
    public static int RemainingQuantity(LoadingPlan plan) => Math.Max(0, TotalQuantity(plan) - (plan?.placedCargo?.Count ?? 0));
    public static float TotalWeight(LoadingPlan plan)
    {
        if (plan?.placedCargo == null || plan.cargoTypes == null) return 0;
        return plan.placedCargo.Sum(box => plan.cargoTypes.Find(type => type.id == box.cargoTypeId)?.weightPerUnit ?? 0f);
    }

    public static PlanStatistics Statistics(LoadingPlan plan)
    {
        var stats = new PlanStatistics();
        if (plan == null) return stats;
        stats.CargoTypeCount = plan.cargoTypes?.Count ?? 0;
        stats.TotalQuantity = TotalQuantity(plan);
        stats.PlacedQuantity = plan.placedCargo?.Count ?? 0;
        stats.RemainingQuantity = Math.Max(0, stats.TotalQuantity - stats.PlacedQuantity);
        stats.CapacityCells = Math.Max(1, (plan.container?.length ?? 1) * (plan.container?.width ?? 1) * (plan.container?.height ?? 1));
        stats.UsedCells = plan.placedCargo?.Sum(x => x.size.x * x.size.y * x.size.z) ?? 0;
        stats.CompletionPercent = stats.TotalQuantity == 0 ? 0 : stats.PlacedQuantity * 100f / stats.TotalQuantity;
        stats.UtilizationPercent = stats.UsedCells * 100f / stats.CapacityCells;
        stats.PlacedWeight = TotalWeight(plan);
        stats.PlannedWeight = plan.cargoTypes?.Sum(x => Math.Max(0, x.quantity) * Math.Max(0f, x.weightPerUnit)) ?? 0f;
        return stats;
    }

    public static PlanHealthReport Check(LoadingPlan plan)
    {
        var report = new PlanHealthReport();
        if (!PlanValidation.TryValidate(plan, out var error)) report.errors.Add(error);
        if (plan == null) { report.Readiness = PlanReadiness.NeedsCheck; return report; }
        var stats = Statistics(plan);
        if (stats.TotalQuantity == 0) report.warnings.Add("Chưa khai báo loại hàng.");
        else if (stats.RemainingQuantity > 0) report.warnings.Add($"Còn {stats.RemainingQuantity} kiện chưa được xếp.");
        if (string.IsNullOrWhiteSpace(plan.orderReference)) report.warnings.Add("Chưa nhập mã đơn hàng / tham chiếu.");
        report.information.Add($"Mức sử dụng container: {Mathf.RoundToInt(stats.UtilizationPercent)}% ({stats.UsedCells}/{stats.CapacityCells} ô).");
        report.information.Add($"Đã xếp {stats.PlacedQuantity}/{stats.TotalQuantity} kiện · Tổng trọng lượng {stats.PlacedWeight:0.##} kg.");
        report.Readiness = report.errors.Count > 0 ? PlanReadiness.NeedsCheck : stats.TotalQuantity == 0 || stats.RemainingQuantity > 0 ? PlanReadiness.Incomplete : report.warnings.Count > 0 ? PlanReadiness.NeedsCheck : PlanReadiness.Ready;
        return report;
    }

    public static bool TryFindNextPlacement(LoadingPlan plan, CargoType type, out Vector3Int position, out int rotation)
    {
        position = Vector3Int.zero; rotation = 0;
        if (plan?.container == null || type == null) return false;
        var rotations = type.allowRotation && type.length != type.width ? new[] { 0, 90 } : new[] { 0 };
        foreach (var candidateRotation in rotations)
        {
            var size = GridPlacement.RotatedSize(new Vector3Int(type.length, type.width, type.height), candidateRotation);
            for (var z = 0; z <= plan.container.height - size.z; z++)
                for (var y = 0; y <= plan.container.width - size.y; y++)
                    for (var x = 0; x <= plan.container.length - size.x; x++)
                    {
                        var candidate = new Vector3Int(x, y, z);
                        if (!GridPlacement.TryPlace(plan, type, candidate, candidateRotation, out _)) continue;
                        position = candidate; rotation = candidateRotation; return true;
                    }
        }
        return false;
    }

    public static int AutoFill(LoadingPlan plan, CargoType type, int maximum = 5000)
    {
        var added = 0;
        while (added < maximum && TryFindNextPlacement(plan, type, out var position, out var rotation))
        {
            var size = GridPlacement.RotatedSize(new Vector3Int(type.length, type.width, type.height), rotation);
            plan.placedCargo.Add(new PlacedCargo { id = Guid.NewGuid().ToString("N"), cargoTypeId = type.id, position = position, size = size, rotation = rotation });
            added++;
        }
        return added;
    }

    public static int AutoFillRemaining(LoadingPlan plan, int maximum = 5000)
    {
        var added = 0;
        if (plan?.cargoTypes == null) return added;
        foreach (var type in plan.cargoTypes)
        {
            if (added >= maximum) break;
            added += AutoFill(plan, type, maximum - added);
        }
        return added;
    }
}
