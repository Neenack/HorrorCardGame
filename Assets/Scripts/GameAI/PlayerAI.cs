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

public abstract class PlayerAI<TPlayer, TAction, TAI>
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{

    protected TPlayer player;

    public PlayerAI(TPlayer playerRef)
    {
        player = playerRef;
    }

    /// <summary>
    /// Called when the AI needs to decide on an action with some new info.
    /// Override as needed per game.
    /// </summary>
    public abstract TAction DecideAction(TurnContext context);

    /// <summary>
    /// Returns a random other player in the game.
    /// </summary>
    protected TPlayer GetRandomPlayer()
    {
        var playersToChoose = player.Game.Players
            .Where(p => !p.Equals(player))
            .ToList();

        if (playersToChoose.Count == 0)
        {
            Debug.LogWarning("No other players available!");
            return null;
        }

        TPlayer randomPlayer = playersToChoose[UnityEngine.Random.Range(0, playersToChoose.Count)];

        return randomPlayer;
    }


}