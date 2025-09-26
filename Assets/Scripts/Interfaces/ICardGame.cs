using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.XR.Haptics;

public interface ICardGame<TAction> where TAction : class
{
    event Action OnGameStarted;
    event Action OnGameEnded;

    void NextTurn();

    IEnumerable<TablePlayer<TAction>> Players { get; }
    IInteractable InteractableDeck { get; }
    PlayingCard TopPileCard { get; }
    NetworkVariable<ulong> CurrentTurnID { get; }


    void PlaceCardOnPile(PlayingCard card, bool placeFaceDown = false, float lerpSpeed = 5f);
    IEnumerator TryExecuteAction(TAction action);

}
