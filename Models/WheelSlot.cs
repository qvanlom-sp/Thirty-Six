namespace Casino_Game.Models;

public sealed record WheelSlot(int DieOne, int DieTwo)
{
    public int Total => DieOne + DieTwo;
    public bool IsHard => DieOne == DieTwo && Total is 4 or 6 or 8 or 10;
    public string Label => IsHard ? $"{Total}H" : Total.ToString();
}
