using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;

public class GridPlacementTests
{
    string testRoot;

    [SetUp] public void SetUp() { testRoot=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ContainerLoadingTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(testRoot);PlanPersistence.RootPathOverride=testRoot; }
    [TearDown] public void TearDown() { PlanPersistence.RootPathOverride=null;if(Directory.Exists(testRoot))Directory.Delete(testRoot,true); }

    LoadingPlan Plan()
    {
        var p=new LoadingPlan{name="PA-001",orderReference="ĐH-001",customerName="Công ty Việt",orderNotes="Giao ca sáng\nƯu tiên hàng dễ vỡ",container=new ContainerConfig{id="C1",name="40FT",length=10,width=3,height=6}};
        p.cargoTypes.Add(new CargoType{id="A",code="A",name="Sầu riêng",length=2,width=2,height=2,quantity=100,weightPerUnit=12.5f,color=Color.green});
        return p;
    }

    [Test] public void ValidMultiCellPlacement() { var p=Plan();Assert.IsTrue(GridPlacement.TryPlace(p,p.cargoTypes[0],new Vector3Int(8,1,0),0,out _)); }
    [Test] public void BoundsAreRejected() { var p=Plan();Assert.IsFalse(GridPlacement.TryPlace(p,p.cargoTypes[0],new Vector3Int(9,0,0),0,out _)); }
    [Test] public void WidthAndHeightBoundsAreRejected() { var p=Plan();Assert.IsFalse(GridPlacement.TryPlace(p,p.cargoTypes[0],new Vector3Int(0,2,0),0,out _));Assert.IsFalse(GridPlacement.TryPlace(p,p.cargoTypes[0],new Vector3Int(0,0,5),0,out _)); }
    [Test] public void TwoLayerCargoStacksAboveOneLayerCargo() { var p=Plan();p.placedCargo.Add(new PlacedCargo{id="BASE",cargoTypeId="A",position=Vector3Int.zero,size=Vector3Int.one});var tall=new CargoType{id="TALL",code="T",name="Cao hai tầng",length=1,width=1,height=2,color=Color.blue};var layer=GridPlacement.LowestSupportedLayer(p,tall,Vector3Int.zero,0);Assert.AreEqual(1,layer);Assert.IsTrue(GridPlacement.TryPlace(p,tall,new Vector3Int(0,0,layer),0,out _)); }
    [Test] public void CollisionIsRejected() { var p=Plan();p.placedCargo.Add(Box("A1",Vector3Int.zero));Assert.IsFalse(GridPlacement.TryPlace(p,p.cargoTypes[0],Vector3Int.zero,0,out _)); }
    [Test] public void AdjacentBoxesDoNotCollide() { var p=Plan();p.placedCargo.Add(Box("A1",Vector3Int.zero));Assert.IsTrue(GridPlacement.TryPlace(p,p.cargoTypes[0],new Vector3Int(2,0,0),0,out _)); }
    [Test] public void RotationSwapsFootprint() { Assert.AreEqual(new Vector3Int(2,1,3),GridPlacement.RotatedSize(new Vector3Int(1,2,3),90)); }
    [Test] public void InvalidRotationIsRejected() { var p=Plan();Assert.IsFalse(GridPlacement.TryPlace(p,p.cargoTypes[0],Vector3Int.zero,45,out var error));StringAssert.Contains("Góc xoay",error); }
    [Test] public void QuantityLimitIsEnforced() { var p=Plan();p.cargoTypes[0].quantity=1;p.placedCargo.Add(Box("A1",Vector3Int.zero));Assert.IsFalse(GridPlacement.TryPlace(p,p.cargoTypes[0],new Vector3Int(2,0,0),0,out var error));StringAssert.Contains("đủ",error); }
    [Test] public void OrientationRuleIsEnforced() { var p=Plan();p.cargoTypes[0].allowRotation=false;Assert.IsFalse(GridPlacement.TryPlace(p,p.cargoTypes[0],Vector3Int.zero,90,out var error));StringAssert.Contains("hướng",error); }
    [Test] public void NonStackableCargoCannotSupportAnotherBox() { var p=Plan();p.cargoTypes[0].stackable=false;p.placedCargo.Add(Box("A1",Vector3Int.zero));Assert.IsFalse(GridPlacement.TryPlace(p,p.cargoTypes[0],new Vector3Int(0,0,2),0,out var error));StringAssert.Contains("đỡ",error); }
    [Test] public void AssistedPlacementIsDeterministicAndStopsAtQuantity() { var p=Plan();p.cargoTypes[0].quantity=3;Assert.AreEqual(3,PlanIntelligence.AutoFill(p,p.cargoTypes[0]));Assert.AreEqual(new Vector3Int(0,0,0),p.placedCargo[0].position);Assert.AreEqual(new Vector3Int(2,0,0),p.placedCargo[1].position);Assert.AreEqual(0,PlanIntelligence.RemainingQuantity(p)); }
    [Test] public void HealthCheckReportsIncompletePlanAndWeight() { var p=Plan();p.cargoTypes[0].quantity=2;p.placedCargo.Add(Box("A1",Vector3Int.zero));var report=PlanIntelligence.Check(p);Assert.IsTrue(report.IsValid);Assert.AreEqual(1,PlanIntelligence.RemainingQuantity(p));Assert.AreEqual(12.5f,PlanIntelligence.TotalWeight(p));Assert.IsTrue(report.warnings.Exists(x=>x.Contains("Còn 1"))); }
    [Test] public void StatisticsAndReadinessUseOneDeterministicSource() { var p=Plan();p.cargoTypes[0].quantity=2;p.placedCargo.Add(Box("A1",Vector3Int.zero));var stats=PlanIntelligence.Statistics(p);Assert.AreEqual(2,stats.TotalQuantity);Assert.AreEqual(1,stats.RemainingQuantity);Assert.AreEqual(8,stats.UsedCells);Assert.AreEqual(50f,stats.CompletionPercent);Assert.AreEqual(PlanReadiness.Incomplete,PlanIntelligence.Check(p).Readiness); }
    [Test] public void AutoFillRemainingHandlesAllTypesInOneBatch() { var p=Plan();p.container=new ContainerConfig{length=4,width=1,height=1};p.cargoTypes.Clear();p.cargoTypes.Add(new CargoType{id="A",code="A",name="A",quantity=2,color=Color.red});p.cargoTypes.Add(new CargoType{id="B",code="B",name="B",quantity=2,color=Color.blue});Assert.AreEqual(4,PlanIntelligence.AutoFillRemaining(p));Assert.AreEqual(0,PlanIntelligence.RemainingQuantity(p)); }
    [Test] public void FiveHundredUnitAutoPlacementCompletesWithinOperationalBudget() { var p=Plan();p.container=new ContainerConfig{length=20,width=5,height=5};p.cargoTypes[0].length=p.cargoTypes[0].width=p.cargoTypes[0].height=1;p.cargoTypes[0].quantity=500;var timer=Stopwatch.StartNew();Assert.AreEqual(500,PlanIntelligence.AutoFillRemaining(p));timer.Stop();Assert.Less(timer.ElapsedMilliseconds,5000); }
    [Test] public void OccupiedCellsCoverWholeFootprint() { var cells=new System.Collections.Generic.List<Vector3Int>(GridPlacement.OccupiedCells(new Vector3Int(4,2,1),new Vector3Int(2,2,1)));Assert.AreEqual(4,cells.Count);Assert.Contains(new Vector3Int(5,3,1),cells); }
    [Test] public void MovingBoxCanIgnoreItself() { var p=Plan();p.placedCargo.Add(Box("A1",new Vector3Int(4,1,0)));Assert.IsTrue(GridPlacement.TryPlace(p,p.cargoTypes[0],new Vector3Int(5,1,0),0,"A1",out _)); }
    [Test] public void ContainerDimensionsAreStrictlyValidated() { var p=Plan();p.container.length=0;Assert.IsFalse(PlanValidation.TryValidate(p,out _));p.container.length=PlanValidation.MaxDimension+1;Assert.IsFalse(PlanValidation.TryValidate(p,out _)); }
    [Test] public void DuplicateCargoCodesAndIdsAreRejected() { var p=Plan();p.cargoTypes.Add(new CargoType{id="A",code="a",name="Khác",length=1,width=1,height=1,color=Color.blue});Assert.IsFalse(PlanValidation.TryValidate(p,out var error));StringAssert.Contains("ID loại hàng",error); }
    [Test] public void ResizeCannotExcludePlacedCargo() { var p=Plan();p.placedCargo.Add(Box("A1",new Vector3Int(8,1,4)));Assert.IsFalse(PlanValidation.CanResize(p,new Vector3Int(9,3,6),out _));Assert.IsTrue(PlanValidation.CanResize(p,new Vector3Int(10,3,6),out _)); }

    [Test] public void SaveLoadRoundTripKeepsMetadataColorAndPlacement()
    {
        var p=Plan();p.placedCargo.Add(Box("BOX",new Vector3Int(1,1,1)));var path=PlanPersistence.Save(p);var loaded=PlanPersistence.Load(path);
        Assert.AreEqual("ĐH-001",loaded.orderReference);Assert.AreEqual("Công ty Việt",loaded.customerName);Assert.AreEqual(p.orderNotes,loaded.orderNotes);Assert.AreEqual(Color.green,loaded.cargoTypes[0].color);Assert.AreEqual(new Vector3Int(1,1,1),loaded.placedCargo[0].position);
    }

    [Test] public void RenameWritesNewFileBeforeDeletingOld()
    {
        var p=Plan();var oldPath=PlanPersistence.Save(p);p.name="PA-ĐỔI-TÊN";var newPath=PlanPersistence.Save(p,oldPath);Assert.IsFalse(File.Exists(oldPath));Assert.IsTrue(File.Exists(newPath));Assert.AreEqual("PA-ĐỔI-TÊN",PlanPersistence.Load(newPath).name);
    }

    [Test] public void CorruptJsonIsSkippedWithoutHidingValidPlan()
    {
        var valid=PlanPersistence.Save(Plan());Directory.CreateDirectory(PlanPersistence.DirectoryPath);File.WriteAllText(System.IO.Path.Combine(PlanPersistence.DirectoryPath,"broken.json"),"{broken");
        var plans=PlanPersistence.ListValid(out var invalid);Assert.AreEqual(1,plans.Count);Assert.AreEqual(1,invalid.Length);CollectionAssert.Contains(PlanPersistence.ListFiles(),valid);
    }

    [Test] public void ExportImportRoundTripIsLosslessAndAvoidsOverwrite()
    {
        var original=Plan();original.placedCargo.Add(Box("B1",new Vector3Int(2,0,0)));var exported=PlanPersistence.ExportJson(original);
        Assert.IsTrue(PlanPersistence.TryImport(exported,out var imported,out var saved,out var error),error);Assert.AreEqual(original.orderNotes,imported.orderNotes);Assert.AreEqual(original.placedCargo[0].rotation,imported.placedCargo[0].rotation);Assert.IsTrue(File.Exists(saved));
        Assert.IsTrue(PlanPersistence.TryImport(exported,out var second,out _,out error),error);Assert.AreNotEqual(imported.name,second.name);
    }

    [Test] public void FullBackupValidatesBeforeRestoringAndNeverOverwrites()
    {
        var original=Plan();PlanPersistence.Save(original);var backupPath=PlanPersistence.ExportBackup();Assert.IsTrue(PlanPersistence.TryReadBackup(backupPath,out var backup,out var error),error);Assert.AreEqual(1,backup.plans.Count);Assert.AreEqual(1,PlanPersistence.RestoreBackup(backup));Assert.AreEqual(2,PlanPersistence.ListFiles().Length);
    }

    [Test] public void CargoCsvRoundTripPreservesBusinessFields()
    {
        var p=Plan();var csv=PlanPersistence.ExportCargoCsv(p);Assert.IsTrue(PlanPersistence.TryReadCargoCsv(csv,out var types,out var error),error);Assert.AreEqual(1,types.Count);Assert.AreEqual("Sầu riêng",types[0].name);Assert.AreEqual(100,types[0].quantity);Assert.AreEqual(12.5f,types[0].weightPerUnit);Assert.IsTrue(types[0].stackable);
    }

    [Test] public void ImportRejectsMalformedNewSchemaAndMissingReferenceWithoutWriting()
    {
        var source=System.IO.Path.Combine(testRoot,"bad.json");File.WriteAllText(source,"{bad");Assert.IsFalse(PlanPersistence.TryImport(source,out _,out _,out _));Assert.AreEqual(0,PlanPersistence.ListFiles().Length);
        var newer=Plan();newer.schemaVersion=LoadingPlan.CurrentSchemaVersion+1;File.WriteAllText(source,JsonUtility.ToJson(newer));Assert.IsFalse(PlanPersistence.TryImport(source,out _,out _,out var schemaError));StringAssert.Contains("chưa được hỗ trợ",schemaError);
        var missing=Plan();missing.placedCargo.Add(new PlacedCargo{id="B1",cargoTypeId="MISSING",position=Vector3Int.zero,size=Vector3Int.one});File.WriteAllText(source,JsonUtility.ToJson(missing));Assert.IsFalse(PlanPersistence.TryImport(source,out _,out _,out var referenceError));StringAssert.Contains("không tồn tại",referenceError);
    }

    [Test] public void LegacySchemaIsMigratedDeterministically() { var p=Plan();p.schemaVersion=0;Assert.IsTrue(PlanValidation.TryValidate(p,out _));Assert.AreEqual(LoadingPlan.CurrentSchemaVersion,p.schemaVersion); }
    [Test] public void SearchMatchesPlanContainerOrderAndCustomer() { var p=Plan();Assert.IsTrue(PlanPersistence.MatchesSearch(p,"pa-001"));Assert.IsTrue(PlanPersistence.MatchesSearch(p,"40ft"));Assert.IsTrue(PlanPersistence.MatchesSearch(p,"đh-001"));Assert.IsTrue(PlanPersistence.MatchesSearch(p,"việt"));Assert.IsFalse(PlanPersistence.MatchesSearch(p,"missing")); }

    [TestCase("1.0.0","1.0.1",-1)]
    [TestCase("1.0.9","1.1.0",-1)]
    [TestCase("1.9.9","2.0.0",-1)]
    [TestCase("1.10.0","1.9.9",1)]
    [TestCase("1.1.0","v1.1.0",0)]
    public void SemanticVersionsAreComparedNumerically(string current,string latest,int expected) { Assert.IsTrue(AppUpdateService.TryCompareVersions(current,latest,out var result));Assert.AreEqual(expected,result); }

    [Test] public void LatestReleaseSelectsApkAndSanitizesNotes()
    {
        const string json="{\"tag_name\":\"v1.2.0\",\"body\":\"## Cải thiện\\n**Nhanh hơn**\",\"html_url\":\"https://github.com/acme/app/releases/tag/v1.2.0\",\"assets\":[{\"name\":\"ContainerLoading.apk\",\"browser_download_url\":\"https://example.test/app.apk\",\"content_type\":\"application/vnd.android.package-archive\"}]}";
        Assert.IsTrue(AppUpdateService.TryParseLatestRelease(json,"1.1.0",out var info,out var error),error);Assert.IsTrue(info.IsUpdateAvailable);Assert.AreEqual("1.2.0",info.VersionName);Assert.AreEqual("https://example.test/app.apk",info.DownloadUrl);StringAssert.DoesNotContain("#",info.ReleaseNotes);
    }

    [Test] public void UpdateReleaseRejectsMalformedVersionAndMissingApk()
    {
        Assert.IsFalse(AppUpdateService.TryParseLatestRelease("{bad","1.0.0",out _,out _));
        Assert.IsFalse(AppUpdateService.TryParseLatestRelease("{\"tag_name\":\"v1.1.0\",\"assets\":[]}","1.0.0",out _,out var error));StringAssert.Contains("APK",error);
    }

    [TestCase("https://github.com/acme/container-loading/releases")]
    [TestCase("acme/container-loading.git")]
    public void RepositoryInputIsNormalized(string value) { Assert.AreEqual("acme/container-loading",AppUpdateService.NormalizeRepository(value)); }

    [Test] public void PdfContainsUnicodeOrderMetadataNotesAndMultiplePages()
    {
        var p=Plan();p.orderNotes=string.Join("\n",System.Linq.Enumerable.Repeat("Ghi chú tiếng Việt: kiểm tra cửa container và niêm phong an toàn.",180));var path=PdfExportSystem.Export(p);try{var bytes=File.ReadAllBytes(path);var text=Encoding.GetEncoding(28591).GetString(bytes);StringAssert.Contains(Utf16Hex(p.orderReference),text);StringAssert.Contains(Utf16Hex("GHI CHÚ ĐƠN HÀNG"),text);StringAssert.Contains("/Type /Font /Subtype /Type0",text);Assert.Greater(Count(text,"/Type /Page "),2);}finally{File.Delete(path);}
    }

    [Test] public void PdfUsesProductionFilenameAndHandlesFiftyCargoTypes()
    {
        var p=Plan();p.containerNumber="CONT-TEST-01";p.cargoTypes.Clear();for(var i=0;i<50;i++)p.cargoTypes.Add(new CargoType{id="T"+i,code="M"+i,name="Hàng thử nghiệm "+i,quantity=3,color=Color.HSVToRGB(i/50f,.7f,.9f)});PdfExportSystem.OutputDirectoryOverride=testRoot;var path=PdfExportSystem.Export(p);PdfExportSystem.OutputDirectoryOverride=null;StringAssert.StartsWith("LoadingPlan_ĐH-001_CONT-TEST-01_",System.IO.Path.GetFileName(path));Assert.Greater(new FileInfo(path).Length,100000);var text=Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(path));Assert.GreaterOrEqual(Count(text,"/Type /Page "),4);
    }

    static PlacedCargo Box(string id,Vector3Int position)=>new(){id=id,cargoTypeId="A",position=position,size=new Vector3Int(2,2,2),rotation=0};
    static string Utf16Hex(string value){var bytes=Encoding.BigEndianUnicode.GetBytes(value);var result=new StringBuilder();foreach(var b in bytes)result.Append(b.ToString("X2"));return result.ToString();}
    static int Count(string value,string token){int count=0,index=0;while((index=value.IndexOf(token,index,StringComparison.Ordinal))>=0){count++;index+=token.Length;}return count;}
}
