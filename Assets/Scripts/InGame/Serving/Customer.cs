using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 손님 타입 열거형 — Normal(일반), Rich(부자), Annoying(진상), Kid(아이 동반)
/// </summary>
public enum CustomerType { Normal, Rich, Annoying, Kid }

/// <summary>
/// 손님 — 주문 데이터, 10초 대기 타이머, 타이머 게이지 UI, 특수 타입을 관리합니다.
/// </summary>
public class Customer : MonoBehaviour
{
    private ShavedIceData orderData;
    public ShavedIceData OrderData => orderData;

    [Header("UI")]
    [SerializeField] private Text speechBubbleText;
    [Tooltip("비우면 씬의 IceMachine 이 하단 토핑 버튼에서 쓰는 스프라이트를 가져옵니다.")]
    [SerializeField] private Sprite orderIconRedBean;
    [SerializeField] private Sprite orderIconMilk;
    [SerializeField] private Sprite orderIconFruit;

    /// <summary>CustomerManager 등에서 넣는 런타임 우선 스프라이트(null 이면 무시).</summary>
    private Sprite runtimeOrderIconRedBean;
    private Sprite runtimeOrderIconMilk;
    private Sprite runtimeOrderIconFruit;

    private Image[] orderIconSlots;
    private GameObject timerGaugeObj;
    private Image timerGaugeFill;

    private IceMachine cachedIceMachineForOrderUi;

    [Header("Type")]
    public CustomerType customerType = CustomerType.Normal;

    // 대기 타이머
    private float maxWaitTime = 10f;
    private float timerSpeedMultiplier = 1f;
    private float waitTimer;
    private bool isTimerActive = false;

    // 이동 (프리팹 루트 Transform만 이동 — 별도 임시 오브젝트 없음)
    private Vector3 targetPos;
    private bool isWalking = false;
    [Tooltip("슬롯까지 등속 이동(월드 단위/초). Lerp가 아닌 MoveTowards로 속도 일정.")]
    [SerializeField] private float walkSpeedWorldUnitsPerSecond = 2.5f;
    [SerializeField] private float arriveEpsilon = 0.02f;

    // 만료 이벤트
    public event Action<Customer> OnExpired;

    private void Awake()
    {
        EnsureCustomerVisuals();
    }

    /// <summary>
    /// 스폰 직후부터 스프라이트가 보이도록 보정 (프리팹 Z스케일 0 등으로 흰 네모만 보이는 경우 방지).
    /// </summary>
    private void EnsureCustomerVisuals()
    {
        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] != null)
                srs[i].enabled = true;
        }

        Vector3 s = transform.localScale;
        if (Mathf.Abs(s.z) < 1e-4f)
            transform.localScale = new Vector3(s.x, s.y, 1f);
    }

    public void MoveTo(Vector3 destPos)
    {
        targetPos = destPos;
        isWalking = true;
    }

    /// <summary>
    /// 대기 타이머 시작 (스폰 즉시 호출)
    /// </summary>
    public void Activate(float waitTime, float speedMult)
    {
        maxWaitTime = waitTime;
        timerSpeedMultiplier = speedMult;
        waitTimer = maxWaitTime;
        isTimerActive = true;
    }

    private void Update()
    {
        // 이동 처리
        if (isWalking)
        {
            float step = walkSpeedWorldUnitsPerSecond * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, step);
            if (Vector3.Distance(transform.position, targetPos) <= arriveEpsilon)
            {
                transform.position = targetPos;
                isWalking = false;
            }
        }

        // 대기 타이머 — 걷는 중에는 카운트 정지, 도착 후부터 카운트다운
        if (isTimerActive && !isWalking)
        {
            waitTimer -= Time.deltaTime * timerSpeedMultiplier;

            // 남은 5초 이하: 타이머 게이지 표시
            if (waitTimer <= 5f)
            {
                if (timerGaugeObj != null && !timerGaugeObj.activeSelf)
                    timerGaugeObj.SetActive(true);

                if (timerGaugeFill != null)
                {
                    timerGaugeFill.fillAmount = Mathf.Clamp01(waitTimer / 5f);

                    // 남은 시간에 따라 색상 변경 (녹→황→적)
                    if (waitTimer <= 2f)
                        timerGaugeFill.color = Color.red;
                    else if (waitTimer <= 3.5f)
                        timerGaugeFill.color = Color.yellow;
                    else
                        timerGaugeFill.color = Color.green;
                }
            }

            // 만료
            if (waitTimer <= 0f)
            {
                isTimerActive = false;
                Debug.Log($"[Customer] 손님 대기 시간 만료! 타입: {customerType}");
                OnExpired?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// 타이머 게이지 UI 연결 (CustomerManager에서 호출)
    /// </summary>
    public void SetupTimerGauge(GameObject gaugeObj, Image fillImg)
    {
        timerGaugeObj = gaugeObj;
        timerGaugeFill = fillImg;
        if (timerGaugeObj != null)
            timerGaugeObj.SetActive(false); // 첫 5초간 숨김
    }

    /// <summary>
    /// 지정된 개수만큼 무작위 토핑을 요구하는 주문 생성
    /// </summary>
    public void InitializeOrder(int requiredToppings)
    {
        orderData = new ShavedIceData();
        orderData.iceTaps = 5;
        orderData.isComplete = true;
        orderData.totalToppings = requiredToppings;

        int[] toppings = { 0, 1, 2 };

        // Fisher-Yates 셔플
        for (int i = 0; i < toppings.Length; i++)
        {
            int rnd = UnityEngine.Random.Range(i, toppings.Length);
            int temp = toppings[rnd];
            toppings[rnd] = toppings[i];
            toppings[i] = temp;
        }

        for (int i = 0; i < requiredToppings && i < 3; i++)
        {
            if (toppings[i] == 0) orderData.hasRedBean = true;
            else if (toppings[i] == 1) orderData.hasMilk = true;
            else if (toppings[i] == 2) orderData.hasFruit = true;
        }

        RefreshBubbleView();
    }

    public void RefreshBubbleView()
    {
        bool anyTopping = orderData.hasRedBean || orderData.hasMilk || orderData.hasFruit;

        if (speechBubbleText != null)
        {
            if (anyTopping)
            {
                speechBubbleText.text = string.Empty;
                speechBubbleText.gameObject.SetActive(false);
            }
            else
            {
                speechBubbleText.gameObject.SetActive(true);
                speechBubbleText.text = "(토핑 없음)";
            }
        }

        EnsureOrderIconSlotsCached();
        if (orderIconSlots == null || orderIconSlots.Length != 3)
            return;

        if (cachedIceMachineForOrderUi == null)
            cachedIceMachineForOrderUi = UnityEngine.Object.FindAnyObjectByType<IceMachine>();

        bool[] has = { orderData.hasRedBean, orderData.hasMilk, orderData.hasFruit };
        IceMachine.ToppingType[] types =
        {
            IceMachine.ToppingType.RedBean,
            IceMachine.ToppingType.Milk,
            IceMachine.ToppingType.Fruit
        };

        int shown = 0;
        for (int t = 0; t < 3; t++)
        {
            if (!has[t])
                continue;

            if (shown >= orderIconSlots.Length)
                break;

            Image img = orderIconSlots[shown];
            if (img != null)
            {
                Sprite s = GetOrderSpriteForTopping(types[t], cachedIceMachineForOrderUi);
                img.sprite = s;
                img.color = s != null ? Color.white : new Color(0.75f, 0.75f, 0.75f, 1f);
                img.raycastTarget = false;
                img.gameObject.SetActive(true);
            }

            shown++;
        }

        for (int s = shown; s < orderIconSlots.Length; s++)
        {
            if (orderIconSlots[s] != null)
                orderIconSlots[s].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 씬의 CustomerManager 인스펙터에 넣은 토핑 아이콘을 쓰려면 스폰 직후 이 메서드를 호출합니다(null 은 해당 토핑만 기본 규칙).
    /// </summary>
    public void ApplyOrderIconSourcesFromManager(Sprite redBean, Sprite milk, Sprite fruit)
    {
        runtimeOrderIconRedBean = redBean;
        runtimeOrderIconMilk = milk;
        runtimeOrderIconFruit = fruit;
        RefreshBubbleView();
    }

    private Sprite GetOrderSpriteForTopping(IceMachine.ToppingType type, IceMachine iceMachine)
    {
        switch (type)
        {
            case IceMachine.ToppingType.RedBean:
                if (runtimeOrderIconRedBean != null) return runtimeOrderIconRedBean;
                if (orderIconRedBean != null) return orderIconRedBean;
                break;
            case IceMachine.ToppingType.Milk:
                if (runtimeOrderIconMilk != null) return runtimeOrderIconMilk;
                if (orderIconMilk != null) return orderIconMilk;
                break;
            case IceMachine.ToppingType.Fruit:
                if (runtimeOrderIconFruit != null) return runtimeOrderIconFruit;
                if (orderIconFruit != null) return orderIconFruit;
                break;
        }

        return iceMachine != null ? iceMachine.GetToppingSpriteForUI(type) : null;
    }

    private void EnsureOrderIconSlotsCached()
    {
        if (orderIconSlots != null && orderIconSlots.Length == 3 && orderIconSlots[0] != null)
            return;

        orderIconSlots = null;
        if (speechBubbleText == null)
            return;

        Transform bubble = speechBubbleText.transform.parent;
        if (bubble == null)
            return;

        Transform row = bubble.Find("OrderIconRow");
        if (row == null)
            return;

        var slots = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            Transform slot = row.Find($"Slot{i}");
            slots[i] = slot != null ? slot.GetComponent<Image>() : null;
        }

        orderIconSlots = slots;
    }

    /// <summary>
    /// 동적으로 생성된 UI Text 할당
    /// </summary>
    public void SetupUI(Text textComponent)
    {
        speechBubbleText = textComponent;
        orderIconSlots = null;
        cachedIceMachineForOrderUi = null;
    }

    /// <summary>
    /// 만들어진 빙수가 이 손님의 주문과 정확히 일치하는지 판별
    /// </summary>
    public bool IsOrderMatched(ShavedIceData iceData)
    {
        if (!iceData.isComplete) return false;

        return iceData.hasRedBean == orderData.hasRedBean &&
               iceData.hasMilk == orderData.hasMilk &&
               iceData.hasFruit == orderData.hasFruit;
    }
}
