using System;
using System.Runtime.InteropServices.WindowsRuntime;
using Unity.Netcode;
using UnityEngine;
using static Interactable;

public class CambioInteractionManager : TableInteractionManager<CambioPlayer, CambioActionData, CambioPlayerAI>
{
    private CambioPlayer playerWhoStackedCard;


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

    protected override InteractDisplay GetInteractDisplay(CambioActionData data)
    {
        switch (data.Type)
        {
            case CambioActionType.Draw:
                PlayingCard drawnCard = PlayingCard.GetPlayingCardFromNetworkID(Game.DrawnCardID.Value);

                //If data card ID is the drawn card, then show the display for discarding
                if (data.CardId == Game.DrawnCardID.Value) return new InteractDisplay("", true, "Discard", GetAbilityString(drawnCard));
                else return new InteractDisplay("", true, "Swap Card", $"Swap card for the {drawnCard.ToString()}");

            case CambioActionType.RevealCard:
                 return new InteractDisplay("", true, "Reveal Card", "Shows you the playing card");
            case CambioActionType.SwapHand:
                 return new InteractDisplay("", true, "Swap Hand", $"Swap your whole hand with {Game.GetPlayerFromTablePlayerID(data.TargetPlayerId).GetName()}");

            case CambioActionType.SelectCard:
                PlayingCard pileCard = PlayingCard.GetPlayingCardFromNetworkID(Game.PileCardID.Value);

                if (pileCard.GetValue(false) == 11)
                    return new InteractDisplay("", true, "Compare Cards", "Select a card to compare");
                else if (pileCard.GetValue(false) == 12)
                    new InteractDisplay("", true, "Blind Swap", "Select a card to swap");
                return nullDisplay;

            case CambioActionType.Stack:
                return new InteractDisplay("", true, "Stack Card", "Try stack a matching card on the pile");

        }

        return nullDisplay;
    }

    private string GetAbilityString(PlayingCard card)
    {
        switch (CambioPlayer.GetCardValue(card))
        {
            case < 6:
                return "Does nothing";
            case 6:
            case 7:
                return "Look at one of your cards";
            case 8:
            case 9:
                return "Look at someone elses card";
            case 10:
                return "Choose a player to swap hands with";
            case 11:
                return "Compare 2 cards and choose which one to keep";
            case 12:
                return "Blind swap 2 cards";
            case 13:
                if (card.Suit == Suit.Spades || card.Suit == Suit.Clubs) return "Does nothing";
                else return "Look at your whole hand";
        }

        return "";
    }

    private void Card_OnInteract_AfterDraw(object sender, InteractEventArgs e)
    {
        Debug.Log("After draw interaction!");

        PlayingCard drawnCard = PlayingCard.GetPlayingCardFromNetworkID(Game.DrawnCardID.Value);
        CambioPlayer currentPlayer = Game.GetPlayerFromTablePlayerID(Game.CurrentPlayerTurnTableID.Value);

        RequestSetCardInteraction(Game.DrawnCardID.Value, false);

        PlayingCard chosenCard = (sender as Interactable).GetComponent<PlayingCard>();

        if (chosenCard == null)
        {
            Debug.LogWarning("Could not find chosen card");
            return;
        }

        if (currentPlayer.HandCardIDs.Contains(chosenCard.NetworkObjectId)) //Chose one of your own cards so trade
        {
            Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.TradeCard, true, currentPlayer.TablePlayerID, chosenCard.NetworkObjectId, currentPlayer.TablePlayerID, Game.DrawnCardID.Value));
        }
        else //Chose the drawn card so discard
        {
            Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.Discard, true, currentPlayer.TablePlayerID, Game.DrawnCardID.Value));
        }
    }

    private void Card_OnInteract_RevealCard(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;
        ulong playerWithCardId = Game.GetPlayerWithCard(cardNetworkId).TablePlayerID;

        Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.RevealCard, true, Game.CurrentPlayerTurnTableID.Value, 0, playerWithCardId, cardNetworkId));
    }

    private void Card_OnInteract_SwapHand(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;
        ulong playerWithCardId = Game.GetPlayerWithCard(cardNetworkId).TablePlayerID;

        Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.SwapHand, true, Game.CurrentPlayerTurnTableID.Value, 0, playerWithCardId, 0));
    }

    private void Card_OnInteract_ChooseCard(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.SelectCard, false, Game.CurrentPlayerTurnTableID.Value, 0, 0, cardNetworkId));
    }
    private void Card_OnInteract_CompareCards(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.ChooseCard, true, Game.CurrentPlayerTurnTableID.Value, 0, 0, cardNetworkId));

    }
    private void Card_OnInteract_Stack(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        playerWhoStackedCard = Game.GetPlayerFromClientID(e.ClientID);

        Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.Stack, false, playerWhoStackedCard.TablePlayerID, 0, 0, cardNetworkId));
    }

    private void Card_OnInteract_CorrectStack(object sender, InteractEventArgs e)
    {
        ulong cardNetworkId = (sender as Interactable).GetComponent<PlayingCard>().NetworkObjectId;

        Game.ExecuteAction(e.ClientID, new CambioActionData(CambioActionType.GiveCard, false, playerWhoStackedCard.TablePlayerID, 0, 0, cardNetworkId));

        playerWhoStackedCard = null;
    }
}
