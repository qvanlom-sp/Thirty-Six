namespace Casino_Game.Models;

public sealed class GameState
{
    public Player Player { get; } = new();
    public UpgradeState Upgrades { get; } = new();
    public PlayerStats Stats { get; } = new();
    public Dictionary<BetTarget, decimal> Bets { get; } = [];
    public decimal AllNumbersBetTotal { get; set; }
    public List<RoundCard> ActiveCards { get; } = [];
    public List<string> History { get; } = [];
    public GamePhase Phase { get; set; } = GamePhase.ComeOut;
    public int RoundNumber { get; set; } = 1;
    public int SpinsThisRound { get; set; }
    public int LifetimeBettingSpins { get; set; }
    public int? Point { get; set; }
    public WheelSlot? LastResult { get; set; }
    public bool IsSpinning { get; set; }
    public int LastWinNumber { get; set; }
    public decimal LastPayout { get; set; }
    public decimal LastLoss { get; set; }
    public bool LastWasSeven { get; set; }
    public bool ExtraLifeSaved { get; set; }
    public bool GoalReached { get; set; }
    public bool ShowVictory { get; set; }
    public int WinStreak { get; set; }
    public decimal LastStreakBonus { get; set; }
    public decimal LastChallengeReward { get; set; }
    public List<RoundChallenge> Challenges { get; } = [];
    public Dictionary<BetTarget, decimal> PreviousBets { get; } = [];
    public List<int> RecentRolls { get; } = [];
    public string Message { get; set; } = "Spin once to choose the round’s point. Betting opens after that.";
    public decimal TotalOnTable => Bets.Values.Sum();
}
