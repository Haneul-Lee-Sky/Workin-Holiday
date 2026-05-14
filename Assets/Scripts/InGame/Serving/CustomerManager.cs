using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

/// <summary>
/// 서빙 결과 구조체 — 서빙 성공 여부와 손님 타입 정보를 반환합니다.
/// </summary>
public struct FulfillResult
{
    public bool success;
    public bool hadCustomer;
    public CustomerType customerType;
}

/// <summary>
/// 손님 매니저 — 손님 생성, 관리, 주문 매칭, 만료 처리를 담당합니다.
/// 스폰 타이밍/방향 결정은 CustomerController가 담당합니다.
/// </summary>
public class CustomerManager : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private ServingManager servingManager;

    [Header("Spawn Points")]
    [SerializeField] private Transform leftSlot;
    [SerializeField] private Transform rightSlot;
    [SerializeField] private Transform bottomSlot;

    [Header("Prefabs")]
    [SerializeField] private GameObject customerPrefab;

    [Header("손님 주문 UI (여기에 넣으면 전 손님에 적용)")]
    [Tooltip("좌측 슬롯 손님 말풍선. 비우면 아래 기본 말풍선을 사용합니다.")]
    [SerializeField] private Sprite speechBubbleFrameSpriteLeft;
    [Tooltip("우측 슬롯 손님 말풍선. 비우면 아래 기본 말풍선을 사용합니다.")]
    [SerializeField] private Sprite speechBubbleFrameSpriteRight;
    [Tooltip("하단 슬롯 손님 말풍선. 비우면 아래 기본 말풍선을 사용합니다.")]
    [SerializeField] private Sprite speechBubbleFrameSpriteDown;
    [Tooltip("하단 손님용 두 번째 말풍선(꼬리 반대 방향 등). 위 Down 과 둘 다 넣으면 하단 스폰마다 번갈아 적용됩니다.")]
    [SerializeField] private Sprite speechBubbleFrameSpriteDownVariant;
    [Tooltip("방향별 칸이 비었을 때 쓰는 말풍선. 모두 비우면 흰색 박스.")]
    [SerializeField] private Sprite speechBubbleFrameSprite;
    [Tooltip("주문 아이콘 — 팥. 비우면 Customer 컴포넌트 또는 IceMachine 토핑 버튼 스프라이트.")]
    [SerializeField] private Sprite orderToppingIconRedBean;
    [SerializeField] private Sprite orderToppingIconMilk;
    [SerializeField] private Sprite orderToppingIconFruit;

    [Header("말풍선 위치 (BubbleBox 앵커드 포지션, 캔버스 로컬)")]
    [SerializeField] private Vector2 speechBubbleAnchoredPositionLeft = new Vector2(0f, 140f);
    [SerializeField] private Vector2 speechBubbleAnchoredPositionRight = new Vector2(0f, 140f);
    [Tooltip("하단 손님: 왼쪽에 말풍선이 붙는 케이스의 BubbleBox 앵커 위치. 스폰마다 왼쪽/오른쪽이 번갈아 적용됩니다.")]
    [SerializeField] private Vector2 speechBubbleAnchoredPositionDownLeft = new Vector2(-200f, 10f);
    [Tooltip("하단 손님: 오른쪽에 말풍선이 붙는 케이스의 BubbleBox 앵커 위치.")]
    [SerializeField] private Vector2 speechBubbleAnchoredPositionDownRight = new Vector2(200f, 10f);

    [Header("NPC 말풍선 캔버스 루트 오프셋 (SpeechBubbleCanvas localPosition)")]
    [SerializeField] private Vector3 speechBubbleHostLocalOffsetLeft;
    [SerializeField] private Vector3 speechBubbleHostLocalOffsetRight;
    [Tooltip("하단: 왼쪽 배치 스폰일 때 SpeechBubbleCanvas localPosition. (0,0,0)이면 아래 레거시 필드 사용.")]
    [SerializeField] private Vector3 speechBubbleHostLocalOffsetDownLeft;
    [Tooltip("하단: 오른쪽 배치 스폰일 때 SpeechBubbleCanvas localPosition.")]
    [SerializeField] private Vector3 speechBubbleHostLocalOffsetDownRight;
    [Tooltip("하단 전용. 위 Left/Right 가 모두 (0,0,0)일 때만 적용됩니다.")]
    [SerializeField] private Vector3 speechBubbleHostLocalOffsetDown;

    [Header("하단 말풍선 방향 보정 (BubbleBox) — 왼쪽/오른쪽 배치 각각")]
    [Tooltip("하단 손님 · 왼쪽 배치(번갈 0번)일 때 BubbleBox X 미러")]
    [SerializeField] private bool speechBubbleFlipXForDownLeft;
    [Tooltip("하단 손님 · 오른쪽 배치(번갈 1번)일 때 BubbleBox X 미러")]
    [SerializeField] private bool speechBubbleFlipXForDownRight;
    [SerializeField] private bool speechBubbleFlipYForDownLeft;
    [SerializeField] private bool speechBubbleFlipYForDownRight;
    [Range(-180f, 180f)]
    [SerializeField] private float speechBubbleRotationZForDownLeft;
    [Range(-180f, 180f)]
    [SerializeField] private float speechBubbleRotationZForDownRight;

    [Header("Customer mount (UI)")]
    [Tooltip("손님 프리팹(NPC)의 부모. 비우면 씬에서 Panel_Maker_Center/Table 을 찾습니다. CustomerSlots가 아닌 테이블 위에 붙습니다.")]
    [SerializeField] private Transform customerMountParent;
    [Tooltip("Table 바깥 스폰 거리 = max(가로,세로 월드) × 이 배수. 값이 클수록 멀리서 등장합니다.")]
    [SerializeField] private float approachDistanceFromTable = 1.2f;
    [Tooltip("손님이 테이블에 너무 딱 붙지 않도록, 테이블 가장자리(Edge)로부터 바깥 방향으로 떨어지는 거리(월드 단위).")]
    [SerializeField] private float stopOffsetFromTableEdge = 0.28f;

    [Header("Timer")]
    [Tooltip("손님이 슬롯에 도착한 후 기다리는 시간(초). 걸어오는 시간은 제외됩니다.")]
    [SerializeField] private float customerWaitTime = 15f;

    private Dictionary<ServingManager.ServeDirection, Customer> activeCustomers =
        new Dictionary<ServingManager.ServeDirection, Customer>();

    /// <summary>하단 손님 스폰 한 번에 Build/Apply 가 두 번 Resolve 하므로, Down 말풍선 선택을 한 번만 계산합니다.</summary>
    private Sprite cachedDownSpeechBubbleSprite;
    private bool cachedDownSpeechBubbleResolved;

    /// <summary>이번 하단 스폰에서 0=왼쪽 배치, 1=오른쪽 배치. Down/Variant 말풍선 스프라이트와 같은 패리티.</summary>
    private int thisSpawnDownSideIndex;

    /// <summary>하단 손님 왼쪽/오른쪽 배치 번갈이 카운터.</summary>
    private int downSpawnSideCounter;

    public event Action<Customer> OnCustomerExpired;
    public event Action<Customer> OnCustomerSpawned;

    private void Awake()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (scoreManager == null) scoreManager = FindAnyObjectByType<ScoreManager>();
        if (servingManager == null) servingManager = FindAnyObjectByType<ServingManager>();

        // 스폰 포인트 자동 바인딩 (Inspector 미할당 시)
        if (leftSlot == null)
        {
            var go = GameObject.Find("LeftSlot");
            if (go != null) leftSlot = go.transform;
            else Debug.LogWarning("[CustomerManager] 'LeftSlot' 오브젝트를 씬에서 찾을 수 없습니다. Inspector에서 직접 연결하세요.");
        }
        if (rightSlot == null)
        {
            var go = GameObject.Find("RightSlot");
            if (go != null) rightSlot = go.transform;
            else Debug.LogWarning("[CustomerManager] 'RightSlot' 오브젝트를 씬에서 찾을 수 없습니다. Inspector에서 직접 연결하세요.");
        }
        if (bottomSlot == null)
        {
            var go = GameObject.Find("BottomSlot");
            if (go != null) bottomSlot = go.transform;
            else Debug.LogWarning("[CustomerManager] 'BottomSlot' 오브젝트를 씬에서 찾을 수 없습니다. Inspector에서 직접 연결하세요.");
        }

        ResolveCustomerMountParent();
        TryAdoptPrefabFromSpawnManagerIfNeeded();
    }

    /// <summary>
    /// 씬에 CustomerManager.customerPrefab 이 비어 있고 SpawnManager 에만 프리팹이 있을 때(구 설정) 자동으로 가져옵니다.
    /// </summary>
    private void TryAdoptPrefabFromSpawnManagerIfNeeded()
    {
        if (customerPrefab != null) return;
        var sm = FindAnyObjectByType<SpawnManager>(FindObjectsInactive.Include);
        if (sm != null && sm.customerPrefab != null)
        {
            customerPrefab = sm.customerPrefab;
            Debug.Log("[CustomerManager] customerPrefab 이 비어 있어 SpawnManager 의 프리팹을 사용합니다. 인스펙터에서 CustomerManager.customerPrefab 을 직접 넣는 것을 권장합니다.");
        }
    }

    /// <summary>
    /// 손님 NPC 부모를 Table 직속으로 맞춥니다.
    /// Ice/PC 등 Table 하위에 붙이면 World Space UI·스프라이트가 얼음 패널 레이캐스트를 가려 2/5 이후 탭이 막히는 현상이 납니다.
    /// </summary>
    private void ResolveCustomerMountParent()
    {
        Transform table = FindTableUnderMakerCenter();
        if (table == null)
        {
            if (customerMountParent == null)
                Debug.LogWarning("[CustomerManager] 'Panel_Maker_Center/Table' 을 찾을 수 없습니다. 손님은 스폰 포인트 부모 하위에 붙습니다.");
            return;
        }

        if (customerMountParent == null)
        {
            customerMountParent = table;
            Debug.Log("[CustomerManager] 손님 부모를 'Panel_Maker_Center/Table' 로 설정했습니다.");
            return;
        }

        if (customerMountParent == table)
            return;

        if (customerMountParent.IsChildOf(table))
        {
            Debug.LogWarning(
                $"[CustomerManager] customerMountParent가 Table 하위 '{customerMountParent.name}' 로 지정되어 있어 Table 루트로 교정합니다. (얼음/하단 UI 입력 가림 방지)");
            customerMountParent = table;
            return;
        }

        // Panel_Maker_Center 밖의 커스텀 부모는 그대로 둡니다.
    }

    private static Transform FindTableUnderMakerCenter()
    {
        var panel = GameObject.Find("Panel_Maker_Center");
        return panel != null ? panel.transform.Find("Table") : null;
    }

    private Transform GetCustomerMountTransform(Transform spawnPoint)
    {
        Transform mount = customerMountParent != null
            ? customerMountParent
            : (spawnPoint != null ? spawnPoint.parent : null);

        // 슬롯이 실수로 Ice 아래에 있으면 손님이 Ice 자식이 되어 전체 하단 입력을 가립니다.
        if (mount != null && mount.name == "Ice")
        {
            Transform table = FindTableUnderMakerCenter();
            if (table != null)
                return table;
        }

        return mount;
    }

    /// <summary>
    /// Table RectTransform 기준으로 손님이 걸어와 멈출 월드 좌표(시작·도착)를 계산합니다.
    /// </summary>
    private bool TryGetTableApproachWorldPositions(RectTransform table, ServingManager.ServeDirection direction, out Vector3 startWorld, out Vector3 endWorld)
    {
        startWorld = endWorld = Vector3.zero;
        if (table == null) return false;

        var corners = new Vector3[4];
        table.GetWorldCorners(corners);

        Vector3 c0 = corners[0], c1 = corners[1], c2 = corners[2], c3 = corners[3];
        Vector3 center = (c0 + c1 + c2 + c3) * 0.25f;
        float edgeLen = Mathf.Max(Vector3.Distance(c0, c1), Vector3.Distance(c1, c2));
        float outwardDistance = Mathf.Max(2f, edgeLen * approachDistanceFromTable);

        switch (direction)
        {
            case ServingManager.ServeDirection.Left:
            {
                Vector3 edgeMid = (c0 + c1) * 0.5f;
                Vector3 outward = (edgeMid - center).normalized;
                endWorld = edgeMid + outward * stopOffsetFromTableEdge;
                startWorld = edgeMid + outward * outwardDistance;
                return true;
            }
            case ServingManager.ServeDirection.Right:
            {
                Vector3 edgeMid = (c2 + c3) * 0.5f;
                Vector3 outward = (edgeMid - center).normalized;
                endWorld = edgeMid + outward * stopOffsetFromTableEdge;
                startWorld = edgeMid + outward * outwardDistance;
                return true;
            }
            case ServingManager.ServeDirection.Down:
            {
                Vector3 edgeMid = (c0 + c3) * 0.5f;
                Vector3 outward = (edgeMid - center).normalized;
                endWorld = edgeMid + outward * stopOffsetFromTableEdge;
                startWorld = edgeMid + outward * outwardDistance;
                return true;
            }
            default:
                return false;
        }
    }

    private static void ApplyFacing(ServingManager.ServeDirection direction, Transform customerRoot)
    {
        if (customerRoot == null) return;

        bool? flipX = null;
        switch (direction)
        {
            // Left: 왼쪽에서 오른쪽으로 걸어오므로 오른쪽(+)을 바라보게
            case ServingManager.ServeDirection.Left:
                flipX = false;
                break;

            // Right: 오른쪽에서 왼쪽으로 걸어오므로 좌우 반전(-)으로 왼쪽을 바라보게
            case ServingManager.ServeDirection.Right:
                flipX = true;
                break;

            default:
                break;
        }

        if (flipX == null) return;

        var sprites = customerRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites != null && sprites.Length > 0)
        {
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                    sprites[i].flipX = flipX.Value;
            }
            return;
        }

        // 폴백: 스프라이트가 없을 때만 루트 스케일 반전
        Vector3 s = customerRoot.localScale;
        float absX = Mathf.Abs(s.x);
        customerRoot.localScale = new Vector3(flipX.Value ? -absX : absX, s.y, s.z);
    }

    // ─────────────────────────────────────
    //  공개 API
    // ─────────────────────────────────────

    /// <summary>
    /// 해당 방향에 손님이 있는지 확인 (CustomerController에서 사용)
    /// </summary>
    public bool HasCustomerAt(ServingManager.ServeDirection dir)
    {
        return activeCustomers.ContainsKey(dir);
    }

    /// <summary>
    /// 지정 방향에 지정 타입의 손님을 스폰합니다. (CustomerController에서 호출)
    /// </summary>
    public void SpawnCustomer(ServingManager.ServeDirection direction, CustomerType type)
    {
        if (activeCustomers.ContainsKey(direction)) return;

        cachedDownSpeechBubbleResolved = false;

        if (direction == ServingManager.ServeDirection.Down)
        {
            thisSpawnDownSideIndex = downSpawnSideCounter % 2;
            downSpawnSideCounter++;
        }

        ResolveCustomerMountParent();
        TryAdoptPrefabFromSpawnManagerIfNeeded();

        Transform spawnPoint = GetSpawnTransform(direction);
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[CustomerManager] 스폰 포인트를 찾을 수 없습니다: {direction}");
            return;
        }

        Transform mount = GetCustomerMountTransform(spawnPoint);
        RectTransform tableRt = mount as RectTransform;

        Vector3 startPos;
        Vector3 targetWorld;

        if (tableRt != null && TryGetTableApproachWorldPositions(tableRt, direction, out startPos, out targetWorld))
        {
            // Table 기준: 카운터 가장자리 밖에서 걸어와 테이블에 붙음
        }
        else
        {
            // 폴백: CustomerSlots 월드 좌표 (구 방식)
            Vector3 slotWorld = spawnPoint.position;
            Vector3 enter = Vector3.zero;
            switch (direction)
            {
                case ServingManager.ServeDirection.Left: enter = Vector3.left; break;
                case ServingManager.ServeDirection.Right: enter = Vector3.right; break;
                case ServingManager.ServeDirection.Down: enter = Vector3.down; break;
            }
            const float spawnOffsetWorld = 5f;
            startPos = slotWorld + enter * spawnOffsetWorld;
            targetWorld = slotWorld;
        }

        GameObject custObj;

        if (customerPrefab != null)
        {
            custObj = Instantiate(customerPrefab);
            Transform ct = custObj.transform;
            ct.SetPositionAndRotation(startPos, Quaternion.identity);
            ApplyFacing(direction, ct);
            if (mount != null)
            {
                ct.SetParent(mount, true);
                ct.SetAsLastSibling();
            }
        }
        else
        {
            custObj = CreateCustomerUI(direction, type);
            custObj.transform.SetPositionAndRotation(startPos, Quaternion.identity);
            ApplyFacing(direction, custObj.transform);
            if (mount != null)
            {
                custObj.transform.SetParent(mount, true);
                custObj.transform.SetAsLastSibling();
            }
        }

        Customer customer = custObj.GetComponent<Customer>();
        if (customer == null) customer = custObj.AddComponent<Customer>();

        // 타입 설정
        customer.customerType = type;

        // 이동 시작 (Table 기준이면 targetWorld 가 테이블 가장자리)
        customer.MoveTo(targetWorld);

        // 대기 타이머 시작 — 진상 손님은 1.5배 속도, 도착 후부터 카운트
        float speedMult = (type == CustomerType.Annoying) ? 1.5f : 1f;
        customer.Activate(customerWaitTime, speedMult);

        // 만료 이벤트 구독
        customer.OnExpired += HandleCustomerExpired;

        // 스프라이트 전용 NPC 프리팹 등 Text가 없으면 주문이 절대 안 보임 → 말풍선 UI를 붙입니다.
        TryBindSpeechBubbleText(custObj, direction, customer);

        ApplySpeechBubbleFrameIfAssigned(custObj, direction);
        customer.ApplyOrderIconSourcesFromManager(
            orderToppingIconRedBean,
            orderToppingIconMilk,
            orderToppingIconFruit);

        // 주문 생성 (아이콘 스프라이트·SetActive 이후에 레이캐스트 정리해야 함)
        int reqToppingCount = scoreManager != null ? scoreManager.GetRequiredToppingCount() : 1;
        customer.InitializeOrder(reqToppingCount);

        // World Space 손님 UI는 메인 Screen Space - Camera Canvas(-100 등)와 별도 정렬이라
        // 배경/패널 뒤로 밀릴 수 있음 → Sort Order를 올립니다.
        // 주문 UI 스프라이트 적용 후에도 Screen Space 말풍선 등이 레이캐스트를 켜면 입력이 죽은 것처럼 보이므로 InitializeOrder 이후에 한 번 더 처리합니다.
        EnsureCustomerWorldCanvasRendersOnTop(custObj.transform);

        activeCustomers[direction] = customer;
        Debug.Log($"[CustomerManager] {direction} 방향에 {type} 손님 등장! (요구 토핑: {reqToppingCount}개)");

        // 스폰 이벤트 — QuestManager가 구독해 특수 손님 등장 카운트에 사용
        OnCustomerSpawned?.Invoke(customer);

        cachedDownSpeechBubbleResolved = false;
    }

    /// <summary>
    /// 모든 활성 손님 제거 (스테이지 전환 시 사용)
    /// </summary>
    public void ClearAllCustomers()
    {
        foreach (var kvp in new Dictionary<ServingManager.ServeDirection, Customer>(activeCustomers))
        {
            if (kvp.Value != null)
            {
                kvp.Value.OnExpired -= HandleCustomerExpired;
                Destroy(kvp.Value.gameObject);
            }
        }
        activeCustomers.Clear();
    }

    /// <summary>
    /// 서빙 시도 — 주문 일치 여부와 손님 타입을 FulfillResult로 반환
    /// </summary>
    public FulfillResult TryFulfillOrder(ServingManager.ServeDirection direction, ShavedIceData iceData)
    {
        FulfillResult result = new FulfillResult();
        result.success = false;
        result.hadCustomer = false;
        result.customerType = CustomerType.Normal;

        if (direction == ServingManager.ServeDirection.TrashUp) return result;

        if (!activeCustomers.ContainsKey(direction))
        {
            Debug.Log($"[CustomerManager] {direction} 방향에는 손님이 없습니다! 서빙 실패.");
            return result;
        }

        Customer targetCustomer = activeCustomers[direction];
        result.hadCustomer = true;
        result.customerType = targetCustomer.customerType;

        targetCustomer.OnExpired -= HandleCustomerExpired;

        if (targetCustomer.IsOrderMatched(iceData))
        {
            result.success = true;
            activeCustomers.Remove(direction);
            Destroy(targetCustomer.gameObject);
        }
        else
        {
            activeCustomers.Remove(direction);
            Destroy(targetCustomer.gameObject);
            Debug.Log($"[CustomerManager] {direction} 방향 손님이 요구한 주문과 다릅니다! 손님이 화를 내며 떠납니다.");
        }

        return result;
    }

    // ─────────────────────────────────────
    //  내부 메서드
    // ─────────────────────────────────────

    private void HandleCustomerExpired(Customer customer)
    {
        // 만료된 손님 찾기
        ServingManager.ServeDirection? expiredDir = null;
        foreach (var kvp in activeCustomers)
        {
            if (kvp.Value == customer)
            {
                expiredDir = kvp.Key;
                break;
            }
        }

        if (expiredDir.HasValue)
        {
            activeCustomers.Remove(expiredDir.Value);
            Debug.Log($"[CustomerManager] {expiredDir.Value} 방향 손님 대기 시간 만료! 퇴장합니다.");
        }

        customer.OnExpired -= HandleCustomerExpired;
        OnCustomerExpired?.Invoke(customer);

        // 콤보 초기화
        if (scoreManager != null) scoreManager.ResetCombo();

        Destroy(customer.gameObject);
    }

    /// <summary>
    /// Customer.RefreshBubbleView 가 쓰는 Legacy Text 를 찾거나, 없으면 NPC 스프라이트 프리팹용 말풍선을 붙입니다.
    /// (SpawnManager 의 NPC_M_1_62 등에는 Text 가 없어 주문이 비어 있었음.)
    /// </summary>
    private void TryBindSpeechBubbleText(GameObject custObj, ServingManager.ServeDirection direction, Customer customer)
    {
        UnityEngine.UI.Text txt = custObj.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (txt == null)
        {
            var tmpType = Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
            if (tmpType != null && custObj.GetComponentInChildren(tmpType, true) != null)
            {
                Debug.LogWarning(
                    "[CustomerManager] 손님에 Legacy Text 가 없고 TMP 만 있습니다. 주문 표시용 BubbleText 를 추가합니다.");
            }

            AddRuntimeSpeechBubbleHost(custObj, direction);
            txt = custObj.GetComponentInChildren<UnityEngine.UI.Text>(true);
        }

        if (txt != null)
            customer.SetupUI(txt);
    }

    private Sprite ResolveSpeechBubbleFrameForDirection(ServingManager.ServeDirection direction)
    {
        switch (direction)
        {
            case ServingManager.ServeDirection.Left:
                if (speechBubbleFrameSpriteLeft != null) return speechBubbleFrameSpriteLeft;
                break;
            case ServingManager.ServeDirection.Right:
                if (speechBubbleFrameSpriteRight != null) return speechBubbleFrameSpriteRight;
                break;
            case ServingManager.ServeDirection.Down:
                if (!cachedDownSpeechBubbleResolved)
                {
                    cachedDownSpeechBubbleSprite = PickDownSpeechBubbleFrameSprite();
                    cachedDownSpeechBubbleResolved = true;
                }

                if (cachedDownSpeechBubbleSprite != null) return cachedDownSpeechBubbleSprite;
                break;
        }

        return speechBubbleFrameSprite;
    }

    private Sprite PickDownSpeechBubbleFrameSprite()
    {
        Sprite primary = speechBubbleFrameSpriteDown;
        Sprite variant = speechBubbleFrameSpriteDownVariant;

        if (primary != null && variant != null)
            return thisSpawnDownSideIndex == 0 ? primary : variant;

        if (primary != null) return primary;
        if (variant != null) return variant;
        return null;
    }

    private static void ApplyImageSpriteAndSlicedType(UnityEngine.UI.Image img, Sprite spr)
    {
        if (img == null || spr == null) return;
        img.sprite = spr;
        Vector4 b = spr.border;
        bool useSliced = b.x > 0f || b.y > 0f || b.z > 0f || b.w > 0f;
        img.type = useSliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
    }

    /// <summary>
    /// BubbleBox Image 에 방향에 맞는 말풍선 스프라이트를 넣습니다.
    /// </summary>
    private void ApplySpeechBubbleFrameIfAssigned(GameObject custRoot, ServingManager.ServeDirection direction)
    {
        if (custRoot == null) return;

        Sprite spr = ResolveSpeechBubbleFrameForDirection(direction);
        if (spr == null) return;

        Transform bubbleTf = FindBubbleBoxTransform(custRoot.transform);
        if (bubbleTf == null) return;

        var img = bubbleTf.GetComponent<UnityEngine.UI.Image>();
        ApplyImageSpriteAndSlicedType(img, spr);
        if (img != null)
            img.raycastTarget = false;

        ApplyBubbleOrientationForSlot(bubbleTf as RectTransform, direction);
    }

    private static Transform FindBubbleBoxTransform(Transform root)
    {
        if (root == null) return null;
        Transform t = root.Find("BubbleBox");
        if (t != null) return t;
        Transform canvas = root.Find("SpeechBubbleCanvas");
        return canvas != null ? canvas.Find("BubbleBox") : null;
    }

    /// <summary>
    /// 스프라이트 NPC 루트 아래에 World Space 말풍선 Canvas 를 붙입니다.
    /// </summary>
    private void AddRuntimeSpeechBubbleHost(GameObject custRoot, ServingManager.ServeDirection direction)
    {
        if (custRoot == null) return;
        if (custRoot.transform.Find("SpeechBubbleCanvas") != null) return;

        GameObject host = new GameObject("SpeechBubbleCanvas");
        host.transform.SetParent(custRoot.transform, false);

        RectTransform rt = host.AddComponent<RectTransform>();
        rt.localRotation = Quaternion.identity;
        rt.localPosition = ResolveSpeechBubbleHostLocalOffset(direction);
        rt.sizeDelta = new Vector2(400f, 400f);

        float ps = Mathf.Max(
            Mathf.Max(Mathf.Abs(custRoot.transform.lossyScale.x), Mathf.Abs(custRoot.transform.lossyScale.y)),
            1e-4f);
        const float referenceWorldScale = 0.003f;
        float sc = referenceWorldScale / ps;
        host.transform.localScale = new Vector3(sc, sc, sc);

        Canvas canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        host.AddComponent<UnityEngine.UI.CanvasScaler>();

        BuildSpeechBubbleUnderCanvasTransform(host.transform, direction);
    }

    /// <summary>
    /// Canvas 루트 아래에 BubbleBox + BubbleText 를 만듭니다. (CreateCustomerUI / 런타임 말풍선 공용)
    /// </summary>
    private UnityEngine.UI.Text BuildSpeechBubbleUnderCanvasTransform(
        Transform canvasRoot,
        ServingManager.ServeDirection selectedSlot)
    {
        GameObject bubbleObj = new GameObject("BubbleBox");
        bubbleObj.transform.SetParent(canvasRoot, false);
        UnityEngine.UI.Image bubbleImg = bubbleObj.AddComponent<UnityEngine.UI.Image>();
        bubbleImg.color = Color.white;
        bubbleImg.raycastTarget = false;
        Sprite frame = ResolveSpeechBubbleFrameForDirection(selectedSlot);
        if (frame != null)
            ApplyImageSpriteAndSlicedType(bubbleImg, frame);
        RectTransform bubbleRt = bubbleObj.GetComponent<RectTransform>();
        bubbleRt.anchoredPosition = ResolveSpeechBubbleAnchoredPosition(selectedSlot);

        bubbleRt.sizeDelta = new Vector2(268f, 172f);

        var vlg = bubbleObj.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 6f;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight = false;

        GameObject labelObj = new GameObject("BubbleLabel");
        labelObj.transform.SetParent(bubbleObj.transform, false);
        UnityEngine.UI.Text labelTxt = labelObj.AddComponent<UnityEngine.UI.Text>();
        labelTxt.alignment = TextAnchor.MiddleCenter;
        labelTxt.fontSize = 22;
        labelTxt.color = Color.black;
        labelTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelTxt.raycastTarget = false;
        var labelLe = labelObj.AddComponent<UnityEngine.UI.LayoutElement>();
        labelLe.preferredWidth = 240f;
        labelLe.preferredHeight = 28f;

        GameObject rowObj = new GameObject("OrderIconRow");
        rowObj.transform.SetParent(bubbleObj.transform, false);
        var hlg = rowObj.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 10;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        var rowLe = rowObj.AddComponent<UnityEngine.UI.LayoutElement>();
        rowLe.preferredHeight = 48f;
        rowLe.preferredWidth = 220f;

        const float iconSize = 40f;
        for (int i = 0; i < 3; i++)
        {
            GameObject slot = new GameObject($"Slot{i}");
            slot.transform.SetParent(rowObj.transform, false);
            UnityEngine.UI.Image slotImg = slot.AddComponent<UnityEngine.UI.Image>();
            slotImg.preserveAspect = true;
            slotImg.color = Color.white;
            slotImg.raycastTarget = false;
            var slotLe = slot.AddComponent<UnityEngine.UI.LayoutElement>();
            slotLe.preferredWidth = iconSize;
            slotLe.preferredHeight = iconSize;
        }

        ApplyBubbleOrientationForSlot(bubbleRt, selectedSlot);

        return labelTxt;
    }

    /// <summary>
    /// 하단 손님 말풍선 꼬리 방향 등을 스프라이트 추가 없이 맞출 때 BubbleBox 의 스케일/회전을 조정합니다.
    /// </summary>
    private void ApplyBubbleOrientationForSlot(RectTransform bubbleRt, ServingManager.ServeDirection direction)
    {
        if (bubbleRt == null) return;

        if (direction != ServingManager.ServeDirection.Down)
        {
            bubbleRt.localScale = Vector3.one;
            bubbleRt.localEulerAngles = Vector3.zero;
            return;
        }

        bool flipX = thisSpawnDownSideIndex == 0 ? speechBubbleFlipXForDownLeft : speechBubbleFlipXForDownRight;
        bool flipY = thisSpawnDownSideIndex == 0 ? speechBubbleFlipYForDownLeft : speechBubbleFlipYForDownRight;
        float rotZ = thisSpawnDownSideIndex == 0 ? speechBubbleRotationZForDownLeft : speechBubbleRotationZForDownRight;

        Vector3 s = Vector3.one;
        if (flipX) s.x = -1f;
        if (flipY) s.y = -1f;
        bubbleRt.localScale = s;
        bubbleRt.localEulerAngles = new Vector3(0f, 0f, rotZ);
    }

    private Vector3 ResolveSpeechBubbleHostLocalOffset(ServingManager.ServeDirection direction)
    {
        switch (direction)
        {
            case ServingManager.ServeDirection.Left: return speechBubbleHostLocalOffsetLeft;
            case ServingManager.ServeDirection.Right: return speechBubbleHostLocalOffsetRight;
            case ServingManager.ServeDirection.Down:
            {
                Vector3 side = thisSpawnDownSideIndex == 0
                    ? speechBubbleHostLocalOffsetDownLeft
                    : speechBubbleHostLocalOffsetDownRight;
                if (side != Vector3.zero) return side;
                return speechBubbleHostLocalOffsetDown;
            }
            default: return Vector3.zero;
        }
    }

    private Vector2 ResolveSpeechBubbleAnchoredPosition(ServingManager.ServeDirection selectedSlot)
    {
        switch (selectedSlot)
        {
            case ServingManager.ServeDirection.Left:
                return speechBubbleAnchoredPositionLeft;
            case ServingManager.ServeDirection.Right:
                return speechBubbleAnchoredPositionRight;
            case ServingManager.ServeDirection.Down:
                return thisSpawnDownSideIndex == 0
                    ? speechBubbleAnchoredPositionDownLeft
                    : speechBubbleAnchoredPositionDownRight;
            default:
                return speechBubbleAnchoredPositionLeft;
        }
    }

    /// <summary>
    /// World Space 말풍선 캔버스 정렬 + 손님 오브젝트 전체 UI에서 레이캐스트 차단.
    /// (Screen Space - Camera 말풍선만 있어도 주문 아이콘 Image 가 터치를 먹으면 게임 입력이 막힙니다.)
    /// </summary>
    private static void EnsureCustomerWorldCanvasRendersOnTop(Transform customerRoot)
    {
        if (customerRoot == null) return;

        const int kCustomerWorldCanvasSortOrder = 200;

        var canvases = customerRoot.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c == null || c.renderMode != RenderMode.WorldSpace) continue;

            c.overrideSorting = true;
            if (c.sortingOrder < kCustomerWorldCanvasSortOrder)
                c.sortingOrder = kCustomerWorldCanvasSortOrder;

            if (c.worldCamera == null)
            {
                var cam = Camera.main;
                if (cam != null) c.worldCamera = cam;
            }
        }

        var graphics = customerRoot.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
        for (int g = 0; g < graphics.Length; g++)
        {
            if (graphics[g] != null)
                graphics[g].raycastTarget = false;
        }
    }

    private GameObject CreateCustomerUI(ServingManager.ServeDirection selectedSlot, CustomerType type)
    {
        GameObject custObj = new GameObject($"CustomerUI_{selectedSlot}_{type}");
        Canvas canvas = custObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        custObj.AddComponent<UnityEngine.UI.CanvasScaler>();

        RectTransform rt = custObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400f, 400f);
        custObj.transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);

        // 몸통 (타입별 색상)
        Color bodyColor = GetCustomerColor(type);
        GameObject bodyObj = new GameObject("BodyBox");
        bodyObj.transform.SetParent(custObj.transform, false);
        UnityEngine.UI.Image img = bodyObj.AddComponent<UnityEngine.UI.Image>();
        img.color = bodyColor;
        img.raycastTarget = false;
        RectTransform bodyRt = bodyObj.GetComponent<RectTransform>();
        bodyRt.anchoredPosition = new Vector2(0, -50f);
        bodyRt.sizeDelta = new Vector2(200f, 200f);

        BuildSpeechBubbleUnderCanvasTransform(custObj.transform, selectedSlot);

        // 타이머 게이지 (머리 위, 기본 숨김)
        GameObject timerBg = new GameObject("TimerGaugeBg");
        timerBg.transform.SetParent(custObj.transform, false);
        UnityEngine.UI.Image bgImg = timerBg.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        bgImg.raycastTarget = false;
        RectTransform timerBgRt = timerBg.GetComponent<RectTransform>();
        timerBgRt.anchoredPosition = new Vector2(0, 230f);
        timerBgRt.sizeDelta = new Vector2(180f, 20f);

        GameObject timerFill = new GameObject("TimerGaugeFill");
        timerFill.transform.SetParent(timerBg.transform, false);
        UnityEngine.UI.Image fillImg = timerFill.AddComponent<UnityEngine.UI.Image>();
        fillImg.color = Color.green;
        fillImg.raycastTarget = false;
        fillImg.type = UnityEngine.UI.Image.Type.Filled;
        fillImg.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        RectTransform fillRt = timerFill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;
        fillRt.anchoredPosition = Vector2.zero;

        // Customer 컴포넌트에 타이머 게이지 연결
        Customer customer = custObj.GetComponent<Customer>();
        if (customer == null) customer = custObj.AddComponent<Customer>();
        customer.SetupTimerGauge(timerBg, fillImg);
        timerBg.SetActive(false);

        return custObj;
    }

    private Color GetCustomerColor(CustomerType type)
    {
        switch (type)
        {
            case CustomerType.Rich:     return new Color(1f, 0.84f, 0f);       // 금색
            case CustomerType.Annoying: return new Color(0.9f, 0.2f, 0.2f);    // 빨강
            case CustomerType.Kid:      return new Color(1f, 0.6f, 0.8f);      // 분홍
            default:                    return new Color(0.2f, 0.8f, 0.8f);    // 청록 (기존)
        }
    }

    private Transform GetSpawnTransform(ServingManager.ServeDirection dir)
    {
        switch (dir)
        {
            case ServingManager.ServeDirection.Left: return leftSlot;
            case ServingManager.ServeDirection.Right: return rightSlot;
            case ServingManager.ServeDirection.Down: return bottomSlot;
            default: return null;
        }
    }
}
