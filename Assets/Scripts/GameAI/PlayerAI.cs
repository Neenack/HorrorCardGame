using System.Linq;
using UnityEngine;
using static UnityEditor.Experimental.GraphView.GraphView;

public enum TurnContext
{
    StartTurn,
    AfterDraw,
    CardAbility,
    AfterTurn
}

public abstract class PlayerAI<TAction> where TAction : class
{
    protected TablePlayer<TAction> player;

    public PlayerAI(TablePlayer<TAction> playerRef)
    {
        player = playerRef;
    }

    /// <summary>
    /// Called when the AI needs to decide on an action with some new info (e.g., picked up card).
    /// Override as needed per game.
    /// </summary>
    public abstract TAction DecideAction(TurnContext context, object extra = null);

    /// <summary>
    /// Returns a random other player of the same type.
    /// </summary>
    protected TablePlayer<TAction> GetRandomPlayer()
    {
        var playersToChoose = player.Game.Players
            .Where(p => !p.Equals(player))
            .ToList();

        if (playersToChoose.Count == 0)
        {
            Debug.LogWarning("No other players available!");
            return null;
        }

        TablePlayer<TAction> randomPlayer = playersToChoose[UnityEngine.Random.Range(0, playersToChoose.Count)];

        return randomPlayer;
    }


}