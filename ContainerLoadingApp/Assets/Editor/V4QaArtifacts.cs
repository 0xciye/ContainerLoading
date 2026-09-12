using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V4QaArtifacts
{
    public static void GenerateDecisionSupportPdf()
    {
        var shipment = new Shipment { shipmentName = "V4 Decision Support QA", orderReference = "ORD-V4-OPS-240", bookingReference = "BK-V4-0901", customerName = "Công ty Logistics Việt Nam", destination = "Cảng Cát Lái", loadingDate = DateTime.Now.ToString("dd/MM/yyyy"), notes = "Dữ liệu QA 5 container, 12 loại và 240 kiện." };
        for (var i = 0; i < 12; i++) shipment.cargoTypes.Add(new CargoType { id = "V4T" + i, code = "HH" + (i + 1).ToString("00"), name = "Loại hàng vận hành " + (i + 1), length = i % 5 == 0 ? 2 : 1, width = i % 4 == 0 ? 2 : 1, height = i % 6 == 0 ? 2 : 1, quantity = 20, weightPerUnit = 30 + i * 15, allowRotation = i % 3 != 0, stackable = i % 5 != 0, color = Color.HSVToRGB(i / 12f, .72f, .9f) });
        for (var i = 0; i < 5; i++) shipment.containers.Add(new LoadingPlan { id = "V4C" + i, shipmentId = shipment.id, name = "Container " + (i + 1).ToString("00"), containerNumber = "CONT-V4-" + (i + 1).ToString("00"), containerType = "40FT", container = new ContainerConfig { id = "40FT-" + i, name = "40FT", length = 12, width = 6, height = 4 }, cargoTypes = shipment.cargoTypes });
        shipment.containers[0].placedCargo.Add(new PlacedCargo { id = "LOCKED-MANUAL", cargoTypeId = "V4T0", position = new Vector3Int(10, 0, 0), size = new Vector3Int(2, 2, 2), locked = true });
        ShipmentIntelligence.Normalize(shipment);
        var result = MultiContainerOptimizer.Generate(shipment, new OptimizationSettings { seed = 4001, timeBudgetMilliseconds = 15000 });
        if (!string.IsNullOrEmpty(result.error) || result.scenarios.Count == 0) throw new InvalidOperationException(result.error ?? "Không có candidate.");
        ScenarioService.Store(shipment, result); ScenarioService.Apply(shipment, result.scenarios.Single(x => x.recommended));
        var output = Path.GetFullPath("output/pdf"); Directory.CreateDirectory(output); PdfExportSystem.OutputDirectoryOverride = output;
        try { var path = PdfExportSystem.Export(shipment); Debug.Log("V4_QA_PDF=" + path); }
        finally { PdfExportSystem.OutputDirectoryOverride = null; }
        AssetDatabase.Refresh();
    }
}
