namespace Casino_Game.Models;

public enum ChallengeKind
{
    Wins, Point, Hardway, Low, High, Even, Odd, Outer, Variety, Streak, PrizeTotal, Powered
}

public sealed record ChallengeDefinition(
    ChallengeKind Kind, string Name, string Description, int Goal, decimal RewardCap, bool Common = false);

public sealed class RoundChallenge(ChallengeDefinition definition)
{
    public ChallengeDefinition Definition { get; } = definition;
    public decimal Progress { get; set; }
    public bool IsComplete { get; set; }
    public HashSet<int> WinningNumbers { get; } = [];
}
