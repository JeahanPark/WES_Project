// SceneAutoReloader.cs
// 외부(에이전트 Edit 툴 / git / 병렬 세션)에서 씬 파일이 바뀌면
// Unity의 "The open scene(s) have been modified externally" 모달이 뜨면서
// 에디터가 잠기고 → MCP 브리지까지 막혀 에이전트가 멈춘다.
//
// 이 스크립트는 열린 씬 파일의 디스크 변경을 주기적으로 폴링해서,
// 모달이 뜨기 전에 선제적으로 silent reload 한다.
//
// 안전장치: 에디터 쪽에 저장 안 한 변경(isDirty)이 있는 씬은 건드리지 않는다.
//   (그 경우는 진짜 충돌이므로 Unity 기본 프롬프트에 맡겨 유실을 막는다.)
//   이런 충돌을 줄이려면 MCP 씬 편집 후 u_editor_scene(save)로 저장할 것.

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneAutoReloader
{
    private const double CheckInterval = 0.5; // 초
    private static double _nextCheck;
    private static readonly Dictionary<string, long> _lastWrite = new();

    static SceneAutoReloader()
    {
        EditorApplication.update += Update;
        Snapshot();
    }

    // 현재 열린 씬들의 디스크 수정 시각을 기준선으로 저장
    private static void Snapshot()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (string.IsNullOrEmpty(s.path) || !File.Exists(s.path)) continue;
            _lastWrite[s.path] = File.GetLastWriteTimeUtc(s.path).Ticks;
        }
    }

    private static void Update()
    {
        if (EditorApplication.timeSinceStartup < _nextCheck) return;
        _nextCheck = EditorApplication.timeSinceStartup + CheckInterval;

        // 플레이/컴파일/임포트 중에는 손대지 않는다
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating) return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (string.IsNullOrEmpty(s.path) || !File.Exists(s.path)) continue;

            long ticks = File.GetLastWriteTimeUtc(s.path).Ticks;

            if (!_lastWrite.TryGetValue(s.path, out long prev))
            {
                _lastWrite[s.path] = ticks;
                continue;
            }
            if (prev == ticks) continue;

            _lastWrite[s.path] = ticks;

            // 저장 안 한 에디터 변경이 있으면 충돌 → 유실 방지 위해 기본 프롬프트에 맡김
            if (s.isDirty)
            {
                Debug.LogWarning($"[SceneAutoReloader] 외부 변경 감지했으나 에디터에 저장 안 한 변경이 있어 건너뜀(충돌): {s.path}");
                continue;
            }

            EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
            Debug.Log($"[SceneAutoReloader] 외부 변경 감지 → 자동 reload: {s.path}");
        }
    }
}
#endif
