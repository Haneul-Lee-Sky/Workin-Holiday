using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 퀘스트 조건 추적기 — 방향별 서빙, 폐기, 콤보, 특수 손님 등장, 전체 서빙을 추적하고 클리어 판정
/// </summary>
public class QuestTracker : MonoBehaviour
{
    private StageData currentStage;
    private bool isCleared = false;

    private int serveLeftCount;
    private int serveRightCount;
    private int serveBottomCount;
    private int trashCount;
    private int currentCombo;
    private int specialGuestCount;  // 특수 손님 등장 횟수 (Rich/Annoying/Kid)

    public event Action OnQuestCleared;
    public event Action<string> OnProgressChanged;

    public void Initialize(StageData data)
    {
        currentStage = data;
        isCleared = false;
        serveLeftCount = 0;
        serveRightCount = 0;
        serveBottomCount = 0;
        trashCount = 0;
        currentCombo = 0;
        specialGuestCount = 0;
        UpdateProgressUI();
    }

    public void OnServeSuccess(ServingManager.ServeDirection direction)
    {
        if (isCleared || currentStage == null || currentStage.isInfiniteMode) return;

        switch (direction)
        {
            case ServingManager.ServeDirection.Left:  serveLeftCount++;   break;
            case ServingManager.ServeDirection.Right: serveRightCount++;  break;
            case ServingManager.ServeDirection.Down:  serveBottomCount++; break;
        }

        UpdateProgressUI();
        TryClear();
    }

    public void OnTrash()
    {
        if (isCleared || currentStage == null || currentStage.isInfiniteMode) return;
        trashCount++;
        UpdateProgressUI();
        TryClear();
    }

    public void OnComboChanged(int combo)
    {
        if (isCleared || currentStage == null || currentStage.isInfiniteMode) return;
        currentCombo = combo;
        UpdateProgressUI();
        TryClear();
    }

    /// <summary>
    /// 특수 손님(Rich/Annoying/Kid)이 등장했을 때 QuestManager에서 호출
    /// </summary>
    public void OnSpecialGuestSpawned(CustomerType type)
    {
        if (isCleared || currentStage == null || currentStage.isInfiniteMode) return;
        if (type == CustomerType.Normal) return;

        specialGuestCount++;
        Debug.Log($"[QuestTracker] 특수 손님 등장 카운트: {specialGuestCount}/{currentStage.specialGuestTarget} ({type})");
        UpdateProgressUI();
        TryClear();
    }

    private void TryClear()
    {
        if (isCleared) return;
        if (currentStage == null || !currentStage.HasAnyQuestCondition()) return;

        // 방향별 서빙 조건
        if (currentStage.serveRightTarget > 0 && serveRightCount < currentStage.serveRightTarget) return;
        if (currentStage.serveLeftTarget > 0 && serveLeftCount < currentStage.serveLeftTarget) return;
        if (currentStage.serveBottomTarget > 0 && serveBottomCount < currentStage.serveBottomTarget) return;

        // 폐기 조건
        if (currentStage.trashTarget > 0 && trashCount < currentStage.trashTarget) return;

        // 콤보 조건
        if (currentStage.comboTarget > 0 && currentCombo < currentStage.comboTarget) return;

        // 특수 손님 등장 조건
        if (currentStage.specialGuestTarget > 0 && specialGuestCount < currentStage.specialGuestTarget) return;

        // 전체 서빙 조건 (방향 무관)
        int totalServed = serveLeftCount + serveRightCount + serveBottomCount;
        if (currentStage.serveTotalTarget > 0 && totalServed < currentStage.serveTotalTarget) return;

        isCleared = true;
        Debug.Log($"[QuestTracker] ★ 퀘스트 클리어! '{currentStage.stageName}'");
        OnQuestCleared?.Invoke();
    }

    /// <summary>현재 퀘스트 진행도 텍스트를 즉시 재발행합니다 (StageUIController 등 외부에서 호출 가능)</summary>
    public void RefreshProgressUI() => UpdateProgressUI();

    private void UpdateProgressUI()
    {
        if (currentStage == null) return;

        List<string> parts = new List<string>();

        if (currentStage.serveRightTarget > 0)
            parts.Add($"우측 서빙 {serveRightCount}/{currentStage.serveRightTarget}");
        if (currentStage.serveLeftTarget > 0)
            parts.Add($"좌측 서빙 {serveLeftCount}/{currentStage.serveLeftTarget}");
        if (currentStage.serveBottomTarget > 0)
            parts.Add($"하단 서빙 {serveBottomCount}/{currentStage.serveBottomTarget}");
        if (currentStage.trashTarget > 0)
            parts.Add($"폐기 {trashCount}/{currentStage.trashTarget}");
        if (currentStage.comboTarget > 0)
            parts.Add($"콤보 {currentCombo}/{currentStage.comboTarget}");
        if (currentStage.specialGuestTarget > 0)
            parts.Add($"특수 손님 {specialGuestCount}/{currentStage.specialGuestTarget}");
        if (currentStage.serveTotalTarget > 0)
        {
            int total = serveLeftCount + serveRightCount + serveBottomCount;
            parts.Add($"서빙 {total}/{currentStage.serveTotalTarget}");
        }

        OnProgressChanged?.Invoke(string.Join(" | ", parts));
    }
}
