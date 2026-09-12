using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class V3QaArtifacts
{
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
