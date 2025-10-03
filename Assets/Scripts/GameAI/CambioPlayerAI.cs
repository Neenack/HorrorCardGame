using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.Playmode;
using UnityEngine;

public class CambioPlayerAI : PlayerAI<CambioPlayer, CambioActionData, CambioPlayerAI>
{
    private CambioPlayer cambioPlayer;

    public CambioPlayerAI(CambioPlayer playerRef) : base(playerRef) 
    { 
        cambioPlayer = playerRef;
    }

    /// <summary>
    /// Given game context will return the action data to execute in the game
    /// </summary>
    public override CambioActionData DecideAction(TurnContext context)
    {
        //UnityEngine.Debug.Log("Action Requested: " + context.ToString());

        return context switch
        {
            TurnContext.StartTurn => StartTurn(),
            TurnContext.AfterDraw => AfterDraw(),
            TurnContext.CardAbility => CardAbility(),
            TurnContext.AfterTurn => AfterTurn(),
            _ => new CambioActionData(CambioActionType.None, true, player.TablePlayerID)
        };
    }

    private CambioActionData StartTurn()
    {
        if (ShouldCallCambio())
        {
            return new CambioActionData(CambioActionType.CallCambio, true, player.TablePlayerID);
        }

        return new CambioActionData(CambioActionType.Draw, false, player.TablePlayerID);
    }

    private CambioActionData AfterDraw()
    {
        PlayingCard DrawnCard = PlayingCard.GetPlayingCardFromNetworkID(player.Game.DrawnCardID.Value);

        if (DrawnCard == null)
        {
            return new CambioActionData(CambioActionType.None, true, player.TablePlayerID);
        }

        int cardValue = CambioPlayer.GetCardValue(DrawnCard);

        //If card is above 5, discard
        if (cardValue > 5)
        {
            return new CambioActionData(CambioActionType.Discard, cardValue < 6, player.TablePlayerID, DrawnCard.NetworkObjectId);
        }

        //If AI has not seen all of its cards then swap
        // OR
        //If card is larger than highest known card then swap
        if (cambioPlayer.SeenCards.Count < player.Hand.Cards.Count
            || CambioPlayer.GetCardValue(GetHighestSeenCard()) > CambioPlayer.GetCardValue(DrawnCard))
        {
            PlayingCard cardToRemove = GetCardtoSwap();

            return new CambioActionData(CambioActionType.TradeCard, true, player.TablePlayerID, cardToRemove.NetworkObjectId, player.TablePlayerID, DrawnCard.NetworkObjectId);
        }

        //Discard
        return new CambioActionData(CambioActionType.Discard, cardValue < 6, player.TablePlayerID, DrawnCard.NetworkObjectId);
    }

    private CambioActionData CardAbility()
    {
        PlayingCard AbilityCard = PlayingCard.GetPlayingCardFromNetworkID(player.Game.PileCardID.Value);

        if (AbilityCard == null) return new CambioActionData(CambioActionType.None, true, player.TablePlayerID);

        int cardValue = CambioPlayer.GetCardValue(AbilityCard);
        if (cardValue < 6) return new CambioActionData(CambioActionType.None, true, player.TablePlayerID);

        switch (cardValue)
        {
            case 6:
            case 7: //LOOK AT YOUR OWN CARD
                PlayingCard cardToReveal = GetCardToReveal();
                return new CambioActionData(CambioActionType.RevealCard, true, player.TablePlayerID, cardToReveal.NetworkObjectId, player.TablePlayerID, cardToReveal.NetworkObjectId);

            case 8:
            case 9: //LOOK AT ANOTHER CARD
                CambioPlayer randomPlayer = GetRandomPlayer();
                PlayingCard randomCard = GetRandomCard(randomPlayer);
                return new CambioActionData(CambioActionType.RevealCard, true, player.TablePlayerID, 0, randomPlayer.TablePlayerID, randomCard.NetworkObjectId);

            case 10: //SWAP HANDS
                if (ShouldSwapHand())
                {
                    return new CambioActionData(CambioActionType.SwapHand, true, player.TablePlayerID, 0, GetRandomPlayer().TablePlayerID, 0);
                }
                ConsoleLog.Instance.Log($"{player.GetName()} (AI) Chose not to swap hands");
                break;

            case 11: //COMPARE 2 AND CHOOSE 1 TO KEEP
                CambioPlayer otherPlayer = GetRandomPlayer();
                return new CambioActionData(CambioActionType.CompareCards, false, player.TablePlayerID, GetCardtoSwap().NetworkObjectId, otherPlayer.TablePlayerID, GetRandomCard(otherPlayer).NetworkObjectId);

            case 12: //BLIND SWAP
                if (ShouldBlindSwap())
                {
                    otherPlayer = GetRandomPlayer();
                    return new CambioActionData(CambioActionType.SwapCard, true, player.TablePlayerID, GetCardtoSwap().NetworkObjectId, otherPlayer.TablePlayerID, GetRandomCard(otherPlayer).NetworkObjectId);
                }
                ConsoleLog.Instance.Log($"{player.GetName()} (AI) Chose not to swap cards");
                break;

            case 13: //RED KING REVEAL WHOLE HAND
                if (AbilityCard.Suit == Suit.Diamonds || AbilityCard.Suit == Suit.Hearts)
                {
                    //Debug.Log("AI: Reveal whole hand");
                    return new CambioActionData(CambioActionType.RevealHand, true, player.TablePlayerID, 0, player.TablePlayerID, 0);
                }
                break;
        }

        return new CambioActionData(CambioActionType.None, true, player.TablePlayerID);
    }

    private CambioActionData AfterTurn()
    {
        if (CanStack())
        {
            PlayingCard cardToStack = GetCardToStack();
            if (cardToStack != null) return new CambioActionData(CambioActionType.Stack, false, player.TablePlayerID, cardToStack.NetworkObjectId, player.TablePlayerID, cardToStack.NetworkObjectId);
        }

        return new CambioActionData(CambioActionType.None, false, player.TablePlayerID);
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
    private PlayingCard GetHighestSeenCard() => cambioPlayer.SeenCards.OrderByDescending(c => CambioPlayer.GetCardValue(c)).FirstOrDefault();




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

    public PlayingCard ChooseBetween2Cards(PlayingCard card1, PlayingCard card2)
    {
        return CambioPlayer.GetCardValue(card1) < CambioPlayer.GetCardValue(card2) ? card1 : card2;
    }

    /// <summary>
    /// Asks the AI if it would like to blind swap 2 cards
    /// </summary>
    /// <returns>True if the AI would like to blind swap</returns>
    private bool ShouldBlindSwap()
    {
        return (player.Hand.Cards.Where(c => !cambioPlayer.SeenCards.Contains(c)).ToList().Count > 0)
            || CambioPlayer.GetCardValue(GetHighestSeenCard()) > 6;
    }

    /// <summary>
    /// Asks the AI if it knows any cards that can be stacked
    /// </summary>
    public bool CanStack() => GetCardToStack() != null;


    /// <summary>
    /// Gets a matching card to stack on the pile
    /// </summary>
    /// <returns>Playing card</returns>
    public PlayingCard GetCardToStack()
    {
        PlayingCard topCard = PlayingCard.GetPlayingCardFromNetworkID(player.Game.PileCardID.Value);
        if (topCard == null) return null;

        foreach (var card in cambioPlayer.SeenCards)
        {
            if (card == null) continue;

            if (card.GetValue(false) == topCard.GetValue(false) && !(card.GetValue(false) == 13 && (topCard.Suit == Suit.Spades || topCard.Suit == Suit.Clubs)))
            {
                return card;
            }
        }

        return null;
    }
}