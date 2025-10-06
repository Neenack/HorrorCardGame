using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.XR;
using static Interactable;

public struct TablePlayerSendParams
{
    public ulong ClientID;
    public ulong TablePlayerID;

    public TablePlayerSendParams(ulong OwnerClientID, ulong TablePlayerID)
    {
        ClientID = OwnerClientID;
        this.TablePlayerID = TablePlayerID;
    }
}


public abstract class TableInteractionManager<TPlayer, TAction, TAI> : NetworkBehaviour
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{

    private CardGame<TPlayer, TAction, TAI> game;
    private List<TablePlayer<TPlayer, TAction, TAI>> players => game?.Players.ToList<TablePlayer<TPlayer, TAction, TAI>>();

    protected TablePlayer<TPlayer, TAction, TAI> GetPlayerFromTableID(ulong TableID)
    {
        return players.FirstOrDefault(p => p.TablePlayerID == TableID);
    }

    protected abstract EventHandler<InteractEventArgs> GetCardOnInteractEvent(TAction action);



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
            TablePlayerSendParams sendParams = new TablePlayerSendParams();
            sendParams.ClientID = player.PlayerData.OwnerClientId;
            sendParams.TablePlayerID = player.TablePlayerID;

            RequestSetCardInteraction(sendParams, cardNetworkID, interactable, action);
        }
    }

    /// <summary>
    /// Sets the card interactable and action for the given player
    /// </summary>
    public void RequestSetCardInteraction(TablePlayerSendParams playerSendParams, ulong cardNetworkID, bool interactable, TAction? action = null)
    {
        if (IsServer)
        {
            // Directly send RPC to target client
            SetCardInteractionClientRpc(playerSendParams, cardNetworkID, interactable, action,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { playerSendParams.ClientID }
                    }
                });
        }
        else
        {
            // Clients shouldn't call this directly, but just in case
            ConsoleLog.Instance.Log("Only server should call RequestSetCardInteraction()");
        }
    }

    [ClientRpc]
    private void SetCardInteractionClientRpc(TablePlayerSendParams playerSendParams, ulong cardNetworkID, bool interactable, TAction? action, ClientRpcParams rpcParams = default)
    {
        // Only execute on correct client
        if (NetworkManager.Singleton.LocalClientId != playerSendParams.ClientID)
            return;

        SetCardInteraction(playerSendParams, cardNetworkID, interactable, action);
    }


    //SHOULD ONLY BE CALLED ON THE PLAYER CLIENT THAT HAS THE INTERACTION
    private void SetCardInteraction(TablePlayerSendParams playerSendParams, ulong cardNetworkID, bool interactable, TAction? action = null)
    {
        PlayingCard cardToInteract = PlayingCard.GetPlayingCardFromNetworkID(cardNetworkID);
        TablePlayer<TPlayer, TAction, TAI> tablePlayer = GetPlayerFromTableID(playerSendParams.TablePlayerID);

        if (cardToInteract == null || tablePlayer == null)
        {
            ConsoleLog.Instance.Log("SetCardInteraction failed: invalid card or player reference.");
            return;
        }

        cardToInteract.Interactable.SetInteractable(interactable);

        if (action.HasValue)
        {
            EventHandler<InteractEventArgs> eventHandler = GetCardOnInteractEvent(action.Value);

            if (interactable)
                tablePlayer.SubscribeCardTo(cardToInteract, eventHandler);
            else
                tablePlayer.UnsubscribeCardFrom(cardToInteract, eventHandler);
        }
        else
        {
            if (!interactable) tablePlayer.UnsubscribeCard(cardToInteract);
        }

        ConsoleLog.Instance.Log($"[Client {NetworkManager.Singleton.LocalClientId}] Card {(interactable ? "enabled" : "disabled")} for Player {playerSendParams.TablePlayerID}");
    }




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
            TablePlayerSendParams sendParams = new TablePlayerSendParams();
            sendParams.ClientID = player.PlayerData.OwnerClientId;
            sendParams.TablePlayerID = player.TablePlayerID;

            RequestSetHandInteraction(sendParams, interactable, action);
        }
    }

    /// <summary>
    /// Sets the hand interactable and action for the given player
    /// </summary>
    public void RequestSetHandInteraction(TablePlayerSendParams playerSendParams, bool interactable, TAction? action = null)
    {
        if (IsServer)
        {
            // Directly send RPC to target client
            SetHandInteractionClientRpc(playerSendParams, interactable, action,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { playerSendParams.ClientID }
                    }
                });
        }
        else
        {
            // Clients shouldn't call this directly, but just in case
            ConsoleLog.Instance.Log("Only server should call RequestSetCardInteraction()");
        }
    }

    [ClientRpc]
    private void SetHandInteractionClientRpc(TablePlayerSendParams playerSendParams, bool interactable, TAction? action, ClientRpcParams rpcParams = default)
    {
        // Only execute on correct client
        if (NetworkManager.Singleton.LocalClientId != playerSendParams.ClientID)
            return;

        TablePlayer<TPlayer, TAction, TAI> tablePlayer = GetPlayerFromTableID(playerSendParams.TablePlayerID);

        foreach (var card in tablePlayer.Hand.Cards) SetCardInteraction(playerSendParams, card.NetworkObjectId, interactable, action);
    }
}
