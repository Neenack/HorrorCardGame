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
    protected TablePlayer<TAction> currentPlayer;

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

    [Header("AI")]
    [SerializeField] protected float AIThinkingTime = 1f;


    // Server-only game state
    private List<PlayingCard> cardPile = new List<PlayingCard>();

    #region Public Accessors

    public NetworkVariable<ulong> CurrentTurnID => currentTurnClientId;
    public PlayingCard TopPileCard => cardPile.Count > 0 ? cardPile[cardPile.Count - 1] : null;
    public IInteractable InteractableDeck => interactableDeck;
    public IEnumerable<TablePlayer<TAction>> Players => players;


    #endregion

    #region Abstract Functions

    protected abstract IEnumerator DealInitialCards();
    protected abstract bool HasGameEnded();
    protected abstract IEnumerator ShowWinnerRoutine();
    public abstract IEnumerator TryExecuteAction(TAction action);

    #endregion

    public override void OnNetworkSpawn()
    {
        interactableDeck = GetComponentInChildren<IInteractable>();

        if (IsServer)
        {
            if (interactableDeck != null) interactableDeck.OnInteract += InteractableDeck_OnInteract;
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
                Debug.Log("Game started!");
                OnGameStarted?.Invoke();
                break;
            case GameState.Ended:
                Debug.Log("Game ended!");
                OnGameEnded?.Invoke();
                break;
        }
    }


    #region Start Logic

    private void InteractableDeck_OnInteract(object sender, EventArgs e)
    {
        if (gameState.Value != GameState.WaitingToStart) return;

        StartGame();
    }

    private void StartGame()
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

        // Start dealing cards
        StartCoroutine(StartGameCoroutine());
    }

    private IEnumerator StartGameCoroutine()
    {
        yield return StartCoroutine(DealInitialCards());

        gameState.Value = GameState.Playing;

        NextTurn();
    }


    #endregion

    #region End Logic


    protected void EndGame()
    {
        Debug.Log("[Server] Game Finished!");

        gameState.Value = GameState.Ended;

        CardPooler.Instance.ReturnAllActiveCards();

        cardPile.Clear();
        topPileCardId.Value = 0;

        gameState.Value = GameState.WaitingToStart;
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



    private IEnumerator HandleAITurn()
    {
        yield return new WaitForSeconds(AIThinkingTime);
        TryExecuteAction(currentPlayer.PlayerAI.DecideAction(TurnContext.StartTurn));
    }

    #endregion

    #region Card Management

    protected PlayingCard DealCardToPlayerHand(TPlayer player)
    {
        if (!IsServer) return null;

        PlayingCard card = CardPooler.Instance.GetCard(cardSpawnTransform.position);
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


    #region Helper Functions



    #endregion

    public Transform TrySetPlayerAtTable(PlayerData playerData)
    {
        foreach (var player in players)
        {
            if (player.IsAI)
            {
                player.SetPlayer(playerData);
                return player.PlayerStandTransform;
            }
        }

        return null;
    }
}
