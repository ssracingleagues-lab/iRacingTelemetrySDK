# ScoringTower

A terminal-based scoring tower sample inspired by broadcast timing graphics.

The tower combines iRacing session metadata with live per-car telemetry to show:

- event/session header with a green status indicator
- current lap and total laps when available
- car position, number, abbreviated driver name, and gap to the leader
- alternating dark rows and green position markers for the top five

## Run

Live iRacing telemetry:

```bash
dotnet run --project Samples/ScoringTower/ScoringTower.csproj
```

IBT playback:

```bash
dotnet run --project Samples/ScoringTower/ScoringTower.csproj -- "Samples/data/formulair04_tsukuba 2kfull 2024-01-09 17-26-10.ibt"
```

Optional display switches:

```bash
dotnet run --project Samples/ScoringTower/ScoringTower.csproj -- \
  "Samples/data/formulair04_tsukuba 2kfull 2024-01-09 17-26-10.ibt" \
  --title "FORD ECOBOOST 200" \
  --rows 20 \
  --refresh 500 \
  --speed 1
```
