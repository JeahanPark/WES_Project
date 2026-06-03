#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// MPPM QA 셋업: 가상 플레이어(Player2)를 [MenuItem]으로 활성화한다.
//
// 주의(brittle): MPPM 1.6.2의 활성 API(UnityPlayer.Activate)는 internal이라 공개 진입점이 없다.
// 외부 어셈블리에서는 리플렉션으로만 호출 가능하며, 패키지 버전이 바뀌면 타입/시그니처가 달라져
// 깨질 수 있다. 활성 상태는 SystemDataStore에 디스크 영속되므로 본 메뉴는 최초 1회만 실행하면 된다.
// 리플렉션 실패 시 수동 활성(Window > Multiplayer > Multiplayer Play Mode > Player 2 체크)으로 폴백한다.
public static class MppmQaSetup
{
    private const string WORKFLOW_ASM = "Unity.Multiplayer.Playmode.Workflow.Editor";
    private const string MPPM_TYPE = "Unity.Multiplayer.Playmode.Workflow.Editor.MultiplayerPlaymode";
    private const string PLAYER_TWO_PROP = "PlayerTwo";
    private const string ACTIVATE_METHOD = "Activate";

    [MenuItem("Tools/MppmQA/가상 플레이어(Player2) 활성", priority = 1)]
    public static void ActivatePlayerTwo()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            GameDebug.LogWarning("[MppmQaSetup] 플레이 중에는 활성할 수 없다. 플레이 정지 후 다시 실행.");
            return;
        }

        if (EditorUtility.scriptCompilationFailed)
        {
            GameDebug.LogWarning("[MppmQaSetup] 컴파일 에러 상태 — 활성 불가. 에러 해결 후 재시도.");
            return;
        }

        if (TryActivateViaReflection(out string error))
        {
            GameDebug.Log("[MppmQaSetup] Player2 활성 요청 완료. 가상 플레이어 프로세스 생성/접속까지 수 분 걸릴 수 있다(최초 1회).");
            return;
        }

        GameDebug.LogWarning(
            $"[MppmQaSetup] 프로그램 활성 실패({error}). 수동 활성으로 진행: " +
            "Window > Multiplayer > Multiplayer Play Mode 창에서 Player 2 체크박스 활성.");
    }

    private static bool TryActivateViaReflection(out string _error)
    {
        _error = null;
        try
        {
            Type mppm = FindType(MPPM_TYPE);
            if (mppm == null)
            {
                _error = "MultiplayerPlaymode 타입 없음(MPPM 미설치/버전 변경)";
                return false;
            }

            PropertyInfo playerTwoProp = mppm.GetProperty(
                PLAYER_TWO_PROP, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (playerTwoProp == null)
            {
                _error = "PlayerTwo 프로퍼티 없음";
                return false;
            }

            object playerTwo = playerTwoProp.GetValue(null);
            if (playerTwo == null)
            {
                _error = "PlayerTwo 인스턴스 null(워크플로 미초기화 — MPPM 창을 한 번 연 뒤 재시도)";
                return false;
            }

            MethodInfo activate = playerTwo.GetType().GetMethod(
                ACTIVATE_METHOD, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (activate == null)
            {
                _error = "Activate 메서드 없음";
                return false;
            }

            // bool Activate(out ActivationError error, List<string> additionalArgs = null)
            object[] args = new object[] { null, null };
            object result = activate.Invoke(playerTwo, args);

            bool ok = result is bool b && b;
            if (!ok)
            {
                _error = $"Activate 반환 false(error={args[0]})";
                return false;
            }

            return true;
        }
        catch (Exception e)
        {
            _error = e.GetBaseException().Message;
            return false;
        }
    }

    private static Type FindType(string _fullName)
    {
        // 어셈블리 한정 이름 우선 시도.
        Type t = Type.GetType($"{_fullName}, {WORKFLOW_ASM}");
        if (t != null)
        {
            return t;
        }

        // 폴백: 로드된 어셈블리 전체 탐색.
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            t = asm.GetType(_fullName);
            if (t != null)
            {
                return t;
            }
        }
        return null;
    }
}
#endif
