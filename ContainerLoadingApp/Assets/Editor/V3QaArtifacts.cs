using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class V3QaArtifacts
{
    public static void GenerateMultiContainerShipmentPdf()
    {
        var shipment=new Shipment{shipmentName="QA Shipment 3 Containers",orderReference="ORD-V3-MULTI-300",bookingReference="BK-20931",customerName="Công ty Kiểm định Việt Nam",destination="Cảng Cát Lái",loadingDate=DateTime.Now.ToString("dd/MM/yyyy"),notes="Báo cáo QA mô hình một chuyến hàng nhiều container."};
        shipment.cargoTypes.Add(new CargoType{id="A",code="A01",name="Hàng A",quantity=100,weightPerUnit=12,color=Color.red});shipment.cargoTypes.Add(new CargoType{id="B",code="B01",name="Hàng B",quantity=50,weightPerUnit=8,color=Color.green});shipment.cargoTypes.Add(new CargoType{id="C",code="C01",name="Hàng C",quantity=25,weightPerUnit=5,color=Color.blue});
        for(var i=0;i<3;i++)shipment.containers.Add(new LoadingPlan{id="QA-C"+i,shipmentId=shipment.id,name="Container "+(i+1).ToString("00"),containerNumber="CONT-QA-"+(i+1).ToString("00"),containerType="40HC",container=new ContainerConfig{id="40HC",name="40HC",length=10,width=5,height=2},cargoTypes=shipment.cargoTypes});
        var distributions=new[,]{{40,35,25},{20,20,10},{10,10,5}};for(var type=0;type<3;type++)for(var container=0;container<3;container++)AddQaUnits(shipment.containers[container],shipment.cargoTypes[type],distributions[type,container]);
        if(ShipmentIntelligence.Statistics(shipment).RemainingUnits!=0)throw new InvalidOperationException("Shipment QA chưa được xếp đủ.");
        var output=Path.GetFullPath("output/pdf");Directory.CreateDirectory(output);PdfExportSystem.OutputDirectoryOverride=output;try{var path=PdfExportSystem.Export(shipment);Debug.Log("V3_MULTI_QA_PDF="+path);}finally{PdfExportSystem.OutputDirectoryOverride=null;}AssetDatabase.Refresh();
    }
    static void AddQaUnits(LoadingPlan plan,CargoType type,int count){var start=plan.placedCargo.Count;for(var i=0;i<count;i++){var cell=start+i;plan.placedCargo.Add(new PlacedCargo{id=Guid.NewGuid().ToString("N"),cargoTypeId=type.id,position=new Vector3Int(cell%10,(cell/10)%5,cell/50),size=Vector3Int.one,rotation=0});}}
    public static void GenerateFullContainerPdf()
    {
        var plan=new LoadingPlan{name="QA_V3_FULL",orderReference="ORD-V3-300",customerName="Công ty Kiểm định Việt Nam",containerNumber="CONT-V3-0001",sealNumber="SEAL-300",destination="Cảng Cát Lái",loadingDate=DateTime.Now.ToString("dd/MM/yyyy"),orderNotes="Container đã được xếp đủ để kiểm tra báo cáo quản trị.\nKiểm tra màu, sơ đồ, Unicode tiếng Việt và cảnh báo trước khi phát hành.",container=new ContainerConfig{id="QA",name="40FT HC",length=12,width=5,height=5}};
        for(var i=0;i<10;i++)plan.cargoTypes.Add(new CargoType{id="QA"+i,code="HH"+(i+1).ToString("00"),name="Loại hàng kiểm thử "+(i+1),quantity=30,weightPerUnit=8+i,color=Color.HSVToRGB(i/10f,.72f,.9f),allowRotation=true,stackable=true});
        var added=PlanIntelligence.AutoFillRemaining(plan);
        if(added!=300||PlanIntelligence.RemainingQuantity(plan)!=0)throw new InvalidOperationException("Không lấp đầy được container QA.");
        var output=Path.GetFullPath("output/pdf");Directory.CreateDirectory(output);PdfExportSystem.OutputDirectoryOverride=output;
        try{var path=PdfExportSystem.Export(plan);Debug.Log("V3_QA_PDF="+path);}
        finally{PdfExportSystem.OutputDirectoryOverride=null;}
        AssetDatabase.Refresh();
    }
}
