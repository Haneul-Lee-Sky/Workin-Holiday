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
    /// 손님 NPC를 Panel_Maker_Center 하위 Table 에 붙이기 위한 부모 Transform 을 찾습니다.
    /// </summary>
    private void ResolveCustomerMountParent()
    {
        if (customerMountParent != null) return;

        var panel = GameObject.Find("Panel_Maker_Center");
        if (panel == null)
        {
            Debug.LogWarning("[CustomerManager] 'Panel_Maker_Center' 를 찾을 수 없습니다. 손님은 기존처럼 CustomerSlots 하위에 붙습니다.");
            return;
        }

        Transform table = panel.transform.Find("Table");
        if (table == null)
        {
            Debug.LogWarning("[CustomerManager] 'Panel_Maker_Center/Table' 을 찾을 수 없습니다. 손님은 기존처럼 CustomerSlots 하위에 붙습니다.");
            return;
        }

        customerMountParent = table;
        Debug.Log("[CustomerManager] 손님 부모를 'Panel_Maker_Center/Table' 로 설정했습니다.");
    }

    private Transform GetCustomerMountTransform(Transform spawnPoint)
    {
        if (customerMountParent != null)
            return customerMountParent;
        return spawnPoint != null ? spawnPoint.parent : null;
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
                custObj.transform.SetParent(mount, true);
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

        // World Space 손님 UI는 메인 Screen Space - Camera Canvas(-100 등)와 별도 정렬이라
        // 배경/패널 뒤로 밀릴 수 있음 → Sort Order를 올립니다.
        EnsureCustomerWorldCanvasRendersOnTop(custObj.transform);

        // 주문 생성
        int reqToppingCount = scoreManager != null ? scoreManager.GetRequiredToppingCount() : 1;
        customer.InitializeOrder(reqToppingCount);

        activeCustomers[direction] = customer;
        Debug.Log($"[CustomerManager] {direction} 방향에 {type} 손님 등장! (요구 토핑: {reqToppingCount}개)");

        // 스폰 이벤트 — QuestManager가 구독해 특수 손님 등장 카운트에 사용
        OnCustomerSpawned?.Invoke(customer);
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

    /// <summary>
    /// 스프라이트 NPC 루트 아래에 World Space 말풍선 Canvas 를 붙입니다.
    /// </summary>
    private static void AddRuntimeSpeechBubbleHost(GameObject custRoot, ServingManager.ServeDirection direction)
    {
        if (custRoot == null) return;
        if (custRoot.transform.Find("SpeechBubbleCanvas") != null) return;

        GameObject host = new GameObject("SpeechBubbleCanvas");
        host.transform.SetParent(custRoot.transform, false);

        RectTransform rt = host.AddComponent<RectTransform>();
        rt.localRotation = Quaternion.identity;
        rt.localPosition = Vector3.zero;
        rt.sizeDelta = new Vector2(400f, 400f);

        float ps = Mathf.Max(
            Mathf.Max(Mathf.Abs(custRoot.transform.lossyScale.x), Mathf.Abs(custRoot.transform.lossyScale.y)),
            1e-4f);
        const float referenceWorldScale = 0.003f;
        float sc = referenceWorldScale / ps;
        host.transform.localScale = new Vector3(sc, sc, sc);

        Canvas canvas = host.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        host.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        host.AddComponent<UnityEngine.UI.CanvasScaler>();

        BuildSpeechBubbleUnderCanvasTransform(host.transform, direction);
    }

    /// <summary>
    /// Canvas 루트 아래에 BubbleBox + BubbleText 를 만듭니다. (CreateCustomerUI / 런타임 말풍선 공용)
    /// </summary>
    private static UnityEngine.UI.Text BuildSpeechBubbleUnderCanvasTransform(
        Transform canvasRoot,
        ServingManager.ServeDirection selectedSlot)
    {
        GameObject bubbleObj = new GameObject("BubbleBox");
        bubbleObj.transform.SetParent(canvasRoot, false);
        UnityEngine.UI.Image bubbleImg = bubbleObj.AddComponent<UnityEngine.UI.Image>();
        bubbleImg.color = Color.white;
        RectTransform bubbleRt = bubbleObj.GetComponent<RectTransform>();

        if (selectedSlot == ServingManager.ServeDirection.Left || selectedSlot == ServingManager.ServeDirection.Right)
            bubbleRt.anchoredPosition = new Vector2(0f, 140f);
        else
        {
            float offsetX = UnityEngine.Random.value > 0.5f ? 200f : -200f;
            bubbleRt.anchoredPosition = new Vector2(offsetX, 10f);
        }

        bubbleRt.sizeDelta = new Vector2(250f, 150f);

        GameObject txtObj = new GameObject("BubbleText");
        txtObj.transform.SetParent(bubbleObj.transform, false);
        UnityEngine.UI.Text txt = txtObj.AddComponent<UnityEngine.UI.Text>();
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 28;
        txt.color = Color.black;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        RectTransform txtRt = txtObj.GetComponent<RectTransform>();
        txtRt.sizeDelta = bubbleRt.sizeDelta;
        return txt;
    }

    /// <summary>
    /// 손님 루트 아래 World Space Canvas가 메인 UI Canvas보다 뒤에 그려지지 않도록 정렬을 고정합니다.
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
    }

    private GameObject CreateCustomerUI(ServingManager.ServeDirection selectedSlot, CustomerType type)
    {
        GameObject custObj = new GameObject($"CustomerUI_{selectedSlot}_{type}");
        Canvas canvas = custObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        custObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
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
        RectTransform bodyRt = bodyObj.GetComponent<RectTransform>();
        bodyRt.anchoredPosition = new Vector2(0, -50f);
        bodyRt.sizeDelta = new Vector2(200f, 200f);

        BuildSpeechBubbleUnderCanvasTransform(custObj.transform, selectedSlot);

        // 타이머 게이지 (머리 위, 기본 숨김)
        GameObject timerBg = new GameObject("TimerGaugeBg");
        timerBg.transform.SetParent(custObj.transform, false);
        UnityEngine.UI.Image bgImg = timerBg.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        RectTransform timerBgRt = timerBg.GetComponent<RectTransform>();
        timerBgRt.anchoredPosition = new Vector2(0, 230f);
        timerBgRt.sizeDelta = new Vector2(180f, 20f);

        GameObject timerFill = new GameObject("TimerGaugeFill");
        timerFill.transform.SetParent(timerBg.transform, false);
        UnityEngine.UI.Image fillImg = timerFill.AddComponent<UnityEngine.UI.Image>();
        fillImg.color = Color.green;
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
