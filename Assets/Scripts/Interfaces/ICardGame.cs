using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.XR.Haptics;

public interface ICardGameEvents
{
    event Action OnGameStarted;
    event Action OnGameEnded;
    event Action OnAnyActionExecuted;
    event Action OnAnyCardDrawn;
}

public interface ICardGame<TPlayer, TAction, TAI> : ICardGameEvents
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{
    IEnumerable<TPlayer> Players { get; }
    IInteractable InteractableDeck { get; }
    NetworkVariable<ulong> PileCardID { get; }
    NetworkVariable<ulong> DrawnCardID { get; }
    NetworkVariable<ulong> CurrentPlayerTurnID { get; }
    NetworkVariable<ulong> CurrentOwnerClientTurnID { get; }

    void TryExecuteAction(ulong playerID, TAction action);

    TPlayer GetPlayerWithCard(ulong cardNetworkId);
    TPlayer GetPlayerFromData(PlayerData data);

}
