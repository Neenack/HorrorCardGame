using System.Threading.Tasks;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public enum Suit
{
    Hearts, Diamonds, Spades, Clubs
}

[CreateAssetMenu(fileName = "NewCard", menuName = "Cards/Playing Card")]
public class PlayingCardSO : ScriptableObject
{
    [Header("Card Prefab")]
    [SerializeField] private Transform cardPrefab;
    [SerializeField] private Texture2D cardTexture;

    [Header("Card Value")]
    [SerializeField] private Suit suit;
    [SerializeField] private int value;

    public Texture2D GetTexture() => cardTexture;
    public Suit Suit => suit;
    public int Value => value;

    public string GetCardName()
    {
        string rankName = value switch
        {
            1 => "Ace",
            11 => "Jack",
            12 => "Queen",
            13 => "King",
            _ => value.ToString()
        };
        return rankName + ($" of {suit}");
    }

    public PlayingCard SpawnCard(Transform pos, bool isFaceDown = true)
    {
        Transform newCard = Instantiate(this.cardPrefab, pos.position, Quaternion.identity);

        newCard.GetComponent<NetworkObject>().Spawn(true);
        newCard.SetParent(pos);

        if (!newCard.TryGetComponent(out PlayingCard playingCard)) playingCard = newCard.AddComponent<PlayingCard>();
        playingCard.SetCard(this);

        return playingCard;
    }
}