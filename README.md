# Thirty-Six

A browser-only Blazor WebAssembly game combining a 36-slot roulette wheel with craps probabilities, persistent upgrades, and reusable power-ups. All game rules and bankroll state live in C#; JavaScript only renders and animates the Canvas wheel and ball.

## Run locally

```powershell
dotnet run
```

Open the local URL shown in the terminal. No backend or database is used.

## GitHub Pages

Push to `main`, then select **GitHub Actions** as the Pages source in the repository settings. The included workflow publishes the static app, replaces the base URL with the repository name, and creates a `404.html` SPA fallback.

## Probability and payouts

The wheel contains all 36 ordered two-dice combinations, so totals occur with exact dice frequency (one 2, six evenly spaced 7s, one 12, and so on). A safe come-out spin chooses the point and deals two cards from a balanced 16-card bonus deck. Every non-7 total is a persistent crapless-style place bet; gold wheel markers show every slot covered by your current bets. A red 7 ends the round, reports the exact net table loss after protection, and opens the upgrade shop. The point pays a 1.5× bonus whenever it hits, including hardways, and the table shows the combined payout increase from upgrades, cards, the point, and active powers. Reaching $20,000 completes the main goal and reveals lifetime statistics, while endless play remains available.

## Expanded play loop

- Consecutive number/hardway winning spins add 5% per previous win, capped at 20%. A miss resets the streak; Extra Life preserves it. Bonuses use the actual prize, not the size of the bankroll.
- Each round draws three unique challenges from a 12-objective pool: one common objective (wins, low/high, even/odd) and two specialists (point hits, hardways, outer totals, distinct numbers, streaks, prize totals, or powered wins). Power Play only appears after a payout power is unlocked. Each pays once per round: 50% of the triggering number/hardway prize, capped at $50–$100 depending on the objective. Streak and challenge bonuses do not multiply each other; losses and unbacked results never advance challenges.
- After three betting spins, bank all table chips and enter the shop voluntarily. Returning chips and cashing out grant no extra prize. Repeat Last Layout restores the last spun bets only onto an empty table and only when affordable.
- The first four betting spins have a visible welcome shield blocking all sevens. After that, the wheel uses normal dice frequencies except for clearly marked card/power blocks.
- Win effects scale with the payout, with special challenge and Extra Life celebrations. Seven-out plays a short descending low chime, even if a red-7 wager paid; Extra Life retains its ascending saved chime. All audio follows the Sound checkbox independently of reduced motion. Optional quick spins and recent results are available. System reduced-motion preferences are respected.
- Chips are fictional. Progress lasts for the current page session; refreshing starts over.

## Verify rules

```powershell
dotnet run --project Tests/EngineChecks.csproj
node Tests/audio-checks.mjs
dotnet publish -c Release -o publish
```

The dependency-free engine checks exercise payouts, loss accounting, streak caps, randomized challenge eligibility and rewards, all power timers, safe spins, cash-out/rebet, bailout, and victory. Audio checks exercise mute, seven-out, red-7 prizes, Extra Life, and reduced-motion routing. Test sources are excluded from the browser app.

## Spin-based power balance

Cooldowns start on activation, including active spins, and advance only on resolved betting spins with chips at risk. Protected spins count; come-out, empty spins, shop visits, and cash-outs do not. Cooldowns carry across rounds. Active effects end on expiry, consumption, or round end. A ready power can be used again in the same round.

| Power | Active window | Cooldown | Maximum active share of a full cycle |
| --- | --- | --- | --- |
| Lucky 5s | 3 spins | 9 spins | 33% |
| Hi / Lo | 4 spins | 14 spins | 29% |
| Nothing Easy | 3 spins | 10 spins | 30% |
| Point Press | 3 spins | 9 spins | 33% |
| Hot Hand | Up to 3 spins; consumed on a win | 10 spins | 30% |
| Bankroll Guard | 4 spins | 10 spins | 40% |
| Play It Safe | 3 spins | 12 spins | 25% |
| Extra Life | Up to 6 spins; consumed on a 7 | 20 spins | 30% |

Unlock prices and payout multipliers remain unchanged. Fixed windows prevent indefinite boosted rounds; protection always has downtime, and cycling through the shop cannot bypass cooldowns. Challenge rewards stay wager-scaled and capped, with at most three claims per round.
