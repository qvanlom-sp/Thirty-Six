namespace Casino_Game.Models;

public sealed class Player
{
    public const decimal AllChip = -1m;
    public decimal Bankroll { get; set; } = 500m;
    public decimal SelectedChip { get; set; } = 5m;
    public decimal SelectedWager => SelectedChip == AllChip ? decimal.Truncate(Bankroll) : SelectedChip;
}
