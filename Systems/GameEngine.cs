using System.Security.Cryptography;
using Casino_Game.Models;

namespace Casino_Game.Systems;

public sealed class GameEngine
{
    private static readonly HashSet<int> PointNumbers = [4, 5, 6, 8, 9, 10];
    private static readonly BetTarget[] HardTargets = [BetTarget.Hard4, BetTarget.Hard6, BetTarget.Hard8, BetTarget.Hard10];

    public static readonly IReadOnlyList<PowerDefinition> PowerDefinitions =
    [
        new(PowerId.HiLo, "Hi / Lo", "For one round, winning 2 and 12 bets pay five times their normal prize.", 150m, 2, "↕"),
        new(PowerId.PlayItSafe, "Play It Safe", "Blocks three of the six red 7 slots for your next three spins.", 250m, 2, "◆"),
        new(PowerId.LuckyFives, "Lucky 5s", "All winning 5 bets pay double for the rest of this round.", 300m, 2, "★"),
        new(PowerId.NothingEasy, "Nothing Easy", "Hardway wins pay double for the rest of this round.", 400m, 3, "H"),
        new(PowerId.ExtraLife, "Extra Life", "The next 7 is ignored. Your bets survive and the round continues.", 1000m, 4, "♥")
    ];

    public GameEngine()
    {
        State = new GameState();
        Wheel = BuildWheel();
    }

    public GameState State { get; }
    public IReadOnlyList<WheelSlot> Wheel { get; }
    public bool BettingIsOpen => State.Phase == GamePhase.BettingRound && !State.IsSpinning;

    public bool PlaceBet(BetTarget target)
    {
        var chip = State.Player.SelectedChip;
        if (!BettingIsOpen || State.Player.Bankroll < chip)
            return false;

        State.Player.Bankroll -= chip;
        State.Bets[target] = State.Bets.GetValueOrDefault(target) + chip;
        State.Message = $"${chip:0} placed on {GetName(target)}. Spin when you’re ready.";
        return true;
    }

    public void ClearBets()
    {
        if (!BettingIsOpen)
            return;

        State.Player.Bankroll += State.TotalOnTable;
        State.Bets.Clear();
        State.Message = "Your chips are back in the rack.";
    }

    public (int Index, WheelSlot Slot) PickOutcome()
    {
        var eligible = Enumerable.Range(0, Wheel.Count)
            .Where(index => State.Phase != GamePhase.ComeOut || PointNumbers.Contains(Wheel[index].Total))
            .Where(index => !IsSlotBlocked(index))
            .ToArray();
        var index = eligible[RandomNumberGenerator.GetInt32(eligible.Length)];
        return (index, Wheel[index]);
    }

    public void PrepareSpin() => ResetResultEffects();

    public bool IsSlotBlocked(int index)
    {
        if (State.Phase == GamePhase.ComeOut)
            return !PointNumbers.Contains(Wheel[index].Total);

        var safe = State.Upgrades.Powers[PowerId.PlayItSafe];
        if (!safe.IsActive || safe.SpinsRemaining <= 0 || Wheel[index].Total != 7)
            return false;

        return Wheel.Select((slot, slotIndex) => (slot, slotIndex))
            .Where(item => item.slot.Total == 7)
            .Take(3)
            .Any(item => item.slotIndex == index);
    }

    public bool IsSlotPowerAffected(int index)
    {
        var slot = Wheel[index];
        var powers = State.Upgrades.Powers;
        if (powers[PowerId.HiLo].IsActive && slot.Total is 2 or 12)
            return true;
        if (powers[PowerId.LuckyFives].IsActive && slot.Total == 5)
            return true;
        if (powers[PowerId.NothingEasy].IsActive && slot.IsHard)
            return true;
        if (powers[PowerId.ExtraLife].IsActive && slot.Total == 7)
            return true;
        return powers[PowerId.PlayItSafe].IsActive && IsSlotBlocked(index);
    }

    public void Resolve(WheelSlot result)
    {
        ResetResultEffects();
        State.LastResult = result;

        if (State.Phase == GamePhase.ComeOut)
        {
            State.Point = result.Total;
            State.Phase = GamePhase.BettingRound;
            State.Message = $"The point is {result.Total}. Betting is now open.";
            AddHistory(State.Message);
            return;
        }

        if (State.Phase != GamePhase.BettingRound)
            return;

        var notes = new List<string>();
        ResolveOneSpinBets(result, notes);

        if (result.Total == 7 && ConsumeExtraLife())
        {
            State.LastWasSeven = true;
            State.ExtraLifeSaved = true;
            TickSpinPowers();
            State.Message = "Extra Life! The 7 was erased, your bets survived, and the round continues.";
            AddHistory(State.Message);
            return;
        }

        ResolvePlaceBets(result, notes);
        ResolveHardways(result, notes);
        TickSpinPowers();

        if (result.Total == 7)
        {
            State.LastWasSeven = true;
            EndRound();
            var payoutText = State.LastPayout > 0 ? $" Your 7 bet won {Money(State.LastPayout)}." : string.Empty;
            State.Message = $"Seven out. The round is over.{payoutText}";
        }
        else if (State.LastPayout > 0)
        {
            State.LastWinNumber = result.Total;
            State.Message = $"{result.Total} hits! You won {Money(State.LastPayout)}. Your place bets stay up.";
        }
        else if (State.Player.Bankroll < 5m && State.TotalOnTable < 5m)
        {
            EndRound();
            State.Message = "Your rack is empty. The kitchen has a quick job for you.";
        }
        else
        {
            State.Message = $"{result.Total} landed. No payout this spin—your place bets stay up.";
        }

        AddHistory(State.Message);
    }

    public decimal UpgradeCost(int number) => 90m + State.Upgrades.PayoutLevels[number] * 85m;

    public bool BuyPayoutUpgrade(int number)
    {
        if (State.Phase != GamePhase.Shop || !State.Upgrades.PayoutLevels.TryGetValue(number, out var level) || level >= 5)
            return false;

        var cost = UpgradeCost(number);
        if (State.Player.Bankroll < cost)
            return false;

        State.Player.Bankroll -= cost;
        State.Upgrades.PayoutLevels[number]++;
        State.Message = $"{number} upgraded. Its place-bet prizes are now +{(level + 1) * 10}%.";
        CheckShopBailout();
        return true;
    }

    public bool BuyPower(PowerId id)
    {
        var definition = PowerDefinitions.Single(power => power.Id == id);
        var state = State.Upgrades.Powers[id];
        if (State.Phase != GamePhase.Shop || state.IsUnlocked || State.Player.Bankroll < definition.UnlockCost)
            return false;

        State.Player.Bankroll -= definition.UnlockCost;
        state.IsUnlocked = true;
        State.Message = $"{definition.Name} permanently unlocked.";
        CheckShopBailout();
        return true;
    }

    public bool ActivatePower(PowerId id)
    {
        var power = State.Upgrades.Powers[id];
        if (!BettingIsOpen || !power.IsUnlocked || power.IsActive || power.UsedThisRound || power.CooldownRemaining > 0)
            return false;

        power.IsActive = true;
        power.UsedThisRound = true;
        if (id == PowerId.PlayItSafe)
            power.SpinsRemaining = 3;
        State.Message = $"{PowerDefinitions.Single(item => item.Id == id).Name} activated.";
        return true;
    }

    public void LeaveShop()
    {
        if (State.Phase != GamePhase.Shop)
            return;

        State.RoundNumber++;
        State.Phase = GamePhase.ComeOut;
        State.Point = null;
        State.LastResult = null;
        ResetResultEffects();
        State.Message = "Come-out spin: the highlighted point numbers are the only possible results.";
    }

    public void CompleteBailout()
    {
        if (State.Phase != GamePhase.Bailout)
            return;

        State.Player.Bankroll += 200m;
        State.Phase = GamePhase.Shop;
        State.Message = "Shift complete. You earned $200—spend carefully.";
    }

    public decimal PayoutMultiplier(int number) => 1m + State.Upgrades.PayoutLevels.GetValueOrDefault(number) * .1m;

    private void ResolveOneSpinBets(WheelSlot result, List<string> notes)
    {
        foreach (var number in new[] { 2, 3, 11, 12 })
        {
            var target = (BetTarget)number;
            if (!State.Bets.Remove(target, out var wager))
                continue;
            if (result.Total != number)
                continue;

            var odds = number is 2 or 12 ? 30m : 15m;
            if (number is 2 or 12 && State.Upgrades.Powers[PowerId.HiLo].IsActive)
                odds *= 5m;
            Award(wager, odds, true);
            notes.Add($"{number} won {Money(wager * odds)}.");
        }

        if (!State.Bets.Remove(BetTarget.Seven, out var sevenWager))
            return;
        if (result.Total == 7)
        {
            Award(sevenWager, 4m, true);
            notes.Add($"Red 7 won {Money(sevenWager * 4m)}.");
        }
    }

    private void ResolvePlaceBets(WheelSlot result, List<string> notes)
    {
        foreach (var (target, wager) in State.Bets.ToArray())
        {
            var number = (int)target;
            if (!PointNumbers.Contains(number))
                continue;

            if (result.Total == 7)
            {
                State.Bets.Remove(target);
            }
            else if (result.Total == number)
            {
                var odds = PlaceOdds(number) * PayoutMultiplier(number) * PointBonus(number);
                if (number == 5 && State.Upgrades.Powers[PowerId.LuckyFives].IsActive)
                    odds *= 2m;
                Award(wager, odds, false);
                notes.Add($"{number} paid {Money(wager * odds)}.");
            }
        }
    }

    private void ResolveHardways(WheelSlot result, List<string> notes)
    {
        foreach (var target in HardTargets)
        {
            if (!State.Bets.TryGetValue(target, out var wager))
                continue;

            var number = (int)target - 100;
            if (result.Total == number && result.IsHard)
            {
                var odds = number is 4 or 10 ? 7m : 9m;
                if (State.Upgrades.Powers[PowerId.NothingEasy].IsActive)
                    odds *= 2m;
                odds *= PointBonus(number);
                Award(wager, odds, false);
                notes.Add($"Hard {number} paid {Money(wager * odds)}.");
            }
            else if (result.Total == 7 || result.Total == number)
            {
                State.Bets.Remove(target);
            }
        }
    }

    private void Award(decimal wager, decimal odds, bool returnStake)
    {
        var profit = decimal.Round(wager * odds, 2);
        State.Player.Bankroll += profit + (returnStake ? wager : 0m);
        State.LastPayout += profit;
    }

    private bool ConsumeExtraLife()
    {
        var power = State.Upgrades.Powers[PowerId.ExtraLife];
        if (!power.IsActive)
            return false;
        power.IsActive = false;
        return true;
    }

    private void TickSpinPowers()
    {
        var safe = State.Upgrades.Powers[PowerId.PlayItSafe];
        if (safe.IsActive && --safe.SpinsRemaining <= 0)
            safe.IsActive = false;
    }

    private void EndRound()
    {
        State.Point = null;
        State.Bets.Clear();
        foreach (var definition in PowerDefinitions)
        {
            var power = State.Upgrades.Powers[definition.Id];
            if (power.CooldownRemaining > 0)
                power.CooldownRemaining--;
            if (power.UsedThisRound)
                power.CooldownRemaining = definition.CooldownRounds;
            power.IsActive = false;
            power.UsedThisRound = false;
            power.SpinsRemaining = 0;
        }

        State.Phase = State.Player.Bankroll < 5m ? GamePhase.Bailout : GamePhase.Shop;
    }

    private void ResetResultEffects()
    {
        State.LastPayout = 0;
        State.LastWinNumber = 0;
        State.LastWasSeven = false;
        State.ExtraLifeSaved = false;
    }

    private void CheckShopBailout()
    {
        if (State.Player.Bankroll < 5m)
            State.Phase = GamePhase.Bailout;
    }

    private void AddHistory(string message)
    {
        State.History.Insert(0, message);
        if (State.History.Count > 6)
            State.History.RemoveAt(State.History.Count - 1);
    }

    private static decimal PlaceOdds(int number) => number switch
    {
        4 or 10 => 9m / 5m,
        5 or 9 => 7m / 5m,
        6 or 8 => 7m / 6m,
        _ => 0m
    };

    private decimal PointBonus(int number) => State.Point == number ? 1.5m : 1m;

    public static string GetName(BetTarget target) => target switch
    {
        BetTarget.Seven => "Red 7",
        BetTarget.Hard4 => "Hard 4",
        BetTarget.Hard6 => "Hard 6",
        BetTarget.Hard8 => "Hard 8",
        BetTarget.Hard10 => "Hard 10",
        _ => ((int)target).ToString()
    };

    private static string Money(decimal value) => $"${value:0.##}";

    private static IReadOnlyList<WheelSlot> BuildWheel()
    {
        var combinations = (from dieOne in Enumerable.Range(1, 6)
                            from dieTwo in Enumerable.Range(1, 6)
                            select new WheelSlot(dieOne, dieTwo)).ToArray();
        var sevens = combinations.Where(slot => slot.Total == 7).ToList();
        var others = combinations.Where(slot => slot.Total != 7).ToList();
        Shuffle(sevens);
        Shuffle(others);

        var wheel = new WheelSlot[36];
        var sevenIndex = 0;
        var otherIndex = 0;
        for (var index = 0; index < wheel.Length; index++)
            wheel[index] = index % 6 == 0 ? sevens[sevenIndex++] : others[otherIndex++];
        return wheel;
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (var index = items.Count - 1; index > 0; index--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (items[index], items[swapIndex]) = (items[swapIndex], items[index]);
        }
    }
}
