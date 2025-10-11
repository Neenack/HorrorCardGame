using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoSingleton<LobbyUI>
{
    [SerializeField] private Transform playerSingleTemplate;
    [SerializeField] private Transform container;
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private TextMeshProUGUI gameModeText;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private Button changeGameModeButton;
    [SerializeField] private Button startGameButton;

    protected override void Awake()
    {
        base.Awake();

        playerSingleTemplate.gameObject.SetActive(false);

        leaveLobbyButton.onClick.AddListener(() => {
            LobbyManager.Instance.LeaveLobby();
        });

        changeGameModeButton.onClick.AddListener(() => {
            LobbyManager.Instance.ChangeGameMode();
        });

        startGameButton.onClick.AddListener(() => {
            LobbyManager.Instance.StartGame();
        });
    }
    private void OnEnable()
    {
        // Re-subscribe when scene loads (in case events were lost)
        if (LobbyManager.Instance != null)
        {
            SubscribeToEvents();

            // Refresh lobby display if we're in a lobby and haven't started game
            if (LobbyManager.Instance.GetJoinedLobby() != null && !LobbyManager.Instance.HasStartedGame)
            {
                UpdateLobby();
                Show();
            }
            else
            {
                Hide();
            }
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        UnsubscribeFromEvents();
    }

    private void SubscribeToEvents()
    {
        if (LobbyManager.Instance == null) return;

        LobbyManager.Instance.OnJoinedLobby -= UpdateLobby_Event;
        LobbyManager.Instance.OnJoinedLobbyUpdate -= UpdateLobby_Event;
        LobbyManager.Instance.OnLobbyGameModeChanged -= UpdateLobby_Event;
        LobbyManager.Instance.OnLeftLobby -= LobbyManager_OnLeftLobby;
        LobbyManager.Instance.OnKickedFromLobby -= LobbyManager_OnLeftLobby;

        LobbyManager.Instance.OnJoinedLobby += UpdateLobby_Event;
        LobbyManager.Instance.OnJoinedLobbyUpdate += UpdateLobby_Event;
        LobbyManager.Instance.OnLobbyGameModeChanged += UpdateLobby_Event;
        LobbyManager.Instance.OnLeftLobby += LobbyManager_OnLeftLobby;
        LobbyManager.Instance.OnKickedFromLobby += LobbyManager_OnLeftLobby;
    }

    private void UnsubscribeFromEvents()
    {
        if (LobbyManager.Instance == null) return;

        LobbyManager.Instance.OnJoinedLobby -= UpdateLobby_Event;
        LobbyManager.Instance.OnJoinedLobbyUpdate -= UpdateLobby_Event;
        LobbyManager.Instance.OnLobbyGameModeChanged -= UpdateLobby_Event;
        LobbyManager.Instance.OnLeftLobby -= LobbyManager_OnLeftLobby;
        LobbyManager.Instance.OnKickedFromLobby -= LobbyManager_OnLeftLobby;
    }

    private void Update()
    {
        if (!LobbyManager.Instance.IsLobbyHost() || startGameButton == null) return;

        startGameButton.enabled = 
            LobbyManager.Instance.GetJoinedLobby().Players.Count > 1 
            || GamemodeSettingsManager.Instance.UseAI;
    }

    private void LobbyManager_OnLeftLobby(object sender, System.EventArgs e)
    {
        ClearLobby();
        Hide();
    }

    private void UpdateLobby_Event(object sender, LobbyManager.LobbyEventArgs e)
    {
        UpdateLobby();
    }

    private void UpdateLobby()
    {
        UpdateLobby(LobbyManager.Instance.GetJoinedLobby());
    }

    private void UpdateLobby(Lobby lobby)
    {
        if (container == null || lobby == null) return;

        ClearLobby();

        foreach (Player player in lobby.Players)
        {
            Transform playerSingleTransform = Instantiate(playerSingleTemplate, container);
            playerSingleTransform.gameObject.SetActive(true);
            LobbyPlayerSingleUI lobbyPlayerSingleUI = playerSingleTransform.GetComponent<LobbyPlayerSingleUI>();

            lobbyPlayerSingleUI.SetKickPlayerButtonVisible(
                LobbyManager.Instance.IsLobbyHost() &&
                player.Id != AuthenticationService.Instance.PlayerId
            );

            lobbyPlayerSingleUI.SetGameSettingsVisible(LobbyManager.Instance.IsLobbyHost());

            lobbyPlayerSingleUI.UpdatePlayer(player);
        }

        changeGameModeButton.gameObject.SetActive(LobbyManager.Instance.IsLobbyHost());

        lobbyNameText.text = lobby.Name;
        playerCountText.text = lobby.Players.Count + "/" + lobby.MaxPlayers;
        gameModeText.text = lobby.Data[LobbyManager.KEY_GAME_MODE].Value;
        lobbyCodeText.text = "Lobby Code: " + lobby.LobbyCode;

        Show();
    }

    private void ClearLobby()
    {
        if (container == null) return;

        foreach (Transform child in container)
        {
            if (child == playerSingleTemplate || child.gameObject == null) continue;
            Destroy(child.gameObject);
        }
    }

    public void Hide()
    {
        gameObject?.SetActive(false);
    }

    private void Show()
    {
        gameObject?.SetActive(true);
    }
}