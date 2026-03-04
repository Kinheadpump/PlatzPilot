# Refactor Report (WIP)

## Commits / PRs
- chore: add config example and report skeleton
- test: add safe arrival characterization tests
- chore(test): tune test project build settings
- refactor(forecast): stabilize beta-binomial and n_eff
- refactor(services): extract study space feature loader
- refactor(navigation): centralize external links
- refactor(config): split app config models

## Config Keys Extracted
| Key | Default | Used In | Notes |
| --- | --- | --- | --- |
| (none yet) |  |  | Existing keys kept; upcoming refactors will extract additional literals. |

## Files Split / Extracted
- Configuration/AppConfig.cs -> split into dedicated config classes under Configuration/
- Services/StudySpaceFeatureService.cs extracted from ViewModels/MainPageViewModel.cs
- Services/NavigationService.cs extracted from ViewModels/MainPageViewModel.cs

## Tests
- Build: `dotnet clean -f net10.0-windows10.0.19041.0` then `dotnet build -f net10.0-windows10.0.19041.0` (success)
- Tests: `dotnet test .\\PlatzPilot.Tests\\PlatzPilot.Tests.csproj` (fails: CS7065 Win32 resources symbol stream format)

## Migrations / Notes
- Test project still fails with CS7065; no safe code deletions until resolved. Documented for follow-up.
