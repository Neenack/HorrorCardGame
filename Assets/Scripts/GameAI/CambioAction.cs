using Unity.Netcode;

public enum CambioActionType
{
    None,
    Draw,
    Discard,
    TradeCard,
    SwapCard,
    SwapHand,
    BlindSwap,
    RevealCard,
    RevealHand,
    CompareCards,
    CallCambio,
    Stack,
    SelectCard,
    ChooseCard,
    GiveCard
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

    public CambioActionData(CambioActionType type, bool endsTurn, ulong playerId, ulong cardNetworkId = 0, ulong targetPlayerId = 0, ulong targetCardNetworkId = 0)
    {
        Type = type;
        EndsTurn = endsTurn;
        PlayerId = playerId;
        CardId = cardNetworkId;
        TargetPlayerId = targetPlayerId;
        TargetCardId = targetCardNetworkId;
    }
}