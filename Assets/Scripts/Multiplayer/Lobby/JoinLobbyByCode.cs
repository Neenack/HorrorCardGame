using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JoinLobbyByCode : MonoBehaviour
{
    [SerializeField] private Button joinLobbyButton;
    [SerializeField] private TextMeshProUGUI codeText;

    private string code = "";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => {
            UI_InputWindow.Show_Static("Join Code", code, "abcdefghijklmnopqrstuvxywzABCDEFGHIJKLMNOPQRSTUVXYWZ1234567890 .,-", 6,
            () => {
                // Cancel
            },
            (string newCode) => {

                code = newCode.ToUpper();
                codeText.text = code;
            });
        });

        joinLobbyButton.onClick.AddListener(() => {
            LobbyManager.Instance.JoinLobbyByCode(code);
        });

        codeText.text = code;
    }
}
