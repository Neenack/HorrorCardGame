using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime deck manager that operates on a CardDeckSO without modifying the asset.
/// </summary>
public class CardDeck
{
    CardDeckSO deckSO;

    private bool shuffleOnStart;

    private List<PlayingCardSO> cards;
    public int Count => cards.Count;

    /// <summary>
    /// Creates a runtime deck from the ScriptableObject
    /// </summary>
    public CardDeck(CardDeckSO deckSO, bool shuffleOnStart = true)
    {
        if (deckSO == null)
        {
            Debug.LogError("RuntimeDeck: CardDeckSO is null!");
            cards = new List<PlayingCardSO>();
        }
        else
        {
            cards = new List<PlayingCardSO>(deckSO.cards);
        }

        this.deckSO = deckSO;
        this.shuffleOnStart = shuffleOnStart;

        if (shuffleOnStart) Shuffle();
    }

    /// <summary>
    /// Shuffle the deck
    /// </summary>
    public void Shuffle()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            int rand = Random.Range(i, cards.Count);
            var temp = cards[i];
            cards[i] = cards[rand];
            cards[rand] = temp;
        }
    }

    /// <summary>
    /// Draw a card from the deck
    /// </summary>
    public PlayingCardSO DrawCard()
    {
        if (cards.Count == 0) return null;

        var card = cards[0];
        cards.RemoveAt(0);

        if (cards.Count == 0) ResetDeck();

        return card;
    }

    /// <summary>
    /// Reset the deck
    /// </summary>
    public void ResetDeck()
    {
        cards = new List<PlayingCardSO>(deckSO.cards);

        if (shuffleOnStart) Shuffle();
    }
}
