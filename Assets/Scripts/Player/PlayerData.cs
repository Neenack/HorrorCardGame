using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Lobbies.Models;
using UnityEngine;

public class PlayerData : NetworkBehaviour
{
    public event Action OnPlayerSpawned;

    private Player lobbyPlayer;


    private NetworkVariable<FixedString32Bytes> playerName = new NetworkVariable<FixedString32Bytes>();
    public NetworkVariable<FixedString32Bytes> PlayerName => playerName;

    private FirstPersonController controller;


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

        string authId = AuthenticationService.Instance.PlayerId;

        if (IsOwner)
        {
            if (IsServer)
            {
                // Host can set its own name directly
                SetPlayer(authId);
            }
            else
            {
                // Remote client asks the server to set it
                SetPlayerServerRpc(authId);
            }
        }

        PlayerManager.Instance.RegisterPlayer(this);
        OnPlayerSpawned?.Invoke();
    }

    private void SetPlayer(string playerId)
    {
        lobbyPlayer = LobbyManager.Instance.GetPlayerById(playerId);
        if (lobbyPlayer == null) return;

        SetName(lobbyPlayer.Data[LobbyManager.KEY_PLAYER_NAME].Value);
    }

    [ServerRpc] private void SetPlayerServerRpc(string playerId) => SetPlayer(playerId);

    public void SetName(string newName)
    {
        if (IsServer) playerName.Value = newName;
    }

    public string GetName() => playerName.Value.ToString();
}
