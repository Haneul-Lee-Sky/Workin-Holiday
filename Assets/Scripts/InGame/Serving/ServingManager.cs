using UnityEngine;
using System;

/// <summary>
/// 서빙 매니저 — 스와이프 서빙 및 폐기 처리
/// 특수 손님 보너스(Rich 2배 매출, Kid 콤보+2)를 적용합니다.
/// </summary>
public class ServingManager : MonoBehaviour
{
    public enum ServeDirection { Left, Right, Down, TrashUp }

    [Header("References")]
    [SerializeField] private IceMachine iceMachine;
    [SerializeField] private CustomerManager customerManager;
    [SerializeField] private ScoreManager scoreManager;

    [Header("Economy")]
    [SerializeField] private int baseRevenuePerServe = 500;

    private int totalRevenue = 0;
    private int currentRoundRevenue = 0;

    // 이벤트 — (stageRevenue: 현재 스테이지 수익, totalRevenue: 게임 전체 누적 수익)
    public event Action<int, int> OnRevenueChanged;
    public event Action<ServeDirection> OnServeSuccess;
    public event Action OnTrash;

    // 프로퍼티
    public int TotalRevenue => totalRevenue;
    public int CurrentRoundRevenue => currentRoundRevenue;

    private void Awake()
    {
        if (iceMachine == null) iceMachine = ResolveActiveIceMachine();
        if (customerManager == null) customerManager = FindAnyObjectByType<CustomerManager>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
    }

    private static IceMachine ResolveActiveIceMachine()
    {
        var all = FindObjectsOfType<IceMachine>(true);
        IceMachine fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (fallback == null) fallback = all[i];
            if (all[i].isActiveAndEnabled) return all[i];
        }
        return fallback;
    }

    /// <summary>
    /// 서빙 시도 (MobileInputManager → 스와이프 방향)
    /// </summary>
    public bool TryServe(ServeDirection direction)
    {
        if (iceMachine == null)
        {
            Debug.LogError("[ServingManager] IceMachine 참조가 없습니다!");
            return false;
        }

        if (direction == ServeDirection.TrashUp)
        {
            return DiscardIce();
        }

        if (!iceMachine.IsComplete)
        {
            Debug.Log($"[중앙 스와이프] 서빙 실패 — 빙수가 아직 완성되지 않았습니다. ({iceMachine.CurrentIceTaps}/{iceMachine.MaxIceTaps})");
            return false;
        }

        ShavedIceData iceData = iceMachine.GetCurrentIce();

        FulfillResult result = new FulfillResult();
        if (customerManager != null)
        {
            result = customerManager.TryFulfillOrder(direction, iceData);
        }

        string dirText;
        switch (direction)
        {
            case ServeDirection.Left:  dirText = "좌측"; break;
            case ServeDirection.Right: dirText = "우측"; break;
            case ServeDirection.Down:  dirText = "하단"; break;
            default:                   dirText = "알 수 없음"; break;
        }

        if (result.success)
        {
            // 콤보 증가 (Kid 손님은 +2)
            int comboIncrement = (result.customerType == CustomerType.Kid) ? 2 : 1;
            if (scoreManager != null) scoreManager.OnServeSuccess(comboIncrement);

            // 매출 계산
            int comboBonus = 0;
            if (scoreManager != null)
            {
                int c = scoreManager.CurrentCombo;
                if (c > 0) comboBonus = ((c - 1) / 10) * 10;
            }
            int revenue = baseRevenuePerServe + comboBonus;

            // Rich 손님: 매출 2배
            if (result.customerType == CustomerType.Rich)
                revenue *= 2;

            totalRevenue += revenue;
            currentRoundRevenue += revenue;

            Debug.Log($"[중앙 스와이프] 서빙 성공 → {dirText} {result.customerType} 손님! +{revenue}원 (토핑 {iceData.ToppingCount}개)");
            OnServeSuccess?.Invoke(direction);
            OnRevenueChanged?.Invoke(currentRoundRevenue, totalRevenue);
        }
        else
        {
            // 서빙 실패 (손님 없음 / 주문 불일치) — 항상 빙수 폐기
            if (scoreManager != null) scoreManager.OnServeFailure();
            string reason = result.hadCustomer ? "주문이 다릅니다" : "해당 방향에 손님이 없습니다";
            Debug.Log($"[중앙 스와이프] 서빙 실패 ({dirText}) — {reason}! 빙수 폐기.");
            OnTrash?.Invoke();
        }

        iceMachine.ResetIce();
        return result.success;
    }

    /// <summary>
    /// 현재 빙수 폐기 (상단 스와이프)
    /// </summary>
    private bool DiscardIce()
    {
        if (iceMachine == null) return false;

        if (iceMachine.CurrentIceTaps == 0)
        {
            Debug.Log("[중앙 스와이프] 폐기 실패 — 제조 중인 빙수가 없습니다.");
            return false;
        }

        Debug.Log($"[중앙 스와이프] 빙수 폐기! (얼음 {iceMachine.CurrentIceTaps}/{iceMachine.MaxIceTaps}, 토핑 {iceMachine.GetCurrentIce().ToppingCount}개)");
        iceMachine.ResetIce();
        OnTrash?.Invoke();
        return true;
    }

    /// <summary>
    /// 라운드 수익 초기화 (스테이지 전환 시)
    /// </summary>
    public void ResetRound()
    {
        currentRoundRevenue = 0;
        OnRevenueChanged?.Invoke(0, totalRevenue);
    }
}
