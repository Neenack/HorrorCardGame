using System;
using Unity.Netcode;
using UnityEngine;

public interface ICambioGame : ICardGame<CambioPlayer, CambioActionData, CambioPlayerAI>
{
    public event Action OnAbilityStarted;

    public NetworkVariable<bool> IsStacking { get; }
}
