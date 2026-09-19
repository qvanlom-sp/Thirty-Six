namespace Casino_Game.Models;

public sealed class PlayerStats
{
    public int RoundsCompleted { get; set; }
    public int Spins { get; set; }
    public int BestStreak { get; set; }
    public int ChallengesCompleted { get; set; }
    public int CashOuts { get; set; }
    public decimal TotalWagered { get; set; }
    public decimal TotalWon { get; set; }
    public decimal BiggestSingleSpinPayout { get; set; }
    public Dictionary<BetTarget, decimal> WageredByTarget { get; } = [];
}
