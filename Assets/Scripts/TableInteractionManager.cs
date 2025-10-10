using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using static Interactable;


public abstract class TableInteractionManager<TPlayer, TAction, TAI> : NetworkBehaviour
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{

    private ICardGame<TPlayer, TAction, TAI> game;
    protected ICardGame<TPlayer, TAction, TAI> Game => game;



    protected InteractDisplay nullDisplay = new InteractDisplay("N/A", true, "Error", "Display not found :)");

    private ulong[] GetClientIDArrayFromPlayerList(IEnumerable<TPlayer> players)
    {
        return players != null && players.Any() ? players.Select(p => p.LocalClientID).ToArray() : null;
    }

    private void Awake()
    {
        game = GetComponent<CardGame<TPlayer, TAction, TAI>>();

        game.OnAnyActionExecuted += Game_OnAnyActionExecuted;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();

        game.OnAnyActionExecuted -= Game_OnAnyActionExecuted;
    }


    #region Abstract Functions

    protected abstract EventHandler<InteractEventArgs> GetCardOnInteractEvent(TAction action);
    protected abstract InteractDisplay GetInteractDisplay(TAction action);

    #endregion

    #region Set Card Interactable

    /// <summary>
    /// Sets the card interactable for the given player
    /// </summary>
    public void SetCardInteraction(PlayingCard card, bool interactable, TPlayer player = null, TAction? action = null)
    {
        SetCardInteractions(new PlayingCard[] { card }, interactable, player == null ? null : new ulong[] { player.LocalClientID }, action);
    }

    /// <summary>
    /// Sets the card interactable for all given players
    /// </summary>
    public void SetCardInteraction(PlayingCard card, bool interactable, IEnumerable<TPlayer> players, TAction? action = null)
    {
        SetCardInteractions(new PlayingCard[] { card }, interactable, GetClientIDArrayFromPlayerList(players), action);
    }

    /// <summary>
    /// Sets a hand interactable for the given player
    /// </summary>
    public void SetHandInteraction(PlayerHand hand, bool interactable, TPlayer player, TAction? action = null)
    {
        SetCardInteractions(hand.Cards, interactable, new ulong[] { player.LocalClientID }, action);
    }

    /// <summary>
    /// Sets a hand interactable for all given players
    /// </summary>
    public void SetHandInteraction(PlayerHand hand, bool interactable, IEnumerable<TPlayer> players = null, TAction? action = null)
    {
        SetCardInteractions(hand.Cards, interactable, GetClientIDArrayFromPlayerList(players), action);
    }

    /// <summary>
    /// Sets a list of cards interactable for the given player
    /// </summary>
    public void SetCardInteractions(IReadOnlyCollection<PlayingCard> cards, bool interactable, TPlayer player, TAction? action = null)
    {
        SetCardInteractions(cards, interactable, new ulong[] { player.LocalClientID }, action);
    }

    /// <summary>
    /// Sets a list of cards interactable for all given players
    /// </summary>
    public void SetCardInteractions(IReadOnlyCollection<PlayingCard> cards, bool interactable, IEnumerable<TPlayer> players, TAction? action = null)
    {
        SetCardInteractions(cards, interactable, GetClientIDArrayFromPlayerList(players), action);
    }

    /// <summary>
    /// Sets the list of cards interactable for all given clients
    /// </summary>
    public void SetCardInteractions(IReadOnlyCollection<PlayingCard> cards, bool interactable, ulong[] clients = null, TAction? action = null)
    {
        if (!IsServer)
        {
            UnityEngine.Debug.Log("Only server should call SetCardInteraction()");
            return;
        }

        foreach (var card in cards)
        {
            if (clients == null || clients.Length == 0)
            {
                card.Interactable.ClearAllowedClients();
                card.Interactable.ResetDisplay();
                continue;
            }

            if (interactable)
                card.Interactable.SetAllowedClients(clients);
            else
                card.Interactable.ClearAllowedClients();

            if (action.HasValue && interactable)
                card.Interactable.SetDisplay(GetInteractDisplay(action.Value));
            else
                card.Interactable.ResetDisplay();
        }

        if (clients != null && clients.Length > 0 && cards.Count > 0)
        {
            ulong[] cardIds = cards.Select(c => c.NetworkObjectId).ToArray();

            if (action.HasValue)
                SubscribeCardsEventClientRpc(clients, cardIds, interactable, action.Value);
            else if (!interactable)
                UnsubscribeCardsEventClientRpc(clients, cardIds);
        }
    }

    [ClientRpc]
    private void SubscribeCardsEventClientRpc(ulong[] targetClients, ulong[] cardNetworkIds, bool subscribe, TAction action)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        if (!targetClients.Contains(localClientId)) return;

        TPlayer player = Game.GetPlayerFromClientID(localClientId);
        EventHandler<InteractEventArgs> handler = GetCardOnInteractEvent(action);

        foreach (ulong cardNetworkId in cardNetworkIds)
        {
            PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardNetworkId);

            if (subscribe)
                player.SubscribeCardTo(card, handler);
            else
                player.UnsubscribeCardFrom(card, handler);
        }
    }

    [ClientRpc]
    private void UnsubscribeCardsEventClientRpc(ulong[] targetClients, ulong[] cardNetworkIds)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        if (!targetClients.Contains(localClientId)) return;

        foreach (ulong cardNetworkId in cardNetworkIds)
        {
            PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardNetworkId);
            TPlayer player = Game.GetPlayerFromClientID(localClientId);

            player.UnsubscribeCard(card);
        }
    }

    #endregion


    #region Reset Interactions

    private void Game_OnAnyActionExecuted()
    {
        ResetAllInteractions();
    }

    public void ResetAllInteractions()
    {
        if (!IsServer) return;

        foreach (var player in Game.Players)
        {
            foreach (var card in player.Hand.Cards)
            {
                card.Interactable.ClearAllowedClients();
                card.Interactable.ResetDisplay();
            }
        }
    }

    #endregion

    #region Interaction Display

    /// <summary>
    /// Sets the interact display for the given card
    /// </summary>
    protected void SetCardInteractDisplay(PlayingCard card, InteractDisplay display)
    {
        card.Interactable.SetDisplay(display);
    }

    /// <summary>
    /// Sets the interact display for the whole player hand
    /// </summary>
    protected void SetHandInteractDisplay(TPlayer player, InteractDisplay display)
    {
        foreach (var card in player.Hand.Cards) card.Interactable.SetDisplay(display);
    }

    #endregion
}