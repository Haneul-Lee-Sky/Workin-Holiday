using UnityEngine;

/// <summary>
/// 퀘스트 매니저 — CustomerManager의 손님 스폰 이벤트를 QuestTracker에 중계합니다.
/// 스테이지 데이터는 Assets/Data/Stages/ 폴더의 ScriptableObject가 담당합니다.
///
/// [스테이지별 퀘스트 요약] — 1~10은 퀘스트 클리어, 11+는 제한시간 수익 모드
///  1  : 우측 1회 서빙 성공          (serveRightTarget=1)
///  2  : 우측 3회 서빙 성공          (serveRightTarget=3)
///  3  : 1회 폐기(Trash) 성공        (trashTarget=1)
///  4  : 좌측 1회 서빙              (serveLeftTarget=1, 좌측 스폰만)
///  5  : 좌·우 각각 1회 서빙         (serveLeftTarget=1, serveRightTarget=1)
///  6  : 좌·우 각각 3회 서빙         (serveLeftTarget=3, serveRightTarget=3)
///  7  : 부자(Rich) 손님 하단 1회 서빙 (forcedType=Rich, serveBottomTarget=1)
///  8  : 진상(Annoying) 손님 하단 1회  (forcedType=Annoying, serveBottomTarget=1)
///  9  : 아이(Kid) 손님 하단 1회      (forcedType=Kid, serveBottomTarget=1)
/// 10  : 방향 무관 5콤보 달성         (comboTarget=5)
/// 11+ : 무한 모드 — 제한시간 내 최고 수익 (isInfiniteMode=true, timeLimit=60)
/// </summary>
public class QuestManager : MonoBehaviour
{
    [Header("References (자동 검색됨)")]
    [SerializeField] private CustomerManager customerManager;
    [SerializeField] private QuestTracker questTracker;

    private void Awake()
    {
        if (customerManager == null) customerManager = FindAnyObjectByType<CustomerManager>();
        if (questTracker == null) questTracker = FindAnyObjectByType<QuestTracker>();
    }

    private void Start()
    {
        if (customerManager != null)
            customerManager.OnCustomerSpawned += HandleCustomerSpawned;
    }

    private void OnDestroy()
    {
        if (customerManager != null)
            customerManager.OnCustomerSpawned -= HandleCustomerSpawned;
    }

    /// <summary>
    /// 손님 스폰 이벤트 → 특수 손님이면 QuestTracker에 알림
    /// </summary>
    private void HandleCustomerSpawned(Customer customer)
    {
        if (questTracker != null && customer != null)
            questTracker.OnSpecialGuestSpawned(customer.customerType);
    }
}
