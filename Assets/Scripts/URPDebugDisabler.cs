using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 런타임에서 URP Rendering Debugger([Debug Canvas] / Display Stats)가
/// Ctrl+Backspace 등으로 켜지지 않도록 매 프레임 차단합니다.
/// </summary>
public class URPDebugDisabler : MonoBehaviour
{
    private static URPDebugDisabler instance;

    private System.Type debugManagerType;
    private object debugManagerInstance;
    private PropertyInfo enableRuntimeUIProp;
    private bool initialized = false;

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
        // [Debug Canvas]가 활성화된 순간 즉시 끔
        var debugCanvas = GameObject.Find("[Debug Canvas]");
        if (debugCanvas != null && debugCanvas.activeSelf)
        {
            debugCanvas.SetActive(false);
            Apply();
        }
    }

    private void TryInit()
    {
        if (initialized) return;

        string[] candidateAssemblies = {
            "Unity.RenderPipelines.Core.Runtime",
            "UnityEngine.Rendering.Core",
            "Unity.RenderPipelines.Universal.Runtime"
        };

        foreach (var asmName in candidateAssemblies)
        {
            var asm = System.AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == asmName);
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
    }

    private void Apply()
    {
        if (!initialized) TryInit();
        if (initialized && debugManagerInstance != null && enableRuntimeUIProp != null)
            enableRuntimeUIProp.SetValue(debugManagerInstance, false);
    }
}
