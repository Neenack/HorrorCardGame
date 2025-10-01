using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class CardPooler : NetworkSingleton<CardPooler>
{
    [Header("Pool Settings")]
    [SerializeField] private int minPoolSize = 10;
    [SerializeField] private int refillAmount = 20;
    [SerializeField] private Transform poolParent;
    [SerializeField] private Vector3 poolPosition = new Vector3(0, -10, 0); // Hidden position

    private Queue<PlayingCard> availableCards = new Queue<PlayingCard>();
    private HashSet<PlayingCard> activeCards = new HashSet<PlayingCard>();
    private bool isRefilling = false;
    private CardDeck deck;

    public int AvailableCount => availableCards.Count;
    public int ActiveCount => activeCards.Count;
    public int TotalCards => AvailableCount + ActiveCount;

    public void SetDeck(CardDeck newDeck)
    {
        if (!IsServer)
        {
            Debug.LogError("SetDeck can only be called on the server!");
            return;
        }

        if (newDeck == null)
        {
            Debug.LogError("CardPooler: Cannot set null deck!");
            return;
        }

        deck = newDeck;
        //Debug.Log($"CardPooler: Deck set to {deck}");
    }

    /// <summary>
    /// Get the current deck being used by the pool
    /// </summary>
    public CardDeck GetDeck()
    {
        return deck;
    }


    /// <summary>
    /// Initialize the card pool with the specified number of cards
    /// </summary>
    public IEnumerator InitializePool()
    {
        if (!IsServer || availableCards.Count > 0) yield break;

        ConsoleLog.Instance.Log("Initialising Card Pool");

        yield return StartCoroutine(CreateCards(deck.Count));
    }

    /// <summary>
    /// Create and spawn cards for the pool
    /// </summary>
    private IEnumerator CreateCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            PlayingCardSO cardSO = deck.DrawCard();
            if (cardSO == null) break;

            // Spawn at pool position
            PlayingCard card = cardSO.SpawnCard(poolParent);
            if (card == null) continue;

            // Wait for network spawn to complete
            yield return new WaitUntil(() => card.NetworkObject != null && card.NetworkObject.IsSpawned);

            // Configure the card for pooling
            SetupPooledCard(card);

            // Add to available queue
            availableCards.Enqueue(card);

            // Yield every few cards to prevent frame drops
            if (i % 5 == 0) yield return null;
        }
    }

    /// <summary>
    /// Setup a card for pooling (hide it, disable interactions, etc.)
    /// </summary>
    private void SetupPooledCard(PlayingCard card)
    {
        // Hide the card
        card.transform.position = poolPosition;

        card.gameObject.SetActive(false);

        UpdateCardTransformClientRpc(card.NetworkObjectId, card.transform.position, false);
    }

    /// <summary>
    /// Get a card from the pool
    /// </summary>
    public PlayingCard GetCard(Vector3 position)
    {
        if (!IsServer)
        {
            Debug.LogError("CardPooler.GetCard() can only be called on the server!");
            return null;
        }

        // Check if we need to refill
        if (availableCards.Count <= minPoolSize && !isRefilling)
        {
            StartCoroutine(RefillPool());
        }

        // If no cards available, return null
        if (availableCards.Count == 0)
        {
            Debug.LogWarning("CardPooler: No cards available in pool!");
            return null;
        }

        PlayingCard card = availableCards.Dequeue();
        activeCards.Add(card);

        card.transform.position = position;
        card.gameObject.SetActive(true);

        UpdateCardTransformClientRpc(card.NetworkObjectId, position, true);

        return card;
    }

    [ClientRpc]
    private void UpdateCardTransformClientRpc(ulong cardId, Vector3 position, bool isActive)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardId, out var obj))
        {
            var card = obj.GetComponent<PlayingCard>();
            card.transform.position = position;
            card.gameObject.SetActive(isActive);
        }
    }

    /// <summary>
    /// Return a card to the pool
    /// </summary>
    public void ReturnCard(PlayingCard card)
    {
        if (!IsServer)
        {
            ReturnCardServerRpc(card.NetworkObjectId);
            return;
        }

        if (!activeCards.Contains(card))
        {
            Debug.LogWarning($"CardPooler: Trying to return card {card.name} that wasn't taken from pool!");
            return;
        }

        activeCards.Remove(card);
        availableCards.Enqueue(card);

        SetupPooledCard(card);
    }

    [ServerRpc]
    private void ReturnCardServerRpc(ulong cardNetworkId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetworkId, out NetworkObject cardNetObj))
        {
            PlayingCard card = cardNetObj.GetComponent<PlayingCard>();
            ReturnCard(card);
        }
    }

    /// <summary>
    /// Returns all currently active cards to the pool.
    /// </summary>
    public void ReturnAllActiveCards()
    {
        if (!IsServer)
        {
            Debug.LogWarning("Server must call return all cards");
            return;
        }

        // Copy the list first because we'll modify it while iterating
        var activeCardsCopy = new List<PlayingCard>(activeCards);

        foreach (var card in activeCardsCopy)
        {
            ReturnCard(card);
        }
    }


    /// <summary>
    /// Refill the pool if it's running low
    /// </summary>
    private IEnumerator RefillPool()
    {
        if (isRefilling) yield break;

        isRefilling = true;
        Debug.Log($"CardPooler: Refilling pool with {refillAmount} cards...");

        yield return StartCoroutine(CreateCards(refillAmount));

        isRefilling = false;
        Debug.Log($"CardPooler: Pool refilled! Available: {AvailableCount}");
    }

    /// <summary>
    /// Force refill the pool (useful for testing or special situations)
    /// </summary>
    [ContextMenu("Force Refill Pool")]
    public void ForceRefillPool()
    {
        if (!IsServer) return;
        StartCoroutine(RefillPool());
    }

    /// <summary>
    /// Get multiple cards at once
    /// </summary>
    public List<PlayingCard> GetCards(int count, Vector3 position)
    {
        List<PlayingCard> cards = new List<PlayingCard>();

        for (int i = 0; i < count; i++)
        {
            PlayingCard card = GetCard(position);
            if (card != null)
            {
                cards.Add(card);
            }
            else
            {
                Debug.LogWarning($"CardPooler: Could only get {i}/{count} cards from pool");
                break;
            }
        }

        return cards;
    }

    /// <summary>
    /// Return multiple cards at once
    /// </summary>
    public void ReturnCards(IEnumerable<PlayingCard> cards)
    {
        foreach (PlayingCard card in cards)
        {
            ReturnCard(card);
        }
    }

    /// <summary>
    /// Clear all cards and rebuild the pool
    /// </summary>
    public void RebuildPool()
    {
        if (!IsServer) return;

        StopAllCoroutines();

        // Return all active cards
        List<PlayingCard> cardsToReturn = new List<PlayingCard>(activeCards);
        foreach (PlayingCard card in cardsToReturn)
        {
            ReturnCard(card);
        }

        // Reset deck
        deck.ResetDeck();

        // Rebuild pool
        StartCoroutine(InitializePool());
    }

    #region Debug Info

    [ContextMenu("Print Pool Status")]
    public void PrintPoolStatus()
    {
        Debug.Log($"CardPooler Status:\n" +
                 $"Available: {AvailableCount}\n" +
                 $"Active: {ActiveCount}\n" +
                 $"Total: {TotalCards}\n" +
                 $"Is Refilling: {isRefilling}");
    }

    private void OnGUI()
    {
        if (!IsServer || !Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 100, 200, 100));
        GUILayout.Label($"Pool Available: {AvailableCount}");
        GUILayout.Label($"Cards Active: {ActiveCount}");
        GUILayout.Label($"Is Refilling: {isRefilling}");

        if (GUILayout.Button("Force Refill"))
        {
            ForceRefillPool();
        }

        GUILayout.EndArea();
    }

    #endregion
}