using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
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
    public event Action OnAnyActionExecuted;
    public event Action OnAnyCardDrawn;

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
    [SerializeField] private List<TPlayer> tablePlayers = new List<TPlayer>();
    private List<TPlayer> activePlayers = new List<TPlayer>();

    [Header("Deck Settings")]
    [SerializeField] private CardDeckSO deckSO;
    [SerializeField] protected float timeBetweenCardDeals = 0.5f;
    [SerializeField] protected Transform cardSpawnTransform;
    [SerializeField] private Transform cardPileTransform;

    private CardDeck deck;
    private IInteractable interactableDeck;

    [Header("AI")]
    [SerializeField] private bool fillBots;
    [SerializeField] protected float AIThinkingTime = 1f;

    // Server-only game state
    protected List<PlayingCard> cardPile = new List<PlayingCard>();
    protected PlayingCard drawnCard;


    private const float CARD_HEIGHT_Y = 0.0025f;

    #region Public Accessors

    public NetworkVariable<ulong> CurrentPlayerTurnID => currentPlayerTurnId;
    public NetworkVariable<ulong> CurrentOwnerClientTurnID => currentOwnerClientTurnId;
    public NetworkVariable<ulong> PileCardID => topPileCardId;
    public NetworkVariable<ulong> DrawnCardID => drawnCardId;
    public IInteractable InteractableDeck => interactableDeck;
    public IEnumerable<TPlayer> Players => activePlayers;


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
        deck = new CardDeck(deckSO);
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
                interactableDeck.SetInteractable(false);
                OnGameStarted?.Invoke();
                break;

            case GameState.Ended:
                OnGameEnded?.Invoke();
                break;
        }
    }

    #region Update

    private void Update()
    {
        if (gameState.Value != GameState.WaitingToStart) return;

        fillBots = AIToggleUI.Instance.UseAI;
        interactableDeck.SetInteractable(fillBots || PlayerManager.Instance.PlayerCount > 1);
    }

    #endregion


    #region Start Logic

    private void InteractableDeck_OnInteract(object sender, EventArgs e)
    {
        if (gameState.Value != GameState.WaitingToStart) return;

        ServerStartGame();
    }

    protected virtual void ServerStartGame()
    {
        if (!IsServer) return;

        ConsoleLog.Instance.Log("Start Game");

        //Make list of active players, if not using bots do not include them
        activePlayers = fillBots ? tablePlayers : tablePlayers.Where(p => p.PlayerData != null).ToList();

        foreach (var player in activePlayers) player.SetGame(this);

        //Change game to starting
        gameState.Value = GameState.Starting;

        ResetGame();

        //Initialise Deck
        CardPooler.Instance.SetDeck(deck);

        // Start dealing cards
        StartCoroutine(StartGameCoroutine());
    }

    private IEnumerator StartGameCoroutine()
    {
        if (!IsServer) yield break;

        yield return StartCoroutine(CardPooler.Instance.InitializePool());

        yield return StartCoroutine(DealInitialCards());

        gameState.Value = GameState.Playing;

        //Allow deck interact
        interactableDeck.SetInteractMode(InteractMode.All);
        interactableDeck.SetDisplay(new InteractDisplay("Pull Card"));

        NextTurn();
    }


    #endregion

    #region End Logic


    protected virtual void ServerEndGame()
    {
        ConsoleLog.Instance.Log("Game Finished!");

        gameState.Value = GameState.Ended;

        ResetGame();

        gameState.Value = GameState.WaitingToStart;
    }

    private void ResetGame()
    {
        currentTurnIndex = -1;
        cardPile.Clear();
        topPileCardId.Value = 0;

        currentPlayer = null;
        currentPlayerTurnId.Value = ulong.MaxValue;

        interactableDeck.ResetDisplay();
        interactableDeck.SetInteractMode(InteractMode.Host);

        foreach (var player in activePlayers) player.ResetHand();

        CardPooler.Instance.ReturnAllActiveCards();
    }


    #endregion

    #region Playing Logic

    protected void NextTurn() => StartCoroutine(NextTurnRoutine());

    protected virtual IEnumerator NextTurnRoutine()
    {
        if (!IsServer) yield break;

        int attempts = 0;
        do
        {
            currentTurnIndex = (currentTurnIndex + 1) % activePlayers.Count;
            currentPlayer = activePlayers[currentTurnIndex];
            attempts++;
        }
        while (!currentPlayer.IsPlaying() && attempts <= activePlayers.Count);

        if (attempts > activePlayers.Count || HasGameEnded())
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
            ConsoleLog.Instance.Log($"{currentPlayer.GetName()} (AI) turn!");
            StartCoroutine(HandleAITurn());
        }
        else
        {
            ConsoleLog.Instance.Log($"{currentPlayer.GetName()} turn!");
        }
    }


    /// <summary>
    /// Disables and unsubscribes from every active playing card
    /// </summary>
    protected void DisableAllCardsAndUnsubscribe()
    {
        foreach (var player in activePlayers)
        {
            player.DisableAllCardsAndUnsubscribeClientRpc();
            player.ResetHandInteractableDisplay();
        }
    }

    #region Requesting Actions

        /// <summary>
        /// Executes an action in the card game
        /// </summary>
    public void ExecuteAction(ulong playerID, TAction action)
    {
        if (IsServer)
        {
            ServerExecuteAction(playerID, action);
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
    private void RequestExecuteActionServerRpc(ulong playerID, TAction action) => ServerExecuteAction(playerID, action);

    private void ServerExecuteAction(ulong playerID, TAction action)
    {
        if (CanOnlyPlayInTurn() && currentOwnerClientTurnId.Value != playerID)
        {
            Debug.LogWarning($"[Server] Player {playerID} tried to execute an action out of turn!");
            return;
        }

        //Disables all previous interactions with cards before executing a new action
        DisableAllCardsAndUnsubscribe();
        StartCoroutine(ExecuteActionRoutine(action));
    }





    /// <summary>
    /// Coroutine to execute an action in the game
    /// </summary>
    protected virtual IEnumerator ExecuteActionRoutine(TAction action)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Client] Only the server can execute actions!");
            yield break;
        }

        OnAnyActionExecuted?.Invoke();

        yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);
    }

    #endregion

    #endregion

    #region Card Management



    /// <summary>
    /// Draws a new card from the deck
    /// </summary>
    protected PlayingCard DrawCard()
    {
        drawnCard = CardPooler.Instance.GetCard(cardSpawnTransform.position);
        drawnCardId.Value = drawnCard.NetworkObjectId;

        OnAnyCardDrawn?.Invoke();

        return drawnCard;
    }


    /// <summary>
    /// Deals a card into the hand of a given player
    /// </summary>
    protected virtual IEnumerator DealCardToPlayer(TPlayer player)
    {
        if (!IsServer) yield break;

        drawnCard = DrawCard();

        if (drawnCard == null) yield break;

        player.AddCardToHand(drawnCard);

        yield return new WaitForEndOfFrame();

        yield return new WaitUntil(() => drawnCard.IsMoving == false);
    }


    /// <summary>
    /// Places a given card on the card pile
    /// </summary>
    public void PlaceCardOnPile(PlayingCard card, bool placeFaceDown = false, float lerpSpeed = 5)
    {
        if (!IsServer) return;

        foreach (var player in activePlayers) player.RequestSetCardInteractable(card.NetworkObjectId, false);

        StartCoroutine(PlaceCardOnPileCoroutine(card, placeFaceDown, lerpSpeed));
    }


    /// <summary>
    /// Coroutine to place a card on the card pile
    /// </summary>
    private IEnumerator PlaceCardOnPileCoroutine(PlayingCard card, bool placeFaceDown = false, float lerpSpeed = 5f)
    {
        // Visual card movement
        Vector3 targetPos = cardPileTransform.position;
        Quaternion targetRot = cardPileTransform.rotation;

        if (placeFaceDown) targetRot *= Quaternion.Euler(180, 0, 0);

        float offsetY = CARD_HEIGHT_Y * cardPile.Count;
        targetPos += Vector3.up * offsetY;

        card.MoveTo(targetPos, lerpSpeed);
        card.RotateTo(targetRot, lerpSpeed);
        card.transform.SetParent(cardPileTransform);

        // Update server state
        cardPile.Add(card);
        topPileCardId.Value = card.NetworkObjectId;

        yield return new WaitForSeconds(timeBetweenCardDeals);
    }




    /// <summary>
    /// Trades a new card for a curret card in a given player
    /// </summary>
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



    /// <summary>
    /// Tries to swap the cards between 2 players
    /// </summary>
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




    /// <summary>
    /// Swap the hands between 2 players
    /// </summary>
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



    /// <summary>
    /// Returns the card from the stack to a given players hand
    /// </summary>
    protected IEnumerator ReturnCardFromPile(CambioPlayer player)
    {
        PlayingCard card = cardPile[cardPile.Count - 1];

        player.AddCardToHand(card);
        cardPile.Remove(card);

        yield return new WaitUntil(() => card.IsMoving == false);
    }

    #endregion

    #region Card Movement

    /// <summary>
    /// Brings the card to a player and faces them
    /// </summary>
    protected void BringCardToPlayer(CambioPlayer player, PlayingCard card, Vector3 offset)
    {
        // Move card above player, then apply offset (like reveal or pull position)
        Vector3 targetPos = player.transform.position + offset;
        card.MoveTo(targetPos, 5f);

        // Rotate card to face upwards relative to player
        Quaternion targetUpwardsRot = Quaternion.LookRotation(player.transform.forward, Vector3.up) * Quaternion.Euler(-90f, 0f, 0);
        card.RotateTo(targetUpwardsRot, 5f);
    }


    /// <summary>
    /// Lifts a card up by a given height
    /// </summary>
    protected void LiftCard(PlayingCard card, float height) => card.MoveTo(card.transform.position + new Vector3(0, height, 0), 5f);

    #endregion

    #region Player AI

    /// <summary>
    /// Coroutine to execute the start turn action for the AI
    /// </summary>
    private IEnumerator HandleAITurn()
    {
        yield return new WaitForSeconds(AIThinkingTime);
        StartCoroutine(ExecuteActionRoutine(currentPlayer.PlayerAI.DecideAction(TurnContext.StartTurn)));
    }

    /// <summary>
    /// Coroutine to execute the action after drawing a card for the AI
    /// </summary>
    protected IEnumerator HandleAIDrawDecision()
    {
        yield return new WaitForSeconds(AIThinkingTime);

        TAction action = currentPlayer.PlayerAI.DecideAction(TurnContext.AfterDraw);
        yield return StartCoroutine(ExecuteActionRoutine(action));
    }

    #endregion

    #region Player Accessors


    /// <summary>
    /// Gets the player from the player data
    /// </summary>
    public TPlayer GetPlayerFromData(PlayerData data)
    {
        foreach (var player in tablePlayers) if (player?.PlayerData == data) return player;
        return null;
    }



    /// <summary>
    /// Gets the player from the owner client ID
    /// </summary>
    protected TPlayer GetPlayerFromClientID(ulong clientID) => GetPlayerFromData(PlayerManager.Instance.GetPlayerDataById(clientID));



    /// <summary>
    /// Gets the player from their table ID
    /// </summary>
    protected TPlayer GetPlayerFromPlayerID(ulong playerID)
    {
        foreach (var player in tablePlayers) if (player?.PlayerId == playerID) return player;
        return null;
    }



    /// <summary>
    /// Gets the player with a specific card
    /// </summary>
    protected TPlayer GetPlayerWithCard(PlayingCard card)
    {
        if (card == null) return null;

        foreach (var player in activePlayers)
        {
            if (player.Hand.Cards.Contains(card))
                return player;
        }
        return null;
    }



    /// <summary>
    /// Gets the player with a specific card from the network ID
    /// </summary>
    public TPlayer GetPlayerWithCard(ulong cardNetworkId)
    {
        foreach (var player in activePlayers)
        {
            if (player.HandCardIDs.Contains(cardNetworkId)) return player;
        }
        return null;
    }

    #endregion

    #region Table Seater Interface

    /// <summary>
    /// Sets the player at the table
    /// </summary>
    public Transform TrySetPlayerAtTable(PlayerData playerData)
    {
        foreach (var player in tablePlayers)
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
