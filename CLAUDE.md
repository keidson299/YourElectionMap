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

`YourElectionMap` is an ASP.NET Core 10 Web API (`YourElectionMap.Server`) serving a React 19 + Vite
SPA (`yourelectionmap.client`), with an xUnit test project (`YourElectionMap.Server.Tests`) and a
console project (`YourElectionMap.Tools`), run by hand, that builds the district data. The client
has a Home page (the address form over a decorative survey map) and a result page (a rail of
districts beside a MapLibre street map; on a phone, the map above a bottom sheet), fed by the
server's one endpoint, `GET /api/districts`. The current plan is
[design/ElectionMapMainDesignPlanV5.md](design/ElectionMapMainDesignPlanV5.md); code comments
refer to its numbered slices.

**The data is real and covers twenty-five states**, Arizona, Arkansas, California, Colorado,
Idaho, Iowa, Kansas, Louisiana, Minnesota, Mississippi, Missouri, Montana, Nebraska, Nevada, New
Mexico, North Carolina, North Dakota, Oklahoma, Oregon, South Carolina, South Dakota, Texas, Utah,
Washington and Wyoming: each state's US House, State Senate and State House/Assembly districts
(Nebraska's Legislature has one chamber, so no `state-lower` row, a few House districts are halves
numbered "4A", "65B", and Lake Pontchartrain is in no Louisiana Senate district), from the Census
Bureau's TIGERweb layers, and its **local rows**: the county (a parish in Louisiana, whose rows'
detail says so), the city or town, and the school district (in Arizona, California, Montana, a corner of Oregon and one place each in Texas and
Minnesota, sometimes a high school and an elementary district), each a whole government named rather
than numbered ("Wake County", "Raleigh city"). An address is placed
by the US Census geocoder and looked up in one file per state in `Districts/Data/`; the next
election at the address comes from Google's Civic Information API. Those are the server's only two calls out.
The browser fetches the street tiles and the map's label fonts from OpenFreeMap (`TILE_SOURCE` in
`src/map/style.ts`), and the page's own fonts, Fraunces and Inter, from Google Fonts
(`index.html`). There is no County Commission, City Council or School Board district data yet,
so the rail lists up to six rows (seven where an address has both a high school and an elementary
district), and fewer where it has no city or school district. An address
outside the twenty-five states is a 404.

**Google needs an API key, and nothing breaks without one.** The server reads
`GoogleCivic:ApiKey` — user secrets in development, the environment variable `GoogleCivic__ApiKey`
when deployed, never `appsettings*.json` — and sends it as the `X-Goog-Api-Key` header, never in the
URL. With no key every lookup goes out with `election: null`, which hides the rail's footer. The
key is a secret under Part 1: do not read, set or print it.

**The site must credit the U.S. Census Bureau for the map data**, with a link to TIGERweb
(https://tigerweb.geo.census.gov/tigerwebmain/TIGERweb_main.html). `src/components/DataCredit.tsx`
does it, on Home and under the result page's district list, and tests check both. Do not remove
it, and any new page that offers or draws the map needs it too.

**The `*-districts.json` files are generated and git-ignored.** A fresh clone has no
`Districts/Data/` folder; with no file at all the server stops at startup (`Program.cs` loads
`DistrictData` eagerly), and without every state's file `DistrictDataTests` and
`DistrictsEndpointTests` fail. They are
built by `YourElectionMap.Tools` from TIGERweb GeoJSON kept outside the repository (see
`YourElectionMap.Tools/DATA-SOURCE.md`), and reach `bin/` through the Web SDK's default `**/*.json`
content glob. A file not in degrees is refused at startup: rebuild it.

## Project priorities

This is a one-person project with a small audience: voters looking up the districts they vote in.
Prefer simple, direct code over abstraction, and treat verbosity and over-architecture as real
problems in reviews. There are no accounts and no stored personal data.

## Commands

All from `YourElectionMap/` unless noted. Every one of these is gated by Part 1 — ask first.

```powershell
dotnet build YourElectionMap.sln
dotnet test YourElectionMap.sln                       # all server tests
dotnet test YourElectionMap.Server.Tests/YourElectionMap.Server.Tests.csproj `
  --filter "FullyQualifiedName~Get_WithAnAddress_ReturnsTheLookupTheClientReads"   # one test
dotnet publish YourElectionMap.Server/YourElectionMap.Server.csproj         # also builds SPA into wwwroot

# Builds each state's district file from its TIGERweb layers. Needed once per clone and again
# when a plan changes.
foreach ($state in 'ar', 'az', 'ca', 'co', 'ia', 'id', 'ks', 'la', 'mn',
           'mo', 'ms', 'mt', 'nc', 'nd', 'ne', 'nm', 'nv', 'ok',
           'or', 'sc', 'sd', 'tx', 'ut', 'wa', 'wy') {
  dotnet run --project YourElectionMap.Tools -- tiger $state `
    "D:\VisualStudio\GitProjects\YourElectionMap\Docs\Tiger\$state" `
    "YourElectionMap.Server\Districts\Data\$state-districts.json"
}
```

```powershell
cd yourelectionmap.client
npm test                                              # vitest run
npm test -- src/App.test.tsx                          # one test file
npm test -- -t "sends a submitted address to the result page"   # one test by name
npm run test:watch                                    # vitest in watch mode
npm run lint                                          # oxlint (not ESLint), .oxlintrc.json
npm run build                                         # tsc -b && vite build
```

Run client scripts through a path with an uppercase drive letter
(`D:/VisualStudio/GitProjects/YourElectionMap/Repo/YourElectionMap/yourelectionmap.client`).
Spelled `d:/`, Vitest loads two copies of its modules and every suite fails in `src/test/setup.ts`
with "Vitest failed to find the current suite" before any test runs. VS Code terminals can open
with the lowercase form, so check this first if every suite fails at once.

VS Code tasks and debug configurations are in `YourElectionMap/.vscode/`; **Full stack
(Edge/Chrome)** is the normal entry point.

## Architecture

**Two dev servers, one origin.** Vite runs on `https://localhost:52462` and the API on
`https://localhost:7132` (`http://localhost:5026`). The browser only talks to 52462, and Vite
proxies the prefixes in `apiProxyRoutes` in
[vite.config.ts](YourElectionMap/yourelectionmap.client/vite.config.ts) — currently `/api` only.
**Any new server route must be added there**, or it falls through to the SPA's `index.html`
fallback and returns HTML instead of JSON.

**Ports are declared in three places and must agree:** `devServerPort` in `vite.config.ts`,
`<SpaProxyServerUrl>` in the server `.csproj`, and the URLs in `Properties/launchSettings.json` and
the VS Code tasks/launch configs. Vite uses `strictPort: true`, so a conflict fails loudly.

**HTTPS in dev is bootstrapped by Vite**, which exports the ASP.NET Core dev certificate to
`%APPDATA%/ASP.NET/https` on first serve. A failure there means `dotnet dev-certs https --trust`
has not been run (one-time setup — ask before running it). From VS Code, F5 runs Vite as a
background task that outlives the debug session; from Visual Studio, `SpaProxy` starts it.

**`Program` is declared `public partial`** at the bottom of `Program.cs` solely so
`WebApplicationFactory<Program>` can host the app in memory; do not remove it.

**How a lookup is answered** (`YourElectionMap.Server/Districts/`). `CensusGeocoder` places the
address; `DistrictData` finds the boundaries containing the point, and names the county, where
the file has counties, as every other row's `area`, for the map chip; for a typed address
`GoogleVoterInfo` then reads Google's election list, picks the soonest coming election that is
nationwide or in the address's state (read from the geocoder's spelling of the match), and asks
the voter lookup about the address for that election by ID. Asked without an ID, Google answers
"Election unknown" even weeks before a general election. `DistrictsController` answers 200, 404 when nothing can be placed or found,
503 when the geocoder did not answer, and 400 for an unusable query; the client reads 404 and 400
as no-match and anything else as an error, so an outage never reads as a bad address. Before
changing it, know that:

- **Everything is in degrees**, `[longitude, latitude]`, from the files to the wire. Nothing is
  projected anywhere.
- **Containment counts ray crossings rather than winding**, because the files store a district as
  loose rings. `Geometry.ToPolygons` sorts them into GeoJSON polygons for the map, which fills by
  that structure.
- **Boundaries are sent at the detail the file stores** (about 15 feet), not thinned: the map zooms
  to street level. Responses are compressed (Brotli or gzip, the
  framework's defaults).
- **Districts are thinned to 15 feet each on its own**, so neighbours overlap or miss by a few
  feet along a shared edge. `DistrictData.Covering` settles a point there: two of a kind keep the
  one it is deeper in, and a whole-state level (congressional, both chambers, county) left empty
  takes the nearest within 50 feet. The Census geocoder puts an address about 21 feet off the
  street's centreline, on its own side, which is clear of that error where a boundary follows a
  street.
- **Google can never fail a lookup.** `GoogleVoterInfo` returns null rather than throwing for no
  key, no election, a refusal, a timeout (four seconds for both calls together), an unreadable
  answer or anything else; only the browser cancelling propagates. The election list is the same
  for every address and is kept in `IMemoryCache` for an hour; a failed read is not kept.
- **A county, city or school row has no unit or number** (both null on the wire); its `office` is
  its name and its id is its level and Census GEOID, `county-37183`. Rows sort broad to local,
  then by number, with an elementary school district after the high school district over it.

**Tests.** `DistrictsEndpointTests` hosts the whole app in memory (`OfflineApp`) and reads the raw
JSON, which is the contract the client reads; new endpoints deserve the same kind of test. **No
test reaches the internet:** `OfflineApp` swaps the geocoder's and Google's `HttpClient` handlers
for `FakeGeocoding` and `FakeVoterInfo` (the fake election's date is relative to today). Everything
else is real, including the district files. Stub any new outward call the same way. The converter
has no tests of its own, since its input is not in the repository; `DistrictDataTests` checks its
output, most strongly with the grid tests that every covered point finds one district per
legislative level (none for Louisiana's Senate on Lake Pontchartrain), one county, at most one city and a school district combination that does not
overlap. `LocalDistrictsTests` covers the
local rows' sorting and naming on a small file it writes itself.
On the client, `src/test/setup.ts` replaces `maplibre-gl` with `src/test/fakeMaplibre.ts`, because
jsdom has no WebGL.

**Client structure.**

- There is no router dependency: `src/routing/` wraps the History API, and the URL decides what is
  looked up and which district is shown, so change it only through `navigate()`.
- `DistrictLookup` in `src/districts/types.ts` is the twin of the server's
  `Districts/DistrictLookup.cs` — change the two together.
- `src/districts/lookups.ts` is the whole data layer. Home and the header go through
  `openLookup`, which looks the target up *before* changing the URL.
- `DistrictMap` makes one MapLibre map for the life of the result page; its whole look comes from
  `buildStyle` in `src/map/style.ts`, so a new selection or theme is one `setStyle`. The layers
  handed to it are memoized in `ResultView`: a new `layers` object rebuilds the style.
- `src/test/sampleLookup.ts` deliberately has all six levels, a ward, a zone and an election: it is
  the only thing exercising wards and zones, which no real data has. Named rows (no unit or
  number) are tested in `labels.test.ts` and `DistrictRail.test.tsx`.
- The phone layout switches at 860px, set in `PHONE_QUERY` (`src/hooks/useMediaQuery.ts`) and
  again in every stylesheet's media query; change them together. To test it, stub `matchMedia` as
  `ResultView.test.tsx` does.
- Sizes, colours and line weights come from the build spec, `design/canvas4/Main.dc.html`. The
  colour tokens live in `src/index.css`, where the dark set appears twice on purpose.
