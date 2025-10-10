using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerNameTag : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;

    private PlayerData playerData;

    private void Awake()
    {
        playerData = GetComponentInParent<PlayerData>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        playerData.PlayerName.OnValueChanged += OnNameChanged;
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        playerData.PlayerName.OnValueChanged -= OnNameChanged;
    }

    private void OnNameChanged(FixedString32Bytes oldValue, FixedString32Bytes newValue)
    {
        SetNameTag();
    }

    public void SetNameTag()
    {
        nameText.text = playerData?.GetName();
    }
}
