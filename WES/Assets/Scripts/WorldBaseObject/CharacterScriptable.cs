using UnityEngine;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Scriptable Objects/CharacterData")]
public class CharacterData : ScriptableObject
{
    [SerializeField] private Vector3 m_WorldUIOffset = new Vector3(0f, 2f, 0f);

    public Vector3 WorldUIOffset => m_WorldUIOffset;
}
