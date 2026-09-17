using System.Security.Cryptography;
using Casino_Game.Models;

namespace Casino_Game.Systems;

public sealed class GameEngine
{
    private static readonly HashSet<int> PointNumbers = [4, 5, 6, 8, 9, 10];

    public GameEngine()
    {
        State = new GameState();
        Wheel = BuildWheel();
    }

    public GameState State { get; }
    public IReadOnlyList<WheelSlot> Wheel { get; }

    public bool PlaceBet(BetTarget target)
    {
        var chip = State.Player.SelectedChip;
        if (State.IsSpinning || State.Player.Bankroll < chip)
            return false;

        State.Player.Bankroll -= chip;
        State.Bets[target] = State.Bets.GetValueOrDefault(target) + chip;
        State.Message = $"${chip:0} chip placed on {GetName(target)}.";
        return true;
    }

    public void ClearBets()
    {
        if (State.IsSpinning)
            return;

        State.Player.Bankroll += State.TotalOnTable;
        State.Bets.Clear();
        State.Message = "Bets returned to your rack.";
    }

    public (int Index, WheelSlot Slot) PickOutcome()
    {
        var index = RandomNumberGenerator.GetInt32(Wheel.Count);
        return (index, Wheel[index]);
    }

    public void Resolve(WheelSlot result)
    {
        var wasComeOut = State.Point is null;
        var oldPoint = State.Point;
        var notes = new List<string>();
        State.LastResult = result;

        ResolveNumberBets(result, notes);
        ResolveHardways(result, notes);
        ResolveDontPass(result.Total, wasComeOut, oldPoint, notes);
        UpdatePoint(result.Total, oldPoint, notes);

        var roll = $"{result.DieOne} + {result.DieTwo} = {result.Total}{(result.IsHard ? " hard" : string.Empty)}";
        State.Message = notes.Count == 0 ? $"{roll}. No bets resolved." : $"{roll}. {string.Join(" ", notes)}";
        State.History.Insert(0, State.Message);
        if (State.History.Count > 6)
            State.History.RemoveAt(State.History.Count - 1);
    }

    private void ResolveNumberBets(WheelSlot result, List<string> notes)
    {
        foreach (var (target, wager) in State.Bets.ToArray())
        {
            var number = (int)target;
            if (number is < 2 or > 12 || number == 7)
                continue;

            if (number is 2 or 3 or 11 or 12)
            {
                State.Bets.Remove(target);
                if (result.Total == number)
                {
                    var odds = number is 2 or 12 ? 30m : 15m;
                    var profit = wager * odds;
                    State.Player.Bankroll += wager + profit;
                    notes.Add($"{number} wins ${profit:0.##}.");
                }
                continue;
            }

            if (result.Total == 7)
            {
                State.Bets.Remove(target);
                notes.Add($"{number} loses ${wager:0.##}.");
            }
            else if (result.Total == number)
            {
                var profit = wager * PlaceOdds(number);
                State.Player.Bankroll += profit;
                notes.Add($"{number} pays ${profit:0.##}.");
            }
        }
    }

    private void ResolveHardways(WheelSlot result, List<string> notes)
    {
        foreach (var target in new[] { BetTarget.Hard4, BetTarget.Hard6, BetTarget.Hard8, BetTarget.Hard10 })
        {
            if (!State.Bets.TryGetValue(target, out var wager))
                continue;

            var number = (int)target - 100;
            if (result.Total == number && result.IsHard)
            {
                var odds = number is 4 or 10 ? 7m : 9m;
                var profit = wager * odds;
                State.Player.Bankroll += profit;
                notes.Add($"Hard {number} pays ${profit:0.##}.");
            }
            else if (result.Total == 7 || result.Total == number)
            {
                State.Bets.Remove(target);
                notes.Add($"Hard {number} loses ${wager:0.##}.");
            }
        }
    }

    private void ResolveDontPass(int total, bool wasComeOut, int? oldPoint, List<string> notes)
    {
        if (!State.Bets.TryGetValue(BetTarget.DontPass, out var wager))
            return;

        if (wasComeOut)
        {
            if (total is 2 or 3)
                SettleDont(wager, true, notes);
            else if (total is 7 or 11)
                SettleDont(wager, false, notes);
            else if (total == 12)
            {
                State.Bets.Remove(BetTarget.DontPass);
                State.Player.Bankroll += wager;
                notes.Add("Don’t Pass pushes on 12.");
            }
        }
        else if (total == 7)
        {
            SettleDont(wager, true, notes);
        }
        else if (total == oldPoint)
        {
            SettleDont(wager, false, notes);
        }
    }

    private void SettleDont(decimal wager, bool won, List<string> notes)
    {
        State.Bets.Remove(BetTarget.DontPass);
        if (won)
            State.Player.Bankroll += wager * 2;
        notes.Add(won ? $"Don’t Pass wins ${wager:0.##}." : $"Don’t Pass loses ${wager:0.##}.");
    }

    private void UpdatePoint(int total, int? oldPoint, List<string> notes)
    {
        if (oldPoint is null && PointNumbers.Contains(total))
        {
            State.Point = total;
            notes.Add($"The point is {total}.");
        }
        else if (oldPoint is not null && total == 7)
        {
            State.Point = null;
            notes.Add("Seven out—new come-out roll.");
        }
        else if (oldPoint is not null && total == oldPoint)
        {
            State.Point = null;
            notes.Add($"Point {oldPoint} made—new come-out roll.");
        }
    }

    private static decimal PlaceOdds(int number) => number switch
    {
        4 or 10 => 9m / 5m,
        5 or 9 => 7m / 5m,
        6 or 8 => 7m / 6m,
        _ => 0m
    };

    public static string GetName(BetTarget target) => target switch
    {
        BetTarget.DontPass => "7 / Don’t",
        BetTarget.Hard4 => "Hard 4",
        BetTarget.Hard6 => "Hard 6",
        BetTarget.Hard8 => "Hard 8",
        BetTarget.Hard10 => "Hard 10",
        _ => ((int)target).ToString()
    };

    private static IReadOnlyList<WheelSlot> BuildWheel()
    {
        var combinations = (from dieOne in Enumerable.Range(1, 6)
                            from dieTwo in Enumerable.Range(1, 6)
                            select new WheelSlot(dieOne, dieTwo)).ToArray();

        // Reorder all 36 unique dice combinations for visual variety. Selection remains uniform.
        return combinations.Select((slot, index) => (slot, key: index * 11 % 37))
            .OrderBy(item => item.key)
            .Select(item => item.slot)
            .ToArray();
    }
}
