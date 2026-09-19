using System.Security.Cryptography;
using Casino_Game.Models;

namespace Casino_Game.Systems;

public sealed class GameEngine
{
    private static readonly HashSet<int> PointNumbers = [4, 5, 6, 8, 9, 10];
    private static readonly HashSet<int> BetNumbers = [2, 3, 4, 5, 6, 8, 9, 10, 11, 12];
    private static readonly BetTarget[] HardTargets = [BetTarget.Hard4, BetTarget.Hard6, BetTarget.Hard8, BetTarget.Hard10];

    public static readonly IReadOnlyList<PowerDefinition> PowerDefinitions =
    [
        new(PowerId.LuckyFives, "Lucky 5s", "Winning 5 bets pay double for the next 3 betting spins.", 300m, 9, 3, "★"),
        new(PowerId.HiLo, "Hi / Lo", "Winning 2 and 12 bets pay five times their normal prize for 4 betting spins.", 450m, 14, 4, "↕"),
        new(PowerId.NothingEasy, "Nothing Easy", "Hardway wins pay double for the next 3 betting spins.", 550m, 10, 3, "H"),
        new(PowerId.PointPress, "Point Press", "Point-number wins pay another 50% for the next 3 betting spins.", 650m, 9, 3, "P"),
        new(PowerId.HotHand, "Hot Hand", "Your next winning number or hardway spin within 3 betting spins pays 75% more.", 750m, 10, 3, "♨"),
        new(PowerId.BankrollGuard, "Bankroll Guard", "For 4 betting spins, a seven-out returns 25% of losing table chips.", 900m, 10, 4, "▣"),
        new(PowerId.PlayItSafe, "Play It Safe", "Blocks three of the six red 7 slots for your next 3 betting spins.", 1250m, 12, 3, "◆"),
        new(PowerId.ExtraLife, "Extra Life", "Ignores the next 7 within 6 betting spins. Your number and hardway bets survive.", 2000m, 20, 6, "♥")
    ];

    public static readonly IReadOnlyList<ChallengeDefinition> ChallengeDeck =
    [
        new(ChallengeKind.Wins, "Three's a charm", "Win on three number or hardway spins.", 3, 50m, true),
        new(ChallengeKind.Low, "Low roller", "Win twice on totals 2 through 6.", 2, 50m, true),
        new(ChallengeKind.High, "High society", "Win twice on totals 8 through 12.", 2, 50m, true),
        new(ChallengeKind.Even, "Even better", "Win on three even totals.", 3, 50m, true),
        new(ChallengeKind.Odd, "Odd couple", "Win twice on odd totals (excluding 7).", 2, 60m, true),
        new(ChallengeKind.Point, "On point", "Win on the point twice.", 2, 75m),
        new(ChallengeKind.Hardway, "The hard way", "Win a hardway wager with matching dice.", 1, 100m),
        new(ChallengeKind.Outer, "Edge of glory", "Win a wager on 2, 3, 11, or 12.", 1, 75m),
        new(ChallengeKind.Variety, "Mix it up", "Win on three different totals.", 3, 60m),
        new(ChallengeKind.Streak, "Heating up", "Win on three consecutive spins. A miss resets progress.", 3, 60m),
        new(ChallengeKind.PrizeTotal, "Chip collector", "Earn $50 in number/hardway prizes before streak and challenge bonuses.", 50, 75m),
        new(ChallengeKind.Powered, "Power play", "Win twice with a payout-boosting power affecting your winning wager.", 2, 75m)
    ];

    public static readonly IReadOnlyList<RoundCard> CardDeck =
    [
        new("Low Road", "2·3", "violet", "+15% payout on 2 and 3.", CardEffect.LowNumbers, .15m),
        new("High Road", "J·Q", "purple", "+15% payout on 11 and 12.", CardEffect.HighNumbers, .15m),
        new("Green Pair", "6·8", "green", "+5% payout on 6 and 8.", CardEffect.SixEight, .05m),
        new("Blue Line", "4·10", "blue", "+8% payout on 4 and 10.", CardEffect.FourTen, .08m),
        new("Teal Tide", "5·9", "teal", "+8% payout on 5 and 9.", CardEffect.FiveNine, .08m),
        new("Safety Net", "20%", "red", "Keep 20% of your losing table chips on seven-out.", CardEffect.SafetyNet, .20m),
        new("Missing Red", "−7", "red", "One of the six 7 slots is blocked this round.", CardEffect.BlockSeven, 1m),
        new("Hard Earned", "H", "gold", "+15% on hardway wins.", CardEffect.Hardways, .15m),
        new("Point Player", "●", "gold", "+10% on point-number wins.", CardEffect.PointBoost, .10m),
        new("Hot Start", "ⅠⅡⅢ", "red", "+10% on wins during the first three spins.", CardEffect.HotStart, .10m),
        new("Even Keel", "2·4·6", "green", "+5% on even-number wins.", CardEffect.EvenNumbers, .05m),
        new("Odd Company", "3·5·9", "purple", "+5% on odd-number wins.", CardEffect.OddNumbers, .05m),
        new("Small Stakes", "$10", "teal", "+8% on winning wagers of $10 or less.", CardEffect.SmallStakes, .08m),
        new("Big Stakes", "$25+", "blue", "+6% on winning wagers of $25 or more.", CardEffect.BigStakes, .06m),
        new("Table Favor", "+3%", "gold", "+3% on every number and hardway win.", CardEffect.AllNumbers, .03m),
        new("Pocket Chip", "$10", "green", "Receive $10 immediately when dealt.", CardEffect.PocketChip, 10m)
    ];

    public GameEngine()
    {
        State = new GameState();
        Wheel = BuildWheel();
    }

    public GameState State { get; }
    public IReadOnlyList<WheelSlot> Wheel { get; }
    public bool BettingIsOpen => State.Phase == GamePhase.BettingRound && !State.IsSpinning;
    public bool CanCashOut => BettingIsOpen && State.SpinsThisRound >= 3;
    public decimal RebetCost => State.PreviousBets.Values.Sum();
    public bool CanRebet => BettingIsOpen && State.Bets.Count == 0 && RebetCost > 0 && State.Player.Bankroll >= RebetCost;

    public bool Rebet()
    {
        if (!CanRebet) return false;
        foreach (var (target, amount) in State.PreviousBets)
        {
            State.Bets[target] = amount;
            State.Stats.WageredByTarget[target] = State.Stats.WageredByTarget.GetValueOrDefault(target) + amount;
        }
        State.Player.Bankroll -= RebetCost;
        State.Stats.TotalWagered += RebetCost;
        State.Message = $"Previous layout restored for {Money(RebetCost)}.";
        return true;
    }

    public bool CashOut()
    {
        if (!CanCashOut) return false;
        var returned = State.TotalOnTable;
        State.Player.Bankroll += returned;
        State.Stats.CashOuts++;
        EndRound();
        ResetResultEffects();
        State.Message = $"Cashed out {Money(returned)}. Your chips are safe. Visit the Back Room.";
        AddHistory(State.Message);
        CheckVictory();
        return true;
    }

    public bool PlaceBet(BetTarget target)
    {
        var chip = State.Player.SelectedChip;
        if (!BettingIsOpen || chip <= 0 || !Enum.IsDefined(target) || State.Player.Bankroll < chip)
            return false;

        State.Player.Bankroll -= chip;
        State.Bets[target] = State.Bets.GetValueOrDefault(target) + chip;
        State.Stats.TotalWagered += chip;
        State.Stats.WageredByTarget[target] = State.Stats.WageredByTarget.GetValueOrDefault(target) + chip;
        State.Message = $"${chip:0} placed on {GetName(target)}. Spin when you’re ready.";
        return true;
    }

    public bool PlaceBetAcrossNumbers()
    {
        var chip = State.Player.SelectedChip;
        if (!BettingIsOpen || chip <= 0 || State.Player.Bankroll < chip)
            return false;

        var share = chip / BetNumbers.Count;
        State.Player.Bankroll -= chip;
        State.AllNumbersBetTotal += chip;
        State.Stats.TotalWagered += chip;
        foreach (var number in BetNumbers)
        {
            var target = (BetTarget)number;
            State.Bets[target] = State.Bets.GetValueOrDefault(target) + share;
            State.Stats.WageredByTarget[target] = State.Stats.WageredByTarget.GetValueOrDefault(target) + share;
        }
        State.Message = $"{Money(chip)} spread across every number — {Money(share)} on each of the ten spots.";
        return true;
    }

    public void ClearBets()
    {
        if (!BettingIsOpen)
            return;

        State.Player.Bankroll += State.TotalOnTable;
        State.Bets.Clear();
        State.AllNumbersBetTotal = 0;
        State.Message = "Your chips are back in the rack.";
        CheckVictory();
    }

    public (int Index, WheelSlot Slot) PickOutcome()
    {
        var eligible = Enumerable.Range(0, Wheel.Count)
            .Where(index => State.Phase != GamePhase.ComeOut || PointNumbers.Contains(Wheel[index].Total))
            .Where(index => State.Phase != GamePhase.BettingRound || State.LifetimeBettingSpins >= 4 || Wheel[index].Total != 7)
            .Where(index => !IsSlotBlocked(index))
            .ToArray();
        var index = eligible[RandomNumberGenerator.GetInt32(eligible.Length)];
        return (index, Wheel[index]);
    }

    public void PrepareSpin()
    {
        ResetResultEffects();
        if (BettingIsOpen && State.Bets.Count > 0)
        {
            State.PreviousBets.Clear();
            foreach (var bet in State.Bets) State.PreviousBets.Add(bet.Key, bet.Value);
        }
    }

    public bool IsSlotBlocked(int index)
    {
        if (State.Phase == GamePhase.ComeOut)
            return !PointNumbers.Contains(Wheel[index].Total);

        if (Wheel[index].Total != 7)
            return false;

        if (State.LifetimeBettingSpins < 4)
            return true;

        var safe = State.Upgrades.Powers[PowerId.PlayItSafe];
        var blockedSevens = State.ActiveCards.Any(card => card.Effect == CardEffect.BlockSeven) ? 1 : 0;
        if (safe.IsActive && safe.SpinsRemaining > 0)
            blockedSevens += 3;
        if (blockedSevens == 0)
            return false;

        return Wheel.Select((slot, slotIndex) => (slot, slotIndex))
            .Where(item => item.slot.Total == 7)
            .Take(blockedSevens)
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
        if (powers[PowerId.PointPress].IsActive && State.Point == slot.Total)
            return true;
        if (powers[PowerId.HotHand].IsActive && slot.Total != 7)
            return true;
        if (powers[PowerId.ExtraLife].IsActive && slot.Total == 7)
            return true;
        if (powers[PowerId.BankrollGuard].IsActive && slot.Total == 7)
            return true;
        return powers[PowerId.PlayItSafe].IsActive && IsSlotBlocked(index);
    }

    public bool IsSlotCardAffected(int index) => IsNumberCardAffected(Wheel[index].Total, Wheel[index].IsHard);

    public bool IsSlotWagered(WheelSlot slot)
    {
        if (slot.Total == 7)
            return State.Bets.ContainsKey(BetTarget.Seven);

        var hasNumberBet = BetNumbers.Contains(slot.Total) && State.Bets.ContainsKey((BetTarget)slot.Total);
        if (!slot.IsHard)
            return hasNumberBet;

        var hardTarget = (BetTarget)(slot.Total + 100);
        return hasNumberBet || State.Bets.ContainsKey(hardTarget);
    }

    public bool IsNumberCardAffected(int number, bool isHard = false)
    {
        return State.ActiveCards.Any(card => card.Effect switch
        {
            CardEffect.LowNumbers => number is 2 or 3,
            CardEffect.HighNumbers => number is 11 or 12,
            CardEffect.SixEight => number is 6 or 8,
            CardEffect.FourTen => number is 4 or 10,
            CardEffect.FiveNine => number is 5 or 9,
            CardEffect.BlockSeven or CardEffect.SafetyNet => number == 7,
            CardEffect.Hardways => isHard,
            CardEffect.PointBoost => number == State.Point,
            CardEffect.HotStart => number != 7 && State.SpinsThisRound < 3,
            CardEffect.SmallStakes or CardEffect.BigStakes or CardEffect.AllNumbers => number != 7,
            CardEffect.EvenNumbers => number != 7 && number % 2 == 0,
            CardEffect.OddNumbers => number != 7 && number % 2 != 0,
            _ => false
        });
    }

    public void Resolve(WheelSlot result)
    {
        if (State.Phase is not (GamePhase.ComeOut or GamePhase.BettingRound)) return;
        if (State.Phase == GamePhase.BettingRound && State.TotalOnTable <= 0) return;
        ResetResultEffects();
        State.LastResult = result;
        State.Stats.Spins++;

        if (State.Phase == GamePhase.ComeOut)
        {
            State.Point = result.Total;
            State.Phase = GamePhase.BettingRound;
            State.SpinsThisRound = 0;
            DealCards();
            DealChallenges();
            State.Message = $"The point is {result.Total}. You drew {State.ActiveCards[0].Name} and {State.ActiveCards[1].Name}.";
            AddHistory(State.Message);
            CheckVictory();
            return;
        }

        if (State.Phase != GamePhase.BettingRound)
            return;

        State.SpinsThisRound++;
        State.LifetimeBettingSpins++;
        State.RecentRolls.Insert(0, result.Total);
        if (State.RecentRolls.Count > 12) State.RecentRolls.RemoveAt(12);
        var notes = new List<string>();
        var tableBeforeSpin = State.TotalOnTable;
        var sevenWager = State.Bets.GetValueOrDefault(BetTarget.Seven);
        ResolveSevenBet(result, notes);

        if (result.Total == 7 && ConsumeExtraLife())
        {
            State.LastWasSeven = true;
            State.ExtraLifeSaved = true;
            TickSpinPowers();
            State.Message = "Extra Life! The 7 was erased, your bets survived, and the round continues.";
            FinalizeSpinStats();
            AddHistory(State.Message);
            return;
        }

        var payoutBeforeNumberWinnings = State.LastPayout;
        ResolvePlaceBets(result, notes);
        ResolveHardways(result, notes);
        ApplyRoundProgress(result, State.LastPayout - payoutBeforeNumberWinnings);
        if (State.Upgrades.Powers[PowerId.HotHand].IsActive && State.LastPayout > payoutBeforeNumberWinnings)
            State.Upgrades.Powers[PowerId.HotHand].IsActive = false;

        if (result.Total == 7)
        {
            State.LastWasSeven = true;
            var losingBets = Math.Max(0m, tableBeforeSpin - sevenWager);
            var protectedAmount = ApplySevenProtection(losingBets);
            State.LastLoss = decimal.Round(losingBets - protectedAmount, 2);
            EndRound();
            var protectionText = protectedAmount > 0 ? $" {Money(protectedAmount)} was protected and returned." : string.Empty;
            var sevenWinText = sevenWager > 0 ? $" Your red 7 won {Money(sevenWager * 4m)} profit and returned its {Money(sevenWager)} chip." : string.Empty;
            State.Message = $"Seven out — {Money(State.LastLoss)} lost from the table.{protectionText}{sevenWinText}";
        }
        else if (State.LastPayout > 0)
        {
            State.LastWinNumber = result.Total;
            var hardLossText = State.LastLoss > 0 ? $" One-spin or hardway bets lost {Money(State.LastLoss)}." : string.Empty;
            State.Message = $"{result.Total} hits! You won {Money(State.LastPayout)}. Your number bets remain on the table.{hardLossText}";
        }
        else if (State.LastLoss > 0)
        {
            State.Message = $"{result.Total} landed. {Money(State.LastLoss)} in one-spin or hardway bets lost. Other number bets remain.";
        }
        else
        {
            State.Message = $"{result.Total} landed. No payout this spin. Your number bets remain on the table.";
        }

        TickSpinPowers();
        if (State.Phase == GamePhase.BettingRound && State.Player.Bankroll + State.TotalOnTable < 5m)
        {
            State.Player.Bankroll += State.TotalOnTable;
            EndRound();
            State.Message += " The kitchen has a fresh stake for you.";
        }
        if (State.LastStreakBonus > 0) State.Message += $" Streak bonus: {Money(State.LastStreakBonus)}.";
        if (State.LastChallengeReward > 0) State.Message += $" Challenge complete: +{Money(State.LastChallengeReward)}!";
        FinalizeSpinStats();
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
        if (!BettingIsOpen || !power.IsUnlocked || power.IsActive || power.CooldownRemaining > 0)
            return false;

        power.IsActive = true;
        var definition = PowerDefinitions.Single(item => item.Id == id);
        power.CooldownRemaining = definition.CooldownSpins;
        power.SpinsRemaining = definition.DurationSpins;
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
        State.ActiveCards.Clear();
        State.WinStreak = 0;
        State.Challenges.Clear();
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

    public void ContinueAfterVictory() => State.ShowVictory = false;

    public string MostBetName()
    {
        if (State.Stats.WageredByTarget.Count == 0)
            return "None yet";
        return GetName(State.Stats.WageredByTarget.MaxBy(item => item.Value).Key);
    }

    public decimal PayoutMultiplier(int number) => 1m + State.Upgrades.PayoutLevels.GetValueOrDefault(number) * .1m;

    public int TotalPayoutBonusPercent(int number, decimal wager, bool isHard = false)
    {
        var multiplier = (isHard ? 1m : PayoutMultiplier(number))
            * PointBonus(number)
            * PowerPayoutMultiplier(number, isHard)
            * CardPayoutMultiplier(number, wager, isHard, true);
        return (int)decimal.Round((multiplier - 1m) * 100m, 0, MidpointRounding.AwayFromZero);
    }

    public int SevenProtectionPercent()
    {
        var rate = State.ActiveCards.Where(card => card.Effect == CardEffect.SafetyNet).Sum(card => card.Value);
        if (State.Upgrades.Powers[PowerId.BankrollGuard].IsActive)
            rate += .25m;
        return (int)decimal.Round(rate * 100m, 0, MidpointRounding.AwayFromZero);
    }

    private void ResolveSevenBet(WheelSlot result, List<string> notes)
    {
        if (!State.Bets.Remove(BetTarget.Seven, out var sevenWager))
            return;
        if (result.Total == 7)
        {
            Award(sevenWager, 4m, true);
            notes.Add($"Red 7 won {Money(sevenWager * 4m)}.");
        }
        else State.LastLoss += sevenWager;
    }

    // Bonuses are proportional to actual prizes: tiny wagers cannot farm fixed rewards.
    private void ApplyRoundProgress(WheelSlot result, decimal numberPrize)
    {
        if (numberPrize <= 0)
        {
            State.WinStreak = 0;
            foreach (var challenge in State.Challenges.Where(c => !c.IsComplete && c.Definition.Kind == ChallengeKind.Streak))
                challenge.Progress = 0;
            return;
        }
        State.WinStreak++;
        State.Stats.BestStreak = Math.Max(State.Stats.BestStreak, State.WinStreak);
        State.LastStreakBonus = decimal.Round(numberPrize * Math.Min(4, State.WinStreak - 1) * .05m, 2);
        var hardwayWon = result.IsHard && State.Bets.ContainsKey((BetTarget)(result.Total + 100));
        var poweredWin = (State.Bets.ContainsKey((BetTarget)result.Total) && PowerPayoutMultiplier(result.Total, false) > 1)
            || (hardwayWon && PowerPayoutMultiplier(result.Total, true) > 1);
        foreach (var challenge in State.Challenges.Where(c => !c.IsComplete))
        {
            challenge.WinningNumbers.Add(result.Total);
            challenge.Progress = challenge.Definition.Kind switch
            {
                ChallengeKind.Variety => challenge.WinningNumbers.Count,
                ChallengeKind.Streak => State.WinStreak,
                ChallengeKind.PrizeTotal => challenge.Progress + numberPrize,
                _ => challenge.Progress + (challenge.Definition.Kind switch
                {
                    ChallengeKind.Wins => true,
                    ChallengeKind.Point => result.Total == State.Point,
                    ChallengeKind.Hardway => hardwayWon,
                    ChallengeKind.Low => result.Total <= 6,
                    ChallengeKind.High => result.Total >= 8,
                    ChallengeKind.Even => result.Total % 2 == 0,
                    ChallengeKind.Odd => result.Total % 2 != 0,
                    ChallengeKind.Outer => result.Total is 2 or 3 or 11 or 12,
                    ChallengeKind.Powered => poweredWin,
                    _ => false
                } ? 1 : 0)
            };
            if (challenge.Progress < challenge.Definition.Goal) continue;
            challenge.Progress = challenge.Definition.Goal;
            challenge.IsComplete = true;
            State.Stats.ChallengesCompleted++;
            State.LastChallengeReward += decimal.Round(Math.Min(challenge.Definition.RewardCap, numberPrize * .5m), 2);
        }
        var bonus = State.LastStreakBonus + State.LastChallengeReward;
        State.Player.Bankroll += bonus;
        State.LastPayout += bonus;
    }

    private void ResolvePlaceBets(WheelSlot result, List<string> notes)
    {
        foreach (var (target, wager) in State.Bets.ToArray())
        {
            var number = (int)target;
            if (!BetNumbers.Contains(number))
                continue;

            if (result.Total == 7)
            {
                State.Bets.Remove(target);
            }
            else if (result.Total == number)
            {
                var odds = PlaceOdds(number) * PayoutMultiplier(number) * PointBonus(number)
                    * PowerPayoutMultiplier(number, false) * CardPayoutMultiplier(number, wager, false);
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
                odds *= PointBonus(number)
                    * PowerPayoutMultiplier(number, true) * CardPayoutMultiplier(number, wager, true);
                Award(wager, odds, false);
                notes.Add($"Hard {number} paid {Money(wager * odds)}.");
            }
            else if (result.Total == number)
            {
                State.Bets.Remove(target);
                State.LastLoss += wager;
                notes.Add($"Easy {number} knocked down {Money(wager)} from Hard {number}.");
            }
            else if (result.Total == 7)
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

    private decimal CardPayoutMultiplier(int number, decimal wager, bool isHard, bool previewNextSpin = false)
    {
        var bonus = State.ActiveCards.Sum(card => card.Effect switch
        {
            CardEffect.LowNumbers when number is 2 or 3 => card.Value,
            CardEffect.HighNumbers when number is 11 or 12 => card.Value,
            CardEffect.SixEight when number is 6 or 8 => card.Value,
            CardEffect.FourTen when number is 4 or 10 => card.Value,
            CardEffect.FiveNine when number is 5 or 9 => card.Value,
            CardEffect.Hardways when isHard => card.Value,
            CardEffect.PointBoost when number == State.Point => card.Value,
            CardEffect.HotStart when (previewNextSpin ? State.SpinsThisRound < 3 : State.SpinsThisRound <= 3) => card.Value,
            CardEffect.EvenNumbers when number % 2 == 0 => card.Value,
            CardEffect.OddNumbers when number % 2 != 0 => card.Value,
            CardEffect.SmallStakes when wager <= 10m => card.Value,
            CardEffect.BigStakes when wager >= 25m => card.Value,
            CardEffect.AllNumbers => card.Value,
            _ => 0m
        });
        return 1m + bonus;
    }

    private decimal PowerPayoutMultiplier(int number, bool isHard)
    {
        var powers = State.Upgrades.Powers;
        var multiplier = 1m;
        if (powers[PowerId.LuckyFives].IsActive && number == 5)
            multiplier *= 2m;
        if (powers[PowerId.HiLo].IsActive && number is 2 or 12)
            multiplier *= 5m;
        if (powers[PowerId.NothingEasy].IsActive && isHard)
            multiplier *= 2m;
        if (powers[PowerId.PointPress].IsActive && number == State.Point)
            multiplier *= 1.5m;
        if (powers[PowerId.HotHand].IsActive)
            multiplier *= 1.75m;
        return multiplier;
    }

    private void DealCards()
    {
        State.ActiveCards.Clear();
        var available = CardDeck.ToList();
        for (var count = 0; count < 2; count++)
        {
            var index = RandomNumberGenerator.GetInt32(available.Count);
            var card = available[index];
            available.RemoveAt(index);
            State.ActiveCards.Add(card);
            if (card.Effect == CardEffect.PocketChip)
                State.Player.Bankroll += card.Value;
        }
    }

    private void DealChallenges()
    {
        State.Challenges.Clear();
        var hasPayoutPower = State.Upgrades.Powers.Any(p => p.Value.IsUnlocked &&
            p.Key is PowerId.LuckyFives or PowerId.HiLo or PowerId.NothingEasy or PowerId.PointPress or PowerId.HotHand);
        var common = ChallengeDeck.Where(c => c.Common).ToList();
        var specialist = ChallengeDeck.Where(c => !c.Common && (c.Kind != ChallengeKind.Powered || hasPayoutPower)).ToList();
        Shuffle(common);
        Shuffle(specialist);
        // Every hand has an accessible objective and two different specialist objectives.
        State.Challenges.Add(new(common[0]));
        State.Challenges.AddRange(specialist.Take(2).Select(c => new RoundChallenge(c)));
    }

    private decimal ApplySevenProtection(decimal tableBalance)
    {
        var rate = State.ActiveCards.Where(card => card.Effect == CardEffect.SafetyNet).Sum(card => card.Value);
        if (State.Upgrades.Powers[PowerId.BankrollGuard].IsActive)
            rate += .25m;
        var saved = decimal.Round(tableBalance * rate, 2);
        State.Player.Bankroll += saved;
        return saved;
    }

    private void FinalizeSpinStats()
    {
        State.Stats.TotalWon += State.LastPayout;
        State.Stats.BiggestSingleSpinPayout = Math.Max(State.Stats.BiggestSingleSpinPayout, State.LastPayout);
        CheckVictory();
    }

    private void CheckVictory()
    {
        if (State.GoalReached || State.Player.Bankroll < 20_000m)
            return;
        State.GoalReached = true;
        State.ShowVictory = true;
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
        // Only resolved, wagered betting spins count. Come-out and shop visits never recharge powers.
        foreach (var power in State.Upgrades.Powers.Values)
        {
            if (power.CooldownRemaining > 0) power.CooldownRemaining--;
            if (power.IsActive && --power.SpinsRemaining <= 0) power.IsActive = false;
            if (!power.IsActive) power.SpinsRemaining = 0;
        }
    }

    private void EndRound()
    {
        State.Stats.RoundsCompleted++;
        State.Point = null;
        State.Bets.Clear();
        State.AllNumbersBetTotal = 0;
        foreach (var definition in PowerDefinitions)
        {
            var power = State.Upgrades.Powers[definition.Id];
            power.IsActive = false;
            power.SpinsRemaining = 0;
        }

        State.Phase = State.Player.Bankroll < 5m ? GamePhase.Bailout : GamePhase.Shop;
    }

    private void ResetResultEffects()
    {
        State.LastPayout = 0;
        State.LastStreakBonus = State.LastChallengeReward = 0;
        State.LastLoss = 0;
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
        2 or 12 => 11m / 2m,
        3 or 11 => 11m / 4m,
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
