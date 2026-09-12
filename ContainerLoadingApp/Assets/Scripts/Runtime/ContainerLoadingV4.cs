using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public sealed partial class ContainerLoadingApp
{
    CancellationTokenSource optimizationCancellation;
    bool optimizationRunning;
    int sequenceStepIndex = -1;

    void OnDestroy() { optimizationCancellation?.Cancel(); optimizationCancellation?.Dispose(); optimizationCancellation = null; }

    async void GenerateSuggestedScenarios()
    {
        if (optimizationRunning || shipment == null) return;
        if (shipment.cargoTypes.Count == 0 || shipment.containers.Count == 0) { status = "Cần có loại hàng và container trước khi tạo phương án."; RefreshMobileUI(true); return; }
        optimizationRunning = true; optimizationCancellation = new CancellationTokenSource(); status = "Đang tính phương án đề xuất… Bạn có thể hủy bất cứ lúc nào."; RefreshMobileUI(true);
        try
        {
            // JsonUtility stays on the main thread; the worker receives an isolated DTO graph.
            var request = JsonUtility.FromJson<Shipment>(JsonUtility.ToJson(shipment));
            ShipmentIntelligence.Normalize(request);
            var token = optimizationCancellation.Token;
            var result = await Task.Run(() => MultiContainerOptimizer.Generate(request, request.optimizationSettings, token), token);
            if (result.cancelled || token.IsCancellationRequested) status = "Đã hủy tính phương án; sơ đồ hiện tại không thay đổi.";
            else if (!string.IsNullOrWhiteSpace(result.error)) status = result.error;
            else
            {
                ScenarioService.Store(shipment, result); shipmentPath = ShipmentPersistence.Save(shipment, shipmentPath);
                status = $"Đã tạo {result.scenarios.Count} phương án để so sánh; sơ đồ hiện tại chưa bị thay đổi.";
            }
        }
        catch (OperationCanceledException) { status = "Đã hủy tính phương án; sơ đồ hiện tại không thay đổi."; }
        catch (Exception exception) { Debug.LogException(exception); status = "Không thể tạo phương án. Sơ đồ hiện tại vẫn được giữ nguyên."; }
        finally { optimizationRunning = false; optimizationCancellation?.Dispose(); optimizationCancellation = null; if (mobileCanvas) RefreshMobileUI(true); }
    }

    void CancelOptimization() { optimizationCancellation?.Cancel(); status = "Đang hủy…"; RefreshMobileUI(true); }

    void ApplyScenario(OptimizationScenario scenario)
    {
        Confirm("Áp dụng " + scenario.name + "?", "Ứng dụng sẽ lưu một bản khôi phục của sơ đồ hiện tại trước khi áp dụng. Bạn có thể quay lại bản xếp thủ công.", () =>
        {
            if (!ScenarioService.Apply(shipment, scenario)) { status = "Không thể áp dụng phương án."; RefreshMobileUI(true); return; }
            plan = shipment.containers.First(); shipmentPath = ShipmentPersistence.Save(shipment, shipmentPath); selectedId = null; placementMode = false; sequenceStepIndex = -1;
            status = "Đã áp dụng " + scenario.name + ". Bản xếp trước đó vẫn có thể khôi phục."; Rebuild(); RefreshMobileUI(true);
        }, "Áp dụng", "Hủy");
    }

    void RestoreBeforeScenario()
    {
        if (!ScenarioService.Restore(shipment)) { status = "Không còn bản xếp trước khi áp dụng để khôi phục."; RefreshMobileUI(true); return; }
        plan = shipment.containers.First(); shipmentPath = ShipmentPersistence.Save(shipment, shipmentPath); selectedId = null; placementMode = false; sequenceStepIndex = -1;
        status = "Đã khôi phục bản xếp thủ công trước khi áp dụng phương án."; Rebuild(); RefreshMobileUI(true);
    }

    void ToggleSelectedLock()
    {
        var selected = FindPlaced(selectedId); if (selected == null) { status = "Hãy chọn một kiện hàng trước."; RefreshMobileUI(true); return; }
        Snapshot(); selected.locked = !selected.locked; status = selected.locked ? "Đã khóa vị trí; optimizer sẽ giữ kiện này." : "Đã mở khóa vị trí."; QueueAutosave(); Rebuild(true);
    }

    void GenerateLoadingSequence()
    {
        if (!LoadingSequencePlanner.Generate(plan, out var warning)) { status = warning; RefreshMobileUI(true); return; }
        sequenceStepIndex = plan.loadingSequence.Count == 0 ? -1 : 0; QueueAutosave(); ShowSequenceStep();
    }

    void MoveSequenceStep(int delta)
    {
        if (plan.loadingSequence == null || plan.loadingSequence.Count == 0) { GenerateLoadingSequence(); return; }
        sequenceStepIndex = Math.Max(0, Math.Min(plan.loadingSequence.Count - 1, sequenceStepIndex + delta)); ShowSequenceStep();
    }

    void ShowSequenceStep()
    {
        if (sequenceStepIndex < 0 || plan.loadingSequence == null || plan.loadingSequence.Count == 0) { status = "Container chưa có hàng để tạo trình tự."; RefreshMobileUI(true); return; }
        var step = plan.loadingSequence[sequenceStepIndex]; selectedId = step.placementId; placementMode = false;
        status = $"Bước {step.step}/{plan.loadingSequence.Count} · {step.cargoCode} · {step.zone} · {step.note}"; Rebuild(true); FocusSelected();
    }
}
