using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

/// <summary>
/// 빙수 제조기 - 얼음 생성(5탭) 및 토핑 관리 전담
/// MobileInputManager로부터 탭 이벤트를 받아 얼음을 생성합니다.
/// </summary>
public class IceMachine : MonoBehaviour
{
    public enum ToppingType { RedBean, Milk, Fruit }

    [Header("Ice Making Settings")]
    [SerializeField] private int maxIceTaps = 5;

    [Header("UI References")]
    [SerializeField] private Image icePreviewImage;
    [SerializeField] private Button redBeanButton;
    [SerializeField] private Button milkButton;
    [SerializeField] private Button fruitButton;
    [Tooltip("얼음 완성 후에만 완전히 보일 하단 토핑 패널 (Panel_Toppings_Bottom)")]
    [SerializeField] private GameObject toppingsPanel;

    [Header("Toppings Panel Visibility")]
    [Tooltip("얼음 생성 중일 때 토핑 패널 투명도 (0=안 보임, 1=완전히 보임)")]
    [SerializeField, Range(0f, 1f)] private float toppingsAlphaWhileBuildingIce = 0.8f;
    [Tooltip("얼음이 완성되어 토핑을 올려야 할 때의 투명도")]
    [SerializeField, Range(0f, 1f)] private float toppingsAlphaWhenReady = 1f;

    private CanvasGroup toppingsCanvasGroup;

    [Header("Toppings Limit")]
    [Tooltip("얼음 완성 후 올릴 수 있는 토핑 총 개수 제한")]
    [SerializeField] private int maxTotalToppings = 10;

    // 내부 상태
    private int currentIceTaps = 0;
    private bool[] toppingsAdded = new bool[3]; // RedBean, Milk, Fruit
    private int totalToppingsAdded = 0;
    /// <summary>IceTapArea(UI)와 MobileInputManager(Input System)가 같은 프레임에 각각 AddIce를 호출하는 경우 방지.</summary>
    private int lastAddIceFrame = -1;

    // 이벤트: 얼음 완성 시 토핑 버튼 활성화 알림
    public event Action OnIceCompleted;
    // 이벤트: 제조기 리셋 시 토핑 버튼 비활성화 알림
    public event Action OnIceReset;
    // 이벤트: 얼음 탭 1회 추가 알림 (current, max)
    public event Action<int, int> OnIceTapAdded;
    // 이벤트: 토핑 추가 알림
    public event Action<ToppingType> OnToppingAdded;

    // 프로퍼티
    public int CurrentIceTaps => currentIceTaps;
    public int MaxIceTaps => maxIceTaps;
    public bool IsComplete => currentIceTaps >= maxIceTaps;
    public bool HasAnyTopping => toppingsAdded[0] || toppingsAdded[1] || toppingsAdded[2];

    private void Awake()
    {
        // Image_IcePreview 자동 바인딩
        if (icePreviewImage == null)
        {
            GameObject iceGO = GameObject.Find("Image_IcePreview");
            if (iceGO != null)
            {
                icePreviewImage = iceGO.GetComponent<Image>();
            }
        }

        if (icePreviewImage != null)
        {
            icePreviewImage.type = Image.Type.Filled;
            icePreviewImage.fillMethod = Image.FillMethod.Vertical;
            icePreviewImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        }

        BindToppingButtons();

        // 토핑 패널 자동 바인딩 (버튼 바인딩 이후에 수행 — 활성 상태일 때 찾아야 함)
        if (toppingsPanel == null)
        {
            GameObject panelGO = GameObject.Find("Panel_Toppings_Bottom");
            if (panelGO != null) toppingsPanel = panelGO;
        }

        // CanvasGroup 준비 — 투명도/상호작용 토글용
        if (toppingsPanel != null)
        {
            toppingsCanvasGroup = toppingsPanel.GetComponent<CanvasGroup>();
            if (toppingsCanvasGroup == null)
            {
                toppingsCanvasGroup = toppingsPanel.AddComponent<CanvasGroup>();
            }
        }

        ResetIce();
    }

    private void BindToppingButtons()
    {
        // 버튼 자동 바인딩 및 이벤트 연결
        if (redBeanButton == null)
        {
            GameObject go = GameObject.Find("Group_RedBean");
            if (go != null) redBeanButton = go.GetComponentInChildren<Button>();
        }
        if (milkButton == null)
        {
            GameObject go = GameObject.Find("Group_Milk");
            if (go != null) milkButton = go.GetComponentInChildren<Button>();
        }
        if (fruitButton == null)
        {
            GameObject go = GameObject.Find("Group_Fruit");
            if (go != null) fruitButton = go.GetComponentInChildren<Button>();
        }

        if (redBeanButton != null) redBeanButton.onClick.AddListener(() => AddTopping(ToppingType.RedBean));
        if (milkButton != null) milkButton.onClick.AddListener(() => AddTopping(ToppingType.Milk));
        if (fruitButton != null) fruitButton.onClick.AddListener(() => AddTopping(ToppingType.Fruit));

        RefreshToppingButtonPresentation();
    }

    private void Start()
    {
        // 다른 Awake가 토핑 쪽을 건드린 뒤에도 한 번 더 맞춤 + 플레이 중 스크립트 저장 직후 재진입 시에도 반영
        RefreshToppingButtonPresentation();
    }

    /// <summary>
    /// 토핑 버튼은 CanvasGroup 알파만으로 비활성/반투명을 표현합니다.
    /// ButtonPressFeedback / ColorTint 전환이 Image.color를 덮어쓰면 팥만 유난히 흐려 보일 수 있어 제거·고정합니다.
    /// </summary>
    private void RefreshToppingButtonPresentation()
    {
        StripPressFeedbackFromToppingButton(redBeanButton);
        StripPressFeedbackFromToppingButton(milkButton);
        StripPressFeedbackFromToppingButton(fruitButton);

        NormalizeButtonGraphic(redBeanButton);
        NormalizeButtonGraphic(milkButton);
        NormalizeButtonGraphic(fruitButton);
    }

    private static void NormalizeButtonGraphic(Button btn)
    {
        if (btn == null) return;

        // targetGraphic + 이 버튼 오브젝트 하위의 Graphic만 정규화 (형제/부모 패널 전체 스캔은 하지 않음)
        var graphics = new HashSet<Graphic>();
        if (btn.targetGraphic != null)
            graphics.Add(btn.targetGraphic);
        foreach (var g in btn.GetComponentsInChildren<Graphic>(true))
        {
            if (g != null) graphics.Add(g);
        }

        foreach (var g in graphics)
        {
            var c = g.color;
            g.color = new Color(c.r, c.g, c.b, 1f);
        }

        // Transition.None은 환경에 따라 그래픽 알파가 깨지는 사례가 있어,
        // ColorTint를 쓰되 Normal/Pressed 등을 모두 같은 색으로 맞춰 "틴트 없음"과 동일하게 둡니다.
        ApplyNeutralColorTint(btn);

        var fb = btn.GetComponent<ButtonPressFeedback>();
        if (fb != null)
            fb.RefreshOriginalColorFromTarget();
    }

    private static void ApplyNeutralColorTint(Button btn)
    {
        Graphic g = btn.targetGraphic != null ? btn.targetGraphic : btn.GetComponent<Graphic>();
        Color n = g != null ? g.color : Color.white;
        n.a = 1f;
        var block = btn.colors;
        block.normalColor = n;
        block.highlightedColor = n;
        block.pressedColor = n;
        block.selectedColor = n;
        block.disabledColor = n;
        block.colorMultiplier = 1f;
        block.fadeDuration = 0f;
        btn.colors = block;
        btn.transition = Selectable.Transition.ColorTint;
    }

    /// <summary>
    /// 토핑 전용: ButtonPressFeedback은 제거만 하고 새로 붙이지 않습니다.
    /// </summary>
    private static void StripPressFeedbackFromToppingButton(Button btn)
    {
        if (btn == null) return;

        Graphic targetGraphic = btn.targetGraphic != null
            ? btn.targetGraphic
            : btn.GetComponent<Graphic>();
        if (targetGraphic != null && !targetGraphic.raycastTarget)
            targetGraphic.raycastTarget = true;

        var feedback = btn.GetComponent<ButtonPressFeedback>();
        if (feedback != null)
        {
            // 제거 직전 눌림 상태를 풀어 두면 OnDisable/원복이 안전합니다.
            feedback.ForceRelease();
            UnityEngine.Object.DestroyImmediate(feedback);
        }
    }

    /// <summary>
    /// 얼음 생성 단계에서는 반투명(상호작용 불가), 완성 후에는 완전 표시(상호작용 가능).
    /// </summary>
    private void SetToppingsReady(bool ready)
    {
        EnsureToppingsCanvasGroup();

        if (toppingsCanvasGroup != null)
        {
            toppingsCanvasGroup.alpha = ready ? toppingsAlphaWhenReady : toppingsAlphaWhileBuildingIce;
            // interactable을 ready에 따라 껐다 켜면 Unity의 Disabled tint(DisabledColor 알파)이 적용될 수 있어
            // "비활성 -> 투명"이 아닌 "알파는 CanvasGroup alpha로만" 제어하도록 interactable은 항상 true로 둡니다.
            toppingsCanvasGroup.interactable = true;
            toppingsCanvasGroup.blocksRaycasts = ready;

            if (redBeanButton != null) redBeanButton.interactable = true;
            if (milkButton != null) milkButton.interactable = true;
            if (fruitButton != null) fruitButton.interactable = true;

            // 토핑 아이콘은 ButtonPressFeedback 없이 운용 — Graphic 알파만 정규화
            NormalizeButtonGraphic(redBeanButton);
            NormalizeButtonGraphic(milkButton);
            NormalizeButtonGraphic(fruitButton);
        }
        else if (toppingsPanel != null)
        {
            toppingsPanel.SetActive(ready);
        }
    }

    private void EnsureToppingsCanvasGroup()
    {
        if (toppingsPanel == null)
        {
            var go = GameObject.Find("Panel_Toppings_Bottom");
            if (go != null) toppingsPanel = go;
        }
        if (toppingsPanel != null && toppingsCanvasGroup == null)
        {
            toppingsCanvasGroup = toppingsPanel.GetComponent<CanvasGroup>();
            if (toppingsCanvasGroup == null)
                toppingsCanvasGroup = toppingsPanel.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// 얼음 1개 추가 (MobileInputManager에서 호출)
    /// </summary>
    public bool AddIce()
    {
        if (Time.frameCount == lastAddIceFrame)
            return false;

        if (IsComplete)
        {
            Debug.Log($"[IceMachine] 이미 빙수가 완성되었습니다. 토핑을 추가하거나 서빙하세요. ({currentIceTaps}/{maxIceTaps})");
            return false;
        }

        lastAddIceFrame = Time.frameCount;
        currentIceTaps++;
        RefreshVisuals();
        OnIceTapAdded?.Invoke(currentIceTaps, maxIceTaps);

        if (IsComplete)
        {
            Debug.Log($"[IceMachine] ★ 빙수 베이스 완성! ({currentIceTaps}/{maxIceTaps}) — 토핑을 추가하거나 서빙하세요.");
            SetToppingsReady(true);
            OnIceCompleted?.Invoke();
        }
        else
        {
            Debug.Log($"[하단 터치] 얼음 생성 ({currentIceTaps}/{maxIceTaps})");
        }

        return true;
    }

    /// <summary>
    /// 토핑 추가 (빙수 완성 후에만 가능)
    /// </summary>
    public bool AddTopping(ToppingType type)
    {
        if (!IsComplete)
        {
            Debug.Log("[IceMachine] 빙수가 아직 완성되지 않았습니다. 얼음을 먼저 생성하세요.");
            return false;
        }

        if (totalToppingsAdded >= maxTotalToppings)
        {
            Debug.Log($"[IceMachine] 토핑은 최대 {maxTotalToppings}개까지 올릴 수 있습니다.");
            return false;
        }

        int index = (int)type;
        // 주문 매칭 로직은 "해당 토핑이 포함되었는지"만 보므로 bool은 첫 1회만 true로 둡니다.
        if (!toppingsAdded[index])
            toppingsAdded[index] = true;

        totalToppingsAdded++;
        Debug.Log($"[하단 터치] 토핑 추가: {type}");
        OnToppingAdded?.Invoke(type);
        return true;
    }

    /// <summary>
    /// 현재 빙수 데이터 반환 (서빙 시 사용)
    /// </summary>
    public ShavedIceData GetCurrentIce()
    {
        ShavedIceData data;
        data.iceTaps = currentIceTaps;
        data.isComplete = IsComplete;
        data.hasRedBean = toppingsAdded[0];
        data.hasMilk = toppingsAdded[1];
        data.hasFruit = toppingsAdded[2];
        data.totalToppings = totalToppingsAdded;
        return data;
    }

    /// <summary>
    /// 제조기 초기화 (서빙 완료 또는 폐기 시)
    /// </summary>
    public void ResetIce()
    {
        lastAddIceFrame = -1;
        currentIceTaps = 0;
        toppingsAdded[0] = false;
        toppingsAdded[1] = false;
        toppingsAdded[2] = false;
        totalToppingsAdded = 0;
        RefreshVisuals();
        SetToppingsReady(false);
        OnIceReset?.Invoke();
    }

    private void RefreshVisuals()
    {
        if (icePreviewImage != null)
        {
            float fillRatio = (float)currentIceTaps / maxIceTaps;
            icePreviewImage.fillAmount = fillRatio;
        }
    }

    // ─────────────────────────────────────
    //  UI 시각화용 헬퍼 (Sprite 접근)
    // ─────────────────────────────────────

    public Sprite GetIceSpriteForUI()
    {
        return icePreviewImage != null ? icePreviewImage.sprite : null;
    }

    public Sprite GetToppingSpriteForUI(ToppingType type)
    {
        Button btn = null;
        switch (type)
        {
            case ToppingType.RedBean: btn = redBeanButton; break;
            case ToppingType.Milk: btn = milkButton; break;
            case ToppingType.Fruit: btn = fruitButton; break;
        }

        if (btn == null) return null;
        var img = btn.GetComponentInChildren<Image>(true);
        return img != null ? img.sprite : null;
    }
}

/// <summary>
/// 빙수 데이터 구조체
/// </summary>
[System.Serializable]
public struct ShavedIceData
{
    public int iceTaps;
    public bool isComplete;
    public bool hasRedBean;
    public bool hasMilk;
    public bool hasFruit;
    public int totalToppings;

    public int ToppingCount
    {
        get
        {
            return totalToppings;
        }
    }
}
