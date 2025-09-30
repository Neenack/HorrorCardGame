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

public abstract class CardGame<TPlayer, TAction, TAI> : NetworkBehaviour, ICardGame<TPlayer, TAction, TAI>, ITable
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{
    public event Action OnGameStarted;
    public event Action OnGameEnded;
    public event Action OnActionExecuted;

    protected NetworkVariable<ulong> currentPlayerTurnId = new NetworkVariable<ulong>(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    protected NetworkVariable<ulong> currentOwnerClientTurnId = new NetworkVariable<ulong>(
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

    protected NetworkVariable<ulong> drawnCardId = new NetworkVariable<ulong>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    [Header("Seats")]
    [SerializeField] protected List<TPlayer> players = new List<TPlayer>();

    [Header("Deck Settings")]
    [SerializeField] private CardDeckSO deckSO;
    [SerializeField] protected float timeBetweenCardDeals = 0.5f;
    [SerializeField] protected Transform cardSpawnTransform;
    [SerializeField] private Transform cardPileTransform;

    private CardDeck deck;
    private IInteractable interactableDeck;

    [Header("AI")]
    [SerializeField] protected float AIThinkingTime = 1f;


    // Server-only game state
    protected List<PlayingCard> cardPile = new List<PlayingCard>();
    protected PlayingCard drawnCard;

    #region Public Accessors

    public NetworkVariable<ulong> CurrentPlayerTurnID => currentPlayerTurnId;
    public NetworkVariable<ulong> CurrentOwnerClientTurnID => currentOwnerClientTurnId;
    public NetworkVariable<ulong> PileCardID => topPileCardId;
    public NetworkVariable<ulong> DrawnCardID => drawnCardId;
    public IInteractable InteractableDeck => interactableDeck;
    public IEnumerable<TPlayer> Players => players;


    #endregion

    #region Abstract Functions

    protected abstract IEnumerator DealInitialCards();
    protected abstract bool HasGameEnded();
    protected abstract IEnumerator ShowWinnerRoutine();
    protected abstract bool CanOnlyPlayInTurn();

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

        ConsoleLog.Instance.AddLog("Start Game");

        //Change game to starting
        gameState.Value = GameState.Starting;

        //Set current turn to default
        currentTurnIndex = -1;

        //Initialise Deck
        deck = new CardDeck(deckSO);
        CardPooler.Instance.SetDeck(deck);

        //Allow deck interact
        interactableDeck.SetInteractMode(InteractMode.All);
        interactableDeck.SetText("Pull Card");

        //Setup AI players on the server
        foreach (var player in players) if (player.IsAI) player.SetGame(this);

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

    protected void NextTurn() => StartCoroutine(NextTurnRoutine());

    protected virtual IEnumerator NextTurnRoutine()
    {
        if (!IsServer) yield break;

        UpdatePlayerHands();

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
            yield break;
        }

        //Set owner client id so only the given client will recieve the input
        currentOwnerClientTurnId.Value = currentPlayer.PlayerData == null ? OwnerClientId : currentPlayer.PlayerData.OwnerClientId;

        yield return new WaitForSeconds(1f);

        //change the turn id
        currentPlayerTurnId.Value = currentPlayer.PlayerId;

        if (currentPlayer.IsAI)
        {
            ConsoleLog.Instance.AddLog($"{currentPlayer.GetName()} (AI) turn!");
            StartCoroutine(HandleAITurn());
        }
        else
        {
            ConsoleLog.Instance.AddLog($"{currentPlayer.GetName()} turn!");
        }
    }

    protected void UpdatePlayerHands()
    {
        foreach (var player in players) player.Hand.UpdateHand();
    }

    protected void DisableAllCardsAndUnsubscribe()
    {
        foreach (var player in players) player.DisableAllCardsAndUnsubscribeClientRpc();
    }

    #region Requesting Actions

    public void TryExecuteAction(ulong playerID, TAction action)
    {
        if (IsServer)
        {
            ServerTryExecuteAction(playerID, action);
            return;
        }

        if (CanOnlyPlayInTurn() && currentOwnerClientTurnId.Value != playerID)
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
        if (CanOnlyPlayInTurn() && currentOwnerClientTurnId.Value != playerID)
        {
            Debug.LogWarning($"[Server] Player {playerID} tried to execute an action out of turn!");
            return;
        }

        DisableAllCardsAndUnsubscribe();

        StartCoroutine(ExecuteActionRoutine(action));
    }

    protected virtual IEnumerator ExecuteActionRoutine(TAction action)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Client] Only the server can execute actions!");
            yield break;
        }

        OnActionExecuted?.Invoke();

        yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);
    }

    #endregion

    #endregion

    #region Card Management

    protected PlayingCard DrawCard()
    {
        drawnCard = CardPooler.Instance.GetCard(cardSpawnTransform.position);
        drawnCardId.Value = drawnCard.NetworkObjectId;

        return drawnCard;
    }

    protected virtual IEnumerator DealCardToPlayer(TPlayer player)
    {
        if (!IsServer) yield break;

        drawnCard = DrawCard();

        if (drawnCard == null) yield break;

        player.AddCardToHand(drawnCard);

        yield return new WaitForEndOfFrame();

        yield return new WaitUntil(() => drawnCard.IsMoving == false);
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

    protected bool TryTradeCard(TPlayer target, PlayingCard cardToAdd, PlayingCard cardToDiscard)
    {
        if (cardToDiscard == null || cardToAdd == null || target == null)
            return false;

        int index = target.Hand.GetIndexOfCard(cardToDiscard);
        if (index == -1) return false;

        if (target.RemoveCardFromHand(cardToDiscard))
        {
            PlaceCardOnPile(cardToDiscard);
            target.InsertCardToHand(cardToAdd, index);
            return true;
        }

        return false;
    }

    protected bool TrySwapCards(TPlayer player1, PlayingCard card1, TPlayer player2, PlayingCard card2)
    {
        int index1 = player1.Hand.GetIndexOfCard(card1);
        int index2 = player2.Hand.GetIndexOfCard(card2);

        if (index1 == -1 || index2 == -1) return false;

        bool remove1 = player1.RemoveCardFromHand(card1);
        bool remove2 = player2.RemoveCardFromHand(card2);

        if (remove1 && remove2)
        {
            player1.InsertCardToHand(card2, index1);
            player2.InsertCardToHand(card1, index2);
            return true;
        }

        return false;
    }

    protected void SwapHands(TPlayer player1, TPlayer player2)
    {
        List<PlayingCard> temp1Cards = new List<PlayingCard>(player1.Hand.Cards);
        List<PlayingCard> temp2Cards = new List<PlayingCard>(player2.Hand.Cards);

        player1.ResetHand();
        player2.ResetHand();

        foreach (var card in temp2Cards)
            player1.AddCardToHand(card);

        foreach (var card in temp1Cards)
            player2.AddCardToHand(card);
    }

    #endregion

    #region Card Movement

    /// <summary>
    /// Shows a card to a player
    /// </summary>
    protected void BringCardToPlayer(CambioPlayer player, PlayingCard card, Vector3 offset)
    {
        // Move card above player, then apply offset (like reveal or pull position)
        Vector3 targetPos = player.transform.position + offset;
        card.MoveTo(targetPos, 5f);

        // Rotate card to face upwards relative to player
        Quaternion targetUpwardsRot = Quaternion.LookRotation(player.transform.forward, Vector3.up) * Quaternion.Euler(90f, 0f, 0);
        card.RotateTo(targetUpwardsRot, 5f);
    }


    /// <summary>
    /// Lifts a card up by a given height
    /// </summary>
    protected void LiftCard(PlayingCard card, float height) => card.MoveTo(card.transform.position + new Vector3(0, height, 0), 5f);

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

        TAction action = currentPlayer.PlayerAI.DecideAction(TurnContext.AfterDraw);
        yield return StartCoroutine(ExecuteActionRoutine(action));
    }

    #endregion

    #region Helper Functions

    public TPlayer GetPlayerFromData(PlayerData data)
    {
        foreach (var player in players) if (player?.PlayerData == data) return player;
        return null;
    }

    protected TPlayer GetPlayerFromClientID(ulong clientID) => GetPlayerFromData(PlayerManager.Instance.GetPlayerDataById(clientID));

    protected TPlayer GetPlayerFromPlayerID(ulong playerID)
    {
        foreach (var player in players) if (player?.PlayerId == playerID) return player;
        return null;
    }

    protected TPlayer GetPlayerWithCard(PlayingCard card)
    {
        if (card == null) return null;

        foreach (var player in players)
        {
            if (player.Hand.Cards.Contains(card))
                return player;
        }
        return null;
    }
    public TPlayer GetPlayerWithCard(ulong cardNetworkId)
    {
        foreach (var player in players)
        {
            if (player.HandCardIDs.Contains(cardNetworkId)) return player;
        }
        return null;
    }

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
