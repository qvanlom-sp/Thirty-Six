using Casino_Game.Models;
using Casino_Game.Systems;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
    Console.WriteLine($"PASS {name}");
}
GameEngine Table()
{
    var game = new GameEngine();
    game.Resolve(new WheelSlot(2, 2));
    game.State.ActiveCards.Clear();
    game.State.Player.Bankroll = 500;
    game.State.Challenges.Clear();
    foreach (var kind in new[] { ChallengeKind.Wins, ChallengeKind.Point, ChallengeKind.Hardway })
        game.State.Challenges.Add(new(GameEngine.ChallengeDeck.Single(c => c.Kind == kind)));
    return game;
}

var game = Table();
Check(game.Wheel.Count == 36 && game.Wheel.Distinct().Count() == 36, "all ordered dice combinations exist");
Check(game.Wheel.Select((s, i) => (s, i)).Where(x => x.s.Total == 7).All(x => game.IsSlotBlocked(x.i)), "welcome shield visible on all sevens");
for (var i = 0; i < 500; i++) CheckOutcome(game.PickOutcome().Slot.Total != 7);
void CheckOutcome(bool valid) { if (!valid) throw new Exception("shield selected seven"); }
Check(true, "500 welcome outcomes exclude seven");
game.State.LifetimeBettingSpins = 4;
Check(game.Wheel.Select((s, i) => (s, i)).Where(x => x.s.Total == 7).All(x => !game.IsSlotBlocked(x.i)), "shield expires after four betting spins");
game.State.Player.SelectedChip = -5;
Check(!game.PlaceBet(BetTarget.Number6) && !game.PlaceBetAcrossNumbers(), "negative wagers rejected");
game.State.Player.SelectedChip = 5;
Check(!game.PlaceBet((BetTarget)99), "invalid targets rejected");
game.PlaceBet(BetTarget.Seven);
game.Resolve(new WheelSlot(3, 3));
Check(game.State.LastLoss == 5 && game.State.Bets.Count == 0 && game.State.Player.Bankroll == 495, "red seven loss reported and debited exactly once");

game = Table();
game.PlaceBet(BetTarget.Number6);
game.PrepareSpin();
Check(!game.CashOut(), "cannot cash out before three spins");
for (var i = 0; i < 6; i++) game.Resolve(new WheelSlot(3, 3));
Check(game.State.WinStreak == 6 && game.State.Stats.BestStreak == 6, "streak records consecutive wins");
Check(game.State.LastStreakBonus == 1.17m, "streak bonus capped at twenty percent");
Check(game.State.Stats.ChallengesCompleted == 1, "three win challenge pays only once and number-only doubles do not count as hardways");
game.Resolve(new WheelSlot(1, 2));
Check(game.State.WinStreak == 0, "miss resets streak");
var before = game.State.Player.Bankroll;
Check(game.CashOut() && game.State.Player.Bankroll == before + 5 && game.State.TotalOnTable == 0 && game.State.Phase == GamePhase.Shop, "cash out returns stake and opens shop");
Check(!game.CashOut(), "cash out cannot duplicate stake");
game.LeaveShop();
game.Resolve(new WheelSlot(2, 2));
Check(game.State.Challenges.Count == 3 && game.State.Challenges.All(c => c.Progress == 0 && !c.IsComplete), "round challenges reset");
Check(game.Rebet() && game.State.Bets[BetTarget.Number6] == 5, "previous layout restored");
Check(!game.Rebet(), "repeat layout cannot stack onto live wagers");

game = Table();
game.PlaceBet(BetTarget.Hard4);
game.Resolve(new WheelSlot(2, 2));
Check(game.State.LastChallengeReward == 26.25m && game.State.Challenges.Single(c => c.Definition.Kind == ChallengeKind.Hardway).IsComplete, "hardway challenge reward proportional to actual prize");
game.Resolve(new WheelSlot(1, 3));
Check(game.State.LastLoss == 5 && game.State.TotalOnTable == 0, "easy result removes hardway");

game = Table();
game.PlaceBet(BetTarget.Number6);
game.State.ActiveCards.Add(GameEngine.CardDeck.Single(x => x.Effect == CardEffect.SafetyNet));
game.Resolve(new WheelSlot(1, 6));
Check(game.State.LastLoss == 4 && game.State.LastPayout == 0 && game.State.Player.Bankroll == 496, "protection returns principal without inflating winnings");
game = Table();
game.State.Player.Bankroll = 5;
game.PlaceBet(BetTarget.Seven);
game.Resolve(new WheelSlot(1, 2));
Check(game.State.Phase == GamePhase.Bailout, "last-chip red seven loss cannot softlock empty table");
game.CompleteBailout();
Check(game.State.Player.Bankroll == 200 && game.State.Phase == GamePhase.Shop, "bailout restores playable bankroll");

game = Table();
game.State.Player.Bankroll = 4;
game.State.Bets[BetTarget.Number6] = 4;
game.Resolve(new WheelSlot(1, 2));
Check(game.State.Phase == GamePhase.BettingRound && game.State.TotalOnTable == 4, "split rack and table wealth does not trigger bailout or discard chips");

game = Table();
game.State.Player.Bankroll = 1;
game.State.Bets[BetTarget.Number6] = 2;
game.Resolve(new WheelSlot(1, 2));
Check(game.State.Phase == GamePhase.Bailout && game.State.Player.Bankroll == 3, "sub-minimum remaining table chips returned before bailout");

game = Table();
game.PlaceBet(BetTarget.Number6);
game.State.Player.Bankroll = 19995;
game.ClearBets();
Check(game.State.ShowVictory, "returned chips can reach victory goal");
var seenChallenges = new HashSet<ChallengeKind>();
for (var round = 0; round < 200; round++)
{
    var dealt = new GameEngine();
    dealt.Resolve(new WheelSlot(2, 2));
    var hand = dealt.State.Challenges;
    if (hand.Count != 3 || hand.Select(c => c.Definition.Kind).Distinct().Count() != 3 || hand.Count(c => c.Definition.Common) != 1)
        throw new Exception("Challenge draw must contain one common and two unique specialists");
    if (hand.Any(c => c.Definition.Kind == ChallengeKind.Powered)) throw new Exception("Locked power challenge dealt");
    foreach (var challenge in hand) seenChallenges.Add(challenge.Definition.Kind);
}
Check(seenChallenges.Count == 11, "200 random hands cover all eleven initially eligible challenges without duplicates");

void SetChallenge(GameEngine engine, ChallengeKind kind)
{
    engine.State.Challenges.Clear();
    engine.State.Challenges.Add(new(GameEngine.ChallengeDeck.Single(c => c.Kind == kind)));
}
foreach (var definition in GameEngine.ChallengeDeck)
{
    var trial = Table();
    SetChallenge(trial, definition.Kind);
    trial.State.Player.SelectedChip = 25;
    trial.PlaceBetAcrossNumbers();
    trial.PlaceBet(BetTarget.Hard4);
    trial.State.Upgrades.Powers[PowerId.HotHand].IsUnlocked = true;
    for (var hit = 0; hit < 6 && !trial.State.Challenges[0].IsComplete; hit++)
    {
        // Keep a valid payout power active for the power-specific eligibility check.
        if (definition.Kind == ChallengeKind.Powered)
        {
            trial.State.Upgrades.Powers[PowerId.HotHand].CooldownRemaining = 0;
            trial.ActivatePower(PowerId.HotHand);
        }
        var result = definition.Kind switch
        {
            ChallengeKind.High => new WheelSlot(4, 4),
            ChallengeKind.Odd => new WheelSlot(2, 3),
            ChallengeKind.Outer => new WheelSlot(1, 1),
            ChallengeKind.Variety => new WheelSlot(1, hit + 1),
            _ => new WheelSlot(2, 2)
        };
        trial.Resolve(result);
    }
    Check(trial.State.Challenges[0].IsComplete && trial.State.LastChallengeReward <= definition.RewardCap, $"{definition.Name} completes with capped reward");
    trial.Resolve(new WheelSlot(2, 2));
    Check(trial.State.LastChallengeReward == 0, $"{definition.Name} pays only once");
}

game = Table();
SetChallenge(game, ChallengeKind.Variety);
game.PlaceBet(BetTarget.Number6);
game.Resolve(new WheelSlot(3, 3));
game.Resolve(new WheelSlot(3, 3));
Check(game.State.Challenges[0].Progress == 1, "variety counts distinct winning totals only");
SetChallenge(game, ChallengeKind.Streak);
game.Resolve(new WheelSlot(1, 2));
Check(game.State.Challenges[0].Progress == 0, "streak challenge resets on a miss");
SetChallenge(game, ChallengeKind.Powered);
game.State.Upgrades.Powers[PowerId.NothingEasy].IsUnlocked = true;
game.ActivatePower(PowerId.NothingEasy);
game.Resolve(new WheelSlot(3, 3));
Check(game.State.Challenges[0].Progress == 0, "hardway boost cannot advance power challenge on a number-only wager");

foreach (var definition in GameEngine.PowerDefinitions)
{
    game = Table();
    game.State.Challenges.Clear();
    game.PlaceBet(BetTarget.Number6);
    var power = game.State.Upgrades.Powers[definition.Id];
    power.IsUnlocked = true;
    Check(game.ActivatePower(definition.Id) && !game.ActivatePower(definition.Id), $"{definition.Name} starts once");
    for (var spin = 1; spin <= definition.CooldownSpins; spin++)
    {
        game.Resolve(new WheelSlot(1, 2));
        if (power.CooldownRemaining != definition.CooldownSpins - spin || power.IsActive != (spin < definition.DurationSpins))
            throw new Exception($"{definition.Name} timer mismatch on spin {spin}");
    }
    Check(game.ActivatePower(definition.Id), $"{definition.Name} can reactivate in same round after exact cooldown");
}

game = Table();
game.PlaceBet(BetTarget.Number6);
var guard = game.State.Upgrades.Powers[PowerId.BankrollGuard];
guard.IsUnlocked = true;
game.ActivatePower(PowerId.BankrollGuard);
for (var i = 0; i < 3; i++) game.Resolve(new WheelSlot(1, 2));
game.Resolve(new WheelSlot(1, 6));
Check(game.State.LastLoss == 3.75m && guard.CooldownRemaining == 6, "guard protects its final active spin and seven-out ticks exactly once");
game.LeaveShop();
game.Resolve(new WheelSlot(2, 2));
Check(guard.CooldownRemaining == 6 && !guard.IsActive, "cooldown persists across shop and come-out");
game.Resolve(new WheelSlot(1, 2));
Check(guard.CooldownRemaining == 6, "empty spins cannot recharge powers");
game.PlaceBet(BetTarget.Number6);
for (var i = 0; i < 3; i++) game.Resolve(new WheelSlot(1, 2));
game.CashOut();
Check(guard.CooldownRemaining == 3, "cash-out does not grant an extra cooldown tick");

game = Table();
game.PlaceBet(BetTarget.Number6);
var life = game.State.Upgrades.Powers[PowerId.ExtraLife];
life.IsUnlocked = true;
game.ActivatePower(PowerId.ExtraLife);
game.Resolve(new WheelSlot(1, 6));
Check(game.State.ExtraLifeSaved && game.State.TotalOnTable == 5 && life.CooldownRemaining == 19 && !life.IsActive,
    "saved seven consumes life and ticks cooldown once");
game.Resolve(new WheelSlot(1, 6));
Check(game.State.Phase == GamePhase.Shop && life.CooldownRemaining == 18, "extra life cannot save consecutive sevens");

game = Table();
game.State.Challenges.Clear();
game.PlaceBet(BetTarget.Number6);
var hot = game.State.Upgrades.Powers[PowerId.HotHand];
hot.IsUnlocked = true;
game.ActivatePower(PowerId.HotHand);
game.Resolve(new WheelSlot(3, 3));
Check(game.State.LastPayout == 10.21m && !hot.IsActive && hot.CooldownRemaining == 9, "hot hand boosts exactly one win then cools down");
Check(GameEngine.PowerDefinitions.All(p => p.CooldownSpins >= p.DurationSpins * 2), "every power has at least half its cycle without coverage");
Console.WriteLine($"{checks} checks passed.");
