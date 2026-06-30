# Copilot Instructions for ApcUpsLogParser

## Solution Context

ApcUpsLogParser is a .NET 9 open-source solution for parsing and visualizing APC UPS PowerChute voltage log data.

Projects:

- `ApcUpsLogParser.Web` — ASP.NET Core web application that serves the static dashboard, exposes voltage APIs, and hosts a SignalR hub.
- `ApcUpsLogParser.Common` — shared models, DTOs, configuration constants, log reading, data processing, and voltage analysis services.
- `ApcUpsLogParser.Desktop` — optional Windows Forms desktop viewer using the shared parser and analysis code.

Primary runtime flow:

1. `ApcUpsLogParser.Web` serves `wwwroot/index.html` and `wwwroot/app.js`.
2. The browser posts requests to `/api/voltage/data`.
3. `VoltageController` delegates to `VoltageAnalysisService`.
4. `VoltageAnalysisService` reads APC PowerChute log data through `LogReader` and processes it through `DataProcessor`.
5. `VoltageHub` and `FileWatcherService` support live updates.

## Repository Instructions

Always apply the senior production development rules in:

- `.github/instructions/production-senior-software-developer.instructions.md`

## Coding Standards

- Target .NET 9 and keep nullable reference types enabled.
- Preserve existing project style unless changing a file that already follows a different local convention.
- Keep changes small, focused, and production-safe.
- Prefer clear, deterministic behavior over clever implementations.
- Avoid introducing new packages unless there is a strong reason.
- Use existing services and models before adding new abstractions.
- Do not silently hide failures; return useful errors or log actionable diagnostics.
- Validate changes with a build when code is modified.

## C# Guidelines

- Use `var` when the type is obvious from the right side.
- Prefer `async` APIs for I/O-bound work when adding new request, file, or network operations.
- Keep DTOs simple and serializable.
- Keep controller actions thin; put business logic in `ApcUpsLogParser.Common` services.
- Preserve nullable safety and avoid null-forgiving operators unless unavoidable.
- Use culture-aware parsing only where input requires it; APC log timestamps currently use `MM/dd/yyyy HH:mm:ss` with `CultureInfo.InvariantCulture`.

## JavaScript Dashboard Guidelines

- Keep `wwwroot/app.js` dependency-free unless there is a clear production need.
- Preserve the existing immediate-invoked function expression structure.
- Use existing chart state variables and helper functions before adding new global state.
- Handle invalid or missing API data defensively.
- Keep live, today, days, and compare modes working consistently.
- In compare mode, align historical comparison data by time-of-day when plotting against the current day.

## Data and Domain Rules

- Source log path is configured through the `APC_UPS_LOG_PATH` environment variable, with a safe local `DataLog` fallback for samples or development.
- Voltage column handling uses zero-based indexing internally and `Constants.VOLTAGE_COLUMN - 1` from callers.
- Nominal voltage is 230V with ±23V tolerance.
- Data gaps are detected using `Constants.GAP_THRESHOLD_MINUTES`.
- Do not add personal machine paths, generated IDE state, publish profiles with user settings, or secrets to the repository.
- Avoid mutating shared input data unexpectedly. If processing transforms values, prefer working on local lists or clearly scoped objects.

## Testing and Validation

- Build the solution after code changes.
- For UI changes, verify the affected mode in `app.js` logic: live, today, days, or compare.
- For API/data changes, verify request/response DTO compatibility with the browser code.
- For parsing changes, preserve support for existing APC PowerChute log format.
