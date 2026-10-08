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
- Any action above performed _by_ a subagent is still gated — inherit these rules, do not relax them.

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
SPA (`yourelectionmap.client`), with an xUnit test project (`YourElectionMap.Server.Tests`) and a
console project (`YourElectionMap.Tools`), run by hand, that builds the district data. The client
has a Home page (the address form over a decorative survey map) and a result page (a rail of
districts beside a MapLibre street map; on a phone, the map above a bottom sheet). The client
fetches its districts from the server's one real endpoint, `GET /api/districts`. The current plan
is [design/ElectionMapMainDesignPlanV5.md](design/ElectionMapMainDesignPlanV5.md); code comments
refer to its numbered slices.

**The data is real, and it is North Carolina only.** An address is placed by the US Census
geocoder — the one call the server makes out to the internet — and the point is then looked up in
`Districts/Data/nc-districts.json`, which holds the state's 14 US House, 50 NC Senate and 120 NC
House districts as the General Assembly enacted them — the 2025 congressional plan and the 2023
legislative plans — with each district's 2020 population. The visitor's browser makes the other
outbound requests: the result map's street tiles and fonts come from OpenFreeMap (`TILE_SOURCE` in
`src/map/style.ts`). There is no County Commission, City Council or School Board data, and no
election data, so the rail lists three districts and its election footer stays hidden. An address
anywhere else is a 404, which the page says in as many words.

**The site must credit the Redistricting Data Hub for the map data**, with a link to
https://redistrictingdatahub.org/. `src/components/DataCredit.tsx` does it, on Home and under the
result page's district list, and tests check both. Do not remove it, and any new page that offers
or draws the map needs it too.

**`nc-districts.json` is generated and git-ignored, not checked in.** A fresh clone has no
`Districts/Data/` folder, and without it the server stops at startup (`Program.cs` loads
`DistrictData` eagerly) and `DistrictDataTests` and `DistrictsEndpointTests` fail. It is built by
`YourElectionMap.Tools` from shapefiles kept outside the repository; nothing in the `.csproj` names
it, and the Web SDK's default `**/*.json` content glob is what copies it to `bin/`.

## Project priorities

This is a one-person project with a small audience: voters looking up the districts they vote in.
Prefer simple, direct code over abstraction, and treat verbosity and over-architecture as real
problems in reviews.There are no accounts and no stored personal data.

## Commands

All from `YourElectionMap/` unless noted. Every one of these is gated by Part 1 — ask first.

```powershell
dotnet build YourElectionMap.sln
dotnet test YourElectionMap.sln                       # all server tests
dotnet test YourElectionMap.Server.Tests/YourElectionMap.Server.Tests.csproj `
  --filter "FullyQualifiedName~Get_WithAnAddress_ReturnsTheLookupTheClientReads"   # one test
dotnet publish YourElectionMap.Server/YourElectionMap.Server.csproj         # also builds SPA into wwwroot

# Builds Districts/Data/nc-districts.json from the enacted plans' shapefiles. Needed once per
# clone (the file is git-ignored) and again when a plan changes; see
# YourElectionMap.Tools/DATA-SOURCE.md.
dotnet run --project YourElectionMap.Tools -- `
  "D:\VisualStudio\GitProjects\YourElectionMap\Docs\MapShapeFiles" `
  YourElectionMap.Server\Districts\Data\nc-districts.json
```

```powershell
cd yourelectionmap.client
npm test                                              # vitest run
npm test -- src/App.test.tsx                          # one test file
npm test -- -t "sends a submitted address to the result page"   # one test by name
npm run test:watch                                    # vitest in watch mode
npm run lint                                          # oxlint
npm run build                                         # tsc -b && vite build
```

Run client scripts through a path with an uppercase drive letter
(`D:/VisualStudio/GitProjects/YourElectionMap/Repo/YourElectionMap/yourelectionmap.client`).
Spelled `d:/`, Vitest loads two copies of its modules and every suite fails in `src/test/setup.ts`
with "Vitest failed to find the current suite" before any test runs. VS Code terminals can open
with the lowercase form, so check this first if every suite fails at once.

VS Code equivalents live in [YourElectionMap/.vscode/tasks.json](YourElectionMap/.vscode/tasks.json)
(`build`, `clean`, `publish`, `watch server`, `client: install/dev server/build/lint`,
`test: server`, `test: client`, `test: client (watch)`, `test: all`). Debug configurations are in
[YourElectionMap/.vscode/launch.json](YourElectionMap/.vscode/launch.json) — **Full stack (Edge/Chrome)**
is the normal entry point; **Client: debug tests (Vitest)** debugs the client suite.

## Architecture

**Two dev servers, one origin.** Vite runs on `https://localhost:52462` and the API on
`https://localhost:7132` (`http://localhost:5026`). The browser only ever talks to 52462; Vite proxies
matching paths to the API, so there is no CORS configuration anywhere in the codebase.

**The proxy route list is the coupling point.** Only prefixes listed in `apiProxyRoutes` in
[YourElectionMap/yourelectionmap.client/vite.config.ts](YourElectionMap/yourelectionmap.client/vite.config.ts)
reach the server — currently `/api` only. **Any new server route must be added
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

**How a lookup is answered**, all in `YourElectionMap.Server/Districts/`. `CensusGeocoder` turns an
address into a latitude and longitude; `StatePlane` projects that into map units with the parameters
from the shapefiles' own `.prj`; `DistrictData` — a singleton holding the whole file — answers which
boundaries contain the point, box-checked first and then by counting ray crossings in `Geometry`.
`DistrictsController` answers 200 with a lookup, 404 when nothing can be placed or found, 503 when
the geocoder did not answer (`GeocoderUnavailableException`), and 400 for an unusable query; the
client reads 404 and 400 as no-match and anything else as an error, so an outage never reads as a
bad address. Three things in there are worth knowing before changing them:

- **Containment is in map units; everything sent is in degrees.** The point is projected into the
  file's State Plane map units to be tested, and each boundary goes back out through
  `StatePlane.ToLatLon` as `[longitude, latitude]`, because the result map is MapLibre, which draws
  in degrees. The lookup's `location` is the geocoder's own longitude and latitude.
- **Containment counts crossings rather than winding.** The file stores a district as loose rings
  and says nothing about which is an island and which a hole, so only the crossing count can tell.
  For the same reason the boundary goes out through `Geometry.ToPolygons`, which sorts the rings
  into GeoJSON polygons (outer ring, then its holes, wound counter-clockwise and clockwise) by how
  many others contain each one; MapLibre fills by that structure, so without it holes would be
  filled and islands left blank. Every district in the enacted plans happens to be one ring, but
  nothing relies on that.
- **Boundaries are sent at the detail the file stores, not thinned.** That is roughly 15 feet. The
  map zooms to street level, where a simplified line would put an address on the wrong side of the
  street, and MapLibre simplifies for wider views itself. Each district's converted boundary is
  worked out once and kept, and responses are gzip-compressed (`AddResponseCompression`): a Raleigh
  lookup is about 55KB of coordinates, and the largest districts several times that.

**Server tests host the whole app in memory.** `DistrictsEndpointTests` uses its nested `OfflineApp`,
a `WebApplicationFactory<Program>`, and reads the raw JSON, so routing, model binding and the names and
formats on the wire are all covered — that JSON is the contract the client reads, so new endpoints
deserve the same kind of test. A controller can also be instantiated directly where only its own
logic is in question. Client tests are Vitest + Testing Library in `jsdom`, matching
`src/**/*.{test,spec}.{ts,tsx}`, with `src/test/setup.ts` as the setup file.

**No test reaches the internet.** `OfflineApp` replaces the geocoder's `HttpClient` handler in
`ConfigureTestServices` with `FakeGeocoding`, which places any address containing one of a few
known words ("Jones", "Tryon", …) and fakes an outage for "Outage". Everything else in
those tests is real, including the whole district file. If you add a service that calls out, stub it
the same way. On the client, `src/test/setup.ts` replaces `maplibre-gl` everywhere with
`src/test/fakeMaplibre.ts` — jsdom has no WebGL, so the real map cannot even be constructed — and
the fake records the style, view and pin it is given, which is what `DistrictMap.test.tsx` checks.

**How the district data is checked.** The converter needs the plans' shapefiles, which are not in
the repository, so it has no tests of its own: it refuses to write a file whose plans are in the
wrong units or missing a district, and `DistrictDataTests` checks the file it wrote. The strongest
of those is `Covering_HasTheThreeLevelsAgreeAcrossTheWholeState`: the three plans were enacted
separately but each is built from the same census blocks and covers the state exactly once, so a
grid of points over the state has to find one district per level everywhere. A layer read wrongly
or converted with the wrong units shows up there. The populations are checked against what each
plan was drawn to, and `DistrictsEndpointTests` pins the three districts of a Raleigh address.

**Client structure.**

- There is no router dependency. `src/routing/history.ts` wraps the History API, and `useRoute`
  reads it through `useSyncExternalStore`. The URL decides what is looked up and which district is
  shown, so change it only through `navigate()`.
- `DistrictLookup` in `src/districts/types.ts` is the contract the result page renders, and the
  twin of the server's records in `Districts/DistrictLookup.cs` — change the two together.
- **The result map is MapLibre GL over OpenFreeMap's street tiles.** `DistrictMap` makes one map
  for the life of the result page and never re-keys it; its whole look comes from `buildStyle` in
  `src/map/style.ts` (streets and street names only, then the districts), so a new selection or
  theme is one `setStyle`, which MapLibre diffs. Colours are read from the CSS tokens at runtime, the
  hatch is drawn on demand (`src/map/hatch.ts`), and where it opens and what the scale bar says are
  pure functions in `src/map/camera.ts`. It is loaded lazily from `ResultView`, because the library
  is most of the bundle. The layers handed to it are memoized in `ResultView` — a new `layers` object
  rebuilds the style, which on every render would diff every coordinate of three boundaries.
- **The design spec's sizes are in survey units: one unit is 96 feet, so 55 make a mile.** That
  is `MAP_UNITS_PER_MILE` in `src/map/frame.ts`. The result map uses it only to turn the spec's
  design frames into miles (the least it opens on) and to size the pin; the SVG survey sheets on
  Home and the waiting page are still drawn in those units. The server's `StatePlane.FeetPerMapUnit`
  is the same 96 feet for the district file's own units, but nothing sent to the client is in them.
- `src/components/surveyMapGeometry.ts` is the hand-drawn stand-in, and it is now only the
  decoration behind the home card. `SurveyGround` leaves out its river, roads and mesh unless asked
  for them with `standIn`, which only Home does.
- `src/test/sampleLookup.ts` is a test fixture, and deliberately not what the server sends — it has
  all six levels, a ward, a zone, populations and an election, which is the only thing exercising
  the parts of the page the real three-level data has nothing to fill.
- `src/districts/lookups.ts` is the whole data layer: it fetches a lookup, keeps every answer of
  the visit keyed by the target's query, and holds the wording for the waiting, no-match and error
  states. Home and the header go through `openLookup`, which looks the target up *before* changing
  the URL, so the map page normally renders a result it already has. Tests drive it with
  `src/test/fakeDistrictsApi.ts`.
- The phone layout switches at 860px. That value is set in `PHONE_QUERY`
  (`src/hooks/useMediaQuery.ts`) and again in every stylesheet's media query, so change them
  together.
- Sizes, colours and line weights come from the build spec, `design/canvas4/Main.dc.html`. The
  colour tokens live in `src/index.css`, where the dark set appears twice on purpose.
- jsdom has no `matchMedia`, `ResizeObserver` or WebGL, so client tests render the desktop layout
  with MapLibre faked (see above). To test the phone layout, stub `matchMedia` as
  `ResultView.test.tsx` does. The map arrives lazily, so a test that needs it waits with `findBy…`.

**Linting is oxlint, not ESLint** — configured in
[YourElectionMap/yourelectionmap.client/.oxlintrc.json](YourElectionMap/yourelectionmap.client/.oxlintrc.json).
