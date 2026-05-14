using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;

/// <summary>
/// 인지심리학 기반 3단 영역 분할 모바일 입력 매니저
/// Screen.height를 실시간 참조하여 화면을 상/중/하 1/3로 분할합니다.
/// 
/// 입력은 Unity Input System 의 <see cref="Pointer"/>.current 하나로 통일 처리합니다.
///  - Device Simulator 창에서 클릭하면 Touchscreen 으로 전달됨
///  - Game 창에서 클릭하면 Mouse 로 전달됨
///  - 실기기에서 터치하면 Touchscreen 으로 전달됨
/// Pointer.current 는 가장 최근에 활성된 포인터 하나만 돌려주므로,
/// 같은 프레임에서 Mouse/Touchscreen 이 동시에 눌려도 중복 발화하지 않습니다.
/// 
/// 하단 1/3: 터치 전용 (얼음 생성, 토핑 버튼)
/// 중앙 1/3: 스와이프 전용 (서빙/폐기)
/// 상단 1/3: 입력 차단 (정보 표시 영역)
/// </summary>
public class MobileInputManager : MonoBehaviour
{
    public enum ScreenZone { Bottom, Center, Top }
    public enum SwipeDirection { Left, Right, Up, Down, None }

    [Header("References")]
    [SerializeField] private IceMachine iceMachine;
    [SerializeField] private ServingManager servingManager;
    [SerializeField] private GameManager gameManager;

    [Header("Zone Layout (비율 조절)")]
    [Tooltip("하단 터치 구역의 화면 비율 (0.0~1.0)")]
    [SerializeField] [Range(0.1f, 0.6f)] private float bottomZoneRatio = 0.333f;
    [Tooltip("중앙 스와이프 구역의 화면 비율 (0.0~1.0)")]
    [SerializeField] [Range(0.1f, 0.6f)] private float centerZoneRatio = 0.333f;

    [Header("Swipe Settings")]
    [SerializeField] private float minSwipeDistance = 28f;
    [SerializeField, Tooltip("중앙 누른 채로 이 시간(초) 넘기면 스와이프 추적만 취소하고 입력 루프를 복구합니다. (Up 누락 시 너무 길면 플레이가 멈춘 것처럼 보입니다.)")]
    private float centerSwipeSafetyTimeoutSeconds = 2.5f;

    const float MinCenterSwipeSafetyTimeout = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool enableInputLog = true;

    // 구역 경계 캐시
    private float cachedBoundaryLow;
    private float cachedBoundaryHigh;
    private int lastScreenHeight;

    // 포인터 추적
    // Center swipe만 Down/Up을 추적하고, Bottom 탭(얼음 추가)은 Press 프레임에 즉시 처리합니다.
    private bool isCenterDown = false;
    private Vector2 centerDownPos;
    private Vector2 centerSwipeFarthestPos;
    private Pointer centerDownPointer;
    private float centerDownStartedUnscaledTime;

    // UI Raycast 재사용
    private PointerEventData pointerEventData;
    private List<RaycastResult> raycastResults = new List<RaycastResult>();
    private RectTransform cachedIceBuildWindowRt;

    // 재사용 버퍼 — 활성 포인터 디바이스 수집
    private readonly List<Pointer> activePointers = new List<Pointer>(4);

    private bool didConfigureUiInputModule;

    private float EffectiveCenterSwipeTimeout =>
        centerSwipeSafetyTimeoutSeconds <= 0f
            ? 2.5f
            : Mathf.Max(centerSwipeSafetyTimeoutSeconds, MinCenterSwipeSafetyTimeout);

    private void Awake()
    {
        if (iceMachine == null)
            iceMachine = ResolveActiveIceMachine();
        if (servingManager == null)
            servingManager = FindAnyObjectByType<ServingManager>();
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        RefreshBoundaries();
    }

    private static IceMachine ResolveActiveIceMachine()
    {
        // Scene may contain multiple IceMachine components (some disabled).
        // Prefer the one that is active+enabled so input updates the visible UI.
        var all = FindObjectsByType<IceMachine>(FindObjectsInactive.Include);
        IceMachine fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (fallback == null) fallback = all[i];
            if (all[i].isActiveAndEnabled) return all[i];
        }
        return fallback;
    }

    private void OnEnable()
    {
        // Input System 이 에디터 포커스와 무관하게 항상 게임뷰/시뮬레이터에 입력을 보내도록 설정
#if UNITY_EDITOR
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
        didConfigureUiInputModule = false;
    }

    private void OnDisable()
    {
        CancelCenterSwipeTracking();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            CancelCenterSwipeTracking();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
            CancelCenterSwipeTracking();
    }

    private void CancelCenterSwipeTracking()
    {
        isCenterDown = false;
        centerDownPointer = null;
        centerSwipeFarthestPos = default;
    }

    /// <summary>
    /// Mouse / Touchscreen.current 만 보면 시뮬레이터 전용 Pointer 가 빠질 수 있어, 활성 Pointer 디바이스를 모두 수집합니다.
    /// 리스트 순서: Mouse 를 index 0 에 두고 나머지를 뒤에 둡니다 → HandlePointerInput 의 역순 for 가 터치 계열을 먼저 처리합니다.
    /// </summary>
    private void CollectActivePointerDevices()
    {
        activePointers.Clear();
        Pointer mouse = null;
        foreach (var device in InputSystem.devices)
        {
            if (!device.enabled || !device.added) continue;
            if (device is Pointer p)
            {
                if (device is Mouse)
                    mouse = p;
                else
                    activePointers.Add(p);
            }
        }
        if (mouse != null)
            activePointers.Insert(0, mouse);
    }

    private void Update()
    {
        // 해상도 변경 감지 시에만 경계 재계산
        if (Screen.height != lastScreenHeight)
        {
            RefreshBoundaries();
        }

        if (!didConfigureUiInputModule)
            TryConfigureUiInputModuleForGameplay();

        if (gameManager != null && !gameManager.IsGameActive) return;

        HandlePointerInput();
    }

    /// <summary>
    /// 기본 Single-unified-pointer 설정은 시뮬레이터(FastTouchscreen)·UI와 겹칠 때 press/up 이 꼬여
    /// 스와이프 대기 상태가 풀리지 않거나 wasPressedThisFrame 이 사라지는 사례가 있습니다.
    /// </summary>
    private void TryConfigureUiInputModuleForGameplay()
    {
        if (didConfigureUiInputModule) return;
        var es = EventSystem.current;
        if (es == null) return;

        if (es.currentInputModule is InputSystemUIInputModule uiMod)
        {
            uiMod.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
            if (enableInputLog)
                Debug.Log("[MobileInputManager] InputSystemUIInputModule.pointerBehavior → AllPointersAsIs (시뮬/멀티터치 안정화)");
        }

        didConfigureUiInputModule = true;
    }

    /// <summary>
    /// Screen.height 기반 구역 경계 계산 (해상도 독립적)
    /// </summary>
    private void RefreshBoundaries()
    {
        lastScreenHeight = Screen.height;
        float h = (float)lastScreenHeight;
        cachedBoundaryLow = h * bottomZoneRatio;
        cachedBoundaryHigh = h * (bottomZoneRatio + centerZoneRatio);
    }

    // 외부 참조용 비율 프로퍼티
    public float BottomZoneRatio => bottomZoneRatio;
    public float CenterZoneRatio => centerZoneRatio;
    public float TopZoneRatio => 1f - bottomZoneRatio - centerZoneRatio;

    /// <summary>
    /// 화면 좌표(Y)로 구역 판별
    /// Screen 좌표: Y=0 하단, Y=Screen.height 상단
    /// </summary>
    public ScreenZone ClassifyZone(Vector2 screenPos)
    {
        if (screenPos.y < cachedBoundaryLow)
            return ScreenZone.Bottom;
        if (screenPos.y < cachedBoundaryHigh)
            return ScreenZone.Center;
        return ScreenZone.Top;
    }

    // ─────────────────────────────────────
    //  포인터 입력 (마우스 + 터치 통합)
    // ─────────────────────────────────────

    /// <summary>
    /// Mouse 와 Touchscreen 을 각각 독립적으로 확인해 Press/Release 를 잡는다.
    /// Pointer.current 에 의존하지 않는 이유: Mouse↔Touchscreen 간 current 가 프레임마다
    /// 바뀌어 Down 과 Up 이 서로 다른 디바이스에서 읽히는 경우가 있음(→ Up 누락).
    /// </summary>
    private void HandlePointerInput()
    {
        // Mouse.current / Touchscreen.current 만 쓰면 시뮬레이터·일부 기기의 보조 Pointer(예: FastTouchscreen)가 빠질 수 있음
        CollectActivePointerDevices();
        if (activePointers.Count == 0) return;

        // Center swipe release 처리 (누르고 있는 동안에는 return 하지 않음 → 아래 새 Press 루프는 항상 실행)
        if (isCenterDown)
        {
            bool deviceLost = centerDownPointer == null || !centerDownPointer.added;
            if (deviceLost)
            {
                CancelCenterSwipeTracking();
            }
            else if (Time.unscaledTime - centerDownStartedUnscaledTime > EffectiveCenterSwipeTimeout)
            {
                if (enableInputLog)
                    Debug.LogWarning("[MobileInputManager] 중앙 스와이프 추적 타임아웃 — 입력 잠금 해제.");
                CancelCenterSwipeTracking();
            }
            else
            {
                // 드래그 중 가장 멀리 이동한 지점 — Up 직후 position 이 (0,0)·시작점으로 튀는 기기/시뮬에서도 서빙 방향 유지
                Vector2 curTrack = centerDownPointer.position.ReadValue();
                if ((curTrack - centerDownPos).sqrMagnitude > (centerSwipeFarthestPos - centerDownPos).sqrMagnitude)
                    centerSwipeFarthestPos = curTrack;

                bool released = centerDownPointer.press.wasReleasedThisFrame
                                 || !centerDownPointer.press.isPressed;

                // 일부 시뮬레이터/기기에서 press.isPressed 가 Up 직후에도 잠깐 true 로 남는 경우 보완
                if (!released && centerDownPointer is Touchscreen touchscreenDevice)
                {
                    var phase = touchscreenDevice.primaryTouch.phase.ReadValue();
                    if (phase == UnityEngine.InputSystem.TouchPhase.Ended
                        || phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                        released = true;
                }

                if (released)
                {
                    Vector2 endPos = centerDownPointer.position.ReadValue();
                    if ((endPos - centerDownPos).magnitude < minSwipeDistance
                        && (centerSwipeFarthestPos - centerDownPos).magnitude >= minSwipeDistance)
                    {
                        endPos = centerSwipeFarthestPos;
                    }

                    isCenterDown = false;
                    Pointer usedPointer = centerDownPointer;
                    centerDownPointer = null;
                    if (enableInputLog)
                        Debug.Log($"[MobileInputManager] Pointer Up @ {endPos} → DispatchInput(Center) (device: {usedPointer.GetType().Name})");
                    DispatchInput(ScreenZone.Center, centerDownPos, endPos);
                }
                // 누른 채 유지 중: return 하지 않음 — 같은 프레임·이후 프레임의 다른 Press(하단 토핑 등) 처리
            }
        }

        // 새 Press 탐색 — Touchscreen 을 Mouse 보다 먼저 확인해 Simulator 우선.
        // 첫 번째로 눌린 디바이스 하나만 잡아 중복 Down 을 방지한다.
        for (int i = activePointers.Count - 1; i >= 0; i--) // 뒤에서부터 = Touchscreen 우선
        {
            Pointer p = activePointers[i];
            if (p.press.wasPressedThisFrame)
            {
                Vector2 downPos = p.position.ReadValue();
                ScreenZone zone = ClassifyZone(downPos);

                // 스와이프 대기 중: 하단·얼음창·상단 에서 새 Press → 잠금 해제 후 정상 분기
                if (isCenterDown)
                {
                    if (zone == ScreenZone.Bottom)
                    {
                        if (enableInputLog)
                            Debug.Log("[MobileInputManager] 스와이프 대기 중 하단 Press → 추적 취소");
                        CancelCenterSwipeTracking();
                    }
                    else if (zone == ScreenZone.Center && IsOverIceBuildWindow(downPos))
                    {
                        if (enableInputLog)
                            Debug.Log("[MobileInputManager] 스와이프 대기 중 얼음창 Press → 추적 취소");
                        CancelCenterSwipeTracking();
                    }
                    else if (zone == ScreenZone.Top)
                    {
                        if (enableInputLog)
                            Debug.Log("[MobileInputManager] 스와이프 대기 중 상단 Press → 추적 취소");
                        CancelCenterSwipeTracking();
                    }
                }

                if (enableInputLog)
                    Debug.Log($"[MobileInputManager] Pointer Down @ {downPos} → Zone: {zone} (device: {p.GetType().Name})");

                // Bottom 탭: 즉시 얼음 추가 (버튼 위 제외)
                if (zone == ScreenZone.Bottom)
                {
                    if (IsOverUIButton(downPos)) return;
                    if (iceMachine == null) iceMachine = ResolveActiveIceMachine();
                    if (iceMachine != null && !iceMachine.IsComplete)
                    {
                        if (enableInputLog) Debug.Log("[하단 영역] 탭 → 얼음 생성");
                        iceMachine.AddIce();
                    }
                    return;
                }

                // Center: 얼음창 위 — 미완성이면 탭으로 얼음 추가, 완성 후에는 같은 영역에서 스와이프 서빙을 시작할 수 있게 함.
                // (완성 뒤에도 여기서 return 만 하면 컵이 중앙을 덮을 때 서빙 제스처를 시작할 곳이 없음)
                if (zone == ScreenZone.Center)
                {
                    if (IsOverIceBuildWindow(downPos))
                    {
                        if (IsOverUIButton(downPos))
                            return;

                        if (iceMachine == null) iceMachine = ResolveActiveIceMachine();
                        if (iceMachine != null && !iceMachine.IsComplete)
                        {
                            if (enableInputLog) Debug.Log("[중앙 영역] 얼음창 탭 → 얼음 생성");
                            iceMachine.AddIce();
                            return;
                        }

                        // 빙수 베이스 완성 후: 컵/얼음창 위에서 스와이프로 서빙·폐기
                        isCenterDown = true;
                        centerDownPos = downPos;
                        centerSwipeFarthestPos = downPos;
                        centerDownPointer = p;
                        centerDownStartedUnscaledTime = Time.unscaledTime;
                        return;
                    }

                    if (IsOverUIButton(downPos))
                        return;

                    // swipe 추적 시작 (얼음창 바깥의 중앙 영역)
                    isCenterDown = true;
                    centerDownPos = downPos;
                    centerSwipeFarthestPos = downPos;
                    centerDownPointer = p;
                    centerDownStartedUnscaledTime = Time.unscaledTime;
                    return;
                }

                // Top은 차단
                OnTopZoneInput();
                return;
            }
        }
    }

    // ─────────────────────────────────────
    //  구역별 입력 분기
    // ─────────────────────────────────────

    private void DispatchInput(ScreenZone zone, Vector2 startPos, Vector2 endPos)
    {
        switch (zone)
        {
            case ScreenZone.Top:
                OnTopZoneInput();
                break;
            case ScreenZone.Center:
                OnCenterZoneInput(startPos, endPos);
                break;
            case ScreenZone.Bottom:
                OnBottomZoneInput(startPos, endPos);
                break;
        }
    }

    /// <summary>
    /// 상단 1/3: 모든 입력 차단 (정보 표시 영역)
    /// </summary>
    private void OnTopZoneInput()
    {
        if (enableInputLog)
        {
            Debug.Log("[상단 영역] 입력 차단 — 정보 표시 구역입니다.");
        }
    }

    /// <summary>
    /// 중앙 1/3: 스와이프 전용.  단순 탭은 무시.
    /// </summary>
    private void OnCenterZoneInput(Vector2 startPos, Vector2 endPos)
    {
        Vector2 delta = endPos - startPos;
        SwipeDirection dir = ResolveSwipe(startPos, endPos);

        if (dir == SwipeDirection.None)
        {
            if (enableInputLog)
                Debug.Log($"[중앙 영역] 스와이프로 인식 안 됨 (이동 {delta.magnitude:F0}px, 최소 약 {minSwipeDistance:F0}px 필요 — 좌/우/아래=서빙, 위=폐기)");

            // 얼음 미완성인데 얼음창 위에서 짧게 뗀 경우 = 탭으로 간주 (스와이프 최소거리 미만이면 기존엔 입력이 전부 무시됨)
            if (iceMachine == null) iceMachine = ResolveActiveIceMachine();
            if (iceMachine != null && !iceMachine.IsComplete &&
                (IsOverIceBuildWindow(startPos) || IsOverIceBuildWindow(endPos)))
            {
                if (enableInputLog)
                    Debug.Log("[중앙 영역] 얼음창 탭(짧은 드래그) → 얼음 생성");
                iceMachine.AddIce();
            }
            return;
        }

        if (servingManager == null)
        {
            Debug.LogWarning("[MobileInputManager] ServingManager 참조가 없습니다!");
            return;
        }

        switch (dir)
        {
            case SwipeDirection.Left:
                if (enableInputLog) Debug.Log("[중앙 스와이프] 서빙 시도 → 좌측");
                servingManager.TryServe(ServingManager.ServeDirection.Left);
                break;
            case SwipeDirection.Right:
                if (enableInputLog) Debug.Log("[중앙 스와이프] 서빙 시도 → 우측");
                servingManager.TryServe(ServingManager.ServeDirection.Right);
                break;
            case SwipeDirection.Down:
                if (enableInputLog) Debug.Log("[중앙 스와이프] 서빙 시도 → 하단");
                servingManager.TryServe(ServingManager.ServeDirection.Down);
                break;
            case SwipeDirection.Up:
                if (enableInputLog) Debug.Log("[중앙 스와이프] 폐기 시도 → 쓰레기통");
                servingManager.TryServe(ServingManager.ServeDirection.TrashUp);
                break;
        }
    }

    /// <summary>
    /// 하단 1/3: 터치 전용 (피츠의 법칙 적용).  스와이프 무시.
    /// </summary>
    private void OnBottomZoneInput(Vector2 startPos, Vector2 endPos)
    {
        SwipeDirection dir = ResolveSwipe(startPos, endPos);
        if (dir != SwipeDirection.None)
        {
            if (enableInputLog)
                Debug.Log("[하단 영역] 스와이프 무시 — 터치(탭)만 허용됩니다.");
            return;
        }

        // UI 버튼 위의 탭은 UGUI EventSystem이 처리
        if (IsOverUIButton(startPos))
            return;
        // 얼음 생성은 IceTapArea(Panel_IceBuildWindow)가 담당합니다.
    }

    // ─────────────────────────────────────
    //  스와이프 판별
    // ─────────────────────────────────────

    /// <summary>
    /// 스와이프 방향 판별.
    /// |deltaX| > |deltaY| → 좌/우,  |deltaY| > |deltaX| → 상/하.
    /// 최소 이동거리 미만이면 None (탭).
    /// </summary>
    private SwipeDirection ResolveSwipe(Vector2 start, Vector2 end)
    {
        Vector2 delta = end - start;
        if (delta.magnitude < minSwipeDistance)
            return SwipeDirection.None;

        float ax = Mathf.Abs(delta.x);
        float ay = Mathf.Abs(delta.y);

        if (ax > ay)
            return delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        else
            return delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
    }

    // ─────────────────────────────────────
    //  유틸리티
    // ─────────────────────────────────────

    private bool IsOverUIButton(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        if (pointerEventData == null)
            pointerEventData = new PointerEventData(EventSystem.current);

        pointerEventData.position = screenPos;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);

        for (int i = 0; i < raycastResults.Count; i++)
        {
            // 레이캐스트 히트 오브젝트 자체 또는 부모 어느 곳에든 Button이 있으면 UI 입력으로 간주
            if (raycastResults[i].gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null)
                return true;
        }
        return false;
    }

    private bool IsOverIceBuildWindow(Vector2 screenPos)
    {
        // UI RaycastTarget 설정에 영향을 받지 않도록 RectTransform 영역 체크를 우선 사용합니다.
        if (cachedIceBuildWindowRt == null)
        {
            var go = GameObject.Find("Panel_IceBuildWindow");
            if (go != null) cachedIceBuildWindowRt = go.GetComponent<RectTransform>();
        }

        if (cachedIceBuildWindowRt == null) return false;

        var canvas = cachedIceBuildWindowRt.GetComponentInParent<Canvas>();
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(cachedIceBuildWindowRt, screenPos, cam);
    }

    /// <summary>
    /// 구역 경계 정보 (DebugZoneOverlay 등 외부 참조용)
    /// </summary>
    public float BoundaryLow => cachedBoundaryLow;
    public float BoundaryHigh => cachedBoundaryHigh;
}
