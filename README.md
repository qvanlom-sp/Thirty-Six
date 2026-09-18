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
