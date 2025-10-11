using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MultiplayerTest : MonoBehaviour
{
    [SerializeField] private Button hostBtn;
    [SerializeField] private Button joinBtn;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TextMeshProUGUI codeText;

    private void Start()
    {
#if UNITY_EDITOR

#else
        HideBtns();
        codeText.gameObject.SetActive(false);
#endif

    }

    public void HostRelay()
    {
        HostAsync();
    }

    private async void HostAsync()
    {
        string code = await ServerRelay.Instance.TryCreateRelay();

        codeText.text = code.ToString();

        LobbyUI.Instance.Hide();
        LobbyListUI.Instance.Hide();
        HideBtns();
    }

    public void JoinLocalRelay()
    {
        ServerRelay.Instance.TryJoinRelay(inputField.text);

        LobbyUI.Instance.Hide();
        LobbyListUI.Instance.Hide();
        HideBtns();
    }

    private void HideBtns()
    {
        hostBtn.gameObject.SetActive(false);
        joinBtn.gameObject.SetActive(false);
        inputField.gameObject.SetActive(false);
    }
}
