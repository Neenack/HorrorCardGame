using Unity.Netcode;
using Unity.Collections;

public struct TablePlayerData : INetworkSerializable
{
    public ulong clientID;
    public NetworkObjectReference playerData;
    public NetworkObjectReference tablePlayerRef;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientID);
        serializer.SerializeValue(ref playerData);
        serializer.SerializeValue(ref tablePlayerRef);
    }
}