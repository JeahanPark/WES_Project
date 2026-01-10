using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(BaseScroll<>), true)]
[CanEditMultipleObjects]
public class BaseScrollEditor : ScrollRectEditor
{
    private SerializedProperty m_CellSizeProperty;
    private SerializedProperty m_SpacingProperty;
    private SerializedProperty m_ReverseDirectionProperty;

    protected override void OnEnable()
    {
        base.OnEnable();

        m_CellSizeProperty = serializedObject.FindProperty("m_CellSize");
        m_SpacingProperty = serializedObject.FindProperty("m_Spacing");
        m_ReverseDirectionProperty = serializedObject.FindProperty("m_ReverseDirection");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Cell Settings", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(m_CellSizeProperty);
        EditorGUILayout.PropertyField(m_SpacingProperty);
        EditorGUILayout.PropertyField(m_ReverseDirectionProperty);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rect Settings", EditorStyles.boldLabel);

        base.OnInspectorGUI();
    }
}
