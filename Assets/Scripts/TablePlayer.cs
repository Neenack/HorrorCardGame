using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public abstract class TablePlayer<TAction> : NetworkBehaviour where TAction : class
{
    private PlayerData playerData = null;
    private ICardGame<TAction> game;

    //Server hand
    private PlayerHand hand;
    //Client hand
    public NetworkList<ulong> handCardIds = new NetworkList<ulong>();


    private PlayerAI<TAction> playerAI = null;

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
    protected bool isTurn => game?.CurrentTurnID.Value == OwnerClientId;
    public ICardGame<TAction> Game => game;
    public PlayerHand Hand => hand;
    public PlayerAI<TAction> PlayerAI => playerAI;
    public Transform PlayerStandTransform => playerStandTransform;

    #endregion

    #region Abstract Functions

    public abstract int GetCardValue(PlayingCard card);
    public abstract bool IsPlaying();
    public abstract int GetScore();

    #endregion

    public override void OnNetworkSpawn()
    {
        if (hand == null)
        {
            hand = new PlayerHand();
            hand.OnHandUpdated += Hand_OnHandUpdated;
        }

        handCardIds.OnListChanged += OnHandCardIdsChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        //Unsubscribes from events
        if (hand != null) hand.OnHandUpdated -= Hand_OnHandUpdated;
        if (game != null) game.CurrentTurnID.OnValueChanged -= OnTurnChanged;
        if (handCardIds != null) handCardIds.OnListChanged -= OnHandCardIdsChanged;
    }


    /// <summary>
    /// Sets the player data for the table player
    /// </summary>
    public void SetPlayer(PlayerData data) => playerData = data;



    /// <summary>
    /// Called when the turn changes for the game
    /// </summary>
    private void OnTurnChanged(ulong oldValue, ulong newValue)
    {
        if (IsAI) return;

        if (NetworkManager.Singleton.LocalClientId != newValue) return;

        Debug.Log($"Turn Changed from {oldValue} to {newValue}");
        Debug.Log($"Local ID: {NetworkManager.Singleton.LocalClientId}    New Turn ID: {newValue}");

        if (oldValue == playerData.OwnerClientId) EndPlayerTurn();
        if (newValue == playerData.OwnerClientId) StartPlayerTurn();
    }



    /// <summary>
    /// Called when the players hand is updated
    /// </summary>
    private void OnHandCardIdsChanged(NetworkListEvent<ulong> changeEvent)
    {
        if (hand == null)
        {
            hand = new PlayerHand();
            hand.OnHandUpdated += Hand_OnHandUpdated;
        }

        hand.ClearHand();
        foreach (ulong cardId in handCardIds)
        {
            if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardId, out NetworkObject cardObj))
            {
                hand.AddCard(cardObj.GetComponent<PlayingCard>());
            }
        }
    }


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

    public void RemoveCardFromHand(PlayingCard card)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"Only the server can remove {card.ToString()} from {GetName()}'s hand!");
            return;
        }

        bool removed = hand.RemoveCard(card);
        if (removed) handCardIds.Remove(card.NetworkObjectId);
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

    public virtual void StartPlayerTurn()
    {
        Debug.Log($"{GetName()} Its your turn!");
    }

    public virtual void EndPlayerTurn()
    {
        Debug.Log($"{GetName()} turn has ended");
    }

    public string GetName()
    {
        if (playerData) return playerData.GetName();
        return "[AI] " + gameObject.name;
    }
}
