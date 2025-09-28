using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem.XR.Haptics;

public interface ICardGame<TPlayer, TAction, TAI>
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{
    event Action OnGameStarted;
    event Action OnGameEnded;

    IEnumerable<TPlayer> Players { get; }
    IInteractable InteractableDeck { get; }
    NetworkVariable<ulong> PileCardID { get; }
    NetworkVariable<ulong> DrawnCardID { get; }
    NetworkVariable<ulong> CurrentPlayerTurnID { get; }
    NetworkVariable<ulong> CurrentOwnerClientTurnID { get; }

    void TryExecuteAction(ulong playerID, TAction action);
    TPlayer GetPlayerWithCard(ulong cardNetworkId);

}
