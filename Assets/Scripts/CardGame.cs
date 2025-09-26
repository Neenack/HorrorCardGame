using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;
public enum GameState
{
    WaitingToStart,
    Starting,
    Playing,
    Ended
}

public abstract class CardGame<TPlayer, TAction> : NetworkBehaviour, ICardGame<TAction>, ITable where TPlayer : TablePlayer<TAction> where TAction : class
{
    public event Action OnGameStarted;
    public event Action OnGameEnded;

    protected NetworkVariable<ulong> currentTurnClientId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    protected int currentTurnIndex = -1;
    protected TPlayer currentPlayer;

    protected NetworkVariable<GameState> gameState = new NetworkVariable<GameState>(
       GameState.WaitingToStart,
       NetworkVariableReadPermission.Everyone,
       NetworkVariableWritePermission.Server
    );

    protected NetworkVariable<ulong> topPileCardId = new NetworkVariable<ulong>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Seats")]
    [SerializeField] protected List<TPlayer> players = new List<TPlayer>();

    [Header("Deck Settings")]
    [SerializeField] private CardDeckSO deckSO;
    [SerializeField] protected float timeBetweenCardDeals = 0.5f;
    [SerializeField] private Transform cardSpawnTransform;
    [SerializeField] private Transform cardPileTransform;

    private CardDeck deck;
    private IInteractable interactableDeck;
    protected PlayingCard drawnCard;

    [Header("AI")]
    [SerializeField] protected float AIThinkingTime = 1f;


    // Server-only game state
    private List<PlayingCard> cardPile = new List<PlayingCard>();

    #region Public Accessors

    public NetworkVariable<ulong> CurrentTurnID => currentTurnClientId;
    public PlayingCard TopPileCard => cardPile.Count > 0 ? cardPile[cardPile.Count - 1] : null;
    public PlayingCard DrawnCard => drawnCard;
    public IInteractable InteractableDeck => interactableDeck;
    public IEnumerable<TablePlayer<TAction>> Players => players;


    #endregion

    #region Abstract Functions

    protected abstract IEnumerator DealInitialCards();
    protected abstract bool HasGameEnded();
    protected abstract IEnumerator ShowWinnerRoutine();
    protected abstract IEnumerator ExecuteActionRoutine(TAction action);

    #endregion

    private void Awake()
    {
        interactableDeck = GetComponentInChildren<IInteractable>();
        interactableDeck.SetInteractable(true);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            if (interactableDeck != null)
            {
                interactableDeck.OnInteract += InteractableDeck_OnInteract;
            }
        }

        gameState.OnValueChanged += OnGameStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (interactableDeck != null) interactableDeck.OnInteract -= InteractableDeck_OnInteract;

        gameState.OnValueChanged -= OnGameStateChanged;
    }


    private void OnGameStateChanged(GameState previousValue, GameState newValue)
    {
        // Handle client-side game state changes
        switch (newValue)
        {
            case GameState.Starting:
                ClientStartGame();
                break;

            case GameState.Ended:
                ClientEndGame();
                break;
        }
    }


    #region Start Logic

    private void InteractableDeck_OnInteract(object sender, EventArgs e)
    {
        if (gameState.Value != GameState.WaitingToStart) return;

        ServerStartGame();
    }

    private void ServerStartGame()
    {
        if (!IsServer) return;

        Debug.Log("[Server] Start Game!");
        
        //Change game to starting
        gameState.Value = GameState.Starting;

        //Set current turn to default
        currentTurnIndex = -1;
        currentTurnClientId.Value = ulong.MaxValue;

        //Initialise Deck
        deck = new CardDeck(deckSO);
        CardPooler.Instance.SetDeck(deck);

        //Allow deck interact
        interactableDeck.SetInteractMode(InteractMode.All);
        interactableDeck.SetText("Pull Card");

        // Start dealing cards
        StartCoroutine(StartGameCoroutine());
    }

    private IEnumerator StartGameCoroutine()
    {
        yield return StartCoroutine(DealInitialCards());

        gameState.Value = GameState.Playing;

        NextTurn();
    }

    private void ClientStartGame()
    {
        Debug.Log("Game started!");

        interactableDeck.SetInteractable(false);

        OnGameStarted?.Invoke();
    }


    #endregion

    #region End Logic


    protected void ServerEndGame()
    {
        Debug.Log("[Server] Game Finished!");

        gameState.Value = GameState.Ended;

        CardPooler.Instance.ReturnAllActiveCards();

        cardPile.Clear();
        topPileCardId.Value = 0;

        interactableDeck.SetText("Start Game");
        interactableDeck.SetInteractMode(InteractMode.Host);

        gameState.Value = GameState.WaitingToStart;
    }


    private void ClientEndGame()
    {
        Debug.Log("Game ended!");

        interactableDeck.SetInteractable(true);

        OnGameEnded?.Invoke();
    }

    #endregion

    #region Playing Logic

    public virtual void NextTurn()
    {
        if (!IsServer) return;

        int attempts = 0;
        do
        {
            currentTurnIndex = (currentTurnIndex + 1) % players.Count;
            currentPlayer = players[currentTurnIndex];
            attempts++;
        }
        while (!currentPlayer.IsPlaying() && attempts <= players.Count);

        if (attempts > players.Count || HasGameEnded())
        {
            StartCoroutine(ShowWinnerRoutine());
            return;
        }

        if (currentPlayer.IsAI)
        {
            Debug.Log($"[Server] AI turn!");

            currentTurnClientId.Value = ulong.MaxValue;
            StartCoroutine(HandleAITurn());
        }
        else
        {
            Debug.Log($"[Server] {currentPlayer.PlayerData.OwnerClientId} turn!");

            currentTurnClientId.Value = currentPlayer.PlayerData.OwnerClientId;
        }
    }

    #region Requesting Actions

    public void TryExecuteAction(ulong playerID, TAction action)
    {
        if (IsServer)
        {
            ServerTryExecuteAction(playerID, action);
            return;
        }

        if (currentTurnClientId.Value != playerID)
        {
            Debug.LogWarning("[Client] It is not your turn to execute an action!");
            return;
        }

        RequestExecuteActionServerRpc(playerID, action);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestExecuteActionServerRpc(ulong playerID, TAction action)
    {
        ServerTryExecuteAction(playerID, action);
    }

    private void ServerTryExecuteAction(ulong playerID, TAction action)
    {
        if (currentTurnClientId.Value != playerID)
        {
            Debug.LogWarning($"[Server] Player {playerID} tried to execute an action out of turn!");
            return;
        }

        StartCoroutine(ExecuteActionRoutine(action));
    }

    #endregion

    #endregion

    #region Card Management

    protected PlayingCard GetNewCard() => CardPooler.Instance.GetCard(cardSpawnTransform.position);
    public virtual void DrawCard(TPlayer player) => DealCardToPlayerHand(player);
    protected PlayingCard DealCardToPlayerHand(TPlayer player)
    {
        if (!IsServer) return null;

        PlayingCard card = GetNewCard();
        if (card == null) return null;

        // Server adds card - this automatically syncs via NetworkList in TablePlayer
        player.AddCardToHand(card);

        return card;
    }

    public void PlaceCardOnPile(PlayingCard card, bool placeFaceDown = false, float lerpSpeed = 5)
    {
        if (!IsServer) return;

        StartCoroutine(PlaceCardOnPileCoroutine(card, placeFaceDown, lerpSpeed));
    }

    private IEnumerator PlaceCardOnPileCoroutine(PlayingCard card, bool placeFaceDown = false, float lerpSpeed = 5f)
    {
        // Visual card movement
        Vector3 targetPos = cardPileTransform.position;
        Quaternion targetRot = cardPileTransform.rotation;

        if (!placeFaceDown) targetRot *= Quaternion.Euler(180, 0, 0);

        float offsetY = 0.0025f * cardPile.Count;
        targetPos += Vector3.up * offsetY;

        card.MoveTo(targetPos, lerpSpeed);
        card.RotateTo(targetRot, lerpSpeed);
        card.transform.SetParent(cardPileTransform);

        // Update server state
        cardPile.Add(card);
        topPileCardId.Value = card.NetworkObjectId;

        yield return new WaitForSeconds(timeBetweenCardDeals);
    }

    #endregion

    #region Player AI

    private IEnumerator HandleAITurn()
    {
        yield return new WaitForSeconds(AIThinkingTime);
        StartCoroutine(ExecuteActionRoutine(currentPlayer.PlayerAI.DecideAction(TurnContext.StartTurn)));
    }

    protected IEnumerator HandleAIDrawDecision()
    {
        yield return new WaitForSeconds(AIThinkingTime);

        TAction action = currentPlayer.PlayerAI.DecideAction(TurnContext.AfterDraw, drawnCard);
        yield return StartCoroutine(ExecuteActionRoutine(action));
    }

    #endregion

    #region Helper Functions



    #endregion

    #region Table Seater Interface

    public Transform TrySetPlayerAtTable(PlayerData playerData)
    {
        foreach (var player in players)
        {
            if (player.IsAI)
            {
                player.SetPlayer(playerData);
                player.SetGame(this);

                return player.PlayerStandTransform;
            }
        }

        return null;
    }

    #endregion
}
