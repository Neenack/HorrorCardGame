using System;
using System.Collections.Generic;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using static Interactable;
using static UnityEngine.Video.VideoPlayer;

public abstract class TablePlayer<TPlayer, TAction, TAI> : NetworkBehaviour
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{
    protected NetworkVariable<ulong> tablePlayerId = new NetworkVariable<ulong>(
       ulong.MaxValue,
       NetworkVariableReadPermission.Everyone,
       NetworkVariableWritePermission.Server
    );

    private PlayerData playerData = null;
    private ICardGame<TPlayer, TAction, TAI> game;

    //Server hand
    private PlayerHand hand;
    //Client hand
    private NetworkList<ulong> handCardIds = new NetworkList<ulong>();

    protected TAI playerAI = null;
    protected bool isTurn = false;

    protected Dictionary<EventHandler<InteractEventArgs>, List<PlayingCard>> eventSubscriptionDictionary = new Dictionary<EventHandler<InteractEventArgs>, List<PlayingCard>>();

    [Header("Player")]
    [SerializeField] private Transform playerStandTransform;

    [Header("Card Positions")]
    [SerializeField] protected Vector3 cardSpacing = new Vector3(0.3f, 0, 0);
    [SerializeField] protected float yOffset = 0;
    [SerializeField] private float fanAngle = 0f;


    #region Public Accessors

    public bool IsAI => playerData == null;
    public PlayerData PlayerData => playerData;
    public ICardGame<TPlayer, TAction, TAI> Game => game;
    public PlayerHand Hand => hand;
    public NetworkList<ulong> HandCardIDs => handCardIds;
    public TAI PlayerAI => playerAI;
    public ulong TablePlayerID => tablePlayerId.Value;
    public Transform PlayerStandTransform => playerStandTransform;
    public ulong LocalClientID => PlayerData ? PlayerData.OwnerClientId : OwnerClientId;

    #endregion

    #region Abstract Functions

    public abstract bool IsPlaying();
    public abstract int GetScore();
    protected abstract TAI CreateAI();

    #endregion

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        ResetHand();

        if (IsServer) AssignTablePlayerID();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        //Unsubscribes from events
        if (hand != null) hand.OnHandUpdated -= Hand_OnHandUpdated;
        if (handCardIds != null) handCardIds.OnListChanged -= OnHandCardIdsChanged;

        UnsubscribeFromGame();
        UnsubscribeAllCards();
    }

    /// <summary>
    /// Sets the player data for the table player
    /// </summary>
    public virtual void SetPlayer(PlayerData data)
    {
        playerData = data;
    }

    /// <summary>
    /// Sets the player ID for the table game
    /// </summary>
    private void AssignTablePlayerID()
    {
        byte[] buffer = new byte[8];
        new System.Random().NextBytes(buffer);
        tablePlayerId.Value = System.BitConverter.ToUInt64(buffer, 0);
    }


    /// <summary>
    /// Sets the game for the table player (called both on server and clients)
    /// </summary>
    public void SetGame(ICardGame<TPlayer, TAction, TAI> game)
    {
        UnsubscribeFromGame();

        this.game = game;

        SubscribeToGame();
    }

    /// <summary>
    /// Returns the players name
    /// </summary>
    public string GetName()
    {
        if (playerData) return playerData.GetName();
        return "[AI] " + gameObject.name;
    }

    #region Game Subscriptions

    protected virtual void SubscribeToGame()
    {
        if (game == null) return;

        game.CurrentGameState.OnValueChanged += OnGameStateChanged;
        game.PileCardID.OnValueChanged += OnPileCardChanged;
        game.CurrentPlayerTurnTableID.OnValueChanged += OnTurnChanged;
        game.OnAnyActionExecuted += Game_OnActionExecuted;
        game.OnGameReset += Game_OnGameReset;
    }
    protected virtual void UnsubscribeFromGame()
    {
        if (game == null) return;

        game.CurrentGameState.OnValueChanged -= OnGameStateChanged;
        game.PileCardID.OnValueChanged -= OnPileCardChanged;
        game.CurrentPlayerTurnTableID.OnValueChanged -= OnTurnChanged;
        game.OnAnyActionExecuted -= Game_OnActionExecuted;
        game.OnGameReset -= Game_OnGameReset;
    }

    /// <summary>
    /// Called when the game state changes
    /// </summary>
    private void OnGameStateChanged(GameState previousValue, GameState newValue)
    {
        switch (newValue)
        {
            case GameState.Starting:
                Game_OnGameStarted();
                break;

            case GameState.Ending:
                Game_OnGameEnded();
                break;
        }
    }

    /// <summary>
    /// Called when the turn changes for the game
    /// </summary>
    private void OnTurnChanged(ulong oldValue, ulong newValue)
    {
        if (IsAI) return;

        UnsubscribeAllCards();

        if (oldValue == TablePlayerID && isTurn)
        {
            OnTurnEnded();
        }

        if (newValue == TablePlayerID && game.GetCurrentTurnPlayer().PlayerData.OwnerClientId == NetworkManager.Singleton.LocalClientId)
        {
            OnTurnStarted();
        }
    }

    /// <summary>
    /// Called when the a new card is placed on the pile
    /// </summary>
    protected virtual void OnPileCardChanged(ulong previousCard, ulong newCard)
    {

    }

    /// <summary>
    /// Called when the game starts
    /// </summary>
    protected virtual void Game_OnGameStarted()
    {
        if (IsServer) CreateAI();
    }

    /// <summary>
    /// Called when the game ends
    /// </summary>
    protected virtual void Game_OnGameEnded() 
    {

    }

    /// <summary>
    /// Called when the game is reset
    /// </summary>
    protected virtual void Game_OnGameReset()
    {
        if (IsServer) handCardIds.Clear();
        ResetHand();
        UnsubscribeAllCards();
    }

    /// <summary>
    /// Called when any action is executed in the game
    /// </summary>
    protected virtual void Game_OnActionExecuted() 
    {
        UnsubscribeAllCards();
    }

    /// <summary>
    /// Called when the players turn starts, and only called on the players local client
    /// </summary>
    protected virtual void OnTurnStarted()
    {
        isTurn = true;
    }

    /// <summary>
    /// Called when the players turn ends, and only called on the players local client
    /// </summary>
    protected virtual void OnTurnEnded()
    {
        isTurn = false;
    }

    /// <summary>
    /// Called when the players hand card ids are updated, syncronizes the hand for each client
    /// </summary>
    protected virtual void OnHandCardIdsChanged(NetworkListEvent<ulong> changeEvent)
    {
        hand.ClearHand();

        foreach (ulong cardId in handCardIds)
        {
            PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardId);
            if (card) hand.AddCard(card);
        }
    }

    #endregion

    #region Card Subscriptions

    /// <summary>
    /// Subscribes a playing card to a given event
    /// </summary>
    public void SubscribeCardTo(PlayingCard card, EventHandler<InteractEventArgs> onInteract) 
    { 
        if (card != null) 
        {
            //Debug.Log($"Subscribing card to {onInteract.Method.Name} on {gameObject.name}");

            card.Interactable.OnInteract += onInteract;
            if (eventSubscriptionDictionary.TryGetValue(onInteract, out List<PlayingCard> cards)) 
            { 
                eventSubscriptionDictionary[onInteract].Add(card);
            } 
            else eventSubscriptionDictionary.Add(onInteract, new List<PlayingCard>() { card }); 
        }
        else Debug.Log("Cannot find card to subscribe event to");
    }

    /// <summary>
    /// Unsubscribes a playing card from a given event
    /// </summary>
    public void UnsubscribeCardFrom(PlayingCard card, EventHandler<InteractEventArgs> onInteract)
    {
        if (card != null)
        {
            //Debug.Log($"Unsubscribing card from {onInteract.Method.Name} on {gameObject.name}");

            card.Interactable.OnInteract -= onInteract;
            if (eventSubscriptionDictionary.TryGetValue(onInteract, out List<PlayingCard> cards))
            {
                eventSubscriptionDictionary[onInteract].Remove(card);
                if (cards.Count <= 0) eventSubscriptionDictionary.Remove(onInteract);
            }
        }
        else ConsoleLog.Instance.Log("Cannot find card to unsubscribe from");
    }

    /// <summary>
    /// Unsubscribes a playing card from all events
    /// </summary>
    public void UnsubscribeCard(PlayingCard card)
    {
        if (card == null)
            return;

        var handlersToRemove = new List<EventHandler<InteractEventArgs>>();

        // Find all handlers that reference this card
        foreach (var kvp in eventSubscriptionDictionary)
        {
            if (kvp.Value.Contains(card))
            {
                card.Interactable.OnInteract -= kvp.Key;
                kvp.Value.Remove(card);

                // If that handler has no more cards, mark it for removal
                if (kvp.Value.Count == 0)
                    handlersToRemove.Add(kvp.Key);
            }
        }

        // Clean up empty handler entries
        foreach (var handler in handlersToRemove)
        {
            eventSubscriptionDictionary.Remove(handler);
        }
    }

    /// <summary>
    /// Unsubscribes from all known subscriptions
    /// </summary>
    public void UnsubscribeAllCards()
    {
        if (eventSubscriptionDictionary.Count > 0)
        {
            //Unsubscribe from all cards if any
            foreach (var kvp in eventSubscriptionDictionary)
            {
                foreach (var card in kvp.Value)
                {
                    //Debug.Log($"Unsubscribing card from {kvp.Key.Method.Name} on {gameObject.name}");

                    card.Interactable.OnInteract -= kvp.Key;
                }
            }
            eventSubscriptionDictionary.Clear();
        }
    }

    #endregion

    #region Card Handling (Server)

    public void AddCardToHand(PlayingCard card)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"Only the server can add {card.ToString()} to {GetName()}'s hand!");
            return;
        }

        //hand.AddCard(card);
        handCardIds.Add(card.NetworkObjectId);
    }

    public void InsertCardToHand(PlayingCard card, int index)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"Only the server can insert {card.ToString()} to {GetName()}'s hand!");
            return;
        }

        //hand.InsertCard(card, index);
        handCardIds.Insert(index, card.NetworkObjectId);
    }

    public bool RemoveCardFromHand(PlayingCard card)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"Only the server can remove {card.ToString()} from {GetName()}'s hand!");
            return false;
        }

        //bool removed = hand.RemoveCard(card);
        //if (removed) handCardIds.Remove(card.NetworkObjectId);

        return handCardIds.Remove(card.NetworkObjectId);
    }

    /// <summary>
    /// Resets the hand for the player
    /// </summary>
    public void ResetHand()
    {
        if (hand == null)
        {
            hand = new PlayerHand();

            hand.OnHandUpdated += Hand_OnHandUpdated;
            handCardIds.OnListChanged += OnHandCardIdsChanged;
        }

        hand.ClearHand();
        if (IsServer) handCardIds.Clear();
    }

    protected virtual void Hand_OnHandUpdated()
    {
        RecentreCards();
    }

    public void RecentreCards(float lerpSpeed = 5f)
    {
        if (!IsServer) return;

        int totalCards = hand.Cards.Count;

        for (int i = 0; i < totalCards; i++)
        {
            PlayingCard card = hand.Cards[i];

            // Get target position and rotation from the player
            Vector3 targetPos = GetCardPosition(i, totalCards);
            Quaternion targetRot = GetCardRotation(i, totalCards);

            // Move and rotate the card smoothly
            card.MoveTo(targetPos, lerpSpeed);
            card.RotateTo(targetRot, lerpSpeed);
        }
    }

    public virtual void SortHand()
    {
        PlayerHand sortedHand = new PlayerHand();

        List<PlayingCard> sortedCards = new List<PlayingCard>(hand.Cards);
        sortedCards.Sort();

        foreach (var card in sortedCards)
        {
            sortedHand.AddCard(card);
        }

        hand = sortedHand;
    }

    public virtual Vector3 GetCardPosition(int cardIndex, int totalCards)
    {
        // Centered base position at this transform
        Vector3 basePos = transform.position + new Vector3(0, yOffset, 0);

        // Spread cards evenly around the middle
        float offsetFactor = cardIndex - (totalCards - 1) / 2f;

        // Offset sideways using local right
        Vector3 sideOffset = transform.right * offsetFactor * cardSpacing.x;
        Vector3 upOffset = transform.up * offsetFactor * cardSpacing.y;
        Vector3 forwardOffset = transform.forward * offsetFactor * cardSpacing.z;

        return basePos + forwardOffset + upOffset + sideOffset;
    }

    public virtual Quaternion GetCardRotation(int cardIndex, int totalCards)
    {
        // How much to angle cards apart (tweak this in inspector)
        float angleStep = fanAngle / Mathf.Max(1, totalCards - 1);

        // Rotate around local up axis, centered on middle card
        float angle = (cardIndex - (totalCards - 1) / 2f) * angleStep;

        return transform.rotation * Quaternion.Euler(180f, angle, 0f);
    }

    #endregion
}
