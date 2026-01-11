using UnityEngine;

public class LobbyController : GameController<LobbyController>
{
    [SerializeField] private Canvas m_Canvas;

    private void Start()
    {
        Managers.Popup.InitializeForScene(m_Canvas);
        Managers.Popup.Open<LobbyPopup>();
    }
}
