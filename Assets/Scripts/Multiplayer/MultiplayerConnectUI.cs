using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;

public class MultiplayerConnectUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    [SerializeField] private TextMeshProUGUI playerCountText;

    private void Start()
    {
        hostButton.onClick.AddListener(HostButtonOnClick);
        clientButton.onClick.AddListener(ClientButtonOnClick);

        PlayerManager.OnPlayerCountUpdated += PlayerManager_OnPlayerCountUpdated;
    }

    private void PlayerManager_OnPlayerCountUpdated()
    {
        playerCountText.text = "Players: " + PlayerManager.Instance.PlayerCount;
    }

    private void HostButtonOnClick()
    {
        NetworkManager.Singleton.StartHost();
    }

    private void ClientButtonOnClick()
    {
        NetworkManager.Singleton.StartClient();
    }
}
