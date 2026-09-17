namespace Casino_Game.Models;

// A deliberately small extension point for later unlocks, power-ups, and progression.
public sealed class UpgradeState
{
    public int LuckLevel { get; set; }
    public int TableLevel { get; set; } = 1;
    public HashSet<string> UnlockedPowerUps { get; } = [];
}
