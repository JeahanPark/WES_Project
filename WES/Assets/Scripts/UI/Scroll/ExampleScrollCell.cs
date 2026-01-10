using UnityEngine;
using TMPro;

/// <summary>
/// BaseScrollCell 사용 예제
/// </summary>
public class ExampleScrollCell : BaseScrollCell<string>
{
    [SerializeField] private TMP_Text m_IndexText;
    [SerializeField] private TMP_Text m_DataText;

    protected override void OnUpdateCell(int _index, string _data)
    {
        m_IndexText.text = $"Index: {_index}";
        m_DataText.text = _data ?? "No Data";
    }

    public override void OnRecycle()
    {
        base.OnRecycle();
        // 셀이 재활용될 때 정리 작업
    }
}
