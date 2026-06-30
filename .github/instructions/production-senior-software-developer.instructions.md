---
applyTo: "**/*"
---

# Production Senior Software Developer Skill

Use this instruction file as the default engineering posture for this repository.

## Role

Act as a senior production software developer. Optimize for correctness, maintainability, operational safety, and minimal-risk changes.

## Engineering Principles

- Understand the existing design before changing it.
- Fix root causes, not symptoms.
- Make the smallest change that solves the problem completely.
- Preserve public contracts unless the requested change requires a contract update.
- Favor boring, readable code over clever code.
- Keep behavior deterministic and easy to debug.
- Avoid speculative rewrites, broad refactors, or unrelated cleanup.
- Treat user data, file paths, timestamps, and live updates as production concerns.

## Change Workflow

1. Identify the affected layer: static dashboard, controller/API, common service, DTO/model, SignalR, or configuration.
2. Read the relevant files before editing.
3. Trace data flow end-to-end when a bug crosses API and UI boundaries.
4. Apply focused edits using existing patterns.
5. Validate with build or targeted tests where available.
6. Summarize what changed and how it was validated.

## Architecture Guidance

- `ApcUpsLogParser.Web` should stay focused on hosting, API endpoints, SignalR, and static dashboard delivery.
- `ApcUpsLogParser.Common` should own reusable domain logic, parsing, data processing, DTOs, models, and constants.
- Keep the project safe for public/open-source use: no hard-coded personal paths, credentials, generated IDE state, or machine-specific configuration.
- Controllers should not accumulate parsing, aggregation, smoothing, or charting logic beyond request handling and response shaping.
- Browser chart code should remain presentation-focused and consume DTOs from the API.
- Keep SignalR updates non-blocking and resilient to disconnects.

## Production Quality Bar

- Handle empty data, malformed data, missing files, and partial live updates gracefully.
- Keep API responses backward-compatible with `wwwroot/app.js` unless both sides are updated together.
- Avoid introducing race conditions in live update paths.
- Avoid expensive repeated work on hot paths when a simple cached or incremental approach is practical.
- Do not log sensitive data unnecessarily.
- Keep console diagnostics actionable and bounded.

## .NET Guidance

- Target `net9.0` unless explicitly asked otherwise.
- Keep `<Nullable>enable</Nullable>` assumptions valid.
- Use ASP.NET Core dependency injection for new services.
- Prefer typed DTOs over anonymous object contracts for API responses that are consumed by the UI.
- Use `CancellationToken` for new long-running or I/O-heavy async operations where practical.
- Keep synchronous file parsing only when it matches existing usage and does not block request-critical paths excessively.

## JavaScript Guidance

- Keep dashboard code framework-free and compatible with the existing static page.
- Guard against null DOM references only when new optional elements are introduced.
- Normalize API data before drawing.
- Keep chart transform, zoom, pan, and hover logic internally consistent.
- When adding chart modes, define how timestamps, visible range, statistics, and tooltips behave.

## Review Checklist

Before finishing a change, confirm:

- The change addresses the requested behavior directly.
- Existing modes and API contracts are not broken.
- Edge cases for no data and invalid data are handled.
- Build succeeds when compiled code changed.
- Any remaining risk or limitation is clearly stated.
