using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerManager : NetworkSingleton<PlayerManager>
{
    public static event Action OnPlayerCountUpdated;

    private NetworkVariable<int> playerCount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private Dictionary<ulong, PlayerData> players = new Dictionary<ulong, PlayerData>();

    public int PlayerCount => playerCount.Value;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
            NetworkManager.OnClientDisconnectCallback += NetworkManager_OnClientDisconnectedCallback;
        }

        playerCount.OnValueChanged += (oldValue, newValue) =>
        {
            OnPlayerCountUpdated?.Invoke();
        };
    }

    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        if (NetworkManager.Singleton.IsServer)
        {
            playerCount.Value++;
            ConsoleLog.Instance.Log($"Player {clientId} connected");
        }

        OnPlayerCountUpdated?.Invoke();
    }

    private void NetworkManager_OnClientDisconnectedCallback(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            playerCount.Value--;
            ConsoleLog.Instance.Log($"Player {clientId} disconnected");
            players.Remove(clientId);
        }

        OnPlayerCountUpdated?.Invoke();
    }

    /// <summary>
    /// Registers the OwnerClientID and the playerData
    /// </summary>
    public void RegisterPlayer(PlayerData player)
    {
        if (!players.ContainsKey(player.OwnerClientId))
        {
            players.Add(player.OwnerClientId, player);
        }
    }

    /// <summary>
    /// Gets the player data of a player from their client ID
    /// </summary>
    public PlayerData GetPlayerDataById(ulong clientId)
    {
        if (players.TryGetValue(clientId, out PlayerData player))
        {
            return player;
        }
        return null;
    }
}
