using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.Playmode;
using Unity.VisualScripting;
using UnityEngine;

public class CambioPlayerAI : PlayerAI<CambioPlayer, CambioActionData, CambioPlayerAI>
{
    private CambioPlayer cambioPlayer;

    #region Difficulty Variables

    private const float EASY_IGNORE_STACK_CHANCE = 0.4f;
    private const float NORMAL_IGNORE_STACK_CHANCE = 0.2f;
    private const float EXPERT_IGNORE_STACK_CHANCE = 0.0f;


    #endregion



    public CambioPlayerAI(CambioPlayer playerRef, Difficulty difficulty) : base(playerRef, difficulty) 
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
                CambioPlayer playerHandToTake = GetPlayerToSwapHand();
                if (playerHandToTake != null) return new CambioActionData(CambioActionType.SwapHand, true, player.TablePlayerID, 0, playerHandToTake.TablePlayerID, 0);
                ConsoleLog.Instance.Log($"{player.GetName()} (AI) Chose not to swap hands");
                break;

            case 11: //COMPARE 2 AND CHOOSE 1 TO KEEP
                CambioPlayer otherPlayer = GetPlayerToCompareCards();
                return new CambioActionData(CambioActionType.CompareCards, false, player.TablePlayerID, GetCardtoSwap().NetworkObjectId, otherPlayer.TablePlayerID, GetRandomCard(otherPlayer).NetworkObjectId);

            case 12: //BLIND SWAP
                if (ShouldBlindSwap())
                {
                    otherPlayer = GetPlayerToCompareCards();
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
        if (cambioPlayer.Hand.Cards.Count == 0) return true;

        int expectedScore = EstimateScore();

        int scoreToCall = difficulty switch
        {
            Difficulty.Easy => 12,
            Difficulty.Normal => 9,
            Difficulty.Expert => 6,
            _ => 9
        };

        if (expectedScore <= scoreToCall)
        {
            return true;
        }

        return false;
    }



    /// <summary>
    /// Estimates the score the player thinks they have
    /// </summary>
    private int EstimateScore()
    {
        float confidence = (float)cambioPlayer.SeenCards.Count / player.Hand.Cards.Count;
        float expectedScore = (cambioPlayer.GetScore() / confidence);

        return (int)expectedScore;
    }


    /// <summary>
    /// Finds the highest known card in player hand
    /// </summary>
    /// <returns>A playing card</returns>
    private PlayingCard GetHighestSeenCard() => cambioPlayer.SeenCards.OrderByDescending(c => CambioPlayer.GetCardValue(c)).FirstOrDefault();




    /// <summary>
    /// Chooses a card the AI would most like to swap out
    /// </summary>
    /// <returns>A playing card</returns>
    public PlayingCard GetCardtoSwap()
    {
        if (WillMisplay()) return GetRandomCard(player);

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
        //If seen all cards or will misplay, return a random card
        if (WillMisplay() || cambioPlayer.SeenCards.Count == player.Hand.Cards.Count)
        {
            return GetRandomCard(player);
        }

        List<PlayingCard> unseenCards = player.Hand.Cards.Where(c => !cambioPlayer.SeenCards.Contains(c)).ToList();

        //if some cards have not been seen, return a random unseen card
        if (unseenCards.Count > 0)
        {
            return unseenCards[UnityEngine.Random.Range(0, unseenCards.Count)];
        }

        //return a random card
        return GetRandomCard(player);
    }




    /// <summary>
    /// Asks the AI if it would like to swap hands
    /// </summary>
    /// <returns>True if the player would like to swap hands</returns>
    private CambioPlayer GetPlayerToSwapHand()
    {
        int handCardCount = player.Hand.Cards.Count;
        int lowestCardCount = int.MaxValue;
        CambioPlayer bestPlayer = null;

        foreach (var otherPlayer in player.Game.Players)
        {
            if (otherPlayer.TablePlayerID == player.TablePlayerID) continue;

            int playerCardCount = otherPlayer.Hand.Cards.Count;
            if (playerCardCount < lowestCardCount)
            {
                lowestCardCount = playerCardCount;
                bestPlayer = otherPlayer;
            }
        }

        //Swap with the lowest players hand
        if (lowestCardCount < handCardCount && !WillMisplay()) return bestPlayer;

        // If AI has the fewest cards, do not swap
        if (lowestCardCount > handCardCount) return null;

        int estimatedScore = EstimateScore();
        int maxScore = player.Hand.Cards.Count * 12; // max possible hand score

        float swapThresholdFraction = difficulty switch
        {
            Difficulty.Easy => 0.3f,
            Difficulty.Normal => 0.4f,
            Difficulty.Expert => 0.5f,
            _ => 0.4f
        };

        bool wantsToSwap = estimatedScore > maxScore * swapThresholdFraction;

        return wantsToSwap && !WillMisplay() ? GetRandomPlayer() : null;
    }



    /// <summary>
    /// Asks the AI which player to take a card from to compare
    /// </summary>
    /// <returns>True if the player would like to swap hands</returns>
    private CambioPlayer GetPlayerToCompareCards()
    {
        if (WillMisplay()) return GetRandomPlayer();

        return GetPlayerWithSmallestHand();
    }



    /// <summary>
    /// Compares 2 cards and selects the lowest value
    /// </summary>
    /// <returns>The playing card it wants to keep</returns>
    public PlayingCard ChooseBetween2Cards(PlayingCard card1, PlayingCard card2)
    {
        int card1Value = CambioPlayer.GetCardValue(card1);
        int card2Value = CambioPlayer.GetCardValue(card2);

        if (WillMisplay()) return UnityEngine.Random.value < 0.5f ? card1 : card2;

        if (card1Value < card2Value) return card1;
        return card2;
    }



    /// <summary>
    /// Asks the AI if it would like to blind swap 2 cards
    /// </summary>
    /// <returns>True if the AI would like to blind swap</returns>
    private bool ShouldBlindSwap()
    {
        int unseenCardCount = player.Hand.Cards.Where(c => !cambioPlayer.SeenCards.Contains(c)).ToList().Count;
        int highestSeenCard = CambioPlayer.GetCardValue(GetHighestSeenCard());

        return (unseenCardCount > 0) || highestSeenCard > 6;
    }


    private CambioPlayer GetPlayerWithSmallestHand()
    {
        var otherPlayers = player.Game.Players
            .Where(p => p.TablePlayerID != player.TablePlayerID)
            .ToList();

        if (otherPlayers.Count == 0) return null;

        // Find the smallest hand size
        int lowestCardCount = otherPlayers.Min(p => p.Hand.Cards.Count);

        // Get all players with the lowest hand size
        var smallestPlayers = otherPlayers
            .Where(p => p.Hand.Cards.Count == lowestCardCount)
            .ToList();

        // If all players have the same size hand, pick a random one
        if (smallestPlayers.Count == otherPlayers.Count)
        {
            return GetRandomPlayer();
        }

        // Otherwise return one of the smallest hand players
        return smallestPlayers[0];
    }

    #region Stacking

    /// <summary>
    /// Asks the AI if it knows any cards that can be stacked
    /// </summary>
    public bool CanStack()
    {
        bool canStack = GetMatchingCardToStack() != null;

        if (!canStack) return false; //Return false if cannot stack

        return WillMisplay() ? false : true; //If can stack, check misplay
    }



    /// <summary>
    /// Gets a matching card to stack on the pile
    /// </summary>
    /// <returns>Playing card</returns>
    public PlayingCard GetCardToStack()
    {
        PlayingCard card = GetMatchingCardToStack();
        if (card == null) return null;

        return WillMisplay() ? GetRandomCard(cambioPlayer) : card;
    }



    /// <summary>
    /// Checks if it has a matching card from the pile
    /// </summary>
    /// <returns>True if player has a card to stack</returns>
    private PlayingCard GetMatchingCardToStack()
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

    #endregion
}