using UnityEngine;

public class CardGameDeckInteractable : Interactable
{
    ICardGameEvents game = null;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        SetAllowedClients(OwnerClientId);

        game = GetComponentInParent<ICardGameEvents>();

        if (game == null) return;

        game.CurrentPlayerTurnTableID.OnValueChanged += OnTurnTableIDChanged;
        game.CurrentGameState.OnValueChanged += OnGameStateChanged;
        game.OnGameReset += Game_OnGameReset;
        game.OnAnyActionExecuted += Game_OnAnyActionExecuted;
    }

    private void Game_OnAnyActionExecuted()
    {
        ClearAllowedClients();
    }

    public override void OnNetworkDespawn()
    {
        if (game == null || !IsServer) return;

        game.CurrentPlayerTurnTableID.OnValueChanged -= OnTurnTableIDChanged;
        game.CurrentGameState.OnValueChanged -= OnGameStateChanged;
    }

    private void OnGameStateChanged(GameState oldValue, GameState newValue)
    {
        switch (newValue)
        {
            case GameState.WaitingToStart:
                SetDisplay(new InteractDisplay("Start Game"));
                SetAllowedClients(OwnerClientId);
                break;

            case GameState.Starting:
                ClearAllowedClients();
                break;

            case GameState.Playing:
                SetDisplay(new InteractDisplay("Pull Card"));
                break;

            case GameState.Ending:
                ClearAllowedClients();
                break;
        }
    }

    private void Update()
    {
        if (game?.CurrentGameState.Value != GameState.WaitingToStart) return;

        bool fillBots = GamemodeSettings.Instance.UseAI;
        bool clientConnecting = ServerRelay.Instance.IsClientConnecting;

        SetInteractable((fillBots || PlayerManager.Instance.PlayerCount > 1) && !clientConnecting);
    }

    private void OnTurnTableIDChanged(ulong oldValue, ulong newValue)
    {
        if (game.IsAI(newValue))
        {
            ClearAllowedClients();
            return;
        }

        SetAllowedClients(game.CurrentPlayerTurnClientID.Value);
    }

    private void Game_OnGameReset()
    {
        ClearAllowedClients();
        SetDisplay(new InteractDisplay("Start Game"));
    }
}
