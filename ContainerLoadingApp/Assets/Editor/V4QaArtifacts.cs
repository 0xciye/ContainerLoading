using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V4QaArtifacts
{
    public static void GenerateDecisionSupportPdf()
    {
        var shipment = new Shipment { shipmentName = "V4 双语报告 QA", orderReference = "ORD-V4-OPS-240", bookingReference = "BK-V4-0901", customerName = "深圳华南物流有限公司", destination = "越南胡志明市吉莱港", loadingDate = DateTime.Now.ToString("dd/MM/yyyy"), notes = "装箱时请注意货物方向，禁止倒置。" };
        for (var i = 0; i < 12; i++) shipment.cargoTypes.Add(new CargoType { id = "V4T" + i, code = "HH" + (i + 1).ToString("00"), name = i == 0 ? "工业电机" : "Loại hàng vận hành " + (i + 1), length = i % 5 == 0 ? 2 : 1, width = i % 4 == 0 ? 2 : 1, height = i % 6 == 0 ? 2 : 1, quantity = 20, weightPerUnit = 30 + i * 15, allowRotation = i % 3 != 0, stackable = i % 5 != 0, color = Color.HSVToRGB(i / 12f, .72f, .9f) });
        for (var i = 0; i < 3; i++) shipment.containers.Add(new LoadingPlan { id = "V4C" + i, shipmentId = shipment.id, name = "Container " + (i + 1).ToString("00"), containerNumber = "CONT-V4-" + (i + 1).ToString("00"), containerType = "40FT", container = new ContainerConfig { id = "40FT-" + i, name = "40FT", length = 12, width = 6, height = 4 }, cargoTypes = shipment.cargoTypes });
        shipment.containers[0].placedCargo.Add(new PlacedCargo { id = "LOCKED-MANUAL", cargoTypeId = "V4T0", position = new Vector3Int(10, 0, 0), size = new Vector3Int(2, 2, 2), locked = true });
        ShipmentIntelligence.Normalize(shipment);
        var result = MultiContainerOptimizer.Generate(shipment, new OptimizationSettings { seed = 4001, timeBudgetMilliseconds = 15000 });
        if (!string.IsNullOrEmpty(result.error) || result.scenarios.Count == 0) throw new InvalidOperationException(result.error ?? "Không có candidate.");
        ScenarioService.Store(shipment, result); ScenarioService.Apply(shipment, result.scenarios.Single(x => x.recommended));
        var output = Path.GetFullPath("output/pdf"); Directory.CreateDirectory(output); PdfExportSystem.OutputDirectoryOverride = output;
        try { var management = PdfExportSystem.Export(shipment); var operational = PdfExportSystem.ExportOperational(shipment); Debug.Log("V41_MANAGEMENT_PDF=" + management); Debug.Log("V41_OPERATIONAL_PDF=" + operational); }
        finally { PdfExportSystem.OutputDirectoryOverride = null; }
        AssetDatabase.Refresh();
    }
}
