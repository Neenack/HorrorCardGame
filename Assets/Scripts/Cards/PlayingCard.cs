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

    private SpriteRenderer frontFace;
    private SpriteRenderer backFace;
    private Transform card;

    private PlayingCardSO cardSO;

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


    private void Awake()
    {
        card = transform.GetChild(0);

        frontFace = card.Find("Front").GetComponent<SpriteRenderer>();
        backFace = card.Find("Back").GetComponent<SpriteRenderer>();

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

    public void SetCard(PlayingCardSO card)
    {
        cardSO = card;

        if (IsServer) cardTypeName.Value = card.name;

        UpdateCardVisuals();
    }

    public void HideFrontFace()
    {
        if (IsServer)
        {
            frontFace.sprite = backFace.sprite;
            HideFrontFaceClientRpc();
        }
    }

    [ClientRpc]
    private void HideFrontFaceClientRpc()
    {
        frontFace.sprite = backFace.sprite;
    }

    public void UpdateCardVisuals()
    {
        frontFace.sprite = cardSO.GetSprite();
    }

    public void FlipCard(bool waitForMovement = true, float flipSpeed = 3f)
    {
        if (!IsServer)
        {
            Debug.LogWarning("Can only be called by the server");
            return;
        }

        // Server flips its own
        StartCoroutine(FlipRoutine(flipSpeed, waitForMovement));

        // Tell everyone else to flip
        FlipCardClientRpc(waitForMovement, flipSpeed);
    }

    private IEnumerator FlipRoutine(float speed, bool afterMovement)
    {
        if (afterMovement) yield return new WaitUntil(() => !moving);

        // Determine start and end angles
        float startAngle = card.localEulerAngles.x;

        // Convert >180 to negative for smooth lerp
        if (startAngle > 180f) startAngle -= 360f;

        float endAngle = startAngle + 180f; // just flip

        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            float x = Mathf.Lerp(startAngle, endAngle, t);
            card.localEulerAngles = new Vector3(x, card.localEulerAngles.y, card.localEulerAngles.z);
            yield return null;
        }

        // Ensure exact final angle
        card.localEulerAngles = new Vector3(endAngle % 360f, card.localEulerAngles.y, card.localEulerAngles.z);

        isFaceDown = !isFaceDown;
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

    [ClientRpc]
    private void FlipCardClientRpc(bool waitForMovement, float flipSpeed)
    {
        if (IsServer) return;

        StartCoroutine(FlipRoutine(flipSpeed, waitForMovement));
    }

    #endregion

    public override string ToString() => cardSO.GetCardName();
}
