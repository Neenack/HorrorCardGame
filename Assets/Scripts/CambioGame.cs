using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class CambioGame : CardGame<CambioPlayer, CambioActionData, CambioPlayerAI>
{
    [Header("Cambio Settings")]
    [SerializeField] private float cardViewingTime = 3f;
    [SerializeField] private Vector3 cardPullPositionOffset = new Vector3(0f, 0.3f, 0f);
    [SerializeField] private float cardLiftHeight = 0.1f;
    [SerializeField] private float cardRevealHeight = 0.2f;
    [SerializeField] private float timeBetweenPlayerReveals = 1f;

    [Header("Stacking Settings")]
    [SerializeField] private bool cardStacking = true;
    [SerializeField] private float stackingTime = 2f;
    private CambioPlayer playerToRecieveCard;
    private bool hasStacked = false;
    private bool waitingForStackInput = false;

    private NetworkVariable<bool> isStacking = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private List<PlayingCard> selectedCards = new List<PlayingCard>();

    #region Start Game

    protected override void ServerStartGame()
    {
        foreach (var player in Players) player.hasPlayedLastTurn.Value = false;

        base.ServerStartGame();
    }

    #endregion


    #region Turn Management

    protected override IEnumerator NextTurnRoutine()
    {
        if (!IsServer) yield break;

        foreach (var player in Players) player.Hand.UpdateHand();

        selectedCards.Clear();

        //Set last turn if someone has called cambio
        if (currentPlayer) currentPlayer.hasPlayedLastTurn.Value = Players.Any(p => !p.IsPlaying());

        //Stacking
        if (cardStacking && cardPile.Count > 0) yield return StartCoroutine(StackingRoutine());

        StartCoroutine(base.NextTurnRoutine());
    }

    /// <summary>
    /// If stacking is enabled, runs the stacking coroutine between turns to allow player to stack cards
    /// </summary>
    private IEnumerator StackingRoutine()
    {
        ConsoleLog.Instance.Log("Stacking enabled!");
        isStacking.Value = true;

        yield return new WaitForSeconds(0.5f);

        foreach (var player in Players)
        {
            if (player.IsAI) continue;

            player.RequestSetStacking(true);
            InteractionManager.RequestSetHandInteraction(true, new CambioActionData(CambioActionType.Stack, false, player.TablePlayerID));
        }

        yield return new WaitForSeconds(stackingTime / 2);

        foreach (var player in Players)
        {
            if (!player.IsAI) continue;
            StartCoroutine(ExecuteActionRoutine(player.PlayerAI.DecideAction(TurnContext.AfterTurn)));
        }

        yield return new WaitForSeconds(stackingTime / 2);

        //Waits until players have finished the stacking routine if someone stacked a card
        yield return new WaitUntil(() => hasStacked == false);

        ConsoleLog.Instance.Log("Stacking disabled!");
        isStacking.Value = false;

        DisableAllCardsAndUnsubscribe();
    }

    /// <summary>
    /// Asks if players are allowed to interact with the game out of turn
    /// </summary>
    protected override bool CanOnlyPlayInTurn() => !isStacking.Value;

    #endregion

    #region Card Dealing

    /// <summary>
    /// Dealing inital cards on game start
    /// </summary>
    protected override IEnumerator DealInitialCards()
    {
        if (!IsServer) yield break;

        // Deal 4 cards to each player
        for (int i = 0; i < 4; i++)
        {
            foreach (var player in Players)
            {
                StartCoroutine(base.DealCardToPlayer(player));
                yield return new WaitForSeconds(timeBetweenCardDeals);
            }
        }

        yield return new WaitForSeconds(1f);

        // Reveal initial cards (positions 0 and 2)
        foreach (var player in Players)
        {
            if (player.Hand.Cards.Count >= 3)
            {
                PlayingCard card1 = player.Hand.GetCard(0);
                PlayingCard card2 = player.Hand.GetCard(2);

                StartCoroutine(RevealCardCoroutine(card1, player, card1.transform.position));
                StartCoroutine(RevealCardCoroutine(card2, player, card2.transform.position));
            }
        }

        yield return new WaitForSeconds(cardViewingTime);
    }

    /// <summary>
    /// Reveals a given card o a given player
    /// </summary>
    private IEnumerator RevealCardCoroutine(PlayingCard card, CambioPlayer player, Vector3 basePos)
    {
        Vector3 originalPos = card.transform.position;
        Quaternion originalRot = card.transform.rotation;

        card.MoveTo(basePos + new Vector3(0, cardRevealHeight, 0), 5f);
        Quaternion targetUpwardsRot = Quaternion.LookRotation(player.transform.forward, Vector3.up) * Quaternion.Euler(-90f, 0f, 0);
        card.RotateTo(targetUpwardsRot, 5f);

        yield return new WaitForSeconds(cardViewingTime);

        card.MoveTo(originalPos, 5f);
        card.RotateTo(originalRot, 5f);

        player.TryAddSeenCard(card);
    }

    /// <summary>
    /// Reveals the hand of a given player
    /// </summary>
    private IEnumerator RevealPlayerHand(CambioPlayer player)
    {
        for (int i = 0; i < player.Hand.Cards.Count; i++)
        {
            PlayingCard card = player.Hand.GetCard(i);

            StartCoroutine(RevealCardCoroutine(card, player, card.transform.position));
        }

        yield return new WaitForSeconds(cardViewingTime);
    }

    /// <summary>
    /// Deals a card to the player, shows it to them and allows for them to decide what to do
    /// </summary>
    protected override IEnumerator DealCardToPlayer(CambioPlayer player)
    {
        //Draw new card
        drawnCard = DrawCard();

        if (drawnCard.Suit == Suit.Joker) JumpscareManager.Instance.Trigger("Clown", player.transform);

        // Position card in front of current player
        BringCardToPlayer(currentPlayer, drawnCard, cardPullPositionOffset);

        SetInteractDisplayOnDrawCard(player);

        yield return new WaitForEndOfFrame();
        yield return new WaitUntil(() => drawnCard.IsMoving == false);

        //Enable interaction for player or handle decision for AI
        if (!player.IsAI)
        {
            TablePlayerSendParams sendParams = player.SendParams;

            //Set hand and drawn card interactable for the player client, and subscribe them to the event for card drawing
            InteractionManager.RequestSetHandInteraction(sendParams, true, new CambioActionData(CambioActionType.Draw, false, currentPlayer.TablePlayerID));
            InteractionManager.RequestSetCardInteraction(sendParams, drawnCard.NetworkObjectId, true, new CambioActionData(CambioActionType.Draw, false, currentPlayer.TablePlayerID));
        }
        else StartCoroutine(HandleAIDrawDecision());
    }

    /// <summary>
    /// Sets the interact display of each card when drawing a card
    /// </summary>
    private void SetInteractDisplayOnDrawCard(CambioPlayer player)
    {
        drawnCard.Interactable.SetDisplay(new InteractDisplay("", true, "Discard", GetAbilityString(drawnCard)));
        player.SetHandInteractDisplay(new InteractDisplay("", true, "Swap Card", $"Swap card for the {drawnCard.ToString()}"));
    }

    #endregion

    #region Game Ended

    /// <summary>
    /// Checks for game end, ends when no players are left playing
    /// </summary>
    protected override bool HasGameEnded()
    {
        foreach (var player in Players)
        {
            if (player.IsPlaying()) return false;
        }
        return true;
    }



    /// <summary>
    /// Shows the winner
    /// </summary>
    protected override IEnumerator ShowWinnerRoutine()
    {
        // Reveal all cards and calculate scores
        var playerScores = new Dictionary<CambioPlayer, int>();

        foreach (var player in Players)
        {
            int score = 0;
            foreach (var card in player.Hand.Cards)
            {
                card.FlipCard();
                score += CambioPlayer.GetCardValue(card);
            }

            playerScores[player] = score;
            ConsoleLog.Instance.Log($"{player.GetName()} has a score of {score}");
            ShowScoreClientRpc(player.TablePlayerID, score);

            yield return new WaitForSeconds(timeBetweenPlayerReveals);
        }

        // Find winner
        int lowestScore = int.MaxValue;
        CambioPlayer winner = null;
        foreach (var kvp in playerScores)
        {
            if (kvp.Value < lowestScore)
            {
                lowestScore = kvp.Value;
                winner = kvp.Key;
            }
        }

        if (winner != null)
        {
            ConsoleLog.Instance.Log($"{winner.GetName()} wins with score {lowestScore}!");
        }

        yield return new WaitForSeconds(3f);
        ServerEndGame();
    }

    [ClientRpc]
    private void ShowScoreClientRpc(ulong playerId, int score)
    {
        GetPlayerFromPlayerID(playerId)?.ShowScore(score);
    }

    #endregion

    #region Action Handling
    protected override IEnumerator ExecuteActionRoutine(CambioActionData action)
    {
        if (!IsServer) yield break;

        yield return StartCoroutine(base.ExecuteActionRoutine(action));

        ConsoleLog.Instance.Log($"{currentPlayer.GetName()} has executed Action: " + action.Type);

        CambioPlayer player = GetPlayerFromPlayerID(action.PlayerId);
        PlayingCard playerCard = PlayingCard.GetPlayingCardFromNetworkID(action.CardId);
        CambioPlayer targetPlayer = GetPlayerFromPlayerID(action.TargetPlayerId);
        PlayingCard targetCard = PlayingCard.GetPlayingCardFromNetworkID(action.TargetCardId);

        switch (action.Type)
        {
            case CambioActionType.None:
                break;

            case CambioActionType.CallCambio:
                player.CallCambio();
                break;

            case CambioActionType.Draw:
                StartCoroutine(DealCardToPlayer(player));
                break;

            case CambioActionType.Discard:
                PlaceCardOnPile(drawnCard);
                yield return new WaitForSeconds(0.5f);
                DoCardAbility();
                yield break;

            case CambioActionType.TradeCard:
                if (TryTradeCard(targetPlayer, targetCard, playerCard)) targetPlayer.TryAddSeenCard(targetCard);
                break;

            case CambioActionType.RevealCard:
                if (targetPlayer.TablePlayerID == CurrentPlayerTurnID.Value) yield return StartCoroutine(RevealCardCoroutine(targetCard, targetPlayer, targetCard.transform.position)); //If revealing your own card
                else yield return StartCoroutine(RevealCardCoroutine(targetCard, player, currentPlayer.transform.position)); //Revealing someone elses card
                break;

            case CambioActionType.SwapHand:
                SwapHands(player, targetPlayer);
                break;

            case CambioActionType.SwapCard:
                TrySwapCards(player, playerCard, targetPlayer, targetCard);
                break;

            case CambioActionType.RevealHand:
                yield return StartCoroutine(RevealPlayerHand(targetPlayer));
                break;

            case CambioActionType.SelectCard:
                SelectCard(targetCard);
                yield break;

            case CambioActionType.CompareCards:
                BringCardsToPlayerToChoose(player, playerCard, targetCard);
                player.TryAddSeenCard(targetCard);
                if (!currentPlayer.IsAI)
                {
                    TablePlayerSendParams sendParams = currentPlayer.SendParams;

                    InteractionManager.RequestSetCardInteraction(sendParams, playerCard.NetworkObjectId, true, new CambioActionData(CambioActionType.CompareCards, true, currentPlayer.TablePlayerID));
                    InteractionManager.RequestSetCardInteraction(sendParams, targetCard.NetworkObjectId, true, new CambioActionData(CambioActionType.CompareCards, true, currentPlayer.TablePlayerID));
                }
                else
                {
                    selectedCards.Add(playerCard);
                    selectedCards.Add(targetCard);
                    StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.ChooseCard, false, currentPlayer.TablePlayerID, 0, 0, currentPlayer.PlayerAI.ChooseBetween2Cards(playerCard, targetCard).NetworkObjectId)));
                }
                yield break;
            case CambioActionType.ChooseCard:
                ChooseCard(targetCard);
                yield break;
            case CambioActionType.Stack:
                StackCard(player, targetCard);
                yield break;
            case CambioActionType.GiveCard:
                GiveStackCard(targetCard);
                yield break;
        }

        yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);

        if (action.EndsTurn) NextTurn();
    }



    /// <summary>
    /// Will enable interaction for the player to execute the card ability, AI will ask for decision before executing
    /// </summary>
    private void DoCardAbility()
    {
        PlayingCard abilityCard = cardPile[cardPile.Count - 1];
        int cardValue = CambioPlayer.GetCardValue(abilityCard);

        ConsoleLog.Instance.Log("Do card ability with card: " + abilityCard.ToString());

        if (currentPlayer.IsAI)
        {
            CambioActionData abilityAction = currentPlayer.PlayerAI.DecideAction(TurnContext.CardAbility);
            StartCoroutine(ExecuteActionRoutine(abilityAction));
            return;
        }

        //Does not enable the skip turn button if the card is below 6
        if (cardValue >= 6 && cardValue != 13) currentPlayer.RequestEnableAbilityStartedInteraction();

        switch (cardValue)
        {
            case 6:
            case 7:
                Debug.Log("Look at your own card!");
                InteractionManager.RequestSetHandInteraction(currentPlayer.SendParams, true, new CambioActionData(CambioActionType.RevealCard, true, currentPlayer.TablePlayerID));
                currentPlayer.SetHandInteractDisplay(new InteractDisplay("", true, "Reveal Card", "Shows you the playing card"));
                return;
            case 8:
            case 9:
                Debug.Log("Look at someone elses card!");
                InteractionManager.RequestSetHandInteraction(currentPlayer.SendParams, false);
                foreach (var player in Players)
                {
                    if (player == currentPlayer) continue;

                    InteractionManager.RequestSetHandInteraction(player.SendParams, true, new CambioActionData(CambioActionType.RevealCard, true, currentPlayer.TablePlayerID));
                    player.SetHandInteractDisplay(new InteractDisplay("", true, "Reveal Card", "Shows you the playing card"));
                }
                return;
            case 10:
                Debug.Log("Swap entire hands!");
                InteractionManager.RequestSetHandInteraction(currentPlayer.SendParams, false);
                foreach (var player in Players)
                {
                    if (player == currentPlayer) continue;

                    InteractionManager.RequestSetHandInteraction(player.SendParams, true, new CambioActionData(CambioActionType.SwapHand, true, currentPlayer.TablePlayerID));
                    player.SetHandInteractDisplay(new InteractDisplay("", true, "Swap Hand", $"Swap your whole hand with {player.GetName()}"));
                }
                return;
            case 11:
                Debug.Log("Choose 2 cards to decide to swap");
                InteractionManager.RequestSetHandInteraction(currentPlayer.SendParams, true, new CambioActionData(CambioActionType.SelectCard, false, currentPlayer.TablePlayerID));
                currentPlayer.SetHandInteractDisplay(new InteractDisplay("", true, "Compare Cards", "Select a card to compare"));
                return;
            case 12:
                Debug.Log("Blind swap!");
                InteractionManager.RequestSetHandInteraction(currentPlayer.SendParams, true, new CambioActionData(CambioActionType.SelectCard, false, currentPlayer.TablePlayerID));
                currentPlayer.SetHandInteractDisplay(new InteractDisplay("", true, "Blind Swap", "Select a card to swap"));
                return;

            case 13:
                Debug.Log("Look at all your cards!");
                StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.RevealHand, true, currentPlayer.TablePlayerID, 0, currentPlayer.TablePlayerID, 0)));
                return;
        }

        NextTurn();
    }

    #endregion

    #region Card Management

    /// <summary>
    /// Selects a card to be compared or swapped.
    /// </summary>
    private void SelectCard(PlayingCard card)
    {
        selectedCards.Add(card);
        LiftCard(card, cardLiftHeight);

        //If you have picked 2 cards, do the ability
        if (selectedCards.Count > 1)
        {
            int value = CambioPlayer.GetCardValue(cardPile[cardPile.Count - 1]);

            if (value == 11) //CHOOSE BETWEEN CARDS
            {
                StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.CompareCards, false, currentPlayer.TablePlayerID, selectedCards[0].NetworkObjectId, 0, selectedCards[1].NetworkObjectId)));
            }

            else if (value == 12) //SWAP CARDS
            {
                PlayingCard playerCardChoice = selectedCards.First(c => GetPlayerWithCard(c) == currentPlayer);
                PlayingCard otherCardChoice = selectedCards.First(c => GetPlayerWithCard(c) != currentPlayer);

                CambioPlayer otherPlayer = GetPlayerWithCard(otherCardChoice);

                StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.SwapCard, true, currentPlayer.TablePlayerID, playerCardChoice.NetworkObjectId, otherPlayer.TablePlayerID,otherCardChoice.NetworkObjectId)));
            }
        }
        else if (selectedCards.Count == 1)
        {
            ConsoleLog.Instance.Log("Select another players card!");

            CambioPlayer playerWithCard = GetPlayerWithCard(card);
            if (playerWithCard.TablePlayerID == currentPlayer.TablePlayerID)
            {
                foreach (var player in Players)
                {
                    if (player == currentPlayer) continue;

                    TablePlayerSendParams sendParams = new TablePlayerSendParams(player.PlayerData.OwnerClientId, player.TablePlayerID);
                    InteractionManager.RequestSetHandInteraction(sendParams, true, new CambioActionData(CambioActionType.SelectCard, false, currentPlayer.TablePlayerID));
                }
            }
        }
    }

    /// <summary>
    /// Current player has chosen a card to keep
    /// </summary>
    private void ChooseCard(PlayingCard chosenCard)
    {
        PlayingCard otherCard = selectedCards.First(c => c != chosenCard);
        selectedCards.Clear();

        //If they chose their current card do nothing
        if (currentPlayer.Hand.Cards.Contains(chosenCard))
        {
            StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.None, true, currentPlayer.TablePlayerID)));
        }
        else
        {
            CambioPlayer otherPlayer = GetPlayerWithCard(chosenCard);
            StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.SwapCard, true, currentPlayer.TablePlayerID, otherCard.NetworkObjectId, otherPlayer.TablePlayerID, chosenCard.NetworkObjectId)));
        }

    }

    /// <summary>
    /// Brings 2 cards to the given player to chooose from
    /// </summary>
    private void BringCardsToPlayerToChoose(CambioPlayer player, PlayingCard card1, PlayingCard card2)
    {
        BringCardToPlayer(player, card1, new Vector3(0, cardPullPositionOffset.y, cardPullPositionOffset.z) + (player.transform.right * -0.2f));
        BringCardToPlayer(player, card2, new Vector3(0, cardPullPositionOffset.y, cardPullPositionOffset.z) + (player.transform.right * 0.2f));
    }

    #endregion

    #region Stacking

    /// <summary>
    /// Called when a player stacks a card
    /// </summary>
    private void StackCard(CambioPlayer playerWhoStacked, PlayingCard cardToStack)
    {
        if (hasStacked) return;

        CambioPlayer playerWithCard = GetPlayerWithCard(cardToStack);

        if (!playerWithCard.RemoveCardFromHand(cardToStack)) return;

        hasStacked = true;

        DisableAllCardsAndUnsubscribe();

        StartCoroutine(StackCoroutine(playerWithCard, playerWhoStacked, cardToStack));
    }

    private IEnumerator StackCoroutine(CambioPlayer playerWithCard, CambioPlayer playerWhoStacked, PlayingCard cardToStack)
    {
        bool isCorrect = cardToStack.GetValue(false) == cardPile[cardPile.Count - 1].GetValue(false);


        PlaceCardOnPile(cardToStack);

        yield return new WaitForSeconds(1f);

        if (playerWithCard.TablePlayerID == playerWhoStacked.TablePlayerID)
        {
            ConsoleLog.Instance.Log($"{playerWithCard.GetName()} tried stacking their own card!");
            if (!isCorrect) //DEAL CARD BACK TO PLAYER AND ANOTHER ONE
            {
                yield return StartCoroutine(ReturnCardFromPile(playerWithCard));

                yield return base.DealCardToPlayer(playerWithCard);
            }
        }
        else
        {
            ConsoleLog.Instance.Log($"{playerWhoStacked.GetName()} tried stacking {playerWithCard.GetName()}'s card!");

            if (!isCorrect)
            {
                yield return StartCoroutine(ReturnCardFromPile(playerWhoStacked));
            }
            else
            {
                waitingForStackInput = true;
                playerToRecieveCard = playerWithCard;

                TablePlayerSendParams sendParams = new TablePlayerSendParams(playerWhoStacked.PlayerData.OwnerClientId, playerWhoStacked.TablePlayerID);

                //Player who stacked can give one of their cards to the other player
                InteractionManager.RequestSetHandInteraction(sendParams, true, new CambioActionData(CambioActionType.GiveCard, false, playerWhoStacked.TablePlayerID));

                yield return new WaitUntil(() => waitingForStackInput == false);
            }
        }

        hasStacked = false;
    }


    private void GiveStackCard(PlayingCard card)
    {
        playerToRecieveCard.AddCardToHand(card);
        waitingForStackInput = false;
    }

    #endregion

    #region Utility

    /// <summary>
    /// Gets the string ability for a given card
    /// </summary>
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

    #endregion
}
