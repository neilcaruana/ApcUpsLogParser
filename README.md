# ApcUpsLogParser

ApcUpsLogParser is a .NET 9 project for parsing, analyzing, and visualizing APC UPS PowerChute voltage log files.

It includes:

- A shared parser and analysis library.
- An ASP.NET Core web dashboard with charting and live updates.
- An optional Windows Forms desktop viewer.

## Features

- Parse APC PowerChute `DataLog` files.
- Display voltage history for today, live data, custom day ranges, and today-vs-yesterday comparison.
- Calculate min, max, average, range, compliance percentage, and data gaps.
- Serve a lightweight browser dashboard from ASP.NET Core.
- Use SignalR to refresh the dashboard when the source log changes.
- Keep the source log path configurable so no personal or machine-specific paths are stored in source control.

## Repository Layout

| Path | Purpose |
|---|---|
| `ApcUpsLogParser.Common` | Shared DTOs, models, constants, log reader, data processing, and voltage analysis services. |
| `APCLogFileAnalyser.Web` | ASP.NET Core web host and static dashboard. The project is renamed to `ApcUpsLogParser.Web`; the folder can be renamed after closing Visual Studio if it is not locked. |
| `ApcUpsLogParser.Desktop.csproj` | Optional Windows Forms desktop viewer. |
| `.github/copilot-instructions.md` | Repository-specific Copilot context. |
| `.github/instructions/production-senior-software-developer.instructions.md` | Senior production engineering guidance for coding agents. |

## Requirements

- .NET 9 SDK
- An APC PowerChute log file, usually named `DataLog`
- Windows for the optional desktop viewer

## Configuration

The parser reads the source log path from the `APC_UPS_LOG_PATH` environment variable.

If `APC_UPS_LOG_PATH` is not set, the application looks for a local file named `DataLog` under the application base directory. This fallback is safe for samples and local development.

### Windows PowerShell

```powershell
$env:APC_UPS_LOG_PATH = "C:\Path\To\PowerChute\DataLog"
```

### Command Prompt

```cmd
set APC_UPS_LOG_PATH=C:\Path\To\PowerChute\DataLog
```

### Bash

```bash
export APC_UPS_LOG_PATH=/path/to/DataLog
```

## Run the Web Dashboard

```bash
dotnet run --project APCLogFileAnalyser.Web/ApcUpsLogParser.Web.csproj
```

The app starts on the first available local port from its configured list. Check the console output for the URL.

Useful endpoints:

- Dashboard: `/`
- Voltage API: `/api/voltage/data`
- Health check: `/api/voltage/health`
- SignalR hub: `/hubs/voltage`
- OpenAPI UI: `/swagger`

## Build

```bash
dotnet build ApcUpsLogParser.sln
```

The solution currently includes the shared library and web dashboard. The desktop project can be built directly on Windows:

```bash
dotnet build ApcUpsLogParser.Desktop.csproj
```

## Data Format

The parser expects APC PowerChute log rows with:

- Timestamp in the first 19 characters using `MM/dd/yyyy HH:mm:ss`
- Whitespace-separated data columns after the timestamp
- Voltage in the configured voltage column, currently column 3 in `Constants.VOLTAGE_COLUMN`

## Security and Public Repository Notes

This repository is prepared to avoid committing private machine state:

- No hard-coded personal log path is required.
- `.vs`, `bin`, `obj`, `.user`, local environment files, logs, and runtime `DataLog` files are ignored.
- The web dashboard uses same-origin requests and does not enable permissive CORS by default.
- Do not commit real UPS logs if they contain sensitive location, host, or operational data.

## Development Notes

- Target framework: .NET 9
- Nullable reference types are enabled.
- Keep parsing and analysis logic in `ApcUpsLogParser.Common`.
- Keep controller code thin and dashboard code dependency-light.
- Run a build before publishing changes.

## License

No license has been selected yet. Add a license before publishing as a true open-source project.
