using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TMPro;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Interactable;

public class CambioPlayer : TablePlayer<CambioPlayer, CambioActionData, CambioPlayerAI>
{
    public NetworkVariable<bool> hasPlayedLastTurn = new NetworkVariable<bool>(false);

    [Header("Cambio Settings")]
    [SerializeField] private Interactable callCambioButton;
    [SerializeField] private Interactable skipAbilityButton;
    [SerializeField] private TextMeshProUGUI calledCambioText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private float rowSpacing = 2.5f;

    private HashSet<PlayingCard> seenCards = new HashSet<PlayingCard>();
    public HashSet<PlayingCard> SeenCards => seenCards;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        HideAllText();
    }

    /// <summary>
    /// Returns if the player is still playing
    /// </summary>
    public override bool IsPlaying() => !hasPlayedLastTurn.Value;

    public void ShowScore(int score)
    {
        scoreText.text = score.ToString();
        scoreText.gameObject.SetActive(true);
    }

    private void HideAllText()
    {
        calledCambioText?.gameObject.SetActive(false);
        scoreText?.gameObject.SetActive(false);
    }

    //Called by the server
    public void CallCambio()
    {
        hasPlayedLastTurn.Value = true;

        NotifyCallCambioClientRpc();
    }

    [ClientRpc] private void NotifyCallCambioClientRpc() => calledCambioText.gameObject.SetActive(true);


    [ServerRpc(RequireOwnership = false)]
    private void RequestSetLastTurnServerRpc(bool lastTurn)
    {
        hasPlayedLastTurn.Value = lastTurn;
    }


    #region Game Subscriptions

    protected override void SubscribeToGame()
    {
        base.SubscribeToGame();

        if (Game is ICambioGame cambioGame)
        {
            cambioGame.OnAbilityStarted += CambioGame_OnAbilityStarted;
            cambioGame.IsStacking.OnValueChanged += OnStackingChanged;
        }

    }

    protected override void UnsubscribeFromGame()
    {
        base.UnsubscribeFromGame();

        if (Game is ICambioGame cambioGame)
        {
            cambioGame.OnAbilityStarted -= CambioGame_OnAbilityStarted;
            cambioGame.IsStacking.OnValueChanged -= OnStackingChanged;
        }

    }

    private void CambioGame_OnAbilityStarted()
    {
        if (isTurn) //CARD ADDED TO PILE AND IT THE PLAYERS TURN
        {
            PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(Game.PileCardID.Value);
            int value = GetCardValue(card);

            if (value >= 6 && value != 13) EnableSkipAbilityButton();
        }
    }
    private void OnStackingChanged(bool oldValue, bool newValue)
    {
        
    }

    protected override void Game_OnGameStarted()
    {
        base.Game_OnGameStarted();

        if (IsServer)
        {
            if (!IsAI)
            {
                callCambioButton.SetAllowedClients(PlayerData.OwnerClientId);
                skipAbilityButton.SetAllowedClients(PlayerData.OwnerClientId);
            }

            hasPlayedLastTurn.Value = false;
        }

        HideAllText();
        HideAllButtons();
    }

    protected override void Game_OnGameEnded()
    {
        base.Game_OnGameEnded();

        HideAllText();
        HideAllButtons();
    }

    protected override void Game_OnActionExecuted()
    {
        base.Game_OnActionExecuted();

        HideAllButtons();
    }

    protected override void Game_OnGameReset()
    {
        base.Game_OnGameReset();

        HideAllText();
        HideAllButtons();
    }

    protected override void OnTurnStarted()
    {
        base.OnTurnStarted();

        if (!IsAI) EnableStartTurnInteraction();

        if (hasPlayedLastTurn.Value == false)
        {
            bool isLastTurn = Game.Players.Any(p => !p.IsPlaying());
            if (IsServer) hasPlayedLastTurn.Value = isLastTurn;
            else RequestSetLastTurnServerRpc(isLastTurn);
        }
    }

    protected override void OnTurnEnded()
    {
        base.OnTurnEnded();

        if (IsAI) return;

        HideAllButtons();
        DisableCallCambioInteraction();
        DisableSkipAbilityInteraction();
    }

    #endregion

    #region Interaction

    private void InteractableDeck_OnInteract(object sender, Interactable.InteractEventArgs e)
    {
        DisableCallCambioInteraction();

        Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.Draw, false, TablePlayerID));
    }
    private void CallCambioButton_OnInteract(object sender, Interactable.InteractEventArgs e)
    {
        DisableCallCambioInteraction();

        Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.CallCambio, true, TablePlayerID));
    }

    private void SkipAbilityButton_OnInteract(object sender, InteractEventArgs e)
    {
        DisableSkipAbilityInteraction();

        Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.None, true, TablePlayerID));
    }

    #endregion

    #region Buttons

    public void HideAllButtons()
    {
        callCambioButton?.gameObject.SetActive(false);
        skipAbilityButton?.gameObject.SetActive(false);
    }

    private void EnableStartTurnInteraction()
    {
        callCambioButton.gameObject.SetActive(true);
        callCambioButton.OnInteract -= CallCambioButton_OnInteract;
        callCambioButton.OnInteract += CallCambioButton_OnInteract;

        Game.InteractableDeck.OnInteract -= InteractableDeck_OnInteract;
        Game.InteractableDeck.OnInteract += InteractableDeck_OnInteract;
    }

    private void DisableCallCambioInteraction()
    {
        callCambioButton.OnInteract -= CallCambioButton_OnInteract;
        Game.InteractableDeck.OnInteract -= InteractableDeck_OnInteract;
        callCambioButton.gameObject.SetActive(false);
    }

    private void EnableSkipAbilityButton()
    {
        skipAbilityButton.gameObject.SetActive(true);
        skipAbilityButton.OnInteract -= SkipAbilityButton_OnInteract;
        skipAbilityButton.OnInteract += SkipAbilityButton_OnInteract;
    }

    private void DisableSkipAbilityInteraction()
    {
        skipAbilityButton.OnInteract -= SkipAbilityButton_OnInteract;
        skipAbilityButton.gameObject.SetActive(false);
    }

    #endregion

    #region Card Values

    /// <summary>
    /// Gets the value of the playing card
    /// </summary>
    public static int GetCardValue(PlayingCard card)
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
    }

    /// <summary>
    /// Adds a card to memory
    /// </summary>
    public void TryAddSeenCard(PlayingCard card)
    {
        if (!Hand.Cards.Contains(card) || HasSeenCard(card)) return;

        seenCards.Add(card);
    }

    /// <summary>
    /// Returns if the player already knows about this card
    /// </summary>
    public bool HasSeenCard(PlayingCard card) => seenCards.Contains(card);

    /// <summary>
    /// Removes a card from memory
    /// </summary>
    public void RemoveSeenCard(PlayingCard card) => seenCards.Remove(card);

    #endregion

    #region AI

    /// <summary>
    /// Initialises the Player AI
    /// </summary>
    protected override CambioPlayerAI CreateAI() => playerAI = new CambioPlayerAI(this, GamemodeSettingsManager.Instance.AIDifficulty);

    #endregion
}
