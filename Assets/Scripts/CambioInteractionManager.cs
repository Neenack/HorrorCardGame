using System;
using Unity.Netcode;
using UnityEngine;
using static Interactable;

public class CambioInteractionManager : TableInteractionManager<CambioPlayer, CambioActionData, CambioPlayerAI>
{
    #region Interact Event Switch

    /// <summary>
    /// When given action data, will subscribe players hand to different actions locally
    /// </summary>
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
    private void Card_OnInteract_AfterDraw(object sender, InteractEventArgs e)
    {
        Debug.Log("After draw interaction!");

        PlayingCard drawnCard = PlayingCard.GetPlayingCardFromNetworkID(Game.DrawnCardID.Value);
        CambioPlayer currentPlayer = GetPlayerFromTableID(Game.CurrentPlayerTurnID.Value);

        RequestSetCardInteraction(Game.DrawnCardID.Value, false);

        PlayingCard chosenCard = (sender as Interactable).GetComponent<PlayingCard>();

        if (chosenCard == null)
        {
            Debug.LogWarning("Could not find chosen card");
            return;
        }

        if (currentPlayer.HandCardIDs.Contains(chosenCard.NetworkObjectId)) //Chose one of your own cards so trade
        {
            Game.ExecuteAction(e.playerID, new CambioActionData(CambioActionType.TradeCard, true, currentPlayer.TablePlayerID, chosenCard.NetworkObjectId, currentPlayer.TablePlayerID, Game.DrawnCardID.Value));
        }
        else //Chose the drawn card so discard
        {
            Game.ExecuteAction(e.playerID, new CambioActionData(CambioActionType.Discard, true, currentPlayer.TablePlayerID, Game.DrawnCardID.Value));
        }
    }

    private void Card_OnInteract_RevealCard(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;
        ulong playerWithCardId = Game.GetPlayerWithCard(cardNetworkId).TablePlayerID;

        Game.ExecuteAction(e.playerID, new CambioActionData(CambioActionType.RevealCard, true, Game.CurrentPlayerTurnID.Value, 0, playerWithCardId, cardNetworkId));
    }

    private void Card_OnInteract_SwapHand(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;
        ulong playerWithCardId = Game.GetPlayerWithCard(cardNetworkId).TablePlayerID;

        Game.ExecuteAction(e.playerID, new CambioActionData(CambioActionType.SwapHand, true, Game.CurrentPlayerTurnID.Value, 0, playerWithCardId, 0));
    }

    private void Card_OnInteract_ChooseCard(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        Game.ExecuteAction(e.playerID, new CambioActionData(CambioActionType.SelectCard, false, Game.CurrentPlayerTurnID.Value, 0, 0, cardNetworkId));
    }
    private void Card_OnInteract_CompareCards(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        Game.ExecuteAction(e.playerID, new CambioActionData(CambioActionType.ChooseCard, true, Game.CurrentPlayerTurnID.Value, 0, 0, cardNetworkId));

    }
    private void Card_OnInteract_Stack(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        PlayerData data = PlayerManager.Instance.GetPlayerDataById(e.playerID);
        CambioPlayer playerWithStacked = Game.GetPlayerFromData(data);

        Game.ExecuteAction(playerWithStacked.TablePlayerID, new CambioActionData(CambioActionType.Stack, false, playerWithStacked.TablePlayerID, 0, 0, cardNetworkId));
    }

    private void Card_OnInteract_CorrectStack(object sender, InteractEventArgs e)
    {
        Debug.Log("Correct stack!"); //NEED TO GET THE PLAYER WHO STACKED

        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        //Game.ExecuteAction(TablePlayerID, new CambioActionData(CambioActionType.GiveCard, false, TablePlayerID, 0, 0, cardNetworkId));
    }
}
