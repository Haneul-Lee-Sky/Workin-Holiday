using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

/// <summary>
/// 스테이지 매니저 — 전체 스테이지 흐름을 관리하고 각 매니저를 오케스트레이션
/// 퀘스트 모드(1~10)와 무한 모드(11+)를 제어합니다.
/// </summary>
public class StageManager : MonoBehaviour
{
    [Header("Stage Data")]
    [SerializeField] private StageData[] allStages;

    [Header("Flow")]
    [Tooltip("퀘스트 클리어 시 로비로 복귀할지 여부 (false면 다음 스테이지로 진행)")]
    [SerializeField] private bool returnToLobbyOnClear = true;
    [Tooltip("클리어 후 로비 복귀까지 대기 시간 (클리어 연출용)")]
    [SerializeField, Range(0f, 5f)] private float clearDelaySeconds = 1.5f;
    [Tooltip("복귀할 로비 씬 이름 (Build Settings에 등록돼 있어야 함)")]
    [SerializeField] private string lobbySceneName = "1_LobbyScene";

    [Header("References")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private CustomerController customerController;
    [SerializeField] private QuestTracker questTracker;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ServingManager servingManager;
    [SerializeField] private CustomerManager customerManager;
    [Tooltip("퀘스트 클리어 시 표시할 결과창. 없으면 바로 로비로 복귀합니다.")]
    [SerializeField] private StageResultUI stageResultUI;

    private int currentStageIndex = 0;
    private bool isLoading = false;       // 중복 로드 방지
    private bool isTransitioning = false; // 스테이지 전환 중 (Invoke 딜레이 중) 클리어 중복 방지

    // 결과창 표시용 캐시 (Invoke가 인자를 지원하지 않아 임시 저장)
    private int pendingEarnedRevenue;
    private string pendingStageName;

    // 이벤트
    public event Action<StageData> OnStageStarted;
    public event Action<int> OnStageCleared;
    public event Action<int, int> OnInfiniteModeDone; // score, revenue

    // 프로퍼티
    public StageData CurrentStage => (allStages != null && currentStageIndex < allStages.Length)
        ? allStages[currentStageIndex] : null;
    public int CurrentStageIndex => currentStageIndex;
    public bool IsQuestMode => CurrentStage != null && !CurrentStage.isInfiniteMode;

    private void Awake()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (customerController == null) customerController = FindAnyObjectByType<CustomerController>();
        if (questTracker == null) questTracker = FindAnyObjectByType<QuestTracker>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
        if (servingManager == null) servingManager = FindAnyObjectByType<ServingManager>();
        if (customerManager == null) customerManager = FindAnyObjectByType<CustomerManager>();
        if (stageResultUI == null) stageResultUI = FindAnyObjectByType<StageResultUI>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        // QuestTracker 이벤트 구독
        if (questTracker != null)
            questTracker.OnQuestCleared += HandleQuestCleared;

        // GameManager 시간 종료 구독
        if (gameManager != null)
            gameManager.OnGameTimeUp += HandleGameTimeUp;

        // ServingManager → QuestTracker 연결
        if (servingManager != null && questTracker != null)
        {
            servingManager.OnServeSuccess += questTracker.OnServeSuccess;
            servingManager.OnTrash += questTracker.OnTrash;
        }

        // ScoreManager → QuestTracker 콤보 연결
        if (scoreManager != null && questTracker != null)
        {
            scoreManager.OnScoreChanged += HandleScoreChanged;
        }

        // 저장된 진행도부터 시작 (없으면 0)
        int startIndex = 0;
        if (DataManager.Instance != null)
            startIndex = DataManager.Instance.QuestStageIndex;
        if (allStages != null)
            startIndex = Mathf.Clamp(startIndex, 0, allStages.Length - 1);

        LoadStage(startIndex);
    }

    private void OnDestroy()
    {
        if (questTracker != null)
            questTracker.OnQuestCleared -= HandleQuestCleared;
        if (gameManager != null)
            gameManager.OnGameTimeUp -= HandleGameTimeUp;
        if (servingManager != null && questTracker != null)
        {
            servingManager.OnServeSuccess -= questTracker.OnServeSuccess;
            servingManager.OnTrash -= questTracker.OnTrash;
        }
        if (scoreManager != null)
            scoreManager.OnScoreChanged -= HandleScoreChanged;
    }

    private void HandleScoreChanged(int score, int combo)
    {
        if (questTracker != null)
            questTracker.OnComboChanged(combo);
    }

    /// <summary>
    /// QuestManager가 코드로 생성한 스테이지 배열을 주입합니다.
    /// Awake() 단계에서 호출되어야 Start() → LoadStage(0) 전에 반영됩니다.
    /// </summary>
    public void SetStages(StageData[] stages)
    {
        allStages = stages;
        Debug.Log($"[StageManager] 스테이지 {stages.Length}개 주입 완료 (QuestManager)");
    }

    public void LoadStage(int index)
    {
        if (allStages == null || index >= allStages.Length)
        {
            Debug.LogWarning($"[StageManager] 스테이지 데이터가 없습니다. index={index}");
            return;
        }

        // 중복 로드 방지
        if (isLoading)
        {
            Debug.LogWarning($"[StageManager] 이미 로딩 중! index={index} 무시됨");
            return;
        }
        isLoading = true;
        isTransitioning = false;
        CancelInvoke(nameof(LoadNextStage));

        currentStageIndex = index;
        StageData stage = allStages[index];

        // timeScale 보장
        Time.timeScale = 1f;

        Debug.Log($"[StageManager] ===== '{stage.stageName}' 시작 ({index+1}/{allStages.Length}) =====");

        // 점수/매출 초기화
        if (scoreManager != null) scoreManager.ResetScore();
        if (servingManager != null) servingManager.ResetRound();

        // 기존 손님 제거
        if (customerManager != null) customerManager.ClearAllCustomers();

        // 퀘스트 초기화
        if (questTracker != null)
            questTracker.Initialize(stage);

        // 손님 스폰 설정
        if (customerController != null)
            customerController.ApplyStageData(stage);

        // 게임 타이머 설정
        if (gameManager != null)
        {
            gameManager.SetTimeLimit(stage.timeLimit);

            if (stage.isInfiniteMode)
            {
                // 무한 모드: 실제 카운트다운 시작
                gameManager.StartGame(enableTimer: true);
            }
            else
            {
                // 퀘스트 모드: 제한 시간 고정 표시만 (카운트다운 없음)
                gameManager.StartGame(enableTimer: false);
            }

            // 모든 스테이지에서 Panel_Timer 항상 표시
            gameManager.ShowTimer(true);
        }

        OnStageStarted?.Invoke(stage);
        isLoading = false;
    }


    private void HandleQuestCleared()
    {
        if (isTransitioning) return; // 이미 전환 중이면 무시
        isTransitioning = true;

        StageData clearedStage = CurrentStage;
        Debug.Log($"[StageManager] ★ '{clearedStage.stageName}' 클리어!");

        // 스폰 중지
        if (customerController != null) customerController.Stop();

        OnStageCleared?.Invoke(currentStageIndex);

        // 결과창용 값 캐시
        pendingStageName = clearedStage != null ? clearedStage.stageName : "스테이지";
        pendingEarnedRevenue = servingManager != null ? servingManager.CurrentRoundRevenue : 0;

        // 퀘스트 모드: 다음 스테이지 인덱스를 DataManager에 저장
        if (clearedStage != null && !clearedStage.isInfiniteMode && DataManager.Instance != null)
        {
            int nextIndex = currentStageIndex + 1;
            if (allStages != null && nextIndex >= allStages.Length)
            {
                // 모든 퀘스트 스테이지 클리어 → isInfiniteMode = true 인 첫 스테이지로 이동
                int infiniteIdx = -1;
                for (int i = 0; i < allStages.Length; i++)
                {
                    if (allStages[i] != null && allStages[i].isInfiniteMode)
                    {
                        infiniteIdx = i;
                        break;
                    }
                }
                nextIndex = infiniteIdx >= 0 ? infiniteIdx : allStages.Length - 1;
                Debug.Log($"[StageManager] ★★★ 모든 퀘스트 클리어! 이후 진입 시 무한 모드(index:{nextIndex})로 시작합니다.");
            }
            DataManager.Instance.SaveQuestStage(nextIndex);
            Debug.Log($"[StageManager] 다음 스테이지 진행도 저장: {nextIndex}");
        }

        // 클리어 연출 시간 후 결과창 표시 → 확인 버튼으로 로비 복귀
        if (stageResultUI != null)
        {
            Invoke(nameof(ShowPendingResult), clearDelaySeconds);
        }
        else if (returnToLobbyOnClear)
        {
            // 결과창이 없으면 폴백: 기존처럼 바로 로비 복귀
            Debug.LogWarning("[StageManager] StageResultUI가 없어 결과창 없이 바로 로비로 복귀합니다.");
            Invoke(nameof(ReturnToLobby), clearDelaySeconds);
        }
        else
        {
            currentStageIndex++;
            if (currentStageIndex < allStages.Length)
            {
                Invoke(nameof(LoadNextStage), clearDelaySeconds);
            }
            else
            {
                Debug.Log("[StageManager] 모든 스테이지를 클리어했습니다! 로비로 복귀합니다.");
                Invoke(nameof(ReturnToLobby), clearDelaySeconds);
            }
        }
    }

    private void ShowPendingResult()
    {
        if (stageResultUI == null)
        {
            ReturnToLobby();
            return;
        }
        stageResultUI.Show(pendingStageName, pendingEarnedRevenue);
    }

    private void LoadNextStage()
    {
        LoadStage(currentStageIndex);
    }

    private void ReturnToLobby()
    {
        if (string.IsNullOrEmpty(lobbySceneName))
        {
            Debug.LogError("[StageManager] 로비 씬 이름이 비어있습니다!");
            isTransitioning = false;
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(lobbySceneName))
        {
            Debug.LogError($"[StageManager] 씬 '{lobbySceneName}'을 로드할 수 없습니다. Build Settings에 등록되었는지 확인하세요.");
            isTransitioning = false;
            return;
        }

        // 일시정지 상태였다면 해제
        Time.timeScale = 1f;
        Debug.Log($"[StageManager] 로비 씬으로 복귀: {lobbySceneName}");
        SceneManager.LoadScene(lobbySceneName);
    }

    private void HandleGameTimeUp()
    {
        if (CurrentStage != null && CurrentStage.isInfiniteMode)
        {
            if (isTransitioning) return;
            isTransitioning = true;

            if (customerController != null) customerController.Stop();

            int score = scoreManager != null ? scoreManager.CurrentScore : 0;
            int revenue = servingManager != null ? servingManager.CurrentRoundRevenue : 0;
            Debug.Log($"[StageManager] 무한 모드 종료! 최종 점수: {score}, 매출: {revenue}원");
            OnInfiniteModeDone?.Invoke(score, revenue);

            // EndGame() 직후 Time.timeScale == 0 이므로 Invoke(스케일 시간)은 영원히 호출되지 않음.
            // 실시간 대기 후 로비로 복귀합니다.
            StartCoroutine(CoReturnToLobbyAfterRealtimeDelay(clearDelaySeconds));
        }
    }

    private IEnumerator CoReturnToLobbyAfterRealtimeDelay(float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSecondsRealtime(delaySeconds);
        ReturnToLobby();
    }
}
