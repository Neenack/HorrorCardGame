using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

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

    private NetworkVariable<bool> isStacking = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private List<PlayingCard> selectedCards = new List<PlayingCard>();

    #region Turn Management

    protected override IEnumerator NextTurnRoutine()
    {
        if (!IsServer) yield break;

        selectedCards.Clear();

        //Set last turn if someone has called cambio
        if (currentPlayer) currentPlayer.hasPlayedLastTurn.Value = Players.Any(p => !p.IsPlaying());

        //Stacking
        if (currentPlayer != null && cardStacking)
        {
            yield return StartCoroutine(StackingRoutine());
        }

        StartCoroutine(base.NextTurnRoutine());
    }

    private IEnumerator StackingRoutine()
    {
        Debug.Log("[Server] Stacking enabled!");
        isStacking.Value = true;

        yield return new WaitForSeconds(0.5f);

        foreach (var player in Players)
        {
            if (player.IsAI) continue;
            player.RequestSetStacking(true);
        }

        yield return new WaitForSeconds(stackingTime);

        Debug.Log("[Server] Stacking disabled!");
        isStacking.Value = false;

        DisableAllCardsAndUnsubscribe();
    }

    protected override bool CanOnlyPlayInTurn() => !isStacking.Value;

    #endregion

    #region Card Dealing

    protected override IEnumerator DealInitialCards()
    {
        if (!IsServer) yield break;

        yield return StartCoroutine(CardPooler.Instance.InitializePool());

        // Deal 4 cards to each player
        for (int i = 0; i < 4; i++)
        {
            foreach (var player in players)
            {
                StartCoroutine(base.DealCardToPlayer(player));
                yield return new WaitForSeconds(timeBetweenCardDeals);
            }
        }

        yield return new WaitForSeconds(1f);

        // Reveal initial cards (positions 0 and 2)
        foreach (var player in players)
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

    private IEnumerator RevealCardCoroutine(PlayingCard card, CambioPlayer player, Vector3 basePos)
    {
        Vector3 originalPos = card.transform.position;
        Quaternion originalRot = card.transform.rotation;

        card.MoveTo(basePos + new Vector3(0, cardRevealHeight, 0), 5f);
        Quaternion targetUpwardsRot = Quaternion.LookRotation(player.transform.forward, Vector3.up) * Quaternion.Euler(90f, 0f, 0);
        card.RotateTo(targetUpwardsRot, 5f);

        yield return new WaitForSeconds(cardViewingTime);

        card.MoveTo(originalPos, 5f);
        card.RotateTo(originalRot, 5f);

        player.TryAddSeenCard(card);
    }
    private IEnumerator RevealPlayerHand(CambioPlayer player)
    {
        for (int i = 0; i < player.Hand.Cards.Count; i++)
        {
            PlayingCard card = player.Hand.GetCard(i);

            StartCoroutine(RevealCardCoroutine(card, player, card.transform.position));
        }

        yield return new WaitForSeconds(cardViewingTime);
    }


    protected override IEnumerator DealCardToPlayer(CambioPlayer player)
    {
        //Draw new card
        drawnCard = DrawCard();

        // Position card in front of current player
        BringCardToPlayer(currentPlayer, drawnCard, cardPullPositionOffset);

        yield return new WaitForEndOfFrame();
        yield return new WaitUntil(() => drawnCard.IsMoving == false);

        //Enable interaction for player or handle decision for AI
        if (!player.IsAI)
        {
            //Set hand and drawn card interactable for the player client, and subscribe them to the event for card drawing
            player.RequestSetHandInteractable(true, new CambioActionData(CambioActionType.Draw, false, currentPlayer.PlayerId));
            player.RequestSetCardInteractable(drawnCard.NetworkObjectId, true, new CambioActionData(CambioActionType.Draw, false, currentPlayer.PlayerId));
        }
        else StartCoroutine(HandleAIDrawDecision());
    }

    #endregion

    #region Game Ended

    //Game ends when there is nobody left playing
    protected override bool HasGameEnded() => !players.Exists(player => player.IsPlaying());

    protected override IEnumerator ShowWinnerRoutine()
    {
        // Reveal all cards and calculate scores
        var playerScores = new Dictionary<CambioPlayer, int>();

        foreach (var player in players)
        {
            int score = 0;
            foreach (var card in player.Hand.Cards)
            {
                card.FlipCard();
                score += player.GetCardValue(card);
            }

            playerScores[player] = score;
            //SetScoreClientRpc(player.OwnerClientId, score);

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
            Debug.Log($"{winner.GetName()} wins with score {lowestScore}!");
            //AnnounceWinnerClientRpc(winner.NetworkObjectId, lowestScore);
        }

        yield return new WaitForSeconds(3f);
        ServerEndGame();
    }

    #endregion

    #region Action Handling

    protected override IEnumerator ExecuteActionRoutine(CambioActionData action)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Client] Only the server can execute actions!");
            yield break;
        }

        yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);

        if (currentPlayer.IsAI)
            Debug.Log($"[Server] AI Player has executed Action: " + action.Type);
        else
            Debug.Log($"[Server] Player {currentPlayerTurnId.Value} has executed Action: " + action.Type);

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
                yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);
                DoCardAbility();
                yield break;

            case CambioActionType.TradeCard:
                if (TryTradeCard(targetPlayer, targetCard, playerCard)) targetPlayer.TryAddSeenCard(targetCard);
                break;

            case CambioActionType.RevealCard:
                if (targetPlayer.PlayerId == player.PlayerId) yield return StartCoroutine(RevealCardCoroutine(targetCard, targetPlayer, targetCard.transform.position)); //If revealing your own card
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
                if (!currentPlayer.IsAI)
                {
                    currentPlayer.RequestSetCardInteractable(playerCard.NetworkObjectId, true, new CambioActionData(CambioActionType.CompareCards, true, currentPlayer.PlayerId));
                    currentPlayer.RequestSetCardInteractable(targetCard.NetworkObjectId, true, new CambioActionData(CambioActionType.CompareCards, true, currentPlayer.PlayerId));
                }
                else
                {
                    selectedCards.Add(playerCard);
                    selectedCards.Add(targetCard);
                    StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.ChooseCard, false, currentPlayer.PlayerId, 0, 0, currentPlayer.PlayerAI.ChooseBetween2Cards(playerCard, targetCard).NetworkObjectId)));
                }
                yield break;
            case CambioActionType.ChooseCard:
                ChooseCard(targetCard);
                yield break;
            case CambioActionType.Stack:
                StackCard(player, targetCard);
                yield break;
        }

        yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);

        if (action.EndsTurn) NextTurn();
    }

    private void DoCardAbility()
    {
        PlayingCard abilityCard = cardPile[cardPile.Count - 1];
        int cardValue = currentPlayer.GetCardValue(abilityCard);

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
            case < 6:
                NextTurn();
                break;
            case 6:
            case 7:
                Debug.Log("Look at your own card!");
                currentPlayer.RequestSetHandInteractable(true, new CambioActionData(CambioActionType.RevealCard, true, currentPlayer.PlayerId));
                break;
            case 8:
            case 9:
                Debug.Log("Look at someone elses card!");
                currentPlayer.RequestSetHandInteractable(false);
                foreach (var player in players)
                {
                    if (player == currentPlayer) continue;
                    player.RequestSetHandInteractable(true, new CambioActionData(CambioActionType.RevealCard, true, currentPlayer.PlayerId));
                }
                break;
            case 10:
                Debug.Log("Swap entire hands!");
                currentPlayer.RequestSetHandInteractable(false);
                foreach (var player in players)
                {
                    if (player == currentPlayer) continue;
                    player.RequestSetHandInteractable(true, new CambioActionData(CambioActionType.SwapHand, true, currentPlayer.PlayerId));
                }
                break;
            case 11:
                Debug.Log("Choose 2 cards to decide to swap");
                currentPlayer.RequestSetHandInteractable(true, new CambioActionData(CambioActionType.SelectCard, false, currentPlayer.PlayerId));
                break;
            case 12:
                Debug.Log("Blind swap!");
                currentPlayer.RequestSetHandInteractable(true, new CambioActionData(CambioActionType.SelectCard, false, currentPlayer.PlayerId));
                break;

            case 13:
                Debug.Log("Look at all your cards!");
                StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.RevealHand, true, currentPlayer.PlayerId, 0, currentPlayer.PlayerId, 0)));
                break;
        }
    }

    #endregion

    #region Card Management

    private void SelectCard(PlayingCard card)
    {
        selectedCards.Add(card);
        LiftCard(card, cardLiftHeight);

        //If you have picked 2 cards, do the ability
        if (selectedCards.Count == 2)
        {
            int value = currentPlayer.GetCardValue(cardPile[cardPile.Count - 1]);

            if (value == 11) //CHOOSE BETWEEN CARDS
            {
                StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.CompareCards, false, currentPlayer.PlayerId, selectedCards[0].NetworkObjectId, 0, selectedCards[1].NetworkObjectId)));
            }

            else if (value == 12) //SWAP CARDS
            {
                PlayingCard playerCardChoice = selectedCards.First(c => GetPlayerWithCard(c) == currentPlayer);
                PlayingCard otherCardChoice = selectedCards.First(c => GetPlayerWithCard(c) != currentPlayer);

                CambioPlayer otherPlayer = GetPlayerWithCard(otherCardChoice);

                StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.SwapCard, true, currentPlayer.PlayerId, playerCardChoice.NetworkObjectId, otherPlayer.PlayerId,otherCardChoice.NetworkObjectId)));
            }
        }
        else if (selectedCards.Count == 1)
        {
            Debug.Log("[Server] Select another players card!");

            CambioPlayer playerWithCard = GetPlayerWithCard(card);
            if (playerWithCard.PlayerId == currentPlayer.PlayerId)
            {
                foreach (var player in players)
                {
                    if (player == currentPlayer) continue;
                    player.RequestSetHandInteractable(true, new CambioActionData(CambioActionType.SelectCard, false, currentPlayer.PlayerId));
                }
            }
        }
    }

    //Called after given the choice between the selected cards and has chosen one
    private void ChooseCard(PlayingCard chosenCard)
    {
        PlayingCard otherCard = selectedCards.First(c => c != chosenCard);

        //If they chose their current card do nothing
        if (currentPlayer.Hand.Cards.Contains(chosenCard))
        {
            StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.None, true, currentPlayer.PlayerId)));
        }
        else
        {
            CambioPlayer otherPlayer = GetPlayerWithCard(chosenCard);
            StartCoroutine(ExecuteActionRoutine(new CambioActionData(CambioActionType.SwapCard, true, currentPlayer.PlayerId, otherCard.NetworkObjectId, otherPlayer.PlayerId, chosenCard.NetworkObjectId)));
        }

    }

    private void BringCardsToPlayerToChoose(CambioPlayer player, PlayingCard card1, PlayingCard card2)
    {
        BringCardToPlayer(player, card1, new Vector3(-0.2f, cardPullPositionOffset.y, cardPullPositionOffset.z));
        BringCardToPlayer(player, card2, new Vector3(0.2f, cardPullPositionOffset.y, cardPullPositionOffset.z));
    }

    #endregion

    #region Stacking

    private void StackCard(CambioPlayer playerWhoStacked, PlayingCard cardToStack)
    {
        CambioPlayer playerWithCard = GetPlayerWithCard(cardToStack);

        if (playerWithCard.PlayerId == playerWhoStacked.PlayerId)
        {
            Debug.Log("[Server] Player tried stacking their own card!");
        }
        else
        {
            Debug.Log("[Server] Player tried stacking someone elses card!");
        }
    }

    #endregion
}
