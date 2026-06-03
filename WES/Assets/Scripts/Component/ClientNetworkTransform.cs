using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 오너(클라이언트) 권위 NetworkTransform.
/// WES의 플레이어 이동은 IsOwner에서 직접 transform을 갱신하므로(클라 권위),
/// 위치/회전 동기화도 오너가 서버·타 클라로 push 해야 한다.
/// 기본 NetworkTransform은 서버 권위라 이 이동 모델과 맞지 않아 별도 정의한다.
/// (NGO 기본 패키지에 ClientNetworkTransform이 포함돼 있지 않음)
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkTransform : NetworkTransform
{
    /// <summary>false = 오너 권위(클라이언트가 위치 동기화 주체).</summary>
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
