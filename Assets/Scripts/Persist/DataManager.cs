using UnityEngine;

/// <summary>
/// 게임 전역 데이터 매니저 (싱글톤).
/// 씬이 전환되어도 DontDestroyOnLoad로 유지되며, 골드·퀘스트 진행도 등 영속 자산을 관리합니다.
/// 외부에서는 CurrentGold 프로퍼티(읽기)와 AddGold/SpendGold/SetGold 메서드(쓰기)로만 접근합니다.
/// 값은 PlayerPrefs에 즉시 저장되어 앱 재시작 후에도 유지됩니다.
/// </summary>
public class DataManager : MonoBehaviour
{
    private const string PREF_KEY_GOLD = "player_gold";
    private const string PREF_KEY_QUEST_STAGE = "quest_stage_index";
    private const int DEFAULT_STARTING_GOLD = 0; // 최초 1회 실행 시 기본 재화 0원

    private static DataManager _instance;

    /// <summary>
    /// DataManager 싱글톤. 씬에 없어도 최초 접근 시 자동으로 생성됩니다.
    /// </summary>
    public static DataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                // 씬에 이미 있는지 먼저 탐색
                _instance = FindAnyObjectByType<DataManager>(FindObjectsInactive.Include);
                if (_instance == null)
                {
                    // 없으면 런타임에 자동 생성
                    var go = new GameObject("[DataManager]");
                    _instance = go.AddComponent<DataManager>();
                    DontDestroyOnLoad(go);
                    Debug.Log("[DataManager] 씬에 DataManager가 없어 자동 생성했습니다.");
                }
            }
            return _instance;
        }
    }

    [Header("Debug")]
    [SerializeField] private bool enableLog = true;

    // ─────────────────────────────────────
    //  골드 (외부 접근은 프로퍼티/메서드로만)
    // ─────────────────────────────────────

    [SerializeField] private int currentGold;

    /// <summary>현재 보유 골드(읽기 전용). 수정은 AddGold/SpendGold/SetGold 사용.</summary>
    public int CurrentGold => currentGold;

    /// <summary>골드가 변경될 때 호출 (현재값, 변화량). +면 획득, -면 지출.</summary>
    public event System.Action<int, int> OnGoldChanged;

    // ─────────────────────────────────────
    //  퀘스트 스테이지 진행도
    // ─────────────────────────────────────

    [SerializeField] private int questStageIndex;

    /// <summary>다음에 시작할 퀘스트 스테이지 인덱스 (0-based).</summary>
    public int QuestStageIndex => questStageIndex;

    /// <summary>퀘스트 스테이지 인덱스를 저장합니다. StageManager에서 클리어 후 호출.</summary>
    public void SaveQuestStage(int index)
    {
        questStageIndex = Mathf.Max(0, index);
        PlayerPrefs.SetInt(PREF_KEY_QUEST_STAGE, questStageIndex);
        PlayerPrefs.Save();
        if (enableLog) Debug.Log($"[DataManager] Quest stage saved: {questStageIndex}");
    }

    /// <summary>퀘스트 스테이지 진행도를 0으로 초기화합니다.</summary>
    public void ResetQuestStage()
    {
        questStageIndex = 0;
        PlayerPrefs.DeleteKey(PREF_KEY_QUEST_STAGE);
        PlayerPrefs.Save();
        if (enableLog) Debug.Log("[DataManager] Quest stage reset to 0");
    }

    // ─────────────────────────────────────
    //  Unity 라이프사이클 & 싱글톤
    // ─────────────────────────────────────

    private void Awake()
    {
        // 싱글톤 중복 방지
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        LoadGold();
        LoadQuestStage();
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // ─────────────────────────────────────
    //  Persistence
    // ─────────────────────────────────────

    private void LoadGold()
    {
        if (PlayerPrefs.HasKey(PREF_KEY_GOLD))
        {
            currentGold = PlayerPrefs.GetInt(PREF_KEY_GOLD);
        }
        else
        {
            // 최초 실행: 기본값 0원으로 초기화 후 저장
            currentGold = DEFAULT_STARTING_GOLD;
            PlayerPrefs.SetInt(PREF_KEY_GOLD, currentGold);
            PlayerPrefs.Save();
        }

        if (enableLog) Debug.Log($"[DataManager] Gold loaded: {currentGold:N0}G");
    }

    private void LoadQuestStage()
    {
        questStageIndex = PlayerPrefs.GetInt(PREF_KEY_QUEST_STAGE, 0);
        if (enableLog) Debug.Log($"[DataManager] Quest stage loaded: {questStageIndex}");
    }

    private void SaveGold()
    {
        PlayerPrefs.SetInt(PREF_KEY_GOLD, currentGold);
        PlayerPrefs.Save();
    }

    // ─────────────────────────────────────
    //  외부 안전 접근 API
    // ─────────────────────────────────────

    /// <summary>지정 금액 이상을 보유 중인지 확인.</summary>
    public bool HasGold(int amount) => currentGold >= amount;

    /// <summary>골드 획득 (amount는 0보다 커야 반영).</summary>
    public void AddGold(int amount)
    {
        if (amount <= 0) return;
        currentGold += amount;
        SaveGold();
        OnGoldChanged?.Invoke(currentGold, amount);
        if (enableLog) Debug.Log($"[DataManager] +{amount:N0}G → {currentGold:N0}G");
    }

    /// <summary>골드 지출 시도. 성공 시 true, 잔액 부족 시 false 반환(차감하지 않음).</summary>
    public bool SpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (currentGold < amount)
        {
            if (enableLog) Debug.LogWarning($"[DataManager] 잔액 부족: 필요 {amount:N0}G, 보유 {currentGold:N0}G");
            return false;
        }

        currentGold -= amount;
        SaveGold();
        OnGoldChanged?.Invoke(currentGold, -amount);
        if (enableLog) Debug.Log($"[DataManager] -{amount:N0}G → {currentGold:N0}G");
        return true;
    }

    /// <summary>디버그/치트용 — 골드를 특정 값으로 강제 설정(0 미만이면 0으로 보정).</summary>
    public void SetGold(int value)
    {
        int clamped = Mathf.Max(0, value);
        int delta = clamped - currentGold;
        currentGold = clamped;
        SaveGold();
        OnGoldChanged?.Invoke(currentGold, delta);
        if (enableLog) Debug.Log($"[DataManager] SetGold → {currentGold:N0}G (Δ{delta:+0;-0;0})");
    }

    /// <summary>저장된 골드 데이터를 초기화(기본값으로 되돌림).</summary>
    public void ResetGold()
    {
        PlayerPrefs.DeleteKey(PREF_KEY_GOLD);
        currentGold = DEFAULT_STARTING_GOLD;
        PlayerPrefs.SetInt(PREF_KEY_GOLD, currentGold);
        PlayerPrefs.Save();
        OnGoldChanged?.Invoke(currentGold, 0);
        if (enableLog) Debug.Log($"[DataManager] Gold reset → {currentGold:N0}G");
    }
}
