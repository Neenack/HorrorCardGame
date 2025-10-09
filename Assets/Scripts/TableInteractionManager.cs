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
    /// Sets the card interactable for all given players
    /// </summary>
    public void SetCardInteraction(PlayingCard card, bool interactable, IEnumerable<TPlayer> players, TAction? action = null)
    {
        SetCardInteraction(card, interactable, GetClientIDArrayFromPlayerList(players), action);
    }

    /// <summary>
    /// Sets the card interactable for the given player
    /// </summary>
    public void SetCardInteraction(PlayingCard card, bool interactable, TPlayer player, TAction? action = null)
    {
        SetCardInteraction(card, interactable, new ulong[] { player.LocalClientID }, action);
    }

    /// <summary>
    /// Sets the card interactable for all given clients
    /// </summary>
    public void SetCardInteraction(PlayingCard card, bool interactable, ulong[] clients = null, TAction? action = null)
    {
        if (!IsServer)
        {
            ConsoleLog.Instance.Log("Only server should call SetCardInteraction()");
            return;
        }

        //No clients, just reset the card
        if (clients == null || clients.Length == 0)
        {
            card.Interactable.ClearAllowedClients();
            card.Interactable.ResetDisplay();
            return;
        }

        //Sets the allowed clients
        if (interactable)
            card.Interactable.SetAllowedClients(clients);
        else
            card.Interactable.ClearAllowedClients();


        // Set display
        if (action.HasValue && interactable)
            card.Interactable.SetDisplay(GetInteractDisplay(action.Value));
        else
            card.Interactable.ResetDisplay();


        //Updates the card event subscriptions
        if (action.HasValue)
            SubscribeCardEventClientRpc(clients, card.NetworkObjectId, interactable, action.Value);
        else if (!interactable)
            UnsubscribeCardEventClientRpc(clients, card.NetworkObjectId);

        ConsoleLog.Instance.Log($"{card.ToString()} has been set interactable for [{string.Join(", ", clients)}]");
    }

    [ClientRpc]
    private void SubscribeCardEventClientRpc(ulong[] targetClients, ulong cardNetworkId, bool subscribe, TAction action)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        if (!targetClients.Contains(localClientId)) return;

        PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardNetworkId);
        TPlayer player = Game.GetPlayerFromClientID(localClientId);

        EventHandler<InteractEventArgs> handler = GetCardOnInteractEvent(action);

        if (subscribe)
            player.SubscribeCardTo(card, handler);
        else
            player.UnsubscribeCardFrom(card, handler);
    }

    [ClientRpc]
    private void UnsubscribeCardEventClientRpc(ulong[] targetClients, ulong cardNetworkId)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        if (!targetClients.Contains(localClientId)) return;

        PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardNetworkId);
        TPlayer player = Game.GetPlayerFromClientID(localClientId);

        player.UnsubscribeCard(card);
    }

    #endregion

    #region Set Hand Interactable

    /// <summary>
    /// Sets a list of cards interactable for all given clients
    /// </summary>
    public void SetHandInteraction(IReadOnlyCollection<PlayingCard> cards, bool interactable, ulong[] clients = null, TAction? action = null)
    {
        if (!IsServer)
        {
            ConsoleLog.Instance.Log("Only server should call RequestSetHandInteraction()");
            return;
        }

        foreach (var card in cards)
        {
            SetCardInteraction(card, interactable, clients, action);
        }
    }

    /// <summary>
    /// Sets a hand interactable for all given clients
    /// </summary>
    public void SetHandInteraction(PlayerHand hand, bool interactable, IEnumerable<TPlayer> players = null, TAction? action = null)
    {
        SetHandInteraction(hand.Cards, interactable, GetClientIDArrayFromPlayerList(players), action);
    }

    /// <summary>
    /// Sets a hand interactable for the given player
    /// </summary>
    public void SetHandInteraction(PlayerHand hand, bool interactable, TPlayer player, TAction? action = null)
    {
        SetHandInteraction(hand.Cards, interactable, new ulong[] { player.LocalClientID }, action);
    }

    /// <summary>
    /// Sets a list of cards interactable for the given player
    /// </summary>
    public void SetHandInteraction(IReadOnlyCollection<PlayingCard> cards, bool interactable, TPlayer player, TAction? action = null)
    {
        SetHandInteraction(cards, interactable, new ulong[] { player.LocalClientID }, action);
    }

    /// <summary>
    /// Sets a list of cards interactable for all given clients
    /// </summary>
    public void SetHandInteraction(IReadOnlyCollection<PlayingCard> cards, bool interactable, IEnumerable<TPlayer> players, TAction? action = null)
    {
        SetHandInteraction(cards, interactable, GetClientIDArrayFromPlayerList(players), action);
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