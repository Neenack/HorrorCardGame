using System.Linq;
using UnityEngine;

public enum TurnContext
{
    StartTurn,
    AfterDraw,
    CardAbility,
    AfterTurn
}

public enum Difficulty
{
    Easy, Normal, Expert
}

public abstract class PlayerAI<TPlayer, TAction, TAI>
    where TPlayer : TablePlayer<TPlayer, TAction, TAI>
    where TAction : struct
    where TAI : PlayerAI<TPlayer, TAction, TAI>
{
    protected TPlayer player;
    protected Difficulty difficulty;

    private const float EASY_MISPLAY_CHANCE = 0.3f;
    private const float NORMAL_MISPLAY_CHANCE = 0.2f;
    private const float EXPERT_MISPLAY_CHANCE = 0.0f;

    public PlayerAI(TPlayer playerRef, Difficulty difficulty)
    {
        player = playerRef;
        this.difficulty = difficulty;

        Debug.Log("Created AI with difficulty: " + difficulty.ToString());
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

    /// <summary>
    /// Checks whether the AI should missplay their action
    /// </summary>
    /// <returns> True if it should missplay </returns>
    protected bool WillMisplay() => UnityEngine.Random.value < GetMisplayChance();


    private float GetMisplayChance() => difficulty switch
    {
        Difficulty.Easy => EASY_MISPLAY_CHANCE,
        Difficulty.Normal => NORMAL_MISPLAY_CHANCE,
        Difficulty.Expert => EXPERT_MISPLAY_CHANCE,
        _ => 0f
    };



    /// <summary>
    /// Returns a random card in a players hand
    /// </summary>
    /// <returns>A random playing card</returns>
    protected PlayingCard GetRandomCard(CambioPlayer player) => player.Hand.GetRandomCard();
}