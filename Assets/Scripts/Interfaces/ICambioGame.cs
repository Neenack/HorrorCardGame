using System;
using UnityEngine;

public interface ICambioGame : ICardGame<CambioPlayer, CambioActionData, CambioPlayerAI>
{
    public event Action OnAbilityStarted;
}
