using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine.XR;
using static Interactable;
using static UnityEngine.Rendering.DebugUI;


public abstract class TableInteractionManager<TPlayer, TAction, TAI> : NetworkBehaviour
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{

    private ICardGame<TPlayer, TAction, TAI> game;
    protected ICardGame<TPlayer, TAction, TAI> Game => game;



    protected InteractDisplay nullDisplay = new InteractDisplay("N/A", true, "Error", "Display not found :)");


    #region Abstract Functions

    protected abstract EventHandler<InteractEventArgs> GetCardOnInteractEvent(TAction action);
    protected abstract InteractDisplay GetInteractDisplay(TAction action);

    #endregion


    private void Awake()
    {
        game = GetComponent<CardGame<TPlayer, TAction, TAI>>();
    }

    #region Set Card Interactable

    /// <summary>
    /// Sets the card interactable and action for all current players
    /// </summary>
    public void RequestSetCardInteraction(ulong cardNetworkID, bool interactable, TAction? action = null)
    {
        if (!IsServer)
        {
            ConsoleLog.Instance.Log("Only server should call RequestSetCardInteraction()");
            return;
        }

        foreach (var player in game.Players)
        {
            if (player.IsAI) continue;

            RequestSetCardInteraction(player.SendParams, cardNetworkID, interactable, action);
        }
    }

    /// <summary>
    /// Sets the card interactable and action for the given player
    /// </summary>
    public void RequestSetCardInteraction(TablePlayerSendParams playerSendParams, ulong cardNetworkID, bool interactable, TAction? action = null)
    {
        if (IsServer)
        {
            if (interactable && action.HasValue)
            {
                PlayingCard card = PlayingCard.GetPlayingCardFromNetworkID(cardNetworkID);
                SetCardInteractDisplay(card, GetInteractDisplay(action.Value));
            }

            ClientRpcParams clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { playerSendParams.LocalClientID }
                }
            };


            // Directly send RPC to target client
            if (action.HasValue) SetCardInteractionClientRpc(playerSendParams, cardNetworkID, interactable, action.Value, clientRpcParams);
            else SetCardInteractionClientRpc(playerSendParams, cardNetworkID, interactable, clientRpcParams);
        }
        else
        {
            // Clients shouldn't call this directly, but just in case
            ConsoleLog.Instance.Log("Only server should call RequestSetCardInteraction()");
        }
    }

    [ClientRpc]
    private void SetCardInteractionClientRpc(TablePlayerSendParams playerSendParams, ulong cardNetworkID, bool interactable, TAction action, ClientRpcParams rpcParams = default)
    {
        // Only execute on correct client
        if (NetworkManager.Singleton.LocalClientId != playerSendParams.LocalClientID)
            return;

        SetCardInteraction(playerSendParams, cardNetworkID, interactable, action);
    }

    [ClientRpc]
    private void SetCardInteractionClientRpc(TablePlayerSendParams playerSendParams, ulong cardNetworkID, bool interactable, ClientRpcParams rpcParams = default)
    {
        // Only execute on correct client
        if (NetworkManager.Singleton.LocalClientId != playerSendParams.LocalClientID)
            return;

        SetCardInteraction(playerSendParams, cardNetworkID, interactable);
    }


    //SHOULD ONLY BE CALLED ON THE PLAYER CLIENT THAT HAS THE INTERACTION
    //E.G PLAYER 2 CHOOSING TO DISCARD A CARD, THIS CODE SHOULD ONLY BE RUN ON PLAYER 2'S CLIENT
    private void SetCardInteraction(TablePlayerSendParams playerSendParams, ulong cardNetworkID, bool interactable, TAction? action = null)
    {
        PlayingCard cardToInteract = PlayingCard.GetPlayingCardFromNetworkID(cardNetworkID);
        TPlayer tablePlayer = Game.GetPlayerFromTablePlayerID(playerSendParams.TablePlayerID);

        if (cardToInteract == null)
        {
            ConsoleLog.Instance.Log($"SetCardInteraction failed: invalid card with ID: {cardNetworkID}");
            return;
        }

        if (tablePlayer == null)
        {
            ConsoleLog.Instance.Log($"SetCardInteraction failed: invalid player reference for table ID {playerSendParams.TablePlayerID}");
            return;
        }

        cardToInteract.Interactable.SetInteractable(interactable);

        if (action.HasValue) //Action was provided
        {
            EventHandler<InteractEventArgs> eventHandler = GetCardOnInteractEvent(action.Value);

            if (interactable)
                tablePlayer.SubscribeCardTo(cardToInteract, eventHandler);
            else
                tablePlayer.UnsubscribeCardFrom(cardToInteract, eventHandler);
        }
        else if (!interactable) tablePlayer.UnsubscribeCard(cardToInteract); //Not interactable and no action

        ConsoleLog.Instance.Log($"[Client {NetworkManager.Singleton.LocalClientId}] {cardToInteract.ToString()} {(interactable ? "enabled" : "disabled")} for {tablePlayer.GetName()} [ID:{playerSendParams.TablePlayerID}] [Client: {playerSendParams.LocalClientID}]");
    }

    #endregion

    #region Set Hand Interactable

    /// <summary>
    /// Sets the hand interactable and action for all current players
    /// </summary>
    public void RequestSetHandInteraction(bool interactable, TAction? action = null)
    {
        if (!IsServer)
        {
            ConsoleLog.Instance.Log("Only server should call RequestSetHandInteraction()");
            return;
        }

        foreach (var player in game.Players)
        {
            RequestSetHandInteraction(player.SendParams, interactable, action);
        }
    }

    /// <summary>
    /// Sets the hand interactable and action for the given player
    /// </summary>
    public void RequestSetHandInteraction(TablePlayerSendParams playerSendParams, bool interactable, TAction? action = null)
    {
        if (IsServer)
        {
            if (interactable && action.HasValue) 
            {
                TPlayer player = Game.GetPlayerFromTablePlayerID(playerSendParams.TablePlayerID);
                SetHandInteractDisplay(player, GetInteractDisplay(action.Value));
            }

            ClientRpcParams rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { playerSendParams.LocalClientID }
                }
            };

            // Directly send RPC to target client
            if (action.HasValue) SetHandInteractionClientRpc(playerSendParams, interactable, action.Value, rpcParams);
            else SetHandInteractionClientRpc(playerSendParams, interactable, rpcParams);
        }
        else
        {
            // Clients shouldn't call this directly, but just in case
            ConsoleLog.Instance.Log("Only server should call RequestSetCardInteraction()");
        }
    }

    [ClientRpc] //WITH ACTION
    private void SetHandInteractionClientRpc(TablePlayerSendParams playerSendParams, bool interactable, TAction action, ClientRpcParams rpcParams = default)
    {
        // Only execute on correct client
        if (NetworkManager.Singleton.LocalClientId != playerSendParams.LocalClientID)
            return;

        TablePlayer<TPlayer, TAction, TAI> tablePlayer = Game.GetPlayerFromTablePlayerID(playerSendParams.TablePlayerID);

        foreach (var card in tablePlayer.Hand.Cards) SetCardInteraction(playerSendParams, card.NetworkObjectId, interactable, action);
    }

    [ClientRpc] //WITHOUT ACTION
    private void SetHandInteractionClientRpc(TablePlayerSendParams playerSendParams, bool interactable, ClientRpcParams rpcParams = default)
    {
        // Only execute on correct client
        if (NetworkManager.Singleton.LocalClientId != playerSendParams.LocalClientID)
            return;

        TablePlayer<TPlayer, TAction, TAI> tablePlayer = Game.GetPlayerFromTablePlayerID(playerSendParams.TablePlayerID);

        foreach (var card in tablePlayer.Hand.Cards) SetCardInteraction(playerSendParams, card.NetworkObjectId, interactable);
    }

    #endregion

    #region Reset Interactions

    public void ResetAllInteractions()
    {
        foreach (var player in game.Players)
        {
            player.DisableAllCardsAndUnsubscribeClientRpc();
            ResetHandInteractableDisplay(player);
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

    /// <summary>
    /// Called to reset the interactable display for all cards in the players hand
    /// </summary>
    public void ResetHandInteractableDisplay(TPlayer player)
    {
        foreach (var card in player.Hand.Cards) card.Interactable.ResetDisplay();
    }

    #endregion
}
