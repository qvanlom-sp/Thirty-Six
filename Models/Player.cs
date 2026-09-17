namespace Casino_Game.Models;

public sealed class Player
{
    public decimal Bankroll { get; set; } = 500m;
    public decimal SelectedChip { get; set; } = 5m;
}
