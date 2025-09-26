using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public class CambioGame : CardGame<CambioPlayer, CambioAction>
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
                DealCardToPlayerHand(player);
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

                RevealCardToPlayerClientRpc(card1.NetworkObjectId, player.OwnerClientId);
                RevealCardToPlayerClientRpc(card2.NetworkObjectId, player.OwnerClientId);
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

    [ClientRpc]
    private void RevealCardToPlayerClientRpc(ulong cardId, ulong playerId)
    {

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
        EndGame();
    }

    #endregion

    #region Action Handling

    public override IEnumerator TryExecuteAction(CambioAction action)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Server must execute actions!");
            yield break;
        }

        Debug.Log("[Server] Executing Action: " + action.Type);

        yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);

        switch (action.Type)
        {
            case CambioActionType.None:
                if (action.EndsTurn) NextTurn();
                yield break;

            case CambioActionType.CallCambio:
                //currentPlayer.CallCambio();
                NextTurn();
                yield break;

            /*
            case CambioActionType.Draw:
                DrawCard(currentPlayer);
                break;

            case CambioActionType.Discard:
                PlaceCardOnPile(drawnCard);

                yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);

                DoCardAbility();
                yield break;

            case CambioActionType.TradeCard:
                if (TryTradeCard(action.TargetPlayer, action.SwapData.Keep, action.SwapData.Discard))
                {
                    action.TargetPlayer.TryAddSeenCard(action.SwapData.Keep);
                }
                break;

            case CambioActionType.SwapCard:
                TrySwapCards(currentPlayer, action.SwapData.Discard, action.TargetPlayer, action.SwapData.Keep);
                break;

            case CambioActionType.SwapHand:
                SwapHands(currentPlayer, action.TargetPlayer);
                break;

            case CambioActionType.RevealCard:

                //If revealing your own card
                if (action.TargetPlayer == currentPlayer)
                {
                    yield return StartCoroutine(RevealCardCoroutine(action.TargetCard, action.TargetPlayer, action.TargetCard.transform.position));
                }
                else //Revealing someone elses card
                {
                    yield return StartCoroutine(RevealCardCoroutine(action.TargetCard, currentPlayer, currentPlayer.transform.position));
                }
                break;

            case CambioActionType.RevealHand:
                yield return StartCoroutine(RevealWholeHand());
                break;


            case CambioActionType.CompareCards:
                swapEventDictionary.Add(action.SwapData.Keep, action.TargetPlayer);
                swapEventDictionary.Add(action.SwapData.Discard, currentPlayer);
                ChooseSwapEvent_BringCardsToChoose();

                yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);

                PlayingCard choice = currentPlayer.GetCardValue(action.SwapData.Keep) < currentPlayer.GetCardValue(action.SwapData.Discard) ? action.SwapData.Keep : action.SwapData.Discard;
                ChooseSwapEvent_ChooseCardToKeep(choice);

                yield break;

            case CambioActionType.Stack:
                StackCard(action.TargetCard, action.TargetPlayer);
                break;
            */
        }

        yield return new WaitForSeconds(currentPlayer.IsAI ? AIThinkingTime : 0);

        if (action.EndsTurn) NextTurn();
    }

    #endregion
}
