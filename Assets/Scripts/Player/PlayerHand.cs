using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerHand
{
    public event Action OnHandUpdated;
    public event Action<PlayingCard> OnShowAnyCard;

    private List<PlayingCard> cards = new List<PlayingCard>();

    public List<PlayingCard> Cards => cards;

    public PlayerHand() { }

    /// <summary>
    /// Adds a card to the  players hand
    /// </summary>
    public void AddCard(PlayingCard card)
    {
        if (card == null) return;

        cards.Add(card);
        card.OnShowCard += Card_OnShowCard;

        OnHandUpdated?.Invoke();
    }



    /// <summary>
    /// Inserts a card into the players hand
    /// </summary>
    public void InsertCard(PlayingCard card, int index)
    {
        if (card == null) return;

        cards.Insert(index, card);
        card.OnShowCard += Card_OnShowCard;

        OnHandUpdated?.Invoke();
    }


    /// <summary>
    /// Removes a card from the players hand
    /// </summary>
    public bool RemoveCard(PlayingCard cardToRemove)
    {
        if (cards.Count == 0 || cardToRemove == null)
            return false;

        // Find the first matching card by instance or CardSO
        var foundCard = cards.FirstOrDefault(c => c == cardToRemove || c.CardSO == cardToRemove.CardSO);

        if (foundCard == null)
            return false;

        foundCard.OnShowCard -= Card_OnShowCard;
        cards.Remove(foundCard);

        OnHandUpdated?.Invoke();

        return true;
    }

    /// <summary>
    /// Clears the players hand -- does not destroy cards
    /// </summary>
    public void ClearHand()
    {
        foreach (var card in cards)
        {
            card.OnShowCard -= Card_OnShowCard;
        }

        cards.Clear();
        OnHandUpdated?.Invoke();
    }

    /// <summary>
    /// Updates the players hand positions
    /// </summary>
    public void UpdateHand()
    {
        OnHandUpdated?.Invoke();
    }


    /// <summary>
    /// Flips all the cards in the players hand
    /// </summary>
    public void ShowCards()
    {
        foreach (var card in cards)
        {
            card.FlipCard();
        }
    }



    /// <summary>
    /// Flips a given card from the index
    /// </summary>
    public void ShowCard(int index)
    {
        if (index < 0 || index >= cards.Count) return;

        cards[index].FlipCard();
    }


    /// <summary>
    /// Flips a given card in the players hand
    /// </summary>
    public void ShowCard(PlayingCard card)
    {
        if (card == null || !cards.Contains(card)) return;

        card.FlipCard();
    }



    /// <summary>
    /// Gets the playing card from the index
    /// </summary>
    public PlayingCard GetCard(int index)
    {
        if (index < 0 || index >= cards.Count) return null;

        return cards[index];
    }


    /// <summary>
    /// Gets the index of a given card
    /// </summary>
    public int GetIndexOfCard(PlayingCard card) => cards.IndexOf(card);



    /// <summary>
    /// Gets a random card from the hand
    /// </summary>
    public PlayingCard GetRandomCard()
    {
        return cards.Count > 0 ? GetCard(UnityEngine.Random.Range(0, cards.Count)) : null;
    }



    /// <summary>
    /// When any card is shown it invokes the event
    /// </summary>
    private void Card_OnShowCard(PlayingCard card)
    {
        OnShowAnyCard?.Invoke(card);
    }



    /// <summary>
    /// Outputs the players name and all of their cards
    /// </summary>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"Hand:");

        foreach (var card in cards)
        {
            if (card != null)
            {
                sb.AppendLine(card.ToString());
            }
        }

        return sb.ToString();
    }
}
