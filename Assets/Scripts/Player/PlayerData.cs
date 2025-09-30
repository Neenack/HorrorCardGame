using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerData : NetworkBehaviour
{
    private NetworkVariable<NetworkString> playerName = new NetworkVariable<NetworkString>();
    private FirstPersonController controller;

    public event Action<PlayerData> OnPlayerSpawned;

    private void Awake()
    {
        controller = GetComponent<FirstPersonController>();
    }

    public override void OnNetworkSpawn()
    {
        Transform standPos = TableSeater.Instance.TrySetPlayerAtTable(this);

        if (standPos != null)
        {
            transform.position = standPos.position;
            transform.rotation = standPos.rotation;
            controller?.DisableMovement();
        }
        else //No seats at the table, enable movement
        {
            controller?.EnableMovement();
        }

        if (IsServer)
        {
            playerName.Value = "Player " + OwnerClientId.ToString();
        }

        PlayerManager.Instance.RegisterPlayer(this);

        OnPlayerSpawned?.Invoke(this);
    }

    public string GetName() => playerName.Value;
}