using Unity.Netcode;

public enum CambioActionType
{
    None,
    Draw,
    Discard,
    TradeCard,
    SwapCard,
    SwapHand,
    RevealCard,
    RevealHand,
    CompareCards,
    CallCambio,
    Stack,
}

[GenerateSerializationForType(typeof(CambioActionData))]
public static class GeneratedCambioActionDataSerialization { }

public struct CambioActionData : INetworkSerializeByMemcpy
{
    public CambioActionType Type;
    public bool EndsTurn;
    public ulong PlayerId;
    public ulong CardId;
    public ulong TargetPlayerId;
    public ulong TargetCardId;

    public CambioActionData(CambioActionType type, bool endsTurn, ulong playerClientId, ulong cardNetworkId = 0, ulong targetPlayerClientId = 0, ulong targetCardNetworkId = 0)
    {
        Type = type;
        EndsTurn = endsTurn;
        PlayerId = playerClientId;
        CardId = cardNetworkId;
        TargetPlayerId = targetPlayerClientId;
        TargetCardId = targetCardNetworkId;
    }
}