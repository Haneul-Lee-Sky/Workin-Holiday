using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 스테이지 결과창 UI — 퀘스트 클리어 시 표시되는 팝업.
///
/// 사용법(씬 구성 예시):
///   Canvas
///     └─ Panel_Result (기본 비활성, CanvasGroup 자동 부착)
///         ├─ Text_Result_Title       (예: "클리어!")
///         ├─ Text_Result_Revenue     (예: "획득 골드 : 12,500G")
///         ├─ Text_Result_Total       (옵션: 현재 보유 골드)
///         └─ Btn_Confirm             ("확인" 버튼)
///
/// StageManager가 클리어 시점에 <see cref="Show(string, int)"/>를 호출합니다.
/// 버튼을 <c>Btn_Confirm</c>으로 이름 지으면 OnClick이 자동 연결됩니다.
/// </summary>
public class StageResultUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("결과창 전체 패널 (이름 'Panel_Result' 자동 바인딩)")]
    [SerializeField] private GameObject panelResult;

    [Header("Texts (이름 규칙 자동 바인딩)")]
    [SerializeField] private Text titleText;      // Text_Result_Title
    [SerializeField] private Text revenueText;    // Text_Result_Revenue
    [SerializeField] private Text totalGoldText;  // Text_Result_Total

    [Header("Button")]
    [SerializeField] private Button confirmButton; // Btn_Confirm

    [Header("Format")]
    [SerializeField] private string titleFormat   = "{0} 클리어!";
    [SerializeField] private string revenueFormat = "획득 골드 : {0}G";
    [SerializeField] private string totalFormat   = "보유 골드 : {0}G";

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "1_LobbyScene";

    [Header("Fade")]
    [SerializeField, Range(0.05f, 1f)] private float fadeDuration = 0.25f;

    private CanvasGroup cg;
    private bool isShown;
    private bool isLoadingScene;

    // ─────────────────────────────────────
    //  Initialization
    // ─────────────────────────────────────

    private void Awake()
    {
        AutoBind();

        if (panelResult != null)
        {
            cg = panelResult.GetComponent<CanvasGroup>();
            if (cg == null) cg = panelResult.AddComponent<CanvasGroup>();

            // 시작 시 숨김
            cg.alpha = 0f;
            cg.interactable = false;
            cg.blocksRaycasts = false;
            panelResult.SetActive(false);
        }

        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirm);
        }
    }

    private void AutoBind()
    {
        if (panelResult == null)
        {
            GameObject go = GameObject.Find("Panel_Result");
            if (go != null) panelResult = go;
        }

        if (titleText == null)      titleText      = FindText("Text_Result_Title", panelResult);
        if (revenueText == null)    revenueText    = FindText("Text_Result_Revenue", panelResult);
        if (totalGoldText == null)  totalGoldText  = FindText("Text_Result_Total", panelResult);

        if (confirmButton == null)
        {
            Transform t = panelResult != null ? panelResult.transform.Find("Btn_Confirm") : null;
            if (t != null) confirmButton = t.GetComponent<Button>();
            if (confirmButton == null)
            {
                GameObject go = GameObject.Find("Btn_Confirm");
                if (go != null) confirmButton = go.GetComponent<Button>();
            }
        }
    }

    private static Text FindText(string name, GameObject inside)
    {
        if (inside != null)
        {
            Transform t = inside.transform.Find(name);
            if (t != null)
            {
                Text txt = t.GetComponent<Text>();
                if (txt != null) return txt;
            }
        }
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Text>() : null;
    }

    // ─────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────

    /// <summary>
    /// 결과창을 표시하고 번 돈을 DataManager.CurrentGold에 가산합니다.
    /// </summary>
    public void Show(string stageName, int earnedRevenue)
    {
        if (isShown) return;
        isShown = true;

        int earned = Mathf.Max(0, earnedRevenue);

        // 1) 골드 가산 — DataManager가 변화 이벤트를 발행해 다음 씬에서도 동기화됩니다.
        if (DataManager.Instance != null && earned > 0)
        {
            DataManager.Instance.AddGold(earned);
        }

        // 2) 텍스트 갱신
        if (titleText != null)
        {
            titleText.text = string.Format(titleFormat, string.IsNullOrEmpty(stageName) ? "스테이지" : stageName);
        }
        if (revenueText != null)
        {
            revenueText.text = string.Format(revenueFormat, earned.ToString("N0"));
        }
        if (totalGoldText != null)
        {
            int totalGold = DataManager.Instance != null ? DataManager.Instance.CurrentGold : earned;
            totalGoldText.text = string.Format(totalFormat, totalGold.ToString("N0"));
        }

        // 3) 게임 일시정지 + 패널 페이드 인
        Time.timeScale = 0f;
        if (panelResult != null && !panelResult.activeSelf) panelResult.SetActive(true);
        StartCoroutine(CoFadeIn());

        Debug.Log($"[StageResultUI] 결과창 표시 — '{stageName}' 클리어, 획득 {earned:N0}G");
    }

    private IEnumerator CoFadeIn()
    {
        if (cg == null) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            // 일시정지 중이므로 unscaledDeltaTime 사용
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
    }

    /// <summary>확인 버튼 핸들러 — 로비 씬으로 복귀.</summary>
    public void OnConfirm()
    {
        if (isLoadingScene) return;
        isLoadingScene = true;

        if (confirmButton != null) confirmButton.interactable = false;

        // 일시정지 해제 — 씬 전환 시 타임스케일 복원 필수
        Time.timeScale = 1f;

        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogError("[StageResultUI] 로비 씬 이름이 비어있습니다!");
            isLoadingScene = false;
            if (confirmButton != null) confirmButton.interactable = true;
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogError($"[StageResultUI] 씬 '{lobbySceneName}'을 로드할 수 없습니다. Build Settings에 등록되었는지 확인하세요.");
            isLoadingScene = false;
            if (confirmButton != null) confirmButton.interactable = true;
            return;
        }

        Debug.Log($"[StageResultUI] 확인 → {lobbySceneName} 로드");
        SceneManager.LoadScene(lobbySceneName);
    }
}
