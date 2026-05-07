using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// "1 터치 = 1 얼음" 전용 입력 영역.
/// Panel_IceBuildWindow 같은 UI 오브젝트에 붙여서 OnPointerDown 때마다 IceMachine.AddIce()를 호출합니다.
/// </summary>
[DisallowMultipleComponent]
public class IceTapArea : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private IceMachine iceMachine;

    private void Awake()
    {
        if (iceMachine == null)
            iceMachine = FindAnyObjectByType<IceMachine>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (iceMachine == null)
            iceMachine = FindAnyObjectByType<IceMachine>();

        if (iceMachine == null) return;
        if (iceMachine.IsComplete) return;

        iceMachine.AddIce();
    }

    public void SetIceMachine(IceMachine m)
    {
        iceMachine = m;
    }
}

