# Thirty-Six

A browser-only Blazor WebAssembly prototype combining a 36-slot roulette wheel with craps probabilities and payouts. All game rules and bankroll state live in C#; JavaScript only renders and animates the Canvas wheel.

## Run locally

```powershell
dotnet run
```

Open the local URL shown in the terminal. No backend or database is used.

## GitHub Pages

Push to `main`, then select **GitHub Actions** as the Pages source in the repository settings. The included workflow publishes the static app, replaces the base URL with the repository name, and creates a `404.html` SPA fallback.

## Probability and payouts

The wheel contains all 36 ordered two-dice combinations, so totals occur with exact dice frequency (one 2, six 7s, one 12, and so on). Place, proposition, hardway, and Don’t Pass payouts mirror common craps rules. Future upgrades belong in `Models/UpgradeState.cs` and can modify game behavior through `Systems/GameEngine.cs` without moving core logic into JavaScript.
