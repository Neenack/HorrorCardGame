using Unity.Netcode;
using UnityEngine;

public struct TablePlayerSendParams : INetworkSerializeByMemcpy
{
    public ulong ClientID;
    public ulong TablePlayerID;

    public TablePlayerSendParams(ulong ownerClientId, ulong tablePlayerId)
    {
        ClientID = ownerClientId;
        TablePlayerID = tablePlayerId;
    }
}
