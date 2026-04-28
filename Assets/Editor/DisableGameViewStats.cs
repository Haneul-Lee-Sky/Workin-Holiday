// DisableGameViewStats.cs
// 에디터 전용 유틸리티: 플레이 모드 진입/종료 시 Game View Stats 창과
// URP Rendering Debugger(Display Stats) 창을 자동으로 끕니다.

using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class DisableGameViewStats
{
    static DisableGameViewStats()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        SuppressStats();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            SuppressStats();
        }
    }

    private static void SuppressStats()
    {
        // 1. Game View 내장 Stats 끄기
        var assembly = Assembly.GetAssembly(typeof(EditorWindow));
        var gameViewType = assembly?.GetType("UnityEditor.GameView");
        if (gameViewType != null)
        {
            var views = Resources.FindObjectsOfTypeAll(gameViewType);
            foreach (var view in views)
            {
                var statsField = gameViewType.GetField(
                    "m_Stats",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (statsField != null)
                    statsField.SetValue(view, false);

                var closeMethod = gameViewType.GetMethod(
                    "OnCloseStatsWindow",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                closeMethod?.Invoke(view, null);
            }
        }

        // 2. URP DebugManager Runtime UI 끄기 (Unity 6 호환)
        SuppressURPDebugUI();
    }

    internal static void SuppressURPDebugUI()
    {
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

        // 코어 런타임 어셈블리 탐색
        string[] candidateAssemblies = {
            "Unity.RenderPipelines.Core.Runtime",
            "UnityEngine.Rendering.Core",
            "Unity.RenderPipelines.Universal.Runtime"
        };

        foreach (var asmName in candidateAssemblies)
        {
            var asm = assemblies.FirstOrDefault(a => a.GetName().Name == asmName);
            if (asm == null) continue;

            var debugManagerType = asm.GetType("UnityEngine.Rendering.DebugManager");
            if (debugManagerType == null) continue;

            var instanceProp = debugManagerType.GetProperty("instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) continue;

            var instance = instanceProp.GetValue(null);
            if (instance == null) continue;

            // Unity 6: enableRuntimeUI / Unity 2022: displayRuntimeUI 둘 다 시도
            foreach (var propName in new[] { "enableRuntimeUI", "displayRuntimeUI" })
            {
                var prop = debugManagerType.GetProperty(propName,
                    BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(instance, false);
                }
            }

            // Debug Canvas 오브젝트가 이미 존재하면 직접 비활성화
            var debugCanvas = GameObject.Find("[Debug Canvas]");
            if (debugCanvas != null)
                debugCanvas.SetActive(false);

            break;
        }
    }
}
