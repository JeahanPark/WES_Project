using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class IntroController : GameController<IntroController>
{
    [SerializeField] private Canvas m_Canvas;

    private IEnumerator Start()
    {
        Managers.Instance.Init();
        Managers.Popup.InitializeForScene(m_Canvas);

        // 모든 씬에서 사용할 CSV 정보를 미리 로드한다 (Info Find 실패 방지)
        yield return Managers.Info.LoadAllInfo().ToCoroutine();

        Managers.Popup.Open<LoginPopup>();
    }
}