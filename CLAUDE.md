# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

# PART 1 — OPERATING RULES (read first, applies to every request)

These rules are set by the repository owner. They apply to **every** AI agent working in this
repository: the main session, subagents, background tasks, scheduled/cron runs, and hooks.

## The core rule

**Ask before you act on anything in the "Requires explicit approval" list below. No exceptions.**

- "Explicit approval" means the owner says yes **in this conversation, for this specific action**.
- Approval for one action is **not** approval for the next one, or for a repeat of the same action
  later in the session. Ask again.
- A pre-existing entry in `.claude/settings*.json` that would auto-approve one of these actions does
  **not** substitute for asking. If the permission system would let a listed action through silently,
  stop and ask anyway.
- If you are blocked and unsure whether something is covered: **treat it as covered and ask.**
- Never work around a denial. A denied request is a decision, not an obstacle to route around with a
  different tool, a subagent, a script, or a rephrased command.

## Requires explicit approval

### Network and outbound data
- Web searches and web fetches of any kind.
- HTTP/HTTPS calls to anything outside `localhost` — `curl`, `wget`, `Invoke-WebRequest`,
  `Invoke-RestMethod`, `fetch`, SDK clients, MCP tools that reach the internet.
- Sending any repository content, file, log, error message, or user data to an external service.
- Publishing anything outward-facing: Artifacts, gists, pastebins, issue/PR comments, chat messages.
- Cloning or pulling from remote repositories.

### Installing, downloading, or changing dependencies
- `npm install` / `npm ci` / `npm update` / `npx <anything>` / `npm audit fix`.
- `dotnet add package`, `dotnet restore` against new packages, `nuget install`,
  `dotnet tool install/update`, `dotnet workload install`.
- `winget`, `choco`, `scoop`, `pip`, or any other package or SDK installer.
- Hand-editing `package.json`, `package-lock.json`, or `.csproj` `<PackageReference>` entries to add,
  remove, or bump a dependency — proposing the change is fine, applying it is not.
- Downloading files, binaries, datasets, models, or certificates from anywhere.

### Running executables and processes
- Starting the app or any server: `dotnet run`, `dotnet watch`, `npm run dev`, `vite`, VS Code tasks
  that launch things, anything that binds a port or stays alive.
- Docker: `docker build`, `docker run`, `docker compose`, or anything touching the daemon.
- `dotnet build`, `dotnet publish`, `dotnet test`, `npm run build`, `npm test`, `npm run lint` —
  ask before the first run of a session; once approved, re-running that same command to check your
  own changes is fine for the rest of the session.
- Any `.exe`, `.msi`, `.bat`, `.cmd`, `.ps1`, or shell script not covered above.
- Background or detached processes, and anything scheduled to run later (cron, wakeups, loops).

### Git and version control
- `git commit`, `git push`, `git tag`, branch creation, merges, rebases.
- Anything destructive or history-rewriting: `git reset --hard`, `git checkout -- <file>`,
  `git clean`, `git restore`, `git stash drop`, `--force` / `--force-with-lease`, `git rm`.
- `gh` CLI operations that create or modify anything on GitHub.
- Read-only inspection (`git status`, `git diff`, `git log`, `git show`) is fine.

### Destructive or wide-reaching file operations
- Deleting or renaming any file or directory.
- Overwriting a file you have not read in full first.
- Bulk edits across many files, or any find-and-replace applied repo-wide.
- Writing anywhere outside this repository, except the session scratchpad directory.
- Editing `.gitignore`, `.dockerignore`, `Dockerfile`, `.github/**`, `.vscode/**`,
  `.claude/**` (settings, permissions, hooks, agents), `launchSettings.json`, or the `.sln` /
  `.esproj` / `.csproj` project files.

### Secrets and configuration
- Reading, writing, printing, or moving credentials: `.env` files, user secrets
  (`dotnet user-secrets`), certificates, API keys, connection strings, tokens.
- `dotnet dev-certs` in any form.
- Changing `appsettings*.json` values that point at hosts, ports, or credentials.

### Delegation
- Spawning subagents (`Agent` tool) or handing work to another session. Only when asked by name.
- Any action above performed *by* a subagent is still gated — inherit these rules, do not relax them.

## Always allowed without asking

Reading files in the repo, searching (Glob/Grep), reading `git` history and status, editing source
files under `YourElectionMap.Server/`, `yourelectionmap.client/src/`, and
`YourElectionMap.Server.Tests/` in service of the current request, writing to the session scratchpad,
and thinking/planning/explaining. When in doubt about anything else, ask.

## How to ask

State the exact command or action, why it is needed, and what it will change or touch. One line is
enough. If several gated actions are needed for one task, list them together up front rather than
interrupting repeatedly — but do not treat approval of the list as approval for anything not on it.

## When knowledge is missing

Do not reach for the internet to fill a gap. Say what you do not know, say what you would look up,
and let the owner decide whether to authorize a search or paste the information in.

---

# PART 2 — THE CODEBASE

## What this is

`YourElectionMap` is an ASP.NET Core 9 Web API (`YourElectionMap.Server`) serving a React 19 + Vite
SPA (`yourelectionmap.client`), with an xUnit test project (`YourElectionMap.Server.Tests`). It is
currently the freshly scaffolded template — `WeatherForecast` is placeholder code, and `App.tsx` is
still the Vite starter page. Nothing election-specific has been built yet.

Note the nesting: the git repository root holds `README.md` and this file; everything else lives one
level down in `YourElectionMap/`, which is where `YourElectionMap.sln` sits and where all commands
below are run from.

## Commands

All from `YourElectionMap/` unless noted. Every one of these is gated by Part 1 — ask first.

```powershell
dotnet build YourElectionMap.sln
dotnet test YourElectionMap.sln                       # all server tests
dotnet test YourElectionMap.Server.Tests/YourElectionMap.Server.Tests.csproj `
  --filter "FullyQualifiedName~GetWeatherForecast_ReturnsOkWithForecasts"   # one test
dotnet publish YourElectionMap.Server/YourElectionMap.Server.csproj         # also builds SPA into wwwroot
```

```powershell
cd yourelectionmap.client
npm test                                              # vitest run
npm test -- src/App.test.tsx                          # one test file
npm test -- -t "counts up when the counter button is clicked"   # one test by name
npm run test:watch                                    # vitest in watch mode
npm run lint                                          # oxlint
npm run build                                         # tsc -b && vite build
```

VS Code equivalents live in [YourElectionMap/.vscode/tasks.json](YourElectionMap/.vscode/tasks.json)
(`build`, `clean`, `publish`, `watch server`, `client: install/build/lint`, `test: server`,
`test: client`, `test: all`). Debug configurations are in
[YourElectionMap/.vscode/launch.json](YourElectionMap/.vscode/launch.json) — **Full stack (Edge/Chrome)**
is the normal entry point.

## Architecture

**Two dev servers, one origin.** Vite runs on `https://localhost:52462` and the API on
`https://localhost:7132` (`http://localhost:5026`). The browser only ever talks to 52462; Vite proxies
matching paths to the API, so there is no CORS configuration anywhere in the codebase.

**The proxy route list is the coupling point.** Only prefixes listed in `apiProxyRoutes` in
[YourElectionMap/yourelectionmap.client/vite.config.ts](YourElectionMap/yourelectionmap.client/vite.config.ts)
reach the server — currently `/api` and `/weatherforecast`. **Any new server route must be added
there**, or it silently falls through to the SPA's `index.html` fallback and returns HTML instead of
JSON. This is the single most common way to break the dev loop.

**Ports are declared in three places and must agree:** `devServerPort` in `vite.config.ts`,
`<SpaProxyServerUrl>` in
[YourElectionMap/YourElectionMap.Server/YourElectionMap.Server.csproj](YourElectionMap/YourElectionMap.Server/YourElectionMap.Server.csproj),
and the URLs in `Properties/launchSettings.json` and the VS Code tasks/launch configs. Vite uses
`strictPort: true`, so a conflict fails loudly rather than drifting to another port and breaking the
proxy.

**HTTPS in dev is bootstrapped by Vite.** `httpsDevCertificate()` in `vite.config.ts` exports the
ASP.NET Core dev certificate to `%APPDATA%/ASP.NET/https` on first serve so both servers present the
same cert. It is skipped for `vite build` and for Vitest. A failure here means
`dotnet dev-certs https --trust` has not been run (one-time setup — ask before running it).

**Two ways to start, same result.** From VS Code, F5 starts the API and runs Vite as a background
task. From Visual Studio, `Microsoft.AspNetCore.SpaProxy` (wired via `<SpaProxyLaunchCommand>`) starts
Vite instead. The Vite dev server outlives the debug session in the VS Code path and must be stopped
separately.

**Server request pipeline** ([Program.cs](YourElectionMap/YourElectionMap.Server/Program.cs)):
controllers + OpenAPI (dev only, at `/openapi/v1.json`), static assets, then
`MapFallbackToFile("/index.html")` — which is what makes an unproxied route return the SPA shell.
`Program` is declared `public partial` at the bottom of the file **solely** so
`WebApplicationFactory<Program>` can host the app in memory; do not remove it.

**Tests come in two flavors.** `WeatherForecastControllerTests` instantiates the controller directly;
`WeatherForecastEndpointTests` uses `IClassFixture<WebApplicationFactory<Program>>` to exercise real
routing and JSON serialization over HTTP. New endpoints deserve the second kind. Client tests are
Vitest + Testing Library in `jsdom`, matching `src/**/*.{test,spec}.{ts,tsx}`, with
`src/test/setup.ts` as the setup file.

**Linting is oxlint, not ESLint** — configured in
[YourElectionMap/yourelectionmap.client/.oxlintrc.json](YourElectionMap/yourelectionmap.client/.oxlintrc.json).
