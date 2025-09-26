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

public class SwapInfo
{
    public PlayingCard Keep;
    public PlayingCard Discard;
    public SwapInfo(PlayingCard keep, PlayingCard discard)
    {
        Keep = keep;
        Discard = discard;
    }
}

public class CambioAction
{
    public CambioActionType Type { get; private set; }
    public bool EndsTurn { get; private set; } = false;
    public PlayingCard TargetCard { get; private set; }
    public CambioPlayer TargetPlayer { get; private set; }
    public SwapInfo SwapData { get; private set; }

    public CambioAction(CambioActionType type, bool endsTurn, CambioPlayer targetPlayer = null, PlayingCard targetCard = null)
    {
        Type = type;
        EndsTurn = endsTurn;
        TargetPlayer = targetPlayer;
        TargetCard = targetCard;
    }

    public CambioAction(CambioActionType type, bool endsTurn, CambioPlayer targetPlayer, SwapInfo swapInfo)
    {
        Type = type;
        EndsTurn = endsTurn;
        SwapData = swapInfo;
        TargetPlayer = targetPlayer;
    }
}
