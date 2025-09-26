using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR;

public class CambioPlayer : TablePlayer<CambioAction>
{
    private NetworkVariable<bool> hasPlayedLastTurn = new NetworkVariable<bool>(false);

    [Header("Cambio Settings")]
    [SerializeField] private Interactable callCambioButton;
    [SerializeField] private Interactable skipAbilityButton;
    [SerializeField] private TextMeshProUGUI calledCambioText;
    [SerializeField] private TextMeshPro scoreText;
    [SerializeField] private float rowSpacing = 2.5f;


    private HashSet<PlayingCard> seenCards = new HashSet<PlayingCard>();
    public HashSet<PlayingCard> SeenCards => seenCards;

    #region Public Accessors

    public bool HasPlayedFinalTurn => hasPlayedLastTurn.Value;

    #endregion


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
    }
    protected override void Game_OnGameEnded()
    {
        base.Game_OnGameEnded();

        calledCambioText?.gameObject.SetActive(false);
        callCambioButton?.gameObject.SetActive(false);
        skipAbilityButton?.gameObject.SetActive(false);
        scoreText?.gameObject.SetActive(false);
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

        DisableStartTurnInteraction();

        EndTurnServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void EndTurnServerRpc()
    {
        hasPlayedLastTurn.Value = Game.Players.Any(p => !p.IsPlaying());
    }

    public override bool IsPlaying() => !hasPlayedLastTurn.Value;

    #endregion

    #region Interaction

    #region Start Of Turn Interaction
    private void CallCambioButton_OnInteract(object sender, Interactable.InteractEventArgs e)
    {
        DisableStartTurnInteraction();

        Game.TryExecuteAction(OwnerClientId, new CambioAction(CambioActionType.CallCambio, true, this));
    }

    private void InteractableDeck_OnInteract(object sender, Interactable.InteractEventArgs e)
    {
        DisableStartTurnInteraction();

        Game.TryExecuteAction(OwnerClientId, new CambioAction(CambioActionType.Draw, false, this));
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

    public void EnableCardDrawnInteraction()
    {
        PlayingCard drawnCard = Game.DrawnCard;
        drawnCard.Interactable.SetInteractable(true);
        drawnCard.Interactable.OnInteract += DrawnCard_OnInteract;

        foreach (var cardId in handCardIds)
        {
            PlayingCard card = GetPlayingCardFromID(cardId);
            card.Interactable.OnInteract += HandCard_OnInteract;
        }
    }

    private void DisableCardDrawnInteration()
    {
        PlayingCard drawnCard = Game.DrawnCard;
        drawnCard.Interactable.SetInteractable(false);
        drawnCard.Interactable.OnInteract -= DrawnCard_OnInteract;

        foreach (var cardId in handCardIds)
        {
            PlayingCard card = GetPlayingCardFromID(cardId);
            card.Interactable.OnInteract -= HandCard_OnInteract;
        }
    }

    private void DrawnCard_OnInteract(object sender, Interactable.InteractEventArgs e)
    {
        if (e.playerID != OwnerClientId) return;

        DisableCardDrawnInteration();

        Game.TryExecuteAction(OwnerClientId, new CambioAction(CambioActionType.Discard, true, this, Game.DrawnCard));
    }

    private void HandCard_OnInteract(object sender, Interactable.InteractEventArgs e)
    {
        if (e.playerID != OwnerClientId) return;

        DisableCardDrawnInteration();

        PlayingCard cardChosen = (sender as Interactable).GetComponent<PlayingCard>();

        Game.TryExecuteAction(OwnerClientId, new CambioAction(CambioActionType.TradeCard, true, this, new SwapInfo(Game.DrawnCard, cardChosen)));
    }

    #endregion

    #endregion

    #region Server Actions

    //SERVER CALLED CAMBIO
    public void CallCambio()
    {
        NotifyCallCambioClientRpc();
    }

    #endregion

    #region Client Actions

    [ClientRpc]
    private void NotifyCallCambioClientRpc()
    {
        calledCambioText.gameObject.SetActive(true);
    }

    #endregion

    #region Card Values

    /// <summary>
    /// Gets the value of the playing card
    /// </summary>
    public override int GetCardValue(PlayingCard card)
    {
        if (card == null) return 13;

        int value = card.GetValue(false);
        if (value == 13 && (card.Suit == Suit.Spades || card.Suit == Suit.Clubs)) return -1;

        return value;
    }

    public override int GetScore()
    {
        int total = 0;
        foreach (var card in Hand.Cards) total += GetCardValue(card);
        return total;
    }

    #endregion

    #region Card Position

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

    private void Hand_OnRemoveCard(PlayingCard card) => RemoveSeenCard(card);

    /// <summary>
    /// Adds a card to memory
    /// </summary>
    public void TryAddSeenCard(PlayingCard card)
    {
        if (!Hand.Cards.Contains(card) || HasSeenCard(card)) return;

        seenCards.Add(card);
    }

    public bool HasSeenCard(PlayingCard card) => seenCards.Contains(card);

    /// <summary>
    /// Removes a card from memory
    /// </summary>
    public void RemoveSeenCard(PlayingCard card)
    {
        seenCards.Remove(card);
    }

    #endregion
}
