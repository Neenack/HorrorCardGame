using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;

public class MultiplayerConnectUI : MonoBehaviour
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private TMP_InputField codeInput;

    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI joinCodeText;

    [SerializeField] private AIToggleUI aiToggle;

    private void Start()
    {
        hostButton.onClick.AddListener(HostButtonOnClick);
        clientButton.onClick.AddListener(ClientButtonOnClick);
        joinCodeText.gameObject.SetActive(false);
        playerCountText.gameObject.SetActive(false);

        PlayerManager.OnPlayerCountUpdated += PlayerManager_OnPlayerCountUpdated;
    }

    private void PlayerManager_OnPlayerCountUpdated()
    {
        playerCountText.text = "Players: " + PlayerManager.Instance.PlayerCount;
    }

    private async void HostButtonOnClick()
    {
        DisableBtns();
        bool successful = await TestRelay.Instance.TryCreateRelay();

        if (successful) JoinGameUI();
        else EnableBtns();
    }

    private async void ClientButtonOnClick()
    {
        if (codeInput.text.Length != 6) return;

        DisableBtns();
        bool successful = await TestRelay.Instance.TryJoinRelay(codeInput.text);

        if (successful) JoinGameUI();
        else EnableBtns();
    }

    private void DisableBtns()
    {
        hostButton.gameObject.SetActive(false);
        clientButton.gameObject.SetActive(false);
        codeInput.gameObject.SetActive(false);
    }

    private void EnableBtns()
    {
        hostButton.gameObject.SetActive(true);
        clientButton.gameObject.SetActive(true);
        codeInput.gameObject.SetActive(true);
    }

    private void JoinGameUI()
    {
        joinCodeText.gameObject.SetActive(true);
        playerCountText.gameObject.SetActive(true);
        joinCodeText.text = "Join Code: " + TestRelay.Instance.JoinCode;

        aiToggle.JoinGame();
    }
}
