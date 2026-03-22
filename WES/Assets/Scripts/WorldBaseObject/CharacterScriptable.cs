using UnityEngine;

/// <summary>
/// 캐릭터 WorldUI 위치 미세조정 전용 ScriptableObject.
/// HP바 등 월드 UI의 오프셋을 캐릭터별로 개별 설정한다.
/// 게임 로직(드랍, 스탯 등)과 무관하다.
/// </summary>
[CreateAssetMenu(fileName = "CharacterScriptable", menuName = "Scriptable Objects/CharacterScriptable")]
public class CharacterScriptable : ScriptableObject
{
    [SerializeField] private Vector3 m_WorldUIOffset = new Vector3(0f, 2f, 0f);

    public Vector3 WorldUIOffset => m_WorldUIOffset;
}
