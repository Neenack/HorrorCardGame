using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class CambioPlayerAI : PlayerAI<CambioAction>
{
    private CambioPlayer cambioPlayer;

    public CambioPlayerAI(CambioPlayer playerRef) : base(playerRef) 
    { 
        cambioPlayer = playerRef as CambioPlayer;
    }

    public override CambioAction DecideAction(TurnContext context, object sender = null)
    {
        UnityEngine.Debug.Log("Action Requested: " + context.ToString());

        return context switch
        {
            TurnContext.StartTurn => StartTurn(),
            TurnContext.AfterDraw => AfterDraw(sender as PlayingCard),
            TurnContext.CardAbility => CardAbility(sender as PlayingCard),
            TurnContext.AfterTurn => AfterTurn(),
            _ => new CambioAction(CambioActionType.None, true)
        };
    }

    private CambioAction StartTurn()
    {
        if (ShouldCallCambio())
        {
            return new CambioAction(CambioActionType.CallCambio, true);
        }

        return new CambioAction(CambioActionType.Draw, false);
    }

    private CambioAction AfterDraw(PlayingCard drawnCard)
    {
        if (drawnCard == null)
        {
            return new CambioAction(CambioActionType.None, true);
        }

        int cardValue = player.GetCardValue(drawnCard);

        //If card is above 5, discard
        if (cardValue > 5)
        {
            return new CambioAction(CambioActionType.Discard, cardValue < 6, cambioPlayer, drawnCard);
        }

        //If AI has not seen all of its cards then swap
        // OR
        //If card is larger than highest known card then swap
        if (cambioPlayer.SeenCards.Count < player.Hand.Cards.Count
            || player.GetCardValue(GetHighestSeenCard()) > player.GetCardValue(drawnCard))
        {
            PlayingCard cardToSwap = GetCardtoSwap();

            return new CambioAction(CambioActionType.TradeCard, true, cambioPlayer, new SwapInfo(drawnCard, cardToSwap));
        }

        //Discard
        return new CambioAction(CambioActionType.Discard, cardValue < 6, cambioPlayer, drawnCard);
    }

    private CambioAction CardAbility(PlayingCard abilityCard)
    {
        if (abilityCard == null) return new CambioAction(CambioActionType.None, true);

        int cardValue = cambioPlayer.GetCardValue(abilityCard);
        if (cardValue < 6) return new CambioAction(CambioActionType.None, true);

        switch (cardValue)
        {
            case 6:
            case 7: //LOOK AT YOUR OWN CARD
                return new CambioAction(CambioActionType.RevealCard, true, cambioPlayer, GetCardToReveal());

            case 8:
            case 9: //LOOK AT ANOTHER CARD
                CambioPlayer randomPlayer = GetRandomPlayer() as CambioPlayer;
                return new CambioAction(CambioActionType.RevealCard, true, randomPlayer, GetRandomCard(randomPlayer));

            case 10: //SWAP HANDS
                if (ShouldSwapHand())
                {
                    return new CambioAction(CambioActionType.SwapHand, true, GetRandomPlayer() as CambioPlayer);
                }
                Debug.Log("AI: Chose not to swap");
                break;

            case 11: //COMPARE 2 AND CHOOSE 1 TO KEEP
                CambioPlayer otherPlayer = GetRandomPlayer() as CambioPlayer;
                return new CambioAction(CambioActionType.CompareCards, false, otherPlayer, new SwapInfo(GetRandomCard(otherPlayer), GetCardtoSwap()));

            case 12: //BLIND SWAP
                if (ShouldBlindSwap())
                {
                    otherPlayer = GetRandomPlayer() as CambioPlayer;
                    return new CambioAction(CambioActionType.SwapCard, true, otherPlayer, new SwapInfo(GetRandomCard(otherPlayer), GetCardtoSwap()));
                }
                Debug.Log("Chose not to swap");
                break;

            case 13: //RED KING REVEAL WHOLE HAND
                if (abilityCard.Suit == Suit.Diamonds || abilityCard.Suit == Suit.Hearts)
                {
                    //Debug.Log("AI: Reveal whole hand");
                    return new CambioAction(CambioActionType.RevealHand, false);
                }
                break;
        }

        return new CambioAction(CambioActionType.None, true);
    }

    private CambioAction AfterTurn()
    {
        if (CanStack())
        {
            return new CambioAction(CambioActionType.Stack, false, cambioPlayer, GetCardToStack());
        }

        return new CambioAction(CambioActionType.None, false);
    }


    /// <summary>
    /// Asks the AI if it wants to call cambio
    /// </summary>
    /// <returns>True to call cambio</returns>
    private bool ShouldCallCambio()
    {
        float confidence = (float)cambioPlayer.SeenCards.Count / player.Hand.Cards.Count;
        float expectedScore = (cambioPlayer.GetScore() / confidence);

        if (expectedScore <= 6)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the highest known card in player hand
    /// </summary>
    /// <returns>A playing card</returns>
    private PlayingCard GetHighestSeenCard() => cambioPlayer.SeenCards.OrderByDescending(c => player.GetCardValue(c)).FirstOrDefault();

    /// <summary>
    /// Returns a random card in a players hand
    /// </summary>
    /// <returns>A random playing card</returns>
    private PlayingCard GetRandomCard(CambioPlayer player) => player.Hand.GetRandomCard();

    /// <summary>
    /// Chooses a card the AI would most like to swap out
    /// </summary>
    /// <returns>A playing card</returns>
    public PlayingCard GetCardtoSwap()
    {
        List<PlayingCard> unseenCards = player.Hand.Cards.Where(c => !cambioPlayer.SeenCards.Contains(c)).ToList();

        if (unseenCards.Count > 0)
        {
            return unseenCards[UnityEngine.Random.Range(0, unseenCards.Count)];
        }

        return GetHighestSeenCard();
    }

    /// <summary>
    /// Asks the AI what index card it would like to look at
    /// </summary>
    /// <returns>A playing card</returns>
    public PlayingCard GetCardToReveal()
    {
        //If seen all cards, return a random card
        if (cambioPlayer.SeenCards.Count == player.Hand.Cards.Count)
        {
            return player.Hand.Cards[UnityEngine.Random.Range(0, player.Hand.Cards.Count)];
        }

        List<PlayingCard> unseenCards = player.Hand.Cards.Where(c => !cambioPlayer.SeenCards.Contains(c)).ToList();

        //if some cards have not been seen, return a random unseen card
        if (unseenCards.Count > 0)
        {
            return unseenCards[UnityEngine.Random.Range(0, unseenCards.Count)];
        }

        //return a random card
        return player.Hand.Cards[UnityEngine.Random.Range(0, player.Hand.Cards.Count)];
    }

    /// <summary>
    /// Asks the AI if it would like to swap hands
    /// </summary>
    /// <returns>True if the player would like to swap hands</returns>
    public bool ShouldSwapHand()
    {
        return cambioPlayer.GetScore() > 10;
    }

    /// <summary>
    /// Asks the AI if it would like to blind swap 2 cards
    /// </summary>
    /// <returns>True if the player would like to blind swap</returns>
    private bool ShouldBlindSwap()
    {
        return (player.Hand.Cards.Where(c => !cambioPlayer.SeenCards.Contains(c)).ToList().Count > 0)
            || cambioPlayer.GetCardValue(GetHighestSeenCard()) > 6;
    }

    public bool CanStack()
    {
        PlayingCard topCard = player.Game.TopPileCard;

        foreach (var card in cambioPlayer.SeenCards)
        {
            if (card.GetValue(false) == topCard.GetValue(false))
            {
                //DONT STACK BLACK KINGS
                if (card.Suit == Suit.Spades || card.Suit == Suit.Clubs) continue;

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets a matching card to stack on the pile
    /// </summary>
    /// <returns>Playing card</returns>
    public PlayingCard GetCardToStack()
    {
        PlayingCard topCard = player.Game.TopPileCard;

        foreach (var card in cambioPlayer.SeenCards)
        {
            if (card.GetValue(false) == topCard.GetValue(false))
            {
                return card;
            }
        }

        return null;
    }
}