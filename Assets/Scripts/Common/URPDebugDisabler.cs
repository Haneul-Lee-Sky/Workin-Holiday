using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 런타임에서 URP Rendering Debugger([Debug Canvas] / Display Stats)가
/// Ctrl+Backspace 등으로 켜지지 않도록 주기적으로 끕니다.
/// </summary>
public class URPDebugDisabler : MonoBehaviour
{
    private static URPDebugDisabler instance;

    private System.Type debugManagerType;
    private object debugManagerInstance;
    private PropertyInfo enableRuntimeUIProp;
    private bool initialized = false;
    /// <summary>런타임에 URP DebugManager 를 못 찾았을 때 — 매 Apply 마다 GetAssemblies() 전체 스캔을 반복하지 않도록.</summary>
    private bool initGiveUp;

    private float nextDebugCanvasPollUnscaledTime = -999f;
    private const float DebugCanvasPollIntervalSeconds = 1f;

    private void Awake()
    {
        // 중복 인스턴스 방지
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }

        // 다른 컴포넌트와 GameObject를 공유 중이면(Transform + 이 컴포넌트 외에 더 있음)
        // DontDestroyOnLoad로 인해 전체 GameObject가 영속화되어 CustomerManager 등의
        // 다른 매니저들까지 잘못 살아남는 문제를 방지하기 위해 전용 GameObject로 분리한다.
        if (gameObject.GetComponents<Component>().Length > 2)
        {
            GameObject standalone = new GameObject("[URPDebugDisabler]");
            URPDebugDisabler copy = standalone.AddComponent<URPDebugDisabler>();
            DontDestroyOnLoad(standalone);
            instance = copy;
            Debug.Log("[URPDebugDisabler] 공유 GameObject에서 전용 GameObject로 분리했습니다.");
            Destroy(this);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        TryInit();
        Apply();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        // GameObject.Find 는 씬 전체를 매 프레임 도는 것과 같아서, 손님/UI가 늘어날수록 프레임이 초 단위로 밀립니다.
        // (Profiler 에서 Behaviour.Update 가 수백 ms 로 보이는 전형적인 원인)
        if (Time.unscaledTime < nextDebugCanvasPollUnscaledTime)
            return;
        nextDebugCanvasPollUnscaledTime = Time.unscaledTime + DebugCanvasPollIntervalSeconds;

        var debugCanvas = GameObject.Find("[Debug Canvas]");
        if (debugCanvas != null && debugCanvas.activeSelf)
        {
            debugCanvas.SetActive(false);
            Apply();
        }
    }

    private void TryInit()
    {
        if (initialized || initGiveUp) return;

        // GetAssemblies() 는 비용이 크므로 후보마다 다시 부르지 않습니다.
        var allAssemblies = System.AppDomain.CurrentDomain.GetAssemblies();

        string[] candidateAssemblies = {
            "Unity.RenderPipelines.Core.Runtime",
            "UnityEngine.Rendering.Core",
            "Unity.RenderPipelines.Universal.Runtime"
        };

        foreach (var asmName in candidateAssemblies)
        {
            var asm = allAssemblies.FirstOrDefault(a => a.GetName().Name == asmName);
            if (asm == null) continue;

            debugManagerType = asm.GetType("UnityEngine.Rendering.DebugManager");
            if (debugManagerType == null) continue;

            var instanceProp = debugManagerType.GetProperty("instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) continue;

            debugManagerInstance = instanceProp.GetValue(null);
            if (debugManagerInstance == null) continue;

            // Unity 6: enableRuntimeUI, Unity 2022: displayRuntimeUI
            foreach (var name in new[] { "enableRuntimeUI", "displayRuntimeUI" })
            {
                var prop = debugManagerType.GetProperty(name,
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    enableRuntimeUIProp = prop;
                    break;
                }
            }

            initialized = true;
            break;
        }

        if (!initialized)
            initGiveUp = true;
    }

    private void Apply()
    {
        if (!initialized && !initGiveUp)
            TryInit();
        if (initialized && debugManagerInstance != null && enableRuntimeUIProp != null)
            enableRuntimeUIProp.SetValue(debugManagerInstance, false);
    }
}
