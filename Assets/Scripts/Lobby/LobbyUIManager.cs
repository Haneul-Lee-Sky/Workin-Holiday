using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 UI 매니저 — [메인 → 모드 선택 → 인게임] 플로우 전담.
///
/// 사용법(씬 구성):
///   Canvas
///     ├─ Text_Gold          (상단 — 현재 보유 골드 실시간 표시)
///     ├─ Panel_Main         (CanvasGroup 자동 부착)
///     │     └─ Btn_ShowModeSelect 등
///     └─ Panel_ModeSelect   (CanvasGroup 자동 부착)
///           ├─ Btn_Normal
///           ├─ Btn_Night
///           ├─ Btn_Back
///           └─ Text_Message (옵션: 잔액 부족 안내용)
///
/// 인스펙터에 패널을 드래그하거나, 이름 규칙을 따르면 자동 바인딩됩니다.
/// 버튼 OnClick에는 아래 public 메서드를 연결하세요:
///   - ShowModeSelect()
///   - BackToMain()
///   - EnterNormalMode()
///   - EnterNightMode()
/// </summary>
public class LobbyUIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject panelMain;
    [SerializeField] private GameObject panelModeSelect;

    [Header("Optional Buttons (자동 바인딩 지원)")]
    [SerializeField] private Button btnShowModeSelect;   // Panel_Main 안의 '시작/모드 선택' 버튼
    [Tooltip("주간 모드 버튼 (Btn_NormalGame 또는 Btn_Normal 자동 바인딩)")]
    [SerializeField] private Button btnNormalMode;
    [Tooltip("야간 모드 버튼 (Btn_NightGame 또는 Btn_Night 자동 바인딩)")]
    [SerializeField] private Button btnNightMode;
    [Tooltip("스페셜 모드 버튼 (Btn_SpecialGame 자동 바인딩, 현재는 피드백만 적용)")]
    [SerializeField] private Button btnSpecialMode;

    [Tooltip("Panel_ModeSelect 바깥에 독립 배치된 뒤로가기 버튼 (Btn_Back 자동 바인딩). Panel_ModeSelect와 함께 표시/숨김됩니다.")]
    [SerializeField] private Button btnBack;

    [Header("Optional UI")]
    [Tooltip("화면 상단에 보유 골드를 실시간 표시할 Text (이름 'Text_Gold' 자동 바인딩)")]
    [SerializeField] private Text goldText;
    [Tooltip("골드 표시 포맷. {0}에 현재 골드 숫자가 들어갑니다.")]
    [SerializeField] private string goldFormat = "보유 골드 : {0}G";
    [Tooltip("잔액 부족 등 안내 메시지를 표시할 Text. 없으면 콘솔 로그만 출력합니다.")]
    [SerializeField] private Text messageText;

    [Header("Scene Names (빌드 세팅에 등록돼 있어야 합니다)")]
    [SerializeField] private string normalSceneName = "2_NormalGameScene";
    [SerializeField] private string nightSceneName  = "2-1_NightGameScene";

    [Header("Economy")]
    [Tooltip("야간 모드 입장료(골드)")]
    [SerializeField] private int nightModeEntryCost = 50000;

    [Header("Fade")]
    [Tooltip("패널 페이드 전환 시간(초)")]
    [SerializeField, Range(0.05f, 1f)] private float fadeDuration = 0.2f;
    [Tooltip("메시지 표시 유지 시간(초)")]
    [SerializeField, Range(0.5f, 4f)]  private float messageHoldDuration = 1.5f;

    private CanvasGroup cgMain;
    private CanvasGroup cgModeSelect;
    private CanvasGroup cgBack;      // Btn_Back 전용 — Panel_ModeSelect와 동기화
    private CanvasGroup cgMessage;

    private Coroutine activeFade;
    private Coroutine activeMessage;

    // ─────────────────────────────────────
    //  Initialization
    // ─────────────────────────────────────

    private void Awake()
    {
        AutoBindPanels();
        AutoBindButtons();

        cgMain       = EnsureCanvasGroup(panelMain);
        cgModeSelect = EnsureCanvasGroup(panelModeSelect);
        cgMessage    = messageText != null ? EnsureCanvasGroup(messageText.gameObject) : null;
        cgBack       = btnBack != null ? EnsureCanvasGroup(btnBack.gameObject) : null;

        // 초기 상태: 메인만 표시, 모드선택/뒤로가기/메시지 숨김
        SetPanelInstant(cgMain, panelMain, true);
        SetPanelInstant(cgModeSelect, panelModeSelect, false);
        if (cgBack != null)    SetPanelInstant(cgBack, btnBack.gameObject, false);
        if (cgMessage != null) SetPanelInstant(cgMessage, messageText.gameObject, false);

        WireButtonEvents();
    }

    private void Start()
    {
        // DataManager 구독 — 골드 실시간 표시
        DataManager data = DataManager.Instance;
        if (data != null)
        {
            data.OnGoldChanged += HandleGoldChanged;
            RefreshGoldText(data.CurrentGold);
        }
        else
        {
            Debug.LogWarning("[LobbyUIManager] DataManager.Instance가 없습니다. 씬에 DataManager를 배치하세요.");
            RefreshGoldText(0);
        }
    }

    private void OnDestroy()
    {
        if (DataManager.Instance != null)
        {
            DataManager.Instance.OnGoldChanged -= HandleGoldChanged;
        }
    }

    private void HandleGoldChanged(int currentGold, int delta)
    {
        RefreshGoldText(currentGold);
    }

    private void RefreshGoldText(int value)
    {
        if (goldText == null) return;
        goldText.text = string.Format(goldFormat, value.ToString("N0"));
    }

    private void AutoBindPanels()
    {
        if (panelMain == null)       panelMain       = GameObject.Find("Panel_Main");
        if (panelModeSelect == null) panelModeSelect = GameObject.Find("Panel_ModeSelect");
    }

    private void AutoBindButtons()
    {
        if (btnShowModeSelect == null) btnShowModeSelect = FindButton("Btn_ShowModeSelect", panelMain);

        // 씬에 따라 Btn_NormalGame 또는 Btn_Normal 두 이름을 모두 허용합니다.
        if (btnNormalMode == null) btnNormalMode =
            FindButton("Btn_NormalGame", panelModeSelect) ??
            FindButton("Btn_Normal",     panelModeSelect);

        // 씬에 따라 Btn_NightGame 또는 Btn_Night 두 이름을 모두 허용합니다.
        if (btnNightMode == null) btnNightMode =
            FindButton("Btn_NightGame", panelModeSelect) ??
            FindButton("Btn_Night",     panelModeSelect);

        // 스페셜 모드 버튼 (OnClick 미연결 — 피드백만 적용)
        if (btnSpecialMode == null) btnSpecialMode = FindButton("Btn_SpecialGame", panelModeSelect);

        // Btn_Back은 Panel_ModeSelect 안팎 어디든 찾습니다.
        if (btnBack == null) btnBack = FindButton("Btn_Back", panelModeSelect);

        if (goldText == null)
        {
            GameObject go = GameObject.Find("Text_Gold");
            if (go != null) goldText = go.GetComponent<Text>();
        }

        if (messageText == null)
        {
            GameObject go = GameObject.Find("Text_Message");
            if (go != null) messageText = go.GetComponent<Text>();
        }
    }

    private static Button FindButton(string name, GameObject inside)
    {
        if (inside != null)
        {
            Transform t = inside.transform.Find(name);
            if (t != null)
            {
                Button b = t.GetComponent<Button>();
                if (b != null) return b;
            }
        }
        GameObject go = GameObject.Find(name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private void WireButtonEvents()
    {
        if (btnShowModeSelect != null) btnShowModeSelect.onClick.AddListener(ShowModeSelect);
        if (btnBack != null)           btnBack.onClick.AddListener(BackToMain);
        if (btnNormalMode != null)     btnNormalMode.onClick.AddListener(EnterNormalMode);
        if (btnNightMode != null)      btnNightMode.onClick.AddListener(EnterNightMode);

        // 모든 로비 버튼에 누름 피드백 자동 부착
        ButtonPressFeedback.EnsureOn(btnShowModeSelect);
        ButtonPressFeedback.EnsureOn(btnBack);
        ButtonPressFeedback.EnsureOn(btnNormalMode);
        ButtonPressFeedback.EnsureOn(btnNightMode);
        ButtonPressFeedback.EnsureOn(btnSpecialMode);
    }

    // ─────────────────────────────────────
    //  Public API (버튼 OnClick에 연결)
    // ─────────────────────────────────────

    /// <summary>메인 → 모드 선택 화면으로 전환 (살짝 페이드). Btn_Back도 함께 표시됩니다.</summary>
    public void ShowModeSelect()
    {
        CrossFade(cgMain, panelMain, false, cgModeSelect, panelModeSelect, true, cgBack, btnBack != null ? btnBack.gameObject : null, true);
    }

    /// <summary>모드 선택 → 메인 화면으로 복귀 (살짝 페이드). Btn_Back도 함께 숨겨집니다.</summary>
    public void BackToMain()
    {
        CrossFade(cgModeSelect, panelModeSelect, false, cgMain, panelMain, true, cgBack, btnBack != null ? btnBack.gameObject : null, false);
    }

    /// <summary>주간(노말) 모드로 진입 — 입장료 없음.</summary>
    public void EnterNormalMode()
    {
        Debug.Log("[LobbyUIManager] 주간 모드 진입");
        LoadGameScene(normalSceneName);
    }

    /// <summary>야간 모드로 진입 — nightModeEntryCost(기본 50000G) 소모. 부족 시 "재화 부족" 로그 후 이동하지 않음.</summary>
    public void EnterNightMode()
    {
        DataManager data = DataManager.Instance;
        if (data == null)
        {
            Debug.LogWarning("[LobbyUIManager] DataManager.Instance가 없습니다. 씬에 DataManager가 있는지 확인하세요.");
            ShowMessage("데이터 매니저를 찾을 수 없습니다.");
            return;
        }

        if (!data.HasGold(nightModeEntryCost))
        {
            Debug.Log($"[LobbyUIManager] 재화 부족 — 필요 {nightModeEntryCost:N0}G, 보유 {data.CurrentGold:N0}G");
            ShowMessage("재화가 부족합니다");
            return;
        }

        data.SpendGold(nightModeEntryCost);
        Debug.Log($"[LobbyUIManager] 야간 모드 진입 — {nightModeEntryCost:N0}G 소모, 잔액 {data.CurrentGold:N0}G");
        LoadGameScene(nightSceneName);
    }

    // ─────────────────────────────────────
    //  Fade Logic
    // ─────────────────────────────────────

    private void CrossFade(
        CanvasGroup cgFrom, GameObject goFrom, bool showFrom,
        CanvasGroup cgTo,   GameObject goTo,   bool showTo,
        CanvasGroup cgExtra = null, GameObject goExtra = null, bool showExtra = false)
    {
        if (activeFade != null) StopCoroutine(activeFade);
        activeFade = StartCoroutine(CoCrossFade(cgFrom, goFrom, showFrom, cgTo, goTo, showTo, cgExtra, goExtra, showExtra));
    }

    private IEnumerator CoCrossFade(
        CanvasGroup cgFrom, GameObject goFrom, bool showFrom,
        CanvasGroup cgTo,   GameObject goTo,   bool showTo,
        CanvasGroup cgExtra, GameObject goExtra, bool showExtra)
    {
        // 대상 패널(To) 활성화 — 알파 0에서 시작
        if (cgTo != null && goTo != null)
        {
            if (!goTo.activeSelf) goTo.SetActive(true);
            cgTo.alpha = showTo ? 0f : 1f;
            cgTo.interactable = false;
            cgTo.blocksRaycasts = false;
        }
        if (cgFrom != null)
        {
            cgFrom.interactable = false;
            cgFrom.blocksRaycasts = false;
        }

        // Extra(Btn_Back) 처리 — showExtra 방향으로 함께 페이드
        if (cgExtra != null && goExtra != null)
        {
            if (showExtra && !goExtra.activeSelf) goExtra.SetActive(true);
            cgExtra.alpha = showExtra ? 0f : 1f;
            cgExtra.interactable = false;
            cgExtra.blocksRaycasts = false;
        }

        float t = 0f;
        float startFrom  = cgFrom  != null ? cgFrom.alpha  : 0f;
        float startTo    = cgTo    != null ? cgTo.alpha    : 0f;
        float startExtra = cgExtra != null ? cgExtra.alpha : 0f;
        float endFrom    = showFrom  ? 1f : 0f;
        float endTo      = showTo    ? 1f : 0f;
        float endExtra   = showExtra ? 1f : 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float eased = EaseOutCubic(Mathf.Clamp01(t / fadeDuration));

            if (cgFrom  != null) cgFrom.alpha  = Mathf.Lerp(startFrom,  endFrom,  eased);
            if (cgTo    != null) cgTo.alpha    = Mathf.Lerp(startTo,    endTo,    eased);
            if (cgExtra != null) cgExtra.alpha = Mathf.Lerp(startExtra, endExtra, eased);
            yield return null;
        }

        SetPanelInstant(cgFrom,  goFrom,  showFrom);
        SetPanelInstant(cgTo,    goTo,    showTo);
        if (cgExtra != null) SetPanelInstant(cgExtra, goExtra, showExtra);
        activeFade = null;
    }

    private static void SetPanelInstant(CanvasGroup cg, GameObject go, bool visible)
    {
        if (cg != null)
        {
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
        }
        if (go != null && go.activeSelf != visible)
        {
            go.SetActive(visible);
        }
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        if (go == null) return null;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    // ─────────────────────────────────────
    //  Message Helper
    // ─────────────────────────────────────

    private void ShowMessage(string msg)
    {
        if (messageText == null)
        {
            Debug.Log($"[LobbyUIManager] {msg}");
            return;
        }

        messageText.text = msg;
        if (activeMessage != null) StopCoroutine(activeMessage);
        activeMessage = StartCoroutine(CoShowMessage());
    }

    private IEnumerator CoShowMessage()
    {
        GameObject go = messageText.gameObject;
        if (!go.activeSelf) go.SetActive(true);

        // Fade in
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cgMessage.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        cgMessage.alpha = 1f;

        yield return new WaitForSecondsRealtime(messageHoldDuration);

        // Fade out
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            cgMessage.alpha = 1f - Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        cgMessage.alpha = 0f;
        go.SetActive(false);
        activeMessage = null;
    }

    // ─────────────────────────────────────
    //  Scene Loading
    // ─────────────────────────────────────

    private void LoadGameScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[LobbyUIManager] 씬 이름이 비어있습니다!");
            return;
        }

        // 입력 차단 — 전환 중 중복 클릭 방지
        if (cgMain != null)       { cgMain.interactable = false;       cgMain.blocksRaycasts = false; }
        if (cgModeSelect != null) { cgModeSelect.interactable = false; cgModeSelect.blocksRaycasts = false; }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[LobbyUIManager] 씬 '{sceneName}'을 로드할 수 없습니다. Build Settings에 등록되었는지 확인하세요.");
            ShowMessage("씬을 불러올 수 없습니다");
            if (cgMain != null)       { cgMain.interactable = true;       cgMain.blocksRaycasts = true; }
            if (cgModeSelect != null) { cgModeSelect.interactable = true; cgModeSelect.blocksRaycasts = true; }
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
