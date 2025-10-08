using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.XR.Haptics;

public interface ICardGameEvents
{
    event Action OnGameReset;

    event Action OnAnyActionExecuted;
    event Action OnAnyCardDrawn;
    event Action OnAnyCardPlacedOnPile;

    NetworkVariable<GameState> CurrentGameState { get; }
    NetworkVariable<ulong> DrawnCardID { get; }
    NetworkVariable<ulong> PileCardID { get; }
    NetworkVariable<ulong> CurrentPlayerTurnTableID { get; }
    NetworkVariable<ulong> CurrentPlayerTurnClientID { get; }

    bool IsAI(ulong tableID);
}

public interface ICardGame<TPlayer, TAction, TAI> : ICardGameEvents
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{
    IEnumerable<TPlayer> Players { get; }
    IInteractable InteractableDeck { get; }

    void ExecuteAction(ulong playerID, TAction action);

    TPlayer GetPlayerWithCard(ulong cardNetworkId);
    TPlayer GetPlayerFromData(PlayerData data);
    TPlayer GetPlayerFromTablePlayerID(ulong tableID);
    TPlayer GetPlayerFromClientID(ulong localClientID);
    TPlayer GetCurrentTurnPlayer();

}
