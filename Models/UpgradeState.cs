namespace Casino_Game.Models;

public sealed class UpgradeState
{
    public int TableLevel { get; set; } = 1;
    public Dictionary<int, int> PayoutLevels { get; } = new()
    {
        [2] = 0, [3] = 0, [4] = 0, [5] = 0, [6] = 0,
        [8] = 0, [9] = 0, [10] = 0, [11] = 0, [12] = 0
    };

    public Dictionary<PowerId, PowerState> Powers { get; } = Enum.GetValues<PowerId>()
        .ToDictionary(id => id, _ => new PowerState());
}
