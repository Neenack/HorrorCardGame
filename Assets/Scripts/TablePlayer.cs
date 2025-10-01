using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using static Interactable;

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
    [SerializeField] private float dealingXRotation = 110f;


    #region Public Accessors

    public bool IsAI => playerData == null;
    public PlayerData PlayerData => playerData;
    public ICardGame<TPlayer, TAction, TAI> Game => game;
    public PlayerHand Hand => hand;
    public NetworkList<ulong> HandCardIDs => handCardIds;
    public TAI PlayerAI => playerAI;
    public ulong PlayerId => tablePlayerId.Value;
    public Transform PlayerStandTransform => playerStandTransform;

    #endregion

    #region Abstract Functions

    public abstract bool IsPlaying();
    public abstract int GetScore();
    protected abstract TAI CreateAI();
    protected abstract EventHandler<InteractEventArgs> GetCardOnInteractEvent(TAction data);

    #endregion

    public override void OnNetworkSpawn()
    {
        ResetHand();
        if (IsServer) AssignTablePlayerID();
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        //Unsubscribes from events
        if (hand != null) hand.OnHandUpdated -= Hand_OnHandUpdated;
        if (handCardIds != null) handCardIds.OnListChanged -= OnHandCardIdsChanged;

        if (game != null)
        {
            game.CurrentPlayerTurnID.OnValueChanged -= OnTurnChanged;
            game.OnGameStarted -= Game_OnGameStarted;
            game.OnGameEnded -= Game_OnGameEnded;
            game.OnAnyActionExecuted -= Game_OnActionExecuted;
        }

        UnsubscribeAll();
    }

    private void AssignTablePlayerID()
    {
        byte[] buffer = new byte[8];
        new System.Random().NextBytes(buffer);
        tablePlayerId.Value = System.BitConverter.ToUInt64(buffer, 0);
    }


    /// <summary>
    /// Sets the player data for the table player
    /// </summary>
    public void SetPlayer(PlayerData data)
    {
        playerData = data;
    }



    /// <summary>
    /// Sets the game for the table player
    /// </summary>
    public void SetGame(ICardGame<TPlayer, TAction, TAI> game)
    {
        this.game = game;

        game.CurrentPlayerTurnID.OnValueChanged += OnTurnChanged;
        game.OnGameStarted += Game_OnGameStarted;
        game.OnGameEnded += Game_OnGameEnded;
        game.OnAnyActionExecuted += Game_OnActionExecuted;

        CreateAI();
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
        handCardIds.Clear();
    }


    #region Player Starting and Ending Turn Logic

    /// <summary>
    /// Called when the turn changes for the game
    /// </summary>
    private void OnTurnChanged(ulong oldValue, ulong newValue)
    {
        if (hand != null && IsServer) hand.UpdateHand();

        if (IsAI) return;

        DisableHandAndUnsubscribe();

        if (oldValue == PlayerId && isTurn)
        {
            EndPlayerTurn();
        }

        if (newValue == PlayerId && game.CurrentOwnerClientTurnID.Value == NetworkManager.Singleton.LocalClientId)
        {
            StartPlayerTurn();
        }
    }

    protected virtual void Game_OnGameEnded() { }
    protected virtual void Game_OnGameStarted() { }
    protected virtual void Game_OnActionExecuted() { }

    /// <summary>
    /// Called when the players turn starts, and only called on the players local client
    /// </summary>
    protected virtual void StartPlayerTurn()
    {
        isTurn = true;
    }

    /// <summary>
    /// Called when the players turn ends, and only called on the players local client
    /// </summary>
    protected virtual void EndPlayerTurn()
    {
        isTurn = false;
    }

    #endregion

    /// <summary>
    /// Disables all playing cards, unsubscribes from all known subscriptions and resets interact display
    /// </summary>
    protected void DisableHandAndUnsubscribe()
    {
        //Disable all cards in hand
        foreach (var card in Hand.Cards)
        {
            card.Interactable.SetInteractable(false);
            card.Interactable.ResetDisplay();
        }

        UnsubscribeAll();
    }

    /// <summary>
    /// Disables all playing cards, unsubscribes from all known subscriptions and resets interact display
    /// </summary>
    [ClientRpc] public void DisableAllCardsAndUnsubscribeClientRpc() => DisableHandAndUnsubscribe();

    /// <summary>
    /// Unsubscribes from all known subscriptions
    /// </summary>
    protected void UnsubscribeAll()
    {
        if (eventSubscriptionDictionary.Count > 0)
        {
            //Unsubscribe from all cards if any
            foreach (var kvp in eventSubscriptionDictionary)
            {
                foreach (var card in kvp.Value)
                {
                    card.Interactable.OnInteract -= kvp.Key;
                }
            }
            eventSubscriptionDictionary.Clear();
        }
    }


    /// <summary>
    /// Returns the players name
    /// </summary>
    public string GetName()
    {
        if (playerData) return playerData.GetName();
        return "[AI] " + gameObject.name;
    }


    /// <summary>
    /// Called when the players hand is updated
    /// </summary>
    protected virtual void OnHandCardIdsChanged(NetworkListEvent<ulong> changeEvent)
    {
        if (hand == null)
        {
            hand = new PlayerHand();
            hand.OnHandUpdated += Hand_OnHandUpdated;
        }

        hand.ClearHand();
        foreach (ulong cardId in handCardIds)
        {
            PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardId);
            if (card) hand.AddCard(card);
        }
    }

    #region Player Interaction

    #region Single Card (No Action)
    public void RequestSetCardInteractable(ulong cardId, bool interactable)
    {
        if (IsServer)
        {
            SetCardInteractionClientRpc(cardId, interactable, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { Game.CurrentOwnerClientTurnID.Value } } });
            return;
        }
        SetCardInteraction(cardId, interactable);
    }
    [ClientRpc] private void SetCardInteractionClientRpc(ulong cardId, bool interactable, ClientRpcParams clientRpcParams) => SetCardInteraction(cardId, interactable);

    #endregion

    #region Single Card (With Action)

    /// <summary>
    /// Requests for the player to enable interactions for a given card
    /// </summary>
    public void RequestSetCardInteractable(ulong cardNetworkID, bool interactable, TAction action)
    {
        if (IsServer)
        {
            SetCardInteractionClientRpc(cardNetworkID, interactable, action, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { PlayerData.OwnerClientId } } });
            return;
        }
        SetCardInteraction(cardNetworkID, interactable, action);
    }
    [ClientRpc] private void SetCardInteractionClientRpc(ulong cardNetworkID, bool interactable, TAction action, ClientRpcParams clientRpcParams) => SetCardInteraction(cardNetworkID, interactable, action);

    private void SetCardInteraction(ulong cardNetworkID, bool interactable, TAction? action = null)
    {
        PlayingCard cardToInteract = PlayingCard.GetPlayingCardFromNetworkID(cardNetworkID);

        cardToInteract.Interactable.SetInteractable(interactable);

        if (interactable && action.HasValue) SubscribeCardTo(cardNetworkID, GetCardOnInteractEvent(action.Value));
        if (!interactable && action.HasValue) UnsubscribeCardFrom(cardNetworkID, GetCardOnInteractEvent(action.Value));
    }

    #endregion

    #region Hand Interactable (No Action)

    public void RequestSetHandInteractable(bool interactable)
    {
        if (IsServer)
        {
            SetHandInteractableClientRpc(interactable, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { Game.CurrentOwnerClientTurnID.Value } } });
            return;
        }
        SetHandInteractable(interactable);
    }
    [ClientRpc] private void SetHandInteractableClientRpc(bool interactable, ClientRpcParams clientRpcParams) => SetHandInteractable(interactable);


    #endregion

    #region Hand Interactable (With Action)
    public void RequestSetHandInteractable(bool interactable, TAction action)
    {
        if (IsServer)
        {
            SetHandInteractableClientRpc(interactable, action, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { Game.CurrentOwnerClientTurnID.Value } } });
            return;
        }
        SetHandInteractable(interactable, action);
    }
    [ClientRpc] private void SetHandInteractableClientRpc(bool interactable, TAction action, ClientRpcParams clientRpcParams) => SetHandInteractable(interactable, action);

    protected void SetHandInteractable(bool interactable, TAction? action = null)
    {
        foreach (ulong cardId in handCardIds)
        {
            PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardId);
            if (card) card.Interactable.SetInteractable(interactable);
        }

        if (interactable && action.HasValue) SubscribeHandTo(GetCardOnInteractEvent(action.Value));
        if (!interactable && action.HasValue) UnsubscribeHandFrom(GetCardOnInteractEvent(action.Value));
    }

    #endregion

    #region Subscribing Specific Cards

    protected void SubscribeCardTo(ulong cardId, EventHandler<InteractEventArgs> onInteract) 
    { 
        PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardId); 
        if (card) 
        { 
            card.Interactable.OnInteract += onInteract; 
            if (eventSubscriptionDictionary.TryGetValue(onInteract, out List<PlayingCard> cards)) 
            { 
                eventSubscriptionDictionary[onInteract].Add(card);
            } 
            else 
            { 
                eventSubscriptionDictionary.Add(onInteract, new List<PlayingCard>() { card }); 
            } 
        } 
    }

    protected void UnsubscribeCardFrom(ulong cardId, EventHandler<InteractEventArgs> onInteract)
    {
        PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardId);
        if (card)
        {
            card.Interactable.OnInteract -= onInteract;
            if (eventSubscriptionDictionary.TryGetValue(onInteract, out List<PlayingCard> cards))
            {
                eventSubscriptionDictionary[onInteract].Remove(card);
                if (cards.Count <= 0) eventSubscriptionDictionary.Remove(onInteract);
            }
        }
    }

    #endregion

    #region Subscribing Hand

    protected void SubscribeHandTo(EventHandler<InteractEventArgs> onInteract)
    {
        foreach (ulong cardId in handCardIds)
        {
            SubscribeCardTo(cardId, onInteract);
        }
    }
    protected void UnsubscribeHandFrom(EventHandler<InteractEventArgs> onInteract)
    {
        foreach (ulong cardId in handCardIds)
        {
            UnsubscribeCardFrom(cardId, onInteract);
        }
    }

    #endregion

    #endregion

    #region Card Handling (Server)

    public void AddCardToHand(PlayingCard card)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"Only the server can add {card.ToString()} to {GetName()}'s hand!");
            return;
        }

        hand.AddCard(card);
        handCardIds.Add(card.NetworkObjectId);
    }

    public void InsertCardToHand(PlayingCard card, int index)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"Only the server can insert {card.ToString()} to {GetName()}'s hand!");
            return;
        }

        hand.InsertCard(card, index);
        handCardIds.Insert(index, card.NetworkObjectId);
    }

    public bool RemoveCardFromHand(PlayingCard card)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"Only the server can remove {card.ToString()} from {GetName()}'s hand!");
            return false;
        }

        bool removed = hand.RemoveCard(card);
        if (removed) handCardIds.Remove(card.NetworkObjectId);

        return removed;
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

        return transform.rotation * Quaternion.Euler(dealingXRotation, angle, 0f);
    }

    #endregion

    #region Card Interaction Display

    /// <summary>
    /// Sets the interact display for the whole hand
    /// </summary>
    public void SetHandInteractDisplay(InteractDisplay display)
    {
        if (!IsServer) return;

        foreach (var card in Hand.Cards) card.Interactable.SetDisplay(display);
    }

    #endregion
}
