using UnityEngine;

public class IntroController : MonoBehaviour
{
    [SerializeField] private Canvas m_Canvas;

    private void Start()
    {
        Managers.Instance.Init();
        Managers.Popup.InitializeForScene(m_Canvas);
        Managers.Popup.Open<LoginPopup>();
    }
}
