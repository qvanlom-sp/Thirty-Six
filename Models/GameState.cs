namespace Casino_Game.Models;

public sealed class GameState
{
    public Player Player { get; } = new();
    public UpgradeState Upgrades { get; } = new();
    public Dictionary<BetTarget, decimal> Bets { get; } = [];
    public List<string> History { get; } = [];
    public int? Point { get; set; }
    public WheelSlot? LastResult { get; set; }
    public bool IsSpinning { get; set; }
    public string Message { get; set; } = "Place a chip, then spin to establish the point.";
    public decimal TotalOnTable => Bets.Values.Sum();
}
