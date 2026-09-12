using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using UnityEngine;

[Serializable]
public sealed class OptimizationSettings
{
    public int seed = 4001;
    public int timeBudgetMilliseconds = 4000;
    public int maxScenarios = 3;
    public bool preserveLockedPlacements = true;
    public bool preferHeavyCargoLow = true;
}

[Serializable]
public sealed class ShipmentChange { public string action, at, detail; }

[Serializable]
public sealed class LoadingStep
{
    public int step;
    public string placementId, cargoTypeId, cargoCode, zone, note;
}

[Serializable]
public sealed class WeightDistribution
{
    public float totalWeight, leftPercent, rightPercent, frontPercent, rearPercent;
    public float centerX, centerY, centerZ;
    public string classification = "Không có dữ liệu";
}

[Serializable]
public sealed class OptimizationMetrics
{
    public int containerCount, totalCargo, placed, remaining, constraintErrors, warnings, loadingSteps;
    public float completionPercent, averageUtilization, minimumUtilization, totalWeight, weightBalanceScore;
    public string weightBalance = "Không có dữ liệu", loadingFeasibility = "Cần kiểm tra";
}

[Serializable]
public sealed class OptimizationScenario
{
    public string id, name, strategy, createdAt, explanation;
    public bool recommended, workingSnapshot;
    public int seed;
    public List<LoadingPlan> containers = new();
    public OptimizationMetrics metrics = new();
    public List<string> warnings = new();
}

public sealed class OptimizationResult
{
    public readonly List<OptimizationScenario> scenarios = new();
    public string error;
    public bool cancelled;
}

public enum ConstraintSeverity { Hard, Soft }

public sealed class ConstraintIssue
{
    public string code, message;
    public ConstraintSeverity severity;
}

public static class ConstraintEvaluator
{
    public static ConstraintIssue Evaluate(LoadingPlan plan, CargoType type, Vector3Int position, Vector3Int size)
    {
        if (plan?.container == null || type == null) return Hard("MISSING_DATA", "Thiếu dữ liệu container hoặc loại hàng.");
        if (position.x < 0 || position.y < 0 || position.z < 0 || position.x + size.x > plan.container.length || position.y + size.y > plan.container.width || position.z + size.z > plan.container.height) return Hard("BOUNDARY", "Vị trí vượt biên container.");
        var collision = plan.placedCargo.FirstOrDefault(x => GridPlacement.Overlaps(position, size, x.position, x.size));
        if (collision != null) return Hard("COLLISION", "Vị trí chồng lên kiện " + collision.id + ".");
        if (position.z == 0) return null;
        for (var x = position.x; x < position.x + size.x; x++) for (var y = position.y; y < position.y + size.y; y++)
        {
            var support = plan.placedCargo.FirstOrDefault(box => box.position.z + box.size.z == position.z && x >= box.position.x && x < box.position.x + box.size.x && y >= box.position.y && y < box.position.y + box.size.y);
            if (support == null) return Hard("UNSUPPORTED", "Hàng chưa được đỡ kín bởi tầng bên dưới.");
            if (!(plan.cargoTypes.Find(t => t.id == support.cargoTypeId)?.stackable ?? false)) return Hard("NON_STACKABLE_SUPPORT", "Hàng bên dưới không cho phép xếp chồng.");
        }
        return null;
    }

    static ConstraintIssue Hard(string code, string message) => new() { code = code, message = message, severity = ConstraintSeverity.Hard };
}

public static class WeightDistributionCalculator
{
    public static WeightDistribution Calculate(LoadingPlan plan)
    {
        var result = new WeightDistribution();
        if (plan?.container == null || plan.placedCargo == null || plan.cargoTypes == null) return result;
        double sx = 0, sy = 0, sz = 0, left = 0, right = 0, front = 0, rear = 0;
        foreach (var box in plan.placedCargo)
        {
            var weight = Math.Max(0, plan.cargoTypes.Find(x => x.id == box.cargoTypeId)?.weightPerUnit ?? 0f);
            if (weight <= 0) continue;
            var cx = box.position.x + box.size.x / 2d;
            var cy = box.position.y + box.size.y / 2d;
            var cz = box.position.z + box.size.z / 2d;
            result.totalWeight += weight; sx += weight * cx; sy += weight * cy; sz += weight * cz;
            if (cy < plan.container.width / 2d) left += weight; else right += weight;
            if (cx < plan.container.length / 2d) front += weight; else rear += weight;
        }
        if (result.totalWeight <= 0) return result;
        result.leftPercent = (float)(left * 100d / result.totalWeight); result.rightPercent = 100f - result.leftPercent;
        result.frontPercent = (float)(front * 100d / result.totalWeight); result.rearPercent = 100f - result.frontPercent;
        result.centerX = (float)(sx / result.totalWeight / Math.Max(1, plan.container.length));
        result.centerY = (float)(sy / result.totalWeight / Math.Max(1, plan.container.width));
        result.centerZ = (float)(sz / result.totalWeight / Math.Max(1, plan.container.height));
        var imbalance = Math.Max(Math.Abs(result.leftPercent - 50f), Math.Abs(result.frontPercent - 50f));
        result.classification = imbalance <= 5 ? "Xuất sắc" : imbalance <= 10 ? "Tốt" : imbalance <= 20 ? "Nên kiểm tra" : "Lệch tải";
        return result;
    }

    public static float Score(WeightDistribution value)
    {
        if (value == null || value.totalWeight <= 0) return 0;
        return Math.Max(0, 100f - 2f * Math.Max(Math.Abs(value.leftPercent - 50f), Math.Abs(value.frontPercent - 50f)));
    }
}

public static class LoadingSequencePlanner
{
    // Coordinate contract: X=0 is the container door/front; larger X is deeper inside.
    public static bool Generate(LoadingPlan plan, out string warning)
    {
        warning = null;
        plan.loadingSequence ??= new List<LoadingStep>(); plan.loadingSequence.Clear();
        if (plan.placedCargo == null || plan.placedCargo.Count == 0) return true;
        var boxes = plan.placedCargo.Where(x => x != null).ToList();
        var before = boxes.ToDictionary(x => x.id, _ => new HashSet<string>());
        foreach (var deep in boxes)
        foreach (var front in boxes)
        {
            if (deep.id == front.id) continue;
            var depthBlocked = deep.position.x > front.position.x && Overlap1D(deep.position.y, deep.size.y, front.position.y, front.size.y) && Overlap1D(deep.position.z, deep.size.z, front.position.z, front.size.z);
            var supports = deep.position.z + deep.size.z == front.position.z && GridPlacement.FootprintsOverlap(deep.position, deep.size, front.position, front.size);
            if (depthBlocked || supports) before[front.id].Add(deep.id);
        }
        var remaining = new HashSet<string>(boxes.Select(x => x.id)); var ordered = new List<PlacedCargo>();
        while (remaining.Count > 0)
        {
            var next = boxes.Where(x => remaining.Contains(x.id) && before[x.id].All(id => !remaining.Contains(id)))
                .OrderByDescending(x => x.position.x).ThenBy(x => x.position.z).ThenBy(x => x.position.y).ThenBy(x => x.id, StringComparer.Ordinal).FirstOrDefault();
            if (next == null) { warning = "Phát hiện vòng phụ thuộc; cần kiểm tra trình tự đóng hàng."; return false; }
            remaining.Remove(next.id); ordered.Add(next);
        }
        for (var i = 0; i < ordered.Count; i++)
        {
            var box = ordered[i]; var type = plan.cargoTypes.Find(x => x.id == box.cargoTypeId);
            var zone = box.position.x >= plan.container.length * 2 / 3 ? "Cuối container" : box.position.x >= plan.container.length / 3 ? "Giữa container" : "Gần cửa";
            plan.loadingSequence.Add(new LoadingStep { step = i + 1, placementId = box.id, cargoTypeId = box.cargoTypeId, cargoCode = type?.code ?? "?", zone = zone, note = box.locked ? "Vị trí đã khóa" : "Đặt tại cột " + (box.position.x + 1) + ", hàng " + (box.position.y + 1) + ", tầng " + (box.position.z + 1) });
        }
        return true;
    }

    static bool Overlap1D(int a, int sizeA, int b, int sizeB) => a < b + sizeB && a + sizeA > b;
}

public static class MultiContainerOptimizer
{
    static readonly string[] Strategies = { "Tiết kiệm container", "Cân bằng tải", "Dễ đóng hàng" };

    public static OptimizationResult Generate(Shipment source, OptimizationSettings settings, CancellationToken cancellationToken = default)
    {
        var result = new OptimizationResult(); settings ??= new OptimizationSettings();
        if (source?.containers == null || source.containers.Count == 0 || source.cargoTypes == null || source.cargoTypes.Count == 0) { result.error = "Chuyến hàng cần có container và loại hàng."; return result; }
        var watch = Stopwatch.StartNew();
        try
        {
            foreach (var strategy in Strategies.Take(Math.Max(1, Math.Min(3, settings.maxScenarios))))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (watch.ElapsedMilliseconds > Math.Max(500, settings.timeBudgetMilliseconds)) break;
                var candidate = BuildScenario(source, settings, strategy, cancellationToken, watch);
                if (strategy == Strategies[0])
                {
                    var maximum = Math.Max(source.containers.Count, 5);
                    while (candidate.metrics.remaining > 0 && candidate.containers.Count < maximum && watch.ElapsedMilliseconds <= Math.Max(500, settings.timeBudgetMilliseconds))
                        candidate = BuildScenario(source, settings, strategy, cancellationToken, watch, candidate.containers.Count + 1);
                }
                result.scenarios.Add(candidate);
            }
            if (result.scenarios.Count == 0) { result.error = "Không tìm được phương án trong thời gian cho phép."; return result; }
            var recommended = result.scenarios.OrderByDescending(x => x.metrics.completionPercent)
                .ThenBy(x => x.metrics.containerCount).ThenBy(x => x.metrics.constraintErrors).ThenBy(x => x.metrics.warnings)
                .ThenByDescending(x => x.metrics.weightBalanceScore).ThenByDescending(x => x.metrics.averageUtilization).First();
            recommended.recommended = true;
            recommended.explanation = Explain(recommended, result.scenarios);
        }
        catch (OperationCanceledException) { result.cancelled = true; result.error = "Đã hủy tính phương án."; }
        catch (Exception exception) { result.error = "Không thể tạo phương án: " + exception.Message; }
        return result;
    }

    static OptimizationScenario BuildScenario(Shipment source, OptimizationSettings settings, string strategy, CancellationToken token, Stopwatch watch, int forcedCount = 0)
    {
        var totalVolume = source.cargoTypes.Sum(x => Math.Max(0, x.quantity) * x.length * x.width * x.height);
        var maxCapacity = source.containers.Max(x => Math.Max(1, x.container.length * x.container.width * x.container.height));
        var theoretical = Math.Max(1, (int)Math.Ceiling(totalVolume / (double)maxCapacity));
        var lockedLast = settings.preserveLockedPlacements ? source.containers.FindLastIndex(c => c.placedCargo.Any(x => x.locked)) + 1 : 0;
        var count = forcedCount > 0 ? forcedCount : strategy == Strategies[0] ? Math.Max(theoretical, lockedLast) : Math.Max(Math.Min(source.containers.Count, 5), lockedLast);
        count = Math.Max(1, Math.Min(Math.Max(source.containers.Count, 5), count));
        var suffix = strategy == Strategies[0] ? "A" : strategy == Strategies[1] ? "B" : "C";
        var scenario = new OptimizationScenario { id = "scenario-" + settings.seed + "-" + suffix, name = "Phương án " + suffix, strategy = strategy, seed = settings.seed, createdAt = DateTime.UtcNow.ToString("O") };
        for (var i = 0; i < count; i++)
        {
            var template = source.containers[Math.Min(i, source.containers.Count - 1)];
            var copy = ClonePlan(template); copy.id = "scenario-container-" + settings.seed + "-" + i; copy.name = "Container " + (i + 1).ToString("00"); copy.containerNumber = i < source.containers.Count ? template.containerNumber : ""; copy.cargoTypes = source.cargoTypes;
            copy.placedCargo = settings.preserveLockedPlacements ? template.placedCargo.Where(x => x.locked).Select(ClonePlacement).ToList() : new List<PlacedCargo>();
            copy.loadingSequence = new List<LoadingStep>(); scenario.containers.Add(copy);
        }
        var types = strategy == Strategies[1]
            ? source.cargoTypes.OrderByDescending(x => x.weightPerUnit).ThenByDescending(Volume).ThenBy(x => x.code, StringComparer.Ordinal).ToList()
            : source.cargoTypes.OrderByDescending(Volume).ThenByDescending(x => x.weightPerUnit).ThenBy(x => x.code, StringComparer.Ordinal).ToList();
        foreach (var type in types)
        {
            var already = scenario.containers.Sum(c => c.placedCargo.Count(x => x.cargoTypeId == type.id));
            for (var unit = already; unit < type.quantity; unit++)
            {
                token.ThrowIfCancellationRequested(); if (watch.ElapsedMilliseconds > Math.Max(500, settings.timeBudgetMilliseconds)) { scenario.warnings.Add("Hết thời gian tìm kiếm; còn hàng chưa xếp."); break; }
                var candidates = strategy == Strategies[1] ? scenario.containers.OrderBy(PlacedWeight).ThenBy(x => x.name).ToList() : scenario.containers;
                if (!TryPlaceInAny(candidates, type, strategy, settings, unit, out var reason)) { scenario.warnings.Add($"{type.code}: {reason}"); break; }
            }
        }
        scenario.containers.RemoveAll(x => x.placedCargo.Count == 0);
        foreach (var container in scenario.containers) if (!LoadingSequencePlanner.Generate(container, out var warning) && warning != null) scenario.warnings.Add(container.name + ": " + warning);
        scenario.metrics = Metrics(source, scenario); return scenario;
    }

    static bool TryPlaceInAny(IEnumerable<LoadingPlan> containers, CargoType type, string strategy, OptimizationSettings settings, int unit, out string reason)
    {
        reason = "không còn vị trí hợp lệ trong các container khả dụng";
        foreach (var plan in containers)
        {
            var rotations = type.allowRotation && type.length != type.width ? new[] { 0, 90 } : new[] { 0 };
            foreach (var rotation in rotations)
            {
                var size = rotation % 180 == 0 ? new Vector3Int(type.length, type.width, type.height) : new Vector3Int(type.width, type.length, type.height);
                for (var z = 0; z <= plan.container.height - size.z; z++)
                for (var y = 0; y <= plan.container.width - size.y; y++)
                {
                    var reverse = strategy == Strategies[2]; var start = reverse ? plan.container.length - size.x : 0; var end = reverse ? -1 : plan.container.length - size.x + 1; var step = reverse ? -1 : 1;
                    for (var x = start; x != end; x += step)
                    {
                        var position = new Vector3Int(x, y, z);
                        if (!CanPlace(plan, type, position, size)) continue;
                        plan.placedCargo.Add(new PlacedCargo { id = $"opt-{settings.seed}-{type.id}-{unit}-{plan.placedCargo.Count}", cargoTypeId = type.id, position = position, size = size, rotation = rotation });
                        return true;
                    }
                }
            }
        }
        return false;
    }

    static bool CanPlace(LoadingPlan plan, CargoType type, Vector3Int position, Vector3Int size)
    {
        return ConstraintEvaluator.Evaluate(plan, type, position, size) == null;
    }

    static OptimizationMetrics Metrics(Shipment source, OptimizationScenario scenario)
    {
        var m = new OptimizationMetrics { containerCount = scenario.containers.Count, totalCargo = source.cargoTypes.Sum(x => Math.Max(0, x.quantity)), placed = scenario.containers.Sum(x => x.placedCargo.Count), warnings = scenario.warnings.Count, totalWeight = scenario.containers.Sum(PlacedWeight), loadingSteps = scenario.containers.Sum(x => x.loadingSequence.Count) };
        m.constraintErrors = scenario.containers.Count(c => !GeneratedPlanValid(c));
        m.remaining = Math.Max(0, m.totalCargo - m.placed); m.completionPercent = m.totalCargo == 0 ? 0 : m.placed * 100f / m.totalCargo;
        var utilizations = scenario.containers.Select(c => c.placedCargo.Sum(x => x.size.x * x.size.y * x.size.z) * 100f / Math.Max(1, c.container.length * c.container.width * c.container.height)).ToList();
        m.averageUtilization = utilizations.Count == 0 ? 0 : utilizations.Average(); m.minimumUtilization = utilizations.Count == 0 ? 0 : utilizations.Min();
        var weights = scenario.containers.Select(WeightDistributionCalculator.Calculate).Where(x => x.totalWeight > 0).ToList();
        m.weightBalanceScore = weights.Count == 0 ? 0 : weights.Average(WeightDistributionCalculator.Score); m.weightBalance = weights.Count == 0 ? "Không có dữ liệu" : m.weightBalanceScore >= 90 ? "Xuất sắc" : m.weightBalanceScore >= 80 ? "Tốt" : m.weightBalanceScore >= 60 ? "Nên kiểm tra" : "Lệch tải";
        m.loadingFeasibility = scenario.warnings.Any(x => x.IndexOf("phụ thuộc", StringComparison.OrdinalIgnoreCase) >= 0) ? "Cần kiểm tra" : "Tốt";
        return m;
    }

    static string Explain(OptimizationScenario best, IEnumerable<OptimizationScenario> all)
    {
        var complete = best.metrics.remaining == 0 ? "xếp đủ toàn bộ hàng" : $"xếp được {best.metrics.placed}/{best.metrics.totalCargo} kiện";
        var minContainers = all.Min(x => x.metrics.containerCount);
        var containerReason = best.metrics.containerCount == minContainers ? $"dùng ít nhất {best.metrics.containerCount} container trong các phương án đã tìm" : $"dùng {best.metrics.containerCount} container";
        var weight = best.metrics.totalWeight > 0 ? $", cân bằng tải {best.metrics.weightBalance.ToLowerInvariant()}" : "";
        return $"Đề xuất vì {complete}, {containerReason}, mức sử dụng trung bình {best.metrics.averageUtilization:0.#}%{weight} và có {best.metrics.warnings} cảnh báo.";
    }

    static int Volume(CargoType type) => type.length * type.width * type.height;
    static float PlacedWeight(LoadingPlan plan) => plan.placedCargo.Sum(x => plan.cargoTypes.Find(t => t.id == x.cargoTypeId)?.weightPerUnit ?? 0f);
    static bool GeneratedPlanValid(LoadingPlan plan)
    {
        if (plan?.container == null || plan.placedCargo == null) return false;
        for (var i = 0; i < plan.placedCargo.Count; i++)
        {
            var box = plan.placedCargo[i]; if (box == null || box.position.x < 0 || box.position.y < 0 || box.position.z < 0 || box.position.x + box.size.x > plan.container.length || box.position.y + box.size.y > plan.container.width || box.position.z + box.size.z > plan.container.height) return false;
            for (var j = 0; j < i; j++) if (GridPlacement.Overlaps(box.position, box.size, plan.placedCargo[j].position, plan.placedCargo[j].size)) return false;
        }
        return true;
    }
    static LoadingPlan ClonePlan(LoadingPlan source) => new()
    {
        id = source.id, shipmentId = source.shipmentId, schemaVersion = source.schemaVersion, name = source.name,
        orderReference = source.orderReference, customerName = source.customerName, containerNumber = source.containerNumber,
        sealNumber = source.sealNumber, destination = source.destination, loadingDate = source.loadingDate,
        orderNotes = source.orderNotes, notes = source.notes, containerType = source.containerType,
        container = new ContainerConfig { id = source.container.id, name = source.container.name, length = source.container.length, width = source.container.width, height = source.container.height },
        cargoTypes = source.cargoTypes, placedCargo = new List<PlacedCargo>(), loadingSequence = new List<LoadingStep>(),
        doorSide = source.doorSide, createdAt = source.createdAt, updatedAt = source.updatedAt
    };
    static PlacedCargo ClonePlacement(PlacedCargo source) => new() { id = source.id, cargoTypeId = source.cargoTypeId, position = source.position, size = source.size, rotation = source.rotation, locked = source.locked };
}

public static class ScenarioService
{
    public static void Store(Shipment shipment, OptimizationResult result)
    {
        if (shipment == null || result == null || result.cancelled || result.scenarios.Count == 0) return;
        var restore = shipment.scenarios?.Where(x => x.workingSnapshot).ToList() ?? new List<OptimizationScenario>();
        shipment.scenarios = result.scenarios.Take(3).Concat(restore).ToList();
        shipment.history.Add(new ShipmentChange { action = "SCENARIOS_GENERATED", at = DateTime.UtcNow.ToString("O"), detail = shipment.scenarios.Count + " phương án" });
    }

    public static bool Apply(Shipment shipment, OptimizationScenario scenario)
    {
        if (shipment == null || scenario?.containers == null || scenario.containers.Count == 0) return false;
        var snapshot = new OptimizationScenario { id = "restore-" + Guid.NewGuid().ToString("N"), name = "Trước khi áp dụng", strategy = "Bản xếp thủ công", createdAt = DateTime.UtcNow.ToString("O"), workingSnapshot = true, containers = CloneContainers(shipment.containers) };
        shipment.scenarios.RemoveAll(x => x.workingSnapshot); shipment.scenarios.Add(snapshot); shipment.restoreScenarioId = snapshot.id;
        shipment.containers = CloneContainers(scenario.containers); shipment.selectedScenarioId = scenario.id;
        ShipmentIntelligence.Normalize(shipment); shipment.history.Add(new ShipmentChange { action = "SCENARIO_APPLIED", at = DateTime.UtcNow.ToString("O"), detail = scenario.name }); return true;
    }

    public static bool Restore(Shipment shipment)
    {
        var snapshot = shipment?.scenarios?.Find(x => x.id == shipment.restoreScenarioId && x.workingSnapshot);
        if (snapshot == null) return false;
        shipment.containers = CloneContainers(snapshot.containers); shipment.selectedScenarioId = ""; shipment.restoreScenarioId = "";
        ShipmentIntelligence.Normalize(shipment); shipment.history.Add(new ShipmentChange { action = "SCENARIO_RESTORED", at = DateTime.UtcNow.ToString("O"), detail = snapshot.name }); return true;
    }

    static List<LoadingPlan> CloneContainers(List<LoadingPlan> containers) => JsonUtility.FromJson<PlanList>(JsonUtility.ToJson(new PlanList { items = containers })).items;
    [Serializable] sealed class PlanList { public List<LoadingPlan> items = new(); }
}
