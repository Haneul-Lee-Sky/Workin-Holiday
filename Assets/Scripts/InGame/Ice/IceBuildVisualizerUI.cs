using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 얼음 생성(탭) 및 토핑 추가를 UI로 시각화합니다.
/// - 얼음 탭 수에 따라 1장의 스프라이트가 단계별로 바뀜(쌓이는 것처럼 보이게)
/// - 토핑 추가 시 얼음 위에 차곡차곡 쌓임
/// - Reset 시 모두 제거
/// 
/// 씬에 미리 UI를 배치하지 않아도 되도록 런타임에 Panel_Maker_Center/Table 아래에 창을 생성합니다.
/// (이미 씬에 만들어둔 Panel_IceBuildWindow 가 있으면 재사용)
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(IceMachine))]
public class IceBuildVisualizerUI : MonoBehaviour
{
    [Header("Auto-create Window (uGUI)")]
    [Tooltip("비어 있으면 Panel_Maker_Center/Table 아래에 자동 생성합니다.")]
    [SerializeField] private RectTransform windowRoot;

    [Tooltip("Table 기준 앵커 위치 오프셋 (px). 값이 커질수록 위로 올라갑니다.")]
    [SerializeField] private Vector2 windowAnchoredPosition = new Vector2(0f, 170f);

    [SerializeField] private Vector2 windowSize = new Vector2(540f, 180f);

    [Header("Ice Stack Sprite (Steps)")]
    [Tooltip("얼음 탭 단계별 스프라이트(1~N). 5단계라면 5개를 넣으세요.")]
    [SerializeField] private Sprite[] iceStepSprites;

    [SerializeField] private float iceDisplaySize = 130f;

    [Header("Split Cup / Ice (from combined sprites)")]
    [Tooltip("컵이 얼음과 함께 합쳐진 스프라이트일 때, 컵/얼음을 런타임에서 분리해 겹쳐 표시합니다.")]
    [SerializeField] private bool splitCupAndIce = true;
    [Tooltip("체크 시, Hierarchy에서 직접 지정한 CupBase 스프라이트를 유지하고 Ice Step Sprites(얼음 전용)를 그대로 사용합니다. (런타임 분리/덮어쓰기 없음)")]
    [SerializeField] private bool useManualCupAndIceSprites = false;
    [Tooltip("스프라이트 rect의 바닥부터 '컵'으로 간주할 높이(px). (이 위는 얼음으로 처리)")]
    [SerializeField] private float cupHeightPixels = 360f;

    [Header("Topping Stack On Ice")]
    [SerializeField] private float toppingIconSize = 56f;
    [Tooltip("얼음 위에 쌓일 때만 적용. 스프라이트 여백 때문에 팥만 작아 보이면 1보다 크게(예: 1.1~1.2)")]
    [SerializeField, Range(0.5f, 2f)] private float stackVisualScaleRedBean = 1f;
    [SerializeField, Range(0.5f, 2f)] private float stackVisualScaleMilk = 1f;
    [SerializeField, Range(0.5f, 2f)] private float stackVisualScaleFruit = 1f;
    [SerializeField] private float toppingBaseYOffset = 78f;
    [SerializeField] private float toppingStackStepY = 22f;
    [Tooltip("체크 시 토핑을 항상 정중앙에만 쌓습니다.")]
    [SerializeField] private bool centerAlignToppings = true;
    [SerializeField] private float toppingStackStepX = 10f;
    [SerializeField] private int toppingMaxSideStep = 2;

    [Header("Window Layout")]
    [SerializeField] private float rowSpacing = 10f;
    [SerializeField] private float iconSpacing = 8f; // legacy: used by FindOrCreateRow

    private IceMachine iceMachine;
    private RectTransform iceContainer;
    private Image cupImage;
    private Image iceStackImage;
    private RectTransform toppingsContainer;
    private int toppingVisualCount = 0;

    // runtime-generated split sprites
    private Sprite cupSpriteRuntime;
    private Sprite[] iceOnlySpritesRuntime;

    private void Awake()
    {
        iceMachine = GetComponent<IceMachine>();
        EnsureWindow();
        if (iceStackImage != null) iceStackImage.enabled = iceStackImage.sprite != null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (centerAlignToppings)
        {
            toppingStackStepX = 0f;
            toppingMaxSideStep = 0;
        }
    }
#endif

    private void OnEnable()
    {
        if (iceMachine == null) iceMachine = GetComponent<IceMachine>();

        EnsureWindow();

        iceMachine.OnIceTapAdded += HandleIceTapAdded;
        iceMachine.OnToppingAdded += HandleToppingAdded;
        iceMachine.OnIceReset += HandleIceReset;

        // 사용자가 Hierarchy에서 CupBase 스프라이트를 직접 넣어둔 경우,
        // 합쳐진 스프라이트 분리 모드보다 "수동 컵/얼음" 모드가 의도에 더 가깝습니다.
        // (체크를 깜빡해도 인게임에서 얼음이 안 보이는 문제를 방지)
        if (cupImage != null && cupImage.sprite != null)
            useManualCupAndIceSprites = true;

        SyncFromMachineState();
    }

    private void OnDisable()
    {
        if (iceMachine == null) return;
        iceMachine.OnIceTapAdded -= HandleIceTapAdded;
        iceMachine.OnToppingAdded -= HandleToppingAdded;
        iceMachine.OnIceReset -= HandleIceReset;
    }

    private void EnsureWindow()
    {
        // 이미 씬에 만들어둔 창이 있으면 재사용 (Play 중 중복 생성 방지)
        if (windowRoot == null)
        {
            var existing = GameObject.Find("Panel_IceBuildWindow");
            if (existing != null)
                windowRoot = existing.GetComponent<RectTransform>();
        }

        if (windowRoot != null)
        {
            EnsureParts();
            EnsureTapArea();
            return;
        }

        var panel = GameObject.Find("Panel_Maker_Center");
        if (panel == null) return;

        var table = panel.transform.Find("Table");
        if (table == null) return;

        var go = new GameObject("Panel_IceBuildWindow", typeof(RectTransform));
        go.transform.SetParent(table, false);
        windowRoot = go.GetComponent<RectTransform>();
        windowRoot.anchorMin = new Vector2(0.5f, 0.5f);
        windowRoot.anchorMax = new Vector2(0.5f, 0.5f);
        windowRoot.pivot = new Vector2(0.5f, 0.5f);
        windowRoot.anchoredPosition = windowAnchoredPosition;
        windowRoot.sizeDelta = windowSize;

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = false;
        layout.spacing = rowSpacing;
        layout.padding = new RectOffset(14, 14, 12, 12);

        EnsureParts();
        EnsureTapArea();
    }

    private void EnsureParts()
    {
        if (windowRoot == null) return;

        EnsureCupAndIceImages(windowRoot, "IceStackContainer", "Image_CupBase", "Image_IceStack");
        toppingsContainer = FindOrCreateToppingsContainer(iceContainer, "ToppingsOnIce");
    }

    private void EnsureTapArea()
    {
        if (windowRoot == null) return;

        // PointerDown 이벤트를 받으려면 Graphic(RaycastTarget)이 필요합니다.
        var img = windowRoot.GetComponent<Image>();
        if (img == null)
            img = windowRoot.gameObject.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0f); // 완전 투명(보이지 않음)
        img.raycastTarget = true;

        var tap = windowRoot.GetComponent<IceTapArea>();
        if (tap == null)
            tap = windowRoot.gameObject.AddComponent<IceTapArea>();

        // IceMachine은 이 스크립트가 붙어있는 오브젝트의 IceMachine을 우선 사용
        var m = GetComponent<IceMachine>();
        if (m != null) tap.SetIceMachine(m);
    }

    private void EnsureCupAndIceImages(RectTransform parent, string containerName, string cupImageName, string iceImageName)
    {
        var existingContainer = parent.Find(containerName) as RectTransform;
        if (existingContainer == null)
        {
            var c = new GameObject(containerName, typeof(RectTransform), typeof(LayoutElement));
            c.transform.SetParent(parent, false);
            existingContainer = c.GetComponent<RectTransform>();

            var le = c.GetComponent<LayoutElement>();
            le.preferredWidth = iceDisplaySize;
            le.preferredHeight = iceDisplaySize;
            le.minWidth = iceDisplaySize;
            le.minHeight = iceDisplaySize;
        }
        iceContainer = existingContainer;

        // Cup image (bottom)
        var cupT = existingContainer.Find(cupImageName);
        if (cupT == null)
        {
            var goCup = new GameObject(cupImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            goCup.transform.SetParent(existingContainer, false);
            var rtCup = goCup.GetComponent<RectTransform>();
            rtCup.anchorMin = new Vector2(0.5f, 0f);
            rtCup.anchorMax = new Vector2(0.5f, 0f);
            rtCup.pivot = new Vector2(0.5f, 0f);
            rtCup.anchoredPosition = Vector2.zero;
            rtCup.sizeDelta = new Vector2(iceDisplaySize, iceDisplaySize);
            cupImage = goCup.GetComponent<Image>();
            cupImage.preserveAspect = true;
            cupImage.color = Color.white;
            cupImage.enabled = false;
        }
        else
        {
            cupImage = cupT.GetComponent<Image>();
        }

        // Ice image (on top)
        var iceT = existingContainer.Find(iceImageName);
        if (iceT == null)
        {
            var goIce = new GameObject(iceImageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            goIce.transform.SetParent(existingContainer, false);
            var rtIce = goIce.GetComponent<RectTransform>();
            rtIce.anchorMin = new Vector2(0.5f, 0f);
            rtIce.anchorMax = new Vector2(0.5f, 0f);
            rtIce.pivot = new Vector2(0.5f, 0f);
            rtIce.anchoredPosition = Vector2.zero;
            rtIce.sizeDelta = new Vector2(iceDisplaySize, iceDisplaySize);
            iceStackImage = goIce.GetComponent<Image>();
            iceStackImage.preserveAspect = true;
            iceStackImage.color = Color.white;
            iceStackImage.enabled = false;
        }
        else
        {
            iceStackImage = iceT.GetComponent<Image>();
        }
    }

    private RectTransform FindOrCreateToppingsContainer(RectTransform iceParent, string name)
    {
        if (iceParent == null) return null;

        var existing = iceParent.Find(name) as RectTransform;
        if (existing != null) return existing;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(iceParent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        return rt;
    }

    // legacy helper (not currently used, but kept to avoid breaking older scene structures)
    private RectTransform FindOrCreateRow(RectTransform parent, string name)
    {
        var existing = parent.Find(name) as RectTransform;
        if (existing != null) return existing;

        var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;

        var h = go.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlHeight = false;
        h.childControlWidth = false;
        h.childForceExpandHeight = false;
        h.childForceExpandWidth = false;
        h.spacing = iconSpacing;

        return rt;
    }

    private void HandleIceTapAdded(int current, int max)
    {
        if (iceStackImage == null) return;
        if (iceStepSprites == null || iceStepSprites.Length == 0) return;

        int idx = Mathf.Clamp(current - 1, 0, iceStepSprites.Length - 1);
        ApplyIceStep(idx);
    }

    private void HandleToppingAdded(IceMachine.ToppingType type)
    {
        if (toppingsContainer == null) return;
        var sprite = iceMachine != null ? iceMachine.GetToppingSpriteForUI(type) : null;
        AddToppingOnIce(sprite, type, $"Topping_{type}_{toppingVisualCount}");
    }

    private void HandleIceReset()
    {
        // 컵은 항상 보이고, 얼음만 제거
        ShowCupOnly();
        ClearChildren(toppingsContainer);
        toppingVisualCount = 0;
    }

    private void SyncFromMachineState()
    {
        if (iceMachine == null || iceStackImage == null) return;

        int current = iceMachine.CurrentIceTaps;
        if (current <= 0)
        {
            ShowCupOnly();
        }
        else if (iceStepSprites != null && iceStepSprites.Length > 0)
        {
            int idx = Mathf.Clamp(current - 1, 0, iceStepSprites.Length - 1);
            ApplyIceStep(idx);
        }

        // 토핑은 현재 프로젝트 구조상 "추가 이벤트"로만 누적되므로,
        // 도메인 리로드 시 재구성은 생략 (필요하면 IceMachine에 토핑 리스트/순서를 제공하면 됨)
    }

    private void ApplyIceStep(int idx)
    {
        if (iceStepSprites == null || iceStepSprites.Length == 0) return;
        idx = Mathf.Clamp(idx, 0, iceStepSprites.Length - 1);

        // Manual mode: keep CupBase sprite as-is, and treat iceStepSprites as "ice-only" steps.
        if (useManualCupAndIceSprites)
        {
            if (cupImage != null)
            {
                // user assigned sprite in inspector
                cupImage.enabled = cupImage.sprite != null;
            }
            iceStackImage.sprite = iceStepSprites[idx];
            iceStackImage.enabled = iceStackImage.sprite != null;
            return;
        }

        if (!splitCupAndIce)
        {
            if (cupImage != null) { cupImage.sprite = null; cupImage.enabled = false; }
            iceStackImage.sprite = iceStepSprites[idx];
            iceStackImage.enabled = iceStackImage.sprite != null;
            return;
        }

        EnsureSplitSpritesCache();
        if (cupImage != null)
        {
            cupImage.sprite = cupSpriteRuntime;
            cupImage.enabled = cupImage.sprite != null;
        }

        if (iceOnlySpritesRuntime != null && idx < iceOnlySpritesRuntime.Length)
        {
            iceStackImage.sprite = iceOnlySpritesRuntime[idx];
            iceStackImage.enabled = iceStackImage.sprite != null;
        }
        else
        {
            iceStackImage.sprite = null;
            iceStackImage.enabled = false;
        }
    }

    private void SetCupAndIceVisible(bool visible)
    {
        if (cupImage != null)
        {
            cupImage.sprite = visible ? cupImage.sprite : null;
            cupImage.enabled = visible && cupImage.sprite != null;
        }
        if (iceStackImage != null)
        {
            iceStackImage.sprite = visible ? iceStackImage.sprite : null;
            iceStackImage.enabled = visible && iceStackImage.sprite != null;
        }
    }

    private void ShowCupOnly()
    {
        if (useManualCupAndIceSprites)
        {
            if (cupImage != null) cupImage.enabled = cupImage.sprite != null;
            if (iceStackImage != null) { iceStackImage.sprite = null; iceStackImage.enabled = false; }
            return;
        }

        if (!splitCupAndIce)
        {
            // 합쳐진 모드일 땐 아무것도 표시하지 않음(탭 후에만 보이게)
            if (cupImage != null) { cupImage.sprite = null; cupImage.enabled = false; }
            if (iceStackImage != null) { iceStackImage.sprite = null; iceStackImage.enabled = false; }
            return;
        }

        if (iceStepSprites == null || iceStepSprites.Length == 0)
            return;

        EnsureSplitSpritesCache();

        if (cupImage != null)
        {
            cupImage.sprite = cupSpriteRuntime;
            cupImage.enabled = cupImage.sprite != null;
        }

        if (iceStackImage != null)
        {
            iceStackImage.sprite = null;
            iceStackImage.enabled = false;
        }
    }

    private void EnsureSplitSpritesCache()
    {
        if (cupSpriteRuntime != null && iceOnlySpritesRuntime != null && iceOnlySpritesRuntime.Length == iceStepSprites.Length)
            return;

        // Build cup sprite from step0, and ice-only sprites from each step by cropping
        var src0 = iceStepSprites[0];
        if (src0 == null) return;

        Texture2D tex = src0.texture;
        float ppu = src0.pixelsPerUnit;

        // rects are in texture pixel space
        Rect r0 = src0.rect;
        float cupH = Mathf.Clamp(cupHeightPixels, 1f, r0.height);

        // Cup: bottom portion
        Rect cupRect = new Rect(r0.x, r0.y, r0.width, cupH);
        cupSpriteRuntime = Sprite.Create(tex, cupRect, new Vector2(0.5f, 0f), ppu, 0, SpriteMeshType.FullRect);

        iceOnlySpritesRuntime = new Sprite[iceStepSprites.Length];
        for (int i = 0; i < iceStepSprites.Length; i++)
        {
            var s = iceStepSprites[i];
            if (s == null) continue;
            Rect r = s.rect;
            float h = Mathf.Clamp(cupHeightPixels, 1f, r.height);
            float iceH = r.height - h;
            if (iceH <= 1f)
            {
                iceOnlySpritesRuntime[i] = null;
                continue;
            }
            Rect iceRect = new Rect(r.x, r.y + h, r.width, iceH);
            iceOnlySpritesRuntime[i] = Sprite.Create(s.texture, iceRect, new Vector2(0.5f, 0f), s.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }

    private static float GetStackVisualScale(IceMachine.ToppingType type, float red, float milk, float fruit)
    {
        switch (type)
        {
            case IceMachine.ToppingType.RedBean: return red;
            case IceMachine.ToppingType.Milk: return milk;
            case IceMachine.ToppingType.Fruit: return fruit;
            default: return 1f;
        }
    }

    private void AddToppingOnIce(Sprite sprite, IceMachine.ToppingType type, string name)
    {
        if (toppingsContainer == null) return;
        if (sprite == null) return;

        float sizeMul = GetStackVisualScale(type, stackVisualScaleRedBean, stackVisualScaleMilk, stackVisualScaleFruit);
        float cell = toppingIconSize * sizeMul;

        float y = toppingBaseYOffset + toppingVisualCount * toppingStackStepY;
        float x = 0f;
        if (!centerAlignToppings && toppingStackStepX > 0f && toppingMaxSideStep > 0)
        {
            int span = toppingMaxSideStep * 2 + 1;
            int i = toppingVisualCount % span;
            int side = i - toppingMaxSideStep;
            x = side * toppingStackStepX;
        }

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(toppingsContainer, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(cell, cell);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = Color.white;

        toppingVisualCount++;
    }

    private static void ClearChildren(RectTransform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            if (child != null) Destroy(child.gameObject);
        }
    }
}

