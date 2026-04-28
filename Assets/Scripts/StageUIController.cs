using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 UI 컨트롤러 — 퀘스트 이름과 진행도를 HUD에 실시간 표시
/// 좌상단 수익 아래에 표시되며, 무한 모드(11+)에서는 숨깁니다.
/// </summary>
public class StageUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text questNameText;
    [SerializeField] private Text questProgressText;

    [Header("References")]
    [SerializeField] private StageManager stageManager;
    [SerializeField] private QuestTracker questTracker;

    private void Awake()
    {
        if (stageManager == null) stageManager = FindAnyObjectByType<StageManager>();
        if (questTracker == null) questTracker = FindAnyObjectByType<QuestTracker>();

        // UI 자동 바인딩
        if (questNameText == null)
        {
            GameObject go = GameObject.Find("Text_QuestName");
            if (go != null) questNameText = go.GetComponent<Text>();
        }
        if (questProgressText == null)
        {
            GameObject go = GameObject.Find("Text_QuestProgress");
            if (go != null) questProgressText = go.GetComponent<Text>();
        }
    }

    private void Start()
    {
        if (stageManager != null)
        {
            stageManager.OnStageStarted += HandleStageStarted;
            stageManager.OnStageCleared += HandleStageCleared;
        }

        if (questTracker != null)
        {
            questTracker.OnProgressChanged += HandleProgressChanged;
        }

        // StageManager.Start()가 먼저 실행되어 LoadStage(0)가 이미 완료된 경우
        // OnStageStarted를 놓쳤으므로 현재 스테이지로 수동 초기화
        StageData current = stageManager != null ? stageManager.CurrentStage : null;
        if (current != null)
            HandleStageStarted(current);
    }

    private void OnDestroy()
    {
        if (stageManager != null)
        {
            stageManager.OnStageStarted -= HandleStageStarted;
            stageManager.OnStageCleared -= HandleStageCleared;
        }
        if (questTracker != null)
        {
            questTracker.OnProgressChanged -= HandleProgressChanged;
        }
    }

    private void HandleStageStarted(StageData data)
    {
        if (data.isInfiniteMode)
        {
            // 무한 모드: 퀘스트 UI 숨김
            if (questNameText != null) questNameText.gameObject.SetActive(false);
            if (questProgressText != null) questProgressText.gameObject.SetActive(false);
        }
        else
        {
            if (questNameText != null)
            {
                questNameText.gameObject.SetActive(true);
                questNameText.text = data.stageName;
            }
            if (questProgressText != null)
            {
                questProgressText.gameObject.SetActive(true);
            }

            // 스테이지 시작과 동시에 클리어 조건 텍스트를 즉시 표시
            // (Initialize()의 OnProgressChanged가 구독 타이밍에 따라 누락될 수 있으므로 강제 재발행)
            if (questTracker != null)
                questTracker.RefreshProgressUI();
        }
    }

    private void HandleProgressChanged(string progress)
    {
        if (questProgressText != null)
            questProgressText.text = progress;
    }

    private void HandleStageCleared(int stageIndex)
    {
        if (questNameText != null)
            questNameText.text = "✓ 클리어!";
        // questProgressText는 마지막 달성 상태(N/N)를 유지 — 다음 스테이지 시작 시 덮어씌워짐
    }
}
