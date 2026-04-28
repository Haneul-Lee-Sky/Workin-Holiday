using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
    [SerializeField] private float minSwipeDistance = 50f;

    [Header("Debug")]
    [SerializeField] private bool enableInputLog = true;

    // 구역 경계 캐시
    private float cachedBoundaryLow;
    private float cachedBoundaryHigh;
    private int lastScreenHeight;

    // 포인터 추적
    private Vector2 pointerDownPos;
    private bool isPointerDown = false;
    private ScreenZone pointerDownZone;
    // Down 때 사용한 정확한 디바이스를 기억해서 Up 이벤트를 동일 디바이스에서 추적한다.
    // (Pointer.current 는 Mouse↔Touchscreen 간에 프레임마다 바뀔 수 있어 Up 이벤트를 놓칠 수 있음)
    private Pointer activeDownPointer;
    private float pointerDownTime;
    private const float POINTER_DOWN_TIMEOUT = 2f; // 안전장치 — 2초 이상 Down 상태면 강제 취소

    // UI Raycast 재사용
    private PointerEventData pointerEventData;
    private List<RaycastResult> raycastResults = new List<RaycastResult>();

    // 재사용 버퍼 — 활성 포인터 디바이스 수집
    private readonly List<Pointer> activePointers = new List<Pointer>(4);

    private void Awake()
    {
        if (iceMachine == null)
            iceMachine = FindAnyObjectByType<IceMachine>();
        if (servingManager == null)
            servingManager = FindAnyObjectByType<ServingManager>();
        if (gameManager == null)
            gameManager = FindAnyObjectByType<GameManager>();

        RefreshBoundaries();
    }

    private void OnEnable()
    {
        // Input System 이 에디터 포커스와 무관하게 항상 게임뷰/시뮬레이터에 입력을 보내도록 설정
#if UNITY_EDITOR
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#endif
    }

    private void Update()
    {
        // 해상도 변경 감지 시에만 경계 재계산
        if (Screen.height != lastScreenHeight)
        {
            RefreshBoundaries();
        }

        if (gameManager != null && !gameManager.IsGameActive) return;

        HandlePointerInput();
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
        // 활성 포인터 디바이스 수집 (Mouse, Touchscreen)
        activePointers.Clear();
        if (Mouse.current != null) activePointers.Add(Mouse.current);
        if (Touchscreen.current != null) activePointers.Add(Touchscreen.current);
        if (activePointers.Count == 0) return;

        // 이미 눌린 상태라면, Down 때 사용한 디바이스의 Release 만 추적한다.
        if (isPointerDown)
        {
            // 디바이스가 사라졌거나 타임아웃이면 강제 해제 (프레임 드롭/씬 전환 보호)
            bool deviceLost = activeDownPointer == null || !activeDownPointer.added;
            bool timedOut = Time.unscaledTime - pointerDownTime > POINTER_DOWN_TIMEOUT;

            if (deviceLost || timedOut)
            {
                if (enableInputLog)
                    Debug.Log($"[MobileInputManager] Pointer Down 상태를 안전 해제 (deviceLost={deviceLost}, timedOut={timedOut})");
                isPointerDown = false;
                activeDownPointer = null;
                return;
            }

            if (activeDownPointer.press.wasReleasedThisFrame)
            {
                Vector2 endPos = activeDownPointer.position.ReadValue();
                isPointerDown = false;
                Pointer usedPointer = activeDownPointer;
                activeDownPointer = null;
                if (enableInputLog)
                    Debug.Log($"[MobileInputManager] Pointer Up @ {endPos} → DispatchInput({pointerDownZone}) (device: {usedPointer.GetType().Name})");
                DispatchInput(pointerDownZone, pointerDownPos, endPos);
            }
            return; // 이미 눌린 상태에서는 새 Press 를 받지 않는다
        }

        // 새 Press 탐색 — Touchscreen 을 Mouse 보다 먼저 확인해 Simulator 우선.
        // 첫 번째로 눌린 디바이스 하나만 잡아 중복 Down 을 방지한다.
        for (int i = activePointers.Count - 1; i >= 0; i--) // 뒤에서부터 = Touchscreen 우선
        {
            Pointer p = activePointers[i];
            if (p.press.wasPressedThisFrame)
            {
                pointerDownPos = p.position.ReadValue();
                pointerDownZone = ClassifyZone(pointerDownPos);
                isPointerDown = true;
                activeDownPointer = p;
                pointerDownTime = Time.unscaledTime;
                if (enableInputLog)
                    Debug.Log($"[MobileInputManager] Pointer Down @ {pointerDownPos} → Zone: {pointerDownZone} (device: {p.GetType().Name})");
                break;
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
        SwipeDirection dir = ResolveSwipe(startPos, endPos);

        if (dir == SwipeDirection.None)
        {
            if (enableInputLog)
                Debug.Log("[중앙 영역] 단순 터치 무시 — 스와이프만 허용됩니다.");
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

        if (iceMachine == null)
        {
            Debug.LogWarning("[MobileInputManager] IceMachine 참조가 없습니다!");
            return;
        }

        if (!iceMachine.IsComplete)
        {
            iceMachine.AddIce();
        }
        else
        {
            if (enableInputLog)
                Debug.Log("[하단 영역] 빙수 완성! 토핑 버튼을 터치하거나 중앙에서 스와이프하여 서빙하세요.");
        }
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

    /// <summary>
    /// 구역 경계 정보 (DebugZoneOverlay 등 외부 참조용)
    /// </summary>
    public float BoundaryLow => cachedBoundaryLow;
    public float BoundaryHigh => cachedBoundaryHigh;
}
