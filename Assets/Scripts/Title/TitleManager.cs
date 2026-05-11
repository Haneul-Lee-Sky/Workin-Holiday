using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 0_Title 씬 전용 매니저.
///   - 시작 버튼을 누르면 1_LobbyScene으로 이동합니다.
///   - targetText(TextMeshProUGUI)의 알파값만 0.5초 페이드인 → 0.5초 페이드아웃으로 반복합니다.
///     버튼 이미지 등 다른 Graphic에는 일절 영향을 주지 않습니다.
/// </summary>
public class TitleManager : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "1_LobbyScene";

    [Header("Start Button (이름이 'Btn_Start'면 자동 바인딩)")]
    [SerializeField] private Button startButton;

    [Header("Blinking Text")]
    [Tooltip("깜빡일 TextMeshProUGUI. 인스펙터에서 직접 연결하거나, Btn_Start의 자식 TMP를 자동 탐색합니다.")]
    public TextMeshProUGUI targetText;
    [Tooltip("알파 0 → 1까지 올라가는 데 걸리는 시간(초)")]
    [SerializeField, Range(0.1f, 2f)] private float fadeInDuration  = 0.5f;
    [Tooltip("알파 1 → 0까지 내려가는 데 걸리는 시간(초)")]
    [SerializeField, Range(0.1f, 2f)] private float fadeOutDuration = 0.5f;

    private bool isLoadingScene;
    private Coroutine blinkRoutine;

    // ─────────────────────────────────────
    //  Lifecycle
    // ─────────────────────────────────────

    private void Awake()
    {
        AutoBindReferences();

        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
        }
        else
        {
            Debug.LogWarning("[TitleManager] startButton이 연결되지 않았습니다. 'Btn_Start' 이름의 Button을 배치하거나 인스펙터에 드래그하세요.");
        }
    }

    private void OnEnable()
    {
        if (targetText != null)
        {
            blinkRoutine = StartCoroutine(CoBlink());
        }
    }

    private void OnDisable()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        // 비활성 시 알파를 1로 복원
        SetTextAlpha(1f);
    }

    private void AutoBindReferences()
    {
        if (startButton == null)
        {
            GameObject go = GameObject.Find("Btn_Start");
            if (go != null) startButton = go.GetComponent<Button>();
        }

        // 인스펙터에 직접 연결되지 않았을 때만 자동 탐색
        if (targetText == null && startButton != null)
        {
            // Btn_Start의 자식 중 TextMeshProUGUI를 찾습니다.
            targetText = startButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (targetText == null)
        {
            Debug.LogWarning("[TitleManager] targetText(TextMeshProUGUI)를 찾지 못했습니다. 인스펙터에서 직접 연결해 주세요.");
        }
    }

    // ─────────────────────────────────────
    //  Public API
    // ─────────────────────────────────────

    /// <summary>시작 버튼 핸들러 — 로비 씬으로 전환.</summary>
    public void StartGame()
    {
        if (isLoadingScene) return;
        isLoadingScene = true;

        if (startButton != null) startButton.interactable = false;

        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogError("[TitleManager] lobbySceneName이 비어있습니다!");
            ResetLoadingState();
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogError($"[TitleManager] 씬 '{lobbySceneName}'을 로드할 수 없습니다. Build Settings에 등록되었는지 확인하세요.");
            ResetLoadingState();
            return;
        }

        Debug.Log($"[TitleManager] 시작 버튼 → {lobbySceneName} 로드");
        SceneManager.LoadScene(lobbySceneName);
    }

    private void ResetLoadingState()
    {
        isLoadingScene = false;
        if (startButton != null) startButton.interactable = true;
    }

    // ─────────────────────────────────────
    //  Blinking Effect
    // ─────────────────────────────────────

    /// <summary>0.5초 페이드인 → 0.5초 페이드아웃을 무한 반복합니다.</summary>
    private IEnumerator CoBlink()
    {
        while (true)
        {
            // 페이드 인: alpha 0 → 1
            yield return CoFade(0f, 1f, fadeInDuration);

            // 페이드 아웃: alpha 1 → 0
            yield return CoFade(1f, 0f, fadeOutDuration);
        }
    }

    private IEnumerator CoFade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            SetTextAlpha(alpha);
            yield return null;
        }
        SetTextAlpha(to);
    }

    private void SetTextAlpha(float alpha)
    {
        if (targetText == null) return;
        Color c = targetText.color;
        c.a = alpha;
        targetText.color = c;
    }
}
