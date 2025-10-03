using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;

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
        // Place player at table if available
        Transform standPos = TableSeater.Instance.TrySetPlayerAtTable(this);
        if (standPos != null)
        {
            transform.position = standPos.position;
            transform.rotation = standPos.rotation;
            controller?.DisableMovement();
        }
        else
        {
            controller?.EnableMovement();
        }

        // Only the owning client tells the server its PlayerId
        if (IsOwner)
        {
            string authId = AuthenticationService.Instance.PlayerId;
            if (IsServer)
            {
                // Host can set its own name directly
                ApplyPlayerName(authId);
            }
            else
            {
                // Remote client asks the server to set it
                SetPlayerIdServerRpc(authId);
            }
        }

        PlayerManager.Instance.RegisterPlayer(this);
        OnPlayerSpawned?.Invoke(this);
    }

    private void ApplyPlayerName(string playerId)
    {
        string lobbyName = LobbyManager.Instance.GetPlayerNameById(playerId);
        playerName.Value = string.IsNullOrEmpty(lobbyName) ? "Player " + OwnerClientId : lobbyName;
    }

    [ServerRpc]
    private void SetPlayerIdServerRpc(string playerId)
    {
        ApplyPlayerName(playerId);
    }

    public string GetName() => playerName.Value;
}
