# Kill

A small, cross-terminal .NET 9 CLI for discovering and acting on running Windows processes using a powerful fuzzy finder. It lists processes with their owning user, supports regex search, and is designed to be fast, safe, and scriptable.

Note: The current implementation focuses on discovering processes; the CLI prints process details and is a foundation for interactive selection and killing.

## Features

- Fuzzy process discovery across both ProcessName and full CommandLine
  - Ranking with bonuses for word boundaries and consecutive matches
  - Only returns results with score ≥ 3 × query length
- Optional regex mode for exact pattern matching
- Each result includes user/domain information (via WMI)
- Robust against access-denied and transient process exit scenarios

## Requirements

- Windows (uses WMI to retrieve command lines and process owners)
- .NET 9 SDK (build) or .NET 9 runtime (run)

## Build

```powershell
# From the repository root
dotnet build
```

## Run

```powershell
# From the repository root
dotnet run --project .\kill -- <query> [options]

# Or run compiled binary (after build)
.\kill\bin\Debug\net9.0\kill.exe <query> [options]
```

### Arguments and options

- query (required): Text to match against process name and command line
- -r, --regex: Treat query as a .NET regex pattern (case-insensitive)
- -f, --force: Bypass interactive selection (select all matches); when kill is implemented, skip confirmation
- -d, --details: Reserved for future use (show additional details)

Example:

```powershell
# Fuzzy find Chrome processes
.\kill\bin\Debug\net9.0\kill.exe chrome

# Regex search for powershell with specific argument
.\kill\bin\Debug\net9.0\kill.exe --regex "^pwsh(.|\n)*-NoProfile"
```

## Fuzzy matching details

Fuzzy matches score characters in order with bonuses for word boundaries (start of words, separators, case humps) and consecutive runs. Results are filtered to only include matches with a score ≥ 3 × length(query). Among included items, results are ordered by:

1) Highest score
2) Shorter process name length
3) Lower PID (stable tie-breaker)

Regex mode bypasses fuzzy scoring and simply includes any items that match the regex.

## Security and permissions

- Command line and owner resolution use WMI (Win32_Process), which may fail for protected/system processes; such failures are handled gracefully.
- User and Domain are shown when available; otherwise, they may be null/empty.

## Project layout

- kill/Program.cs: CLI entry point and options
- kill/Processes.cs: ProcessManager with fuzzy/regex discovery
- kill/ProcessWithUser.cs: Process wrapper that includes owning user/domain

## Roadmap ideas

- Enhance the interactive picker with filtering and multi-select
- Add confirmation and --force kill logic
- Rich console output (colors, columns)
- Unit tests

## License

MIT (or as you prefer). Update this section to your desired license.
