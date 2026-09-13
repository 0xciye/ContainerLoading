using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

public sealed class V4PlanningTests
{
    string root;

    [SetUp] public void SetUp() { root = Path.Combine(Path.GetTempPath(), "ContainerLoadingV4_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); PlanPersistence.RootPathOverride = root; PdfExportSystem.OutputDirectoryOverride = root; }
    [TearDown] public void TearDown() { PlanPersistence.RootPathOverride = null; PdfExportSystem.OutputDirectoryOverride = null; if (Directory.Exists(root)) Directory.Delete(root, true); }

    [Test] public void OptimizerGeneratesComparableValidDeterministicCandidates()
    {
        var shipment = RealisticShipment(); var settings = new OptimizationSettings { seed = 77, timeBudgetMilliseconds = 10000 };
        var first = MultiContainerOptimizer.Generate(shipment, settings); var second = MultiContainerOptimizer.Generate(shipment, settings);
        Assert.IsNull(first.error); Assert.AreEqual(3, first.scenarios.Count); Assert.AreEqual(1, first.scenarios.Count(x => x.recommended));
        Assert.AreEqual(Signature(first), Signature(second));
        foreach (var scenario in first.scenarios)
        {
            Assert.LessOrEqual(scenario.metrics.placed, scenario.metrics.totalCargo);
            foreach (var container in scenario.containers) Assert.IsTrue(PlanValidation.TryValidate(container, out var error), error);
        }
        StringAssert.Contains("Đề xuất vì", first.scenarios.Single(x => x.recommended).explanation);
    }

    [Test] public void ApplyScenarioCreatesRestorableSnapshotWithoutDestroyingManualWork()
    {
        var shipment = RealisticShipment(); var originalId = shipment.containers[0].placedCargo[0].id;
        var result = MultiContainerOptimizer.Generate(shipment, shipment.optimizationSettings); ScenarioService.Store(shipment, result);
        Assert.IsTrue(ScenarioService.Apply(shipment, result.scenarios[0])); Assert.IsNotEmpty(shipment.restoreScenarioId); Assert.IsFalse(shipment.containers.SelectMany(x => x.placedCargo).Any(x => x.id == originalId));
        Assert.IsTrue(ScenarioService.Restore(shipment)); Assert.IsTrue(shipment.containers.SelectMany(x => x.placedCargo).Any(x => x.id == originalId));
    }

    [Test] public void LockedPlacementIsPreservedWhenOptimizingRemaining()
    {
        var shipment = RealisticShipment(); var locked = shipment.containers[0].placedCargo[0]; locked.locked = true;
        var result = MultiContainerOptimizer.Generate(shipment, shipment.optimizationSettings);
        Assert.IsTrue(result.scenarios.All(s => s.containers.SelectMany(c => c.placedCargo).Any(x => x.id == locked.id && x.locked && x.position == locked.position)));
    }

    [Test] public void LoadingSequencePlacesDeepAndSupportedCargoFirst()
    {
        var plan = EmptyPlan(); var type = plan.cargoTypes[0];
        plan.placedCargo.Add(Box("deep", type.id, new Vector3Int(6, 0, 0)));
        plan.placedCargo.Add(Box("front", type.id, new Vector3Int(1, 0, 0)));
        plan.placedCargo.Add(Box("upper", type.id, new Vector3Int(6, 0, 1)));
        Assert.IsTrue(LoadingSequencePlanner.Generate(plan, out var warning), warning);
        Assert.Less(plan.loadingSequence.FindIndex(x => x.placementId == "deep"), plan.loadingSequence.FindIndex(x => x.placementId == "front"));
        Assert.Less(plan.loadingSequence.FindIndex(x => x.placementId == "deep"), plan.loadingSequence.FindIndex(x => x.placementId == "upper"));
    }

    [Test] public void WeightDistributionHandlesBalancedImbalancedAndEmptyData()
    {
        var plan = EmptyPlan(); plan.container = new ContainerConfig { length = 10, width = 4, height = 3 }; plan.cargoTypes[0].weightPerUnit = 100;
        plan.placedCargo.Add(Box("left-front", "T00", new Vector3Int(1, 0, 0))); plan.placedCargo.Add(Box("right-front", "T00", new Vector3Int(1, 3, 0)));
        plan.placedCargo.Add(Box("left-rear", "T00", new Vector3Int(8, 0, 0))); plan.placedCargo.Add(Box("right-rear", "T00", new Vector3Int(8, 3, 0)));
        var balanced = WeightDistributionCalculator.Calculate(plan); Assert.AreEqual(50f, balanced.leftPercent, .01f); Assert.AreEqual(50f, balanced.rightPercent, .01f); Assert.AreEqual("Xuất sắc", balanced.classification);
        plan.placedCargo.RemoveAll(x => x.position.y > 1); var leftHeavy = WeightDistributionCalculator.Calculate(plan); Assert.AreEqual(100f, leftHeavy.leftPercent, .01f); Assert.AreEqual("Lệch tải", leftHeavy.classification);
        plan.cargoTypes[0].weightPerUnit = 0; var empty = WeightDistributionCalculator.Calculate(plan); Assert.AreEqual(0, empty.totalWeight); Assert.AreEqual("Không có dữ liệu", empty.classification);
    }

    [Test] public void SchemaThreeLoadsAsVersionFourWithSafeDefaults()
    {
        var shipment = RealisticShipment(); var json = JsonUtility.ToJson(shipment).Replace("\"schemaVersion\":4", "\"schemaVersion\":3"); var legacy = JsonUtility.FromJson<Shipment>(json);
        legacy.scenarios = null; legacy.optimizationSettings = null; legacy.history = null; ShipmentIntelligence.Normalize(legacy);
        Assert.AreEqual(4, legacy.schemaVersion); Assert.IsNotNull(legacy.scenarios); Assert.IsNotNull(legacy.optimizationSettings); Assert.IsNotNull(legacy.history); Assert.IsTrue(legacy.containers.All(x => x.loadingSequence != null));
    }

    [Test] public void FiveContainerTwoHundredUnitWorkflowPersistsRestoresAndExportsPdf()
    {
        var shipment = RealisticShipment(); Assert.GreaterOrEqual(shipment.cargoTypes.Sum(x => x.quantity), 200);
        var timer = Stopwatch.StartNew(); var result = MultiContainerOptimizer.Generate(shipment, new OptimizationSettings { seed = 9, timeBudgetMilliseconds = 10000 }); timer.Stop();
        Assert.Less(timer.ElapsedMilliseconds, 10000); ScenarioService.Store(shipment, result); Assert.IsTrue(ScenarioService.Apply(shipment, result.scenarios.Single(x => x.recommended)));
        var path = ShipmentPersistence.Save(shipment); var loaded = ShipmentPersistence.Load(path); Assert.AreEqual(shipment.selectedScenarioId, loaded.selectedScenarioId); Assert.IsNotEmpty(loaded.scenarios);
        var backupPath = ShipmentPersistence.ExportBackup(); Assert.IsTrue(ShipmentPersistence.TryReadBackup(backupPath, out var backup, out var backupError), backupError); Assert.AreEqual(1, backup.shipments.Count);
        var pdf = PdfExportSystem.Export(loaded); Assert.IsTrue(File.Exists(pdf)); var raw = Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(pdf)); Assert.Greater(Count(raw, "/Type /Page "), loaded.containers.Count + 2);
    }

    [Test] public void PreCancelledOptimizationStopsWithoutCandidates()
    {
        var cancellation = new CancellationTokenSource(); cancellation.Cancel(); var result = MultiContainerOptimizer.Generate(RealisticShipment(), new OptimizationSettings(), cancellation.Token);
        Assert.IsTrue(result.cancelled); Assert.IsEmpty(result.scenarios);
    }

    [Test] public void ConstraintEvaluatorReturnsStableReasonCode()
    {
        var plan = EmptyPlan(); plan.placedCargo.Add(Box("occupied", "T00", Vector3Int.zero));
        var collision = ConstraintEvaluator.Evaluate(plan, plan.cargoTypes[0], Vector3Int.zero, Vector3Int.one);
        Assert.AreEqual("COLLISION", collision.code); Assert.AreEqual(ConstraintSeverity.Hard, collision.severity);
        var boundary = ConstraintEvaluator.Evaluate(plan, plan.cargoTypes[0], new Vector3Int(10, 0, 0), Vector3Int.one);
        Assert.AreEqual("BOUNDARY", boundary.code);
    }

    [Test] public void ReportLocalizationProvidesVietnameseAndSimplifiedChinese()
    {
        StringAssert.Contains("BÁO CÁO", ReportLocalization.Get("ReportTitle"));
        StringAssert.Contains("集装箱", ReportLocalization.Get("ReportTitle"));
        Assert.AreEqual("Khách hàng / 客户", ReportLocalization.Inline("Customer"));
    }

    static Shipment RealisticShipment()
    {
        var shipment = new Shipment { shipmentName = "V4 QA", orderReference = "ORD-V4-240", customerName = "QA Logistics", destination = "Bangkok" };
        for (var i = 0; i < 12; i++) shipment.cargoTypes.Add(new CargoType { id = "T" + i.ToString("00"), code = "SKU" + i.ToString("00"), name = "Hàng kiểm thử " + i, length = i % 5 == 0 ? 2 : 1, width = i % 4 == 0 ? 2 : 1, height = i % 6 == 0 ? 2 : 1, quantity = 20, weightPerUnit = 25 + i * 12, allowRotation = i % 3 != 0, stackable = i % 5 != 0, color = Color.HSVToRGB(i / 12f, .72f, .9f) });
        for (var i = 0; i < 5; i++) shipment.containers.Add(new LoadingPlan { id = "C" + i, shipmentId = shipment.id, name = "Container " + (i + 1).ToString("00"), containerNumber = "CONT-V4-" + (i + 1).ToString("00"), containerType = "40FT", container = new ContainerConfig { id = "CFG" + i, name = "40FT", length = 12, width = 6, height = 4 }, cargoTypes = shipment.cargoTypes });
        shipment.containers[0].placedCargo.Add(Box("manual-keep", "T00", new Vector3Int(10, 0, 0), new Vector3Int(2, 2, 2)));
        ShipmentIntelligence.Normalize(shipment); return shipment;
    }

    static LoadingPlan EmptyPlan()
    {
        var type = new CargoType { id = "T00", code = "A", name = "A", length = 1, width = 1, height = 1, quantity = 10, weightPerUnit = 10, stackable = true, color = Color.blue };
        return new LoadingPlan { name = "SEQ", container = new ContainerConfig { length = 10, width = 4, height = 3 }, cargoTypes = new System.Collections.Generic.List<CargoType> { type } };
    }

    static PlacedCargo Box(string id, string type, Vector3Int position, Vector3Int? size = null) => new() { id = id, cargoTypeId = type, position = position, size = size ?? Vector3Int.one };
    static string Signature(OptimizationResult result) => string.Join("|", result.scenarios.SelectMany(s => s.containers).SelectMany(c => c.placedCargo).Select(x => x.cargoTypeId + ":" + x.position + ":" + x.rotation));
    static int Count(string value, string token) { var count = 0; var index = 0; while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0) { count++; index += token.Length; } return count; }
}
