using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 범용 버튼 터치 피드백.
/// 눌렀을 때 살짝 어둡고 작게 보이도록 하여 플레이어가 터치 여부를 시각적으로 인지할 수 있게 합니다.
/// Button 컴포넌트가 있는 GameObject에 부착하거나, 코드에서 AddComponent로 자동 추가할 수 있습니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonPressFeedback : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Press Feedback")]
    [Tooltip("누른 동안 적용할 스케일 배율 (1 미만이면 작아짐)")]
    [SerializeField, Range(0.6f, 1f)] private float pressedScale = 0.92f;

    [Tooltip("누른 동안 곱해질 RGB 배율 (1 미만이면 어두워짐)")]
    [SerializeField, Range(0.3f, 1f)] private float darkenFactor = 0.75f;

    [Tooltip("체크 해제 시 눌림 중 색만 바꾸지 않습니다(스케일 피드백만). 토핑처럼 CanvasGroup 알파와 겹치면 팥만 흐려 보일 수 있어 끕니다.")]
    [SerializeField] private bool darkenColorOnPress = true;

    private Button button;
    private Graphic targetGraphic;
    private Vector3 originalScale;
    private Color originalColor;
    private bool isPressed;
    /// <summary>Awake가 끝나기 전에 컴포넌트만 제거되면 originalColor 등이 기본값이라 OnDisable에서 그래픽을 망가뜨릴 수 있음</summary>
    private bool visualsInitialized;

    private void Awake()
    {
        button = GetComponent<Button>();
        targetGraphic = button.targetGraphic != null ? button.targetGraphic : GetComponent<Graphic>();
        CacheOriginalScale();
        if (targetGraphic != null)
            originalColor = targetGraphic.color;

        button.transition = Selectable.Transition.None;
        visualsInitialized = true;
    }

    private void OnEnable()
    {
        // HorizontalLayoutGroup 등 적용 전 Awake에서 scale이 (0,0,0)으로 잡히는 경우가 있어 재캐시합니다.
        if (button == null) button = GetComponent<Button>();
        if (targetGraphic == null && button != null)
            targetGraphic = button.targetGraphic != null ? button.targetGraphic : GetComponent<Graphic>();
        if (!isPressed)
            CacheOriginalScale();
    }

    private void CacheOriginalScale()
    {
        originalScale = transform.localScale;
        if (originalScale.sqrMagnitude < 1e-6f)
            originalScale = Vector3.one;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!button.interactable) return;
        ApplyPressed();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isPressed) ApplyReleased();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isPressed) ApplyReleased();
    }

    private void OnDisable()
    {
        // Awake 전에 DestroyImmediate 되면 originalColor=(0,0,0,0) 등으로 Image를 망가뜨릴 수 있음
        if (!visualsInitialized) return;
        // 비활성화 시에도 눌림 색/스케일이 남지 않도록 항상 원복
        ResetVisualToOriginal();
    }

    private void ApplyPressed()
    {
        isPressed = true;
        transform.localScale = originalScale * pressedScale;
        if (targetGraphic != null && darkenColorOnPress)
        {
            targetGraphic.color = new Color(
                originalColor.r * darkenFactor,
                originalColor.g * darkenFactor,
                originalColor.b * darkenFactor,
                originalColor.a);
        }
    }

    /// <summary>
    /// 토핑 UI 등 CanvasGroup 알파와 겹칠 때는 색 어둡게 하지 않고 스케일만 쓰는 것이 안전합니다.
    /// </summary>
    public void SetDarkenColorOnPress(bool value)
    {
        darkenColorOnPress = value;
        RefreshOriginalColorFromTarget();
        if (!isPressed && targetGraphic != null)
            targetGraphic.color = originalColor;
    }

    private void ApplyReleased()
    {
        ResetVisualToOriginal();
    }

    /// <summary>
    /// isPressed 플래그와 무관하게 스케일/색을 저장값으로 되돌립니다.
    /// (PointerUp이 누락되면 isPressed만 false인 채 색이 어둡게 남는 경우 방지)
    /// </summary>
    private void ResetVisualToOriginal()
    {
        isPressed = false;
        if (originalScale.sqrMagnitude < 1e-6f)
            originalScale = Vector3.one;
        transform.localScale = originalScale;
        if (targetGraphic != null)
            targetGraphic.color = originalColor;
    }

    public void ForceRelease()
    {
        ResetVisualToOriginal();
    }

    /// <summary>
    /// 외부에서 Image.color 등을 수정한 뒤 호출하면, 릴리즈 시 다시 덮어쓰는 저장 색을 현재 그래픽과 맞춥니다.
    /// </summary>
    public void RefreshOriginalColorFromTarget()
    {
        // 눌림 중이 아닐 때만 스케일 기준을 갱신 (눌린 축소 스케일을 원본으로 저장하지 않도록)
        if (!isPressed)
        {
            var s = transform.localScale;
            if (s.sqrMagnitude > 1e-6f)
                originalScale = s;
            else
                originalScale = Vector3.one;
        }

        if (targetGraphic == null) return;
        originalColor = targetGraphic.color;
    }

    public static void RefreshOriginalFor(Button btn)
    {
        if (btn == null) return;
        var fb = btn.GetComponent<ButtonPressFeedback>();
        if (fb != null) fb.RefreshOriginalColorFromTarget();
    }

    /// <summary>
    /// 버튼에 ButtonPressFeedback이 없으면 자동 부착합니다.
    /// RaycastTarget도 함께 보정합니다.
    /// </summary>
    public static void EnsureOn(Button btn)
    {
        if (btn == null) return;

        Graphic g = btn.targetGraphic != null ? btn.targetGraphic : btn.GetComponent<Graphic>();
        if (g != null && !g.raycastTarget) g.raycastTarget = true;

        if (btn.GetComponent<ButtonPressFeedback>() == null)
            btn.gameObject.AddComponent<ButtonPressFeedback>();
    }

    public static void ForceRelease(Button btn)
    {
        if (btn == null) return;
        var fb = btn.GetComponent<ButtonPressFeedback>();
        if (fb != null) fb.ForceRelease();
    }
}
