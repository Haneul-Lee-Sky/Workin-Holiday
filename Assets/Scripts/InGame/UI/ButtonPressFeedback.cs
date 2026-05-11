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

    private Button button;
    private Graphic targetGraphic;
    private Vector3 originalScale;
    private Color originalColor;
    private bool isPressed;

    private void Awake()
    {
        button = GetComponent<Button>();
        targetGraphic = button.targetGraphic != null ? button.targetGraphic : GetComponent<Graphic>();
        originalScale = transform.localScale;
        if (targetGraphic != null)
        {
            originalColor = targetGraphic.color;
        }

        // Button의 기본 ColorTint가 우리 색상 변경과 충돌하지 않도록 전환을 끕니다.
        button.transition = Selectable.Transition.None;
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
        if (isPressed) ApplyReleased();
    }

    private void ApplyPressed()
    {
        isPressed = true;
        transform.localScale = originalScale * pressedScale;
        if (targetGraphic != null)
        {
            targetGraphic.color = new Color(
                originalColor.r * darkenFactor,
                originalColor.g * darkenFactor,
                originalColor.b * darkenFactor,
                originalColor.a);
        }
    }

    private void ApplyReleased()
    {
        isPressed = false;
        transform.localScale = originalScale;
        if (targetGraphic != null)
        {
            targetGraphic.color = originalColor;
        }
    }

    public void ForceRelease()
    {
        if (isPressed) ApplyReleased();
    }

    /// <summary>
    /// 외부에서 Image.color 등을 수정한 뒤 호출하면, 릴리즈 시 다시 덮어쓰는 저장 색을 현재 그래픽과 맞춥니다.
    /// </summary>
    public void RefreshOriginalColorFromTarget()
    {
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
