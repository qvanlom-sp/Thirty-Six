namespace Casino_Game.Models;

public enum PowerId
{
    HiLo,
    PlayItSafe,
    LuckyFives,
    NothingEasy,
    ExtraLife,
    PointPress,
    HotHand,
    BankrollGuard
}

public sealed record PowerDefinition(
    PowerId Id,
    string Name,
    string Description,
    decimal UnlockCost,
    int CooldownSpins,
    int DurationSpins,
    string Icon);

public sealed class PowerState
{
    public bool IsUnlocked { get; set; }
    public bool IsActive { get; set; }
    public int CooldownRemaining { get; set; }
    public int SpinsRemaining { get; set; }
}
