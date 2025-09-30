using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEditor.ShaderKeywordFilter;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.XR;
using static Interactable;

public class CambioPlayer : TablePlayer<CambioPlayer, CambioActionData, CambioPlayerAI>
{
    public NetworkVariable<bool> hasPlayedLastTurn = new NetworkVariable<bool>(false);

    [Header("Cambio Settings")]
    [SerializeField] private Interactable callCambioButton;
    [SerializeField] private Interactable skipAbilityButton;
    [SerializeField] private TextMeshProUGUI calledCambioText;
    [SerializeField] private TextMeshPro scoreText;
    [SerializeField] private float rowSpacing = 2.5f;

    private HashSet<PlayingCard> seenCards = new HashSet<PlayingCard>();
    public HashSet<PlayingCard> SeenCards => seenCards;


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        calledCambioText?.gameObject.SetActive(false);
        callCambioButton?.gameObject.SetActive(false);
        skipAbilityButton?.gameObject.SetActive(false);
        scoreText?.gameObject.SetActive(false);
    }

    #region Turn Logic

    protected override void Game_OnGameStarted()
    {
        base.Game_OnGameStarted();

        calledCambioText?.gameObject.SetActive(false);
        callCambioButton?.gameObject.SetActive(false);
        skipAbilityButton?.gameObject.SetActive(false);
        scoreText?.gameObject.SetActive(false);

        seenCards.Clear();
    }
    protected override void Game_OnGameEnded()
    {
        base.Game_OnGameEnded();

        calledCambioText?.gameObject.SetActive(false);
        callCambioButton?.gameObject.SetActive(false);
        skipAbilityButton?.gameObject.SetActive(false);
        scoreText?.gameObject.SetActive(false);
    }

    protected override void Game_OnActionExecuted()
    {
        base.Game_OnActionExecuted();

        callCambioButton?.gameObject.SetActive(false);
        skipAbilityButton?.gameObject.SetActive(false);
    }

    protected override void StartPlayerTurn()
    {
        base.StartPlayerTurn();

        callCambioButton.gameObject.SetActive(true);

        EnableStartTurnInteraction();
    }

    protected override void EndPlayerTurn()
    {
        base.EndPlayerTurn();

        callCambioButton.gameObject.SetActive(false);
        skipAbilityButton.gameObject.SetActive(false);

        DisableStartTurnInteraction();
    }

    public override bool IsPlaying() => !hasPlayedLastTurn.Value;

    #endregion

    #region Interaction

    #region Interact Event Switch
    protected override EventHandler<InteractEventArgs> GetCardOnInteractEvent(CambioActionData data)
    {
        switch (data.Type)
        {
            case CambioActionType.Draw:
                return Card_OnInteract_AfterDraw;

            case CambioActionType.RevealCard:
                return Card_OnInteract_RevealCard;
            case CambioActionType.SwapHand:
                return Card_OnInteract_SwapHand;

            case CambioActionType.SelectCard:
                return Card_OnInteract_ChooseCard;
            case CambioActionType.CompareCards:
                return Card_OnInteract_CompareCards;

            case CambioActionType.Stack:
                return Card_OnInteract_Stack;
            case CambioActionType.GiveCard:
                return Card_OnInteract_CorrectStack;

        }

        return null;
    }

    #endregion

    #region Start Of Turn Interaction

    private void CallCambioButton_OnInteract(object sender, Interactable.InteractEventArgs e)
    {
        DisableStartTurnInteraction();

        Game.TryExecuteAction(e.playerID, new CambioActionData(CambioActionType.CallCambio, true, PlayerId));
    }

    //Called by the server
    public void CallCambio()
    {
        hasPlayedLastTurn.Value = true;

        NotifyCallCambioClientRpc();
    }

    [ClientRpc]
    private void NotifyCallCambioClientRpc()
    {
        calledCambioText.gameObject.SetActive(true);
    }

    private void InteractableDeck_OnInteract(object sender, Interactable.InteractEventArgs e)
    {
        DisableStartTurnInteraction();

        Game.TryExecuteAction(e.playerID, new CambioActionData(CambioActionType.Draw, false, PlayerId));
    }

    private void EnableStartTurnInteraction()
    {
        callCambioButton.OnInteract += CallCambioButton_OnInteract;
        Game.InteractableDeck.OnInteract += InteractableDeck_OnInteract;
        Game.InteractableDeck.SetInteractable(true);
    }

    private void DisableStartTurnInteraction()
    {
        callCambioButton.OnInteract -= CallCambioButton_OnInteract;
        Game.InteractableDeck.OnInteract -= InteractableDeck_OnInteract;
        Game.InteractableDeck.SetInteractable(false);
        callCambioButton.gameObject.SetActive(false);
    }

    #endregion

    #region After Card Draw Interaction

    private void Card_OnInteract_AfterDraw(object sender, InteractEventArgs e)
    {
        PlayingCard drawnCard = PlayingCard.GetPlayingCardFromNetworkID(Game.DrawnCardID.Value);
        drawnCard.Interactable.SetInteractable(false);
        UnsubscribeCardFrom(Game.DrawnCardID.Value, Card_OnInteract_AfterDraw);

        PlayingCard chosenCard = (sender as Interactable).GetComponent<PlayingCard>();

        if (chosenCard == null)
        {
            Debug.LogWarning("Could not find chosen card");
            return;
        }

        if (HandCardIDs.Contains(chosenCard.NetworkObjectId)) //Chose one of your own cards so trade
        {
            Game.TryExecuteAction(e.playerID, new CambioActionData(CambioActionType.TradeCard, true, PlayerId, chosenCard.NetworkObjectId, PlayerId, Game.DrawnCardID.Value));
        }
        else //Chose the drawn card so discard
        {
            Game.TryExecuteAction(e.playerID, new CambioActionData(CambioActionType.Discard, true, PlayerId, Game.DrawnCardID.Value));
        }
    }

    #endregion

    #region Ability Interaction

    #region Start Ability

    /// <summary>
    /// Requests for the player to enable interactions for starting an ability
    /// </summary>
    public void RequestEnableAbilityStartedInteraction()
    {
        if (IsServer)
        {
            EnableAbilityStartedInteractionClientRpc(PlayerData.OwnerClientId);
            return;
        }
        EnableAbilityStartedInteraction();
    }

    [ClientRpc]
    private void EnableAbilityStartedInteractionClientRpc(ulong localClientId)
    {
        if (PlayerData == null || localClientId != NetworkManager.Singleton.LocalClientId) return;

        EnableAbilityStartedInteraction();
    }

    private void EnableAbilityStartedInteraction()
    {
        skipAbilityButton.gameObject.SetActive(true);
        skipAbilityButton.SetInteractable(true);
        skipAbilityButton.OnInteract -= SkipAbilityButton_OnInteract;
        skipAbilityButton.OnInteract += SkipAbilityButton_OnInteract;
    }

    private void DisableAbilityStartedInteraction()
    {
        skipAbilityButton.SetInteractable(false);
        skipAbilityButton.gameObject.SetActive(false);
        skipAbilityButton.OnInteract -= SkipAbilityButton_OnInteract;
    }

    private void SkipAbilityButton_OnInteract(object sender, InteractEventArgs e)
    {
        DisableAbilityStartedInteraction();

        Game.TryExecuteAction(e.playerID, new CambioActionData(CambioActionType.None, true, PlayerId));
    }

    #endregion

    #region Ability Events

    #region Reveal Card

    private void Card_OnInteract_RevealCard(object sender, InteractEventArgs e)
    {
        DisableAbilityStartedInteraction();

        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;
        ulong playerWithCardId = Game.GetPlayerWithCard(cardNetworkId).PlayerId;

        Game.TryExecuteAction(e.playerID, new CambioActionData(CambioActionType.RevealCard, true, Game.CurrentPlayerTurnID.Value, 0, playerWithCardId, cardNetworkId));
    }

    #endregion

    #region Swap Hand

    private void Card_OnInteract_SwapHand(object sender, InteractEventArgs e)
    {
        DisableAbilityStartedInteraction();

        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;
        ulong playerWithCardId = Game.GetPlayerWithCard(cardNetworkId).PlayerId;

        Game.TryExecuteAction(e.playerID, new CambioActionData(CambioActionType.SwapHand, true, Game.CurrentPlayerTurnID.Value, 0, playerWithCardId, 0));
    }

    #endregion

    #region Swap Cards

    private void Card_OnInteract_ChooseCard(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;
        Game.TryExecuteAction(e.playerID, new CambioActionData(CambioActionType.SelectCard, false, Game.CurrentPlayerTurnID.Value, 0, 0, cardNetworkId));
    }

    #endregion

    #region Compare Cards

    private void Card_OnInteract_CompareCards(object sender, InteractEventArgs e)
    {
        DisableAbilityStartedInteraction();

        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        Game.TryExecuteAction(e.playerID, new CambioActionData(CambioActionType.ChooseCard, true, Game.CurrentPlayerTurnID.Value, 0, 0, cardNetworkId));
    }

    #endregion

    #endregion

    #endregion

    #region Stacking

    public void RequestSetStacking(bool interactable)
    {
        if (IsServer)
        {
            SetStackingClientRpc(interactable);
            return;
        }

        SetStacking(interactable);
    }
    [ClientRpc] private void SetStackingClientRpc(bool interactable) => SetStacking(interactable);

    private void SetStacking(bool interactable)
    {
        foreach (var player in Game.Players)
        {
            foreach (var cardId in player.HandCardIDs)
            {
                //Debug.Log($"Card with ID:{cardId} has been enabled for stacking for player with ID: {player.PlayerId}");
                RequestSetCardInteractable(cardId, interactable, new CambioActionData(CambioActionType.Stack, false, player.PlayerId));
            }
        }
    }

    private void Card_OnInteract_Stack(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        PlayerData data = PlayerManager.Instance.GetPlayerDataById(e.playerID);
        CambioPlayer playerWithStacked = Game.GetPlayerFromData(data);

        Game.TryExecuteAction(playerWithStacked.PlayerId, new CambioActionData(CambioActionType.Stack, false, playerWithStacked.PlayerId, 0, 0, cardNetworkId));
    }

    private void Card_OnInteract_CorrectStack(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        Game.TryExecuteAction(PlayerId, new CambioActionData(CambioActionType.GiveCard, false, PlayerId, 0, 0, cardNetworkId));
    }

    #endregion

    #endregion

    #region Card Values

    /// <summary>
    /// Gets the value of the playing card
    /// </summary>
    public static new int GetCardValue(PlayingCard card)
    {
        if (card == null) return 13;

        int value = card.GetValue(false);
        if (value == 13 && (card.Suit == Suit.Spades || card.Suit == Suit.Clubs)) return -1;

        return value;
    }

    /// <summary>
    /// Gets the total player score for their hand
    /// </summary>
    public override int GetScore()
    {
        int total = 0;
        foreach (var card in Hand.Cards) total += GetCardValue(card);
        return total;
    }

    #endregion

    #region Card Position (Server Only)

    /// <summary>
    /// Gets the position of the playing card for the player
    /// </summary>
    public override Vector3 GetCardPosition(int cardIndex, int totalCards)
    {
        // Grid arrangement: 2 rows (bottom + top)
        int column = cardIndex / 2;   // every 2 cards start a new column
        int row = cardIndex % 2;      // 0 = bottom row, 1 = top row

        // Base position (centered at player transform, respecting table height)
        Vector3 basePos = transform.position + new Vector3(0, yOffset, 0);

        int totalColumns = Mathf.CeilToInt(totalCards / 2f);

        // Offset so that cards are centered around transform.position
        float centerOffset = (totalColumns - 1) / 2f;

        // Offsets
        Vector3 sideOffset = transform.right * ((column - centerOffset) * cardSpacing.x);
        Vector3 rowOffset = -transform.forward * (row * rowSpacing);

        return basePos + sideOffset + rowOffset;
    }

    #endregion

    #region Memory

    protected override void OnHandCardIdsChanged(NetworkListEvent<ulong> changeEvent)
    {
        base.OnHandCardIdsChanged(changeEvent);

        //Remove all cards which are no longer in the hand
        seenCards = new HashSet<PlayingCard>(seenCards.Where(x => Hand.Cards.Contains(x)));

        //Debug.Log($"Player {PlayerId} has seen {seenCards.Count} cards");
    }

    /// <summary>
    /// Adds a card to memory
    /// </summary>
    public void TryAddSeenCard(PlayingCard card)
    {
        if (!Hand.Cards.Contains(card) || HasSeenCard(card)) return;

        seenCards.Add(card);

        //Debug.Log($"Player {PlayerId} has seen {seenCards.Count} cards");
    }

    public bool HasSeenCard(PlayingCard card) => seenCards.Contains(card);

    /// <summary>
    /// Removes a card from memory
    /// </summary>
    public void RemoveSeenCard(PlayingCard card) => seenCards.Remove(card);

    #endregion

    #region AI

    protected override CambioPlayerAI CreateAI() => playerAI = new CambioPlayerAI(this);

    #endregion
}
