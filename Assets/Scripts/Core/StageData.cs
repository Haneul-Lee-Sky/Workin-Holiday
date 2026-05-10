using UnityEngine;

/// <summary>
/// 스테이지 데이터 — 각 스테이지의 스폰 규칙과 퀘스트 조건을 정의합니다.
/// </summary>
[CreateAssetMenu(fileName = "Stage_New", menuName = "WorkinHoliday/StageData")]
public class StageData : ScriptableObject
{
    [Header("기본 정보")]
    public string stageName;

    [Header("스폰 규칙")]
    public bool allowLeft;
    public bool allowRight;
    public bool allowBottom;
    public CustomerType forcedCustomerType = CustomerType.Normal;
    public bool randomizeCustomerType = false;
    public float spawnInterval = 3f;

    [Header("퀘스트 조건 (0 = 해당 조건 없음)")]
    public int serveLeftTarget;
    public int serveRightTarget;
    public int serveBottomTarget;
    public int trashTarget;
    public int comboTarget;
    public int specialGuestTarget;  // 특수 손님(부자/진상/아이) 등장 목표 횟수
    public int serveTotalTarget;    // 방향 무관 전체 서빙 목표 횟수

    [Header("특수 손님 강제 스폰 (Stage 7~9)")]
    public bool forceSpecialOnAllSlots = false; // true → 모든 슬롯에 forcedCustomerType 강제 스폰

    [Header("무한 모드 (Stage 11+)")]
    public bool isInfiniteMode = false;
    public float timeLimit = 60f;

    /// <summary>
    /// 퀘스트 조건이 하나라도 설정되어 있는지 확인
    /// </summary>
    public bool HasAnyQuestCondition()
    {
        return serveLeftTarget > 0 || serveRightTarget > 0 || serveBottomTarget > 0
            || trashTarget > 0 || comboTarget > 0
            || specialGuestTarget > 0 || serveTotalTarget > 0;
    }
}
