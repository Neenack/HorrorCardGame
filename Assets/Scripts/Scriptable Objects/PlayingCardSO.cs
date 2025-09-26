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
    [SerializeField] private Sprite cardSprite;

    [Header("Card Value")]
    [SerializeField] private Suit suit;
    [SerializeField] private int value;

    public Sprite GetSprite() => cardSprite;
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

    public PlayingCard SpawnCard(Transform pos)
    {
        Quaternion rotation = Quaternion.LookRotation(pos.forward, Vector3.up);
        Transform newCard = Instantiate(this.cardPrefab, pos.position, rotation, pos);

        newCard.GetComponent<NetworkObject>().Spawn(true);

        if (newCard.TryGetComponent(out PlayingCard playingCard))
        {
            playingCard.SetCard(this);
            return playingCard;
        }
        return null;
    }
}