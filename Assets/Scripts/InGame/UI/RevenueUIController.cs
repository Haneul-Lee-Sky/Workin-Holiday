using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 수익 HUD 컨트롤러 — Text_Revenue 한 오브젝트에 두 줄로 표시합니다.
///   윗 줄: 누적 수익 (게임 시작 후 전체 합계)
///   아랫줄: 현재 수익 (이번 스테이지에서 번 금액)
///
/// 숫자가 바뀔 때마다 슬롯머신 Ease-Out 롤업 애니메이션을 동시에 적용합니다.
/// 두 값을 Update()에서 함께 처리해 하나의 text.text 호출로 깔끔하게 업데이트합니다.
/// </summary>
public class RevenueUIController : MonoBehaviour
{
    [Header("UI Reference (비워두면 'Text_Revenue' 이름으로 자동 검색)")]
    [SerializeField] private Text revenueText;

    [Header("Animation")]
    [SerializeField] [Range(0.05f, 1.5f)] private float rollDuration = 0.45f;

    [Header("References (비워두면 자동 검색)")]
    [SerializeField] private ServingManager servingManager;

    // 롤업 상태 — 누적 수익
    private float displayTotal;
    private int   fromTotal;
    private int   targetTotal;
    private float timerTotal;

    // 롤업 상태 — 현재 스테이지 수익
    private float displayStage;
    private int   fromStage;
    private int   targetStage;
    private float timerStage;

    private void Awake()
    {
        if (servingManager == null)
            servingManager = FindAnyObjectByType<ServingManager>();

        if (revenueText == null)
        {
            var go = GameObject.Find("Text_Revenue");
            if (go != null) revenueText = go.GetComponent<Text>();
        }

        // 초기값
        displayTotal = 0; fromTotal = 0; targetTotal = 0; timerTotal = rollDuration;
        displayStage = 0; fromStage = 0; targetStage = 0; timerStage = rollDuration;
        RefreshText();
    }

    private void Start()
    {
        if (servingManager != null)
            servingManager.OnRevenueChanged += HandleRevenueChanged;
        else
            Debug.LogWarning("[RevenueUIController] ServingManager를 찾을 수 없습니다!");
    }

    private void OnDestroy()
    {
        if (servingManager != null)
            servingManager.OnRevenueChanged -= HandleRevenueChanged;
    }

    private void HandleRevenueChanged(int stageRevenue, int totalRevenue)
    {
        // 누적 수익 롤업 시작
        fromTotal   = Mathf.RoundToInt(displayTotal);
        targetTotal = totalRevenue;
        timerTotal  = 0f;

        // 현재 스테이지 수익 롤업 시작
        fromStage   = Mathf.RoundToInt(displayStage);
        targetStage = stageRevenue;
        timerStage  = 0f;
    }

    private void Update()
    {
        bool dirty = false;

        if (timerTotal < rollDuration)
        {
            timerTotal  += Time.unscaledDeltaTime;
            float t      = EaseOutCubic(Mathf.Clamp01(timerTotal / rollDuration));
            displayTotal = Mathf.Lerp(fromTotal, targetTotal, t);
            dirty        = true;
        }

        if (timerStage < rollDuration)
        {
            timerStage  += Time.unscaledDeltaTime;
            float t      = EaseOutCubic(Mathf.Clamp01(timerStage / rollDuration));
            displayStage = Mathf.Lerp(fromStage, targetStage, t);
            dirty        = true;
        }

        if (dirty) RefreshText();
    }

    private void RefreshText()
    {
        if (revenueText == null) return;
        revenueText.text =
            "누적 수익 " + Fmt(Mathf.RoundToInt(displayTotal)) + "\n" +
            "현재 수익 " + Fmt(Mathf.RoundToInt(displayStage));
    }

    /// <summary>Ease-Out Cubic — 처음엔 빠르고 끝에서 부드럽게 멈춤</summary>
    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    /// <summary>#,###원 형식 (예: 1,500원)</summary>
    private static string Fmt(int value) => value.ToString("#,0") + "원";
}
