using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BaseScroll 사용 예제
/// </summary>
public class ExampleScroll : BaseScroll<string>
{
    protected override void OnAwake()
    {
        base.OnAwake();
        // 초기화 코드
    }

    // 사용 예시:
    public void TestScroll()
    {
        List<string> testData = new List<string>();
        for (int i = 0; i < 100; i++)
        {
            testData.Add($"Data Item {i}");
        }
        SetData(testData);
    }
}
