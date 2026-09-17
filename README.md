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

The wheel contains all 36 ordered two-dice combinations, so totals occur with exact dice frequency (one 2, six evenly spaced 7s, one 12, and so on). A safe come-out spin chooses the point from 4/5/6/8/9/10, betting remains open until a red 7 ends the round, and the shop then offers permanent number upgrades and power unlocks. The point pays a 1.5× bonus whenever it hits, including hardways. Players below $5 can complete a short manual dishwashing bailout for $200.
