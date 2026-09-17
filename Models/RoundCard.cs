namespace Casino_Game.Models;

public enum CardEffect
{
    LowNumbers,
    HighNumbers,
    SixEight,
    FourTen,
    FiveNine,
    SafetyNet,
    BlockSeven,
    Hardways,
    PointBoost,
    HotStart,
    EvenNumbers,
    OddNumbers,
    SmallStakes,
    BigStakes,
    AllNumbers,
    PocketChip
}

public sealed record RoundCard(
    string Name,
    string Symbol,
    string Suit,
    string Description,
    CardEffect Effect,
    decimal Value);
