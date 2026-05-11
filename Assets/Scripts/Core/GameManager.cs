using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 게임 매니저 — 타이머 및 게임 상태 관리 전담
/// 퀘스트 모드에서는 타이머 비활성화, 무한 모드에서는 활성화됩니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform timerFillRect;
    public Image timerFillImage;
    public Text timerText;

    [Header("Game Settings")]
    public float maxGameTime = 60f;
    private float currentGameTime;
    private bool isGameActive = false;
    private bool isTimerEnabled = false;

    [Header("Timer Warning")]
    [SerializeField] private float yellowThreshold = 30f;  // 이 초부터 노란색
    [SerializeField] private float redThreshold    = 10f;  // 이 초부터 빨간색
    [SerializeField] private float blinkThreshold  = 3f;   // 이 초부터 깜빡임
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color yellowColor  = new Color(1f, 0.92f, 0.02f, 1f); // 노랑
    [SerializeField] private Color redColor     = new Color(1f, 0.15f, 0.10f, 1f); // 빨강
    [SerializeField] private float blinkSpeed = 5f;

    // 이벤트
    public event Action OnGameTimeUp;

    // 공개 프로퍼티
    public bool IsGameActive => isGameActive;
    public float ElapsedGameTime => maxGameTime - currentGameTime;

    private void Awake()
    {
        // 타이머 Fill 자동 바인딩
        GameObject timerFillGO = GameObject.Find("Image_TimerFill");
        if (timerFillGO != null)
        {
            timerFillImage = timerFillGO.GetComponent<Image>();
            timerFillRect = timerFillGO.GetComponent<RectTransform>();
        }

        // EventSystem 확인
        if (UnityEngine.EventSystems.EventSystem.current == null && FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            Debug.LogWarning("[시스템] 씬에 EventSystem이 없습니다! UI 클릭이 작동하지 않습니다.");
        }

        // 타이머 텍스트 자동 생성
        GameObject timerTextGO = GameObject.Find("Text_TimePlaceholder");
        if (timerTextGO == null && timerFillRect != null)
        {
            timerTextGO = new GameObject("Text_TimePlaceholder");
            timerTextGO.transform.SetParent(timerFillRect.parent, false);
            RectTransform txtRect = timerTextGO.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;
            timerText = timerTextGO.AddComponent<Text>();
            timerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            timerText.fontSize = 40;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.color = Color.white;
        }
        else if (timerTextGO != null)
        {
            timerText = timerTextGO.GetComponent<Text>();
        }
    }

    // Start()에서 자동 시작 제거 — StageManager가 제어합니다.

    /// <summary>
    /// 제한 시간 설정 (StageManager에서 호출)
    /// </summary>
    public void SetTimeLimit(float time)
    {
        maxGameTime = time;
    }

    /// <summary>
    /// 게임 시작 (enableTimer=false → 퀘스트 모드: 타이머 고정 표시, true → 무한 모드: 카운트다운)
    /// </summary>
    public void StartGame(bool enableTimer = true)
    {
        Time.timeScale = 1f;
        isTimerEnabled = enableTimer;

        // 퀘스트/무한 모드 공통: 초기 시간 설정 및 UI 표시
        currentGameTime = maxGameTime;
        if (timerFillImage != null)
        {
            timerFillImage.gameObject.SetActive(true);
            timerFillImage.fillAmount = 1f;
            timerFillImage.color = defaultColor;
        }

        // 초기 시간 텍스트 표시 (퀘스트 모드에서는 이 값이 고정 유지됨)
        ShowTimerText(currentGameTime);

        isGameActive = true;
        Debug.Log($"[시스템] 게임 시작! 타이머: {(enableTimer ? maxGameTime + "초 카운트다운" : "고정 표시 (퀘스트 모드)")}");
    }

    /// <summary>
    /// 타이머 텍스트를 MM:SS 형식으로 업데이트
    /// </summary>
    private void ShowTimerText(float time)
    {
        if (timerText == null) return;
        int sec = Mathf.CeilToInt(time);
        timerText.text = string.Format("{0:D2}:{1:D2}", sec / 60, sec % 60);
    }

    private void Update()
    {
        if (isGameActive && isTimerEnabled)
        {
            currentGameTime -= Time.deltaTime;
            UpdateTimerVisuals();

            if (currentGameTime <= 0)
            {
                EndGame();
            }
        }
    }

    private void UpdateTimerVisuals()
    {
        if (!isTimerEnabled) return;

        if (timerFillImage != null)
        {
            // fillAmount: 전체 시간 기준으로 부드럽게 감소
            timerFillImage.fillAmount = Mathf.Clamp01(currentGameTime / maxGameTime);

            // ── 색상 단계
            if (currentGameTime <= blinkThreshold)
            {
                // 3초 이하: 빨강 ↔ 흰색 깜빡임
                float pulse = (Mathf.Sin(Time.unscaledTime * blinkSpeed) + 1f) * 0.5f;
                timerFillImage.color = Color.Lerp(redColor, Color.white, pulse * 0.4f);
            }
            else if (currentGameTime <= redThreshold)
            {
                // 10초 이하: 빨간색 고정
                timerFillImage.color = redColor;
            }
            else if (currentGameTime <= yellowThreshold)
            {
                // 30초 ~ 10초: 노랑 → 빨강 그라데이션
                float t = 1f - (currentGameTime - redThreshold) / (yellowThreshold - redThreshold);
                timerFillImage.color = Color.Lerp(yellowColor, redColor, t);
            }
            else
            {
                // 30초 초과: 기본 색상 (흰색)
                timerFillImage.color = defaultColor;
            }
        }
        else if (timerFillRect != null)
        {
            // Image 없을 때 폴백: scale 기반
            float scale = Mathf.Clamp01(currentGameTime / maxGameTime);
            timerFillRect.localScale = new Vector3(scale, 1f, 1f);
        }

        if (timerText != null)
        {
            ShowTimerText(currentGameTime);
        }
    }

    /// <summary>
    /// 타이머 UI 표시/숨김 (퀘스트 모드에서는 숨김)
    /// </summary>
    public void ShowTimer(bool show)
    {
        // Panel_Timer 자체를 표시/숨김
        if (timerFillRect != null && timerFillRect.parent != null)
            timerFillRect.parent.gameObject.SetActive(show);
    }

    private void EndGame()
    {
        currentGameTime = 0;
        isGameActive = false;
        UpdateTimerVisuals();
        Debug.Log("[시스템] 제한 시간 종료! 영업 마감!");

        OnGameTimeUp?.Invoke();
        Time.timeScale = 0f;
    }
}
