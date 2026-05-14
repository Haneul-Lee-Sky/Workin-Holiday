using UnityEngine;

/// <summary>
/// 개발 단계 전용 디버그 오버레이
/// 화면을 3등분하여 각 구역을 반투명 색상으로 시각화합니다.
/// 릴리스 빌드에서는 자동으로 비활성화됩니다.
/// </summary>
public class DebugZoneOverlay : MonoBehaviour
{
    [Header("Debug Settings")]
    [SerializeField] private bool showOverlay = true;
    [SerializeField] [Range(0.05f, 0.5f)] private float alpha = 0.15f;

    private Texture2D blueTex;
    private Texture2D greenTex;
    private Texture2D redTex;
    private Texture2D whiteTex;

    private GUIStyle bigLabel;
    private GUIStyle smallLabel;
    private bool stylesReady = false;

    private MobileInputManager cachedInputMgr;

    private void OnEnable()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        showOverlay = false;
        enabled = false;
        return;
#endif
        cachedInputMgr = FindAnyObjectByType<MobileInputManager>();
        blueTex = MakeTex(new Color(0.2f, 0.5f, 1f));
        greenTex = MakeTex(new Color(0.2f, 0.8f, 0.3f));
        redTex = MakeTex(new Color(1f, 0.3f, 0.3f));
        whiteTex = MakeTex(Color.white);
    }

    private void OnDisable()
    {
        SafeDestroy(blueTex);
        SafeDestroy(greenTex);
        SafeDestroy(redTex);
        SafeDestroy(whiteTex);
    }

    private Texture2D MakeTex(Color c)
    {
        Texture2D t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    private void SafeDestroy(Object obj)
    {
        if (obj != null) Destroy(obj);
    }

    private void BuildStyles()
    {
        if (stylesReady) return;

        bigLabel = new GUIStyle(GUI.skin.label);
        bigLabel.fontSize = 28;
        bigLabel.fontStyle = FontStyle.Bold;
        bigLabel.alignment = TextAnchor.MiddleCenter;
        bigLabel.normal.textColor = Color.white;

        smallLabel = new GUIStyle(GUI.skin.label);
        smallLabel.fontSize = 18;
        smallLabel.alignment = TextAnchor.MiddleCenter;
        smallLabel.normal.textColor = new Color(1f, 1f, 1f, 0.7f);

        stylesReady = true;
    }

    private void OnGUI()
    {
        if (!showOverlay) return;

#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        return;
#endif

        BuildStyles();

        float sw = Screen.width;
        float sh = Screen.height;

        // MobileInputManager에서 비율 가져오기 (없으면 기본 1/3)
        float bottomRatio = 0.33f;
        float centerRatio = 0.33f;
        if (cachedInputMgr == null)
            cachedInputMgr = FindAnyObjectByType<MobileInputManager>();
        if (cachedInputMgr != null)
        {
            bottomRatio = cachedInputMgr.BottomZoneRatio;
            centerRatio = cachedInputMgr.CenterZoneRatio;
        }
        float topRatio = 1f - bottomRatio - centerRatio;

        // OnGUI 좌표계: Y=0 상단, Y=sh 하단
        // 따라서 상단이 먼저, 하단이 마지막
        float topH = sh * topRatio;
        float centerH = sh * centerRatio;
        float bottomH = sh * bottomRatio;

        GUI.color = new Color(1, 1, 1, alpha);
        GUI.DrawTexture(new Rect(0, 0, sw, topH), redTex);
        GUI.DrawTexture(new Rect(0, topH, sw, centerH), greenTex);
        GUI.DrawTexture(new Rect(0, topH + centerH, sw, bottomH), blueTex);

        // 구분선
        GUI.color = new Color(1, 1, 1, 0.5f);
        GUI.DrawTexture(new Rect(0, topH - 1, sw, 2), whiteTex);
        GUI.DrawTexture(new Rect(0, topH + centerH - 1, sw, 2), whiteTex);

        // 라벨
        GUI.color = Color.white;

        float labelY1 = topH * 0.35f;
        GUI.Label(new Rect(0, labelY1, sw, 36), "입력 차단", bigLabel);
        GUI.Label(new Rect(0, labelY1 + 34, sw, 26),
            string.Format("점수 / 타이머 / 버프  ({0}%)", (int)(topRatio * 100)), smallLabel);

        float labelY2 = topH + centerH * 0.35f;
        GUI.Label(new Rect(0, labelY2, sw, 36), "스와이프 전용", bigLabel);
        GUI.Label(new Rect(0, labelY2 + 34, sw, 26),
            string.Format("좌/우/하=서빙 | 상=폐기  ({0}%)", (int)(centerRatio * 100)), smallLabel);

        float labelY3 = topH + centerH + bottomH * 0.35f;
        GUI.Label(new Rect(0, labelY3, sw, 36), "터치 전용", bigLabel);
        GUI.Label(new Rect(0, labelY3 + 34, sw, 26),
            string.Format("탭=얼음생성 | 완성 후 토핑  ({0}%)", (int)(bottomRatio * 100)), smallLabel);

        // 해상도 정보
        GUI.Label(new Rect(10, sh - 28, 400, 24),
            string.Format("Screen: {0}x{1} | Bottom:{2}px Center:{3}px Top:{4}px",
                (int)sw, (int)sh, (int)bottomH, (int)centerH, (int)topH),
            smallLabel);
    }

    public void Toggle()
    {
        showOverlay = !showOverlay;
    }
}
