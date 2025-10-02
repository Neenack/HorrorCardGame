using System;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayingCard : NetworkBehaviour
{
    private NetworkVariable<FixedString64Bytes> cardTypeName = new NetworkVariable<FixedString64Bytes>(
        "",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public event Action<PlayingCard> OnShowCard;
    private PlayingCardSO cardSO;
    private MeshRenderer meshRenderer;

    private Vector3 targetPos;

    private float speed;
    private bool moving = false;
    private bool isFaceDown = true;

    private Quaternion targetRot;
    private bool rotating = false;
    private float rotSpeed;

    public int GetValue(bool isAceHigh) => (isAceHigh && cardSO.Value == 1) ? cardSO.Value + 13 : cardSO.Value;

    public PlayingCardSO CardSO => cardSO;
    public Suit Suit => cardSO.Suit;
    public bool IsFaceDown => isFaceDown;


    private IInteractable interactable;
    public IInteractable Interactable => interactable;

    #region Public Accessor

    public bool IsMoving => moving;

    #endregion


    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        interactable = GetComponent<IInteractable>();
    }

    public override void OnNetworkSpawn()
    {
        // When clients receive the card type, update their visuals
        if (!IsServer)
        {
            cardTypeName.OnValueChanged += OnCardTypeReceived;

            if (!string.IsNullOrEmpty(cardTypeName.Value.ToString()))
            {
                OnCardTypeReceived("", cardTypeName.Value);
            }
        }
    }

    private void OnCardTypeReceived(FixedString64Bytes oldValue, FixedString64Bytes newValue)
    {
        // Find the ScriptableObject by name
        PlayingCardSO cardData = Resources.Load<PlayingCardSO>(newValue.ToString());

        if (cardData == null)
        {
            Debug.LogWarning($"Could not find Card Data for: {newValue}");
            return;
        }

        this.cardSO = cardData;
        UpdateCardVisuals();
    }

    /// <summary>
    /// Returns the playing card from a network ID
    /// </summary>
    public static PlayingCard GetPlayingCardFromNetworkID(ulong cardNetworkID)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cardNetworkID, out NetworkObject cardObj))
        {
            return cardObj.GetComponent<PlayingCard>();
        }

        return null;
    }

    public void SetCard(PlayingCardSO card)
    {
        cardSO = card;

        if (IsServer) cardTypeName.Value = card.name;

        UpdateCardVisuals();
    }

    public void UpdateCardVisuals()
    {
        meshRenderer.material.mainTexture = cardSO.GetTexture();
    }

    public void FlipCard(float flipSpeed = 10f)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Can only be called by the server");
            return;
        }

        Quaternion currentRot = transform.rotation;
        Quaternion targetRotation = currentRot * Quaternion.Euler(180f, 0f, 0f);

        RotateTo(targetRotation, flipSpeed);

        OnShowCard?.Invoke(this);
    }

    public void MoveTo(Vector3 target, float lerpSpeed)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"{ToString()} can only be moved by the server!");
            return;
        }

        targetPos = target;
        speed = lerpSpeed;
        moving = true;

        // Tell all clients to move too
        MoveToClientRpc(target, lerpSpeed);
    }

    public void RotateTo(Quaternion targetRotation, float lerpSpeed)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"{ToString()} can only be rotated by the server!");
            return;
        }

        targetRot = targetRotation;
        rotSpeed = lerpSpeed;
        rotating = true;

        // Tell all clients to rotate too
        RotateToClientRpc(targetRotation, lerpSpeed);
    }

    void Update()
    {
        if (moving)
        {
            transform.position = Vector3.Lerp(transform.position, targetPos, speed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPos) < 0.01f)
            {
                transform.position = targetPos;
                moving = false;
            }
        }

        if (rotating)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, rotSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, targetRot) < 0.5f) // close enough
            {
                transform.rotation = targetRot;
                rotating = false;
            }
        }
    }

    #region Client Functions

    [ClientRpc]
    private void MoveToClientRpc(Vector3 target, float lerpSpeed)
    {
        if (IsServer) return; // server already set flags

        targetPos = target;
        speed = lerpSpeed;
        moving = true;
    }

    [ClientRpc]
    private void RotateToClientRpc(Quaternion targetRotation, float lerpSpeed)
    {
        if (IsServer) return;

        targetRot = targetRotation;
        rotSpeed = lerpSpeed;
        rotating = true;
    }

    #endregion

    public override string ToString() => cardSO.GetCardName();
}
