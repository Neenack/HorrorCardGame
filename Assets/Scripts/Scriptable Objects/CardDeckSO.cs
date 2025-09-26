using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewCardDeck", menuName = "Cards/Card Deck")]
public class CardDeckSO : ScriptableObject
{
    [Header("General Card Settings")]
    public string deckName;
    public List<PlayingCardSO> cards = new List<PlayingCardSO>();
}
