# YourElectionMap

Type an address and see the election districts you vote in, drawn on a map.

An ASP.NET Core 9 API (`YourElectionMap.Server`) serving a React 19 + Vite SPA
(`yourelectionmap.client`), with an xUnit test project (`YourElectionMap.Server.Tests`) and a
console project (`YourElectionMap.Tools`) that builds the district data.

The data is real and covers **North Carolina only**: its 14 US House, 50 State Senate and 120
State House districts, drawn with [MapLibre GL](https://maplibre.org/) over a street map from
[OpenFreeMap](https://openfreemap.org/). Two things reach out to the internet: the server places
addresses with the US Census geocoder, and the visitor's browser fetches the street map's tiles
from OpenFreeMap. There is no county, city, school board or election data yet. The plan is
[design/ElectionMapMainDesignPlanV5.md](design/ElectionMapMainDesignPlanV5.md).

## Developing in VS Code

### One-time setup

```powershell
dotnet dev-certs https --trust        # trust the local HTTPS certificate
cd YourElectionMap/yourelectionmap.client
npm install
```

**Generate the district file.** `YourElectionMap.Server/Districts/Data/nc-districts.json` is
git-ignored, and the server will not start without it. It is built from the North Carolina
General Assembly's enacted district plans (the 2025 congressional plan and the 2023 legislative
plans), whose shapefiles are kept outside the repository; see
[DATA-SOURCE.md](YourElectionMap/YourElectionMap.Tools/DATA-SOURCE.md) for where they come from
and the folder layout the tool expects. From `YourElectionMap/`:

```powershell
dotnet run --project YourElectionMap.Tools -- `
  "<MapShapeFiles folder holding the three plan folders>" `
  YourElectionMap.Server\Districts\Data\nc-districts.json
```

Open `YourElectionMap/YourElectionMap.code-workspace` (File > Open Workspace from File),
and install the recommended extensions when prompted — C# Dev Kit provides the debugger and
test explorer, Vitest Explorer runs the client tests.

### Running

Press <kbd>F5</kbd> and pick **Full stack (Edge)** or **Full stack (Chrome)**. That starts:

| Piece | URL | Notes |
| --- | --- | --- |
| Vite dev server | https://localhost:52462 | HMR, and the URL to browse |
| ASP.NET Core API | https://localhost:7132 (http://localhost:5026) | debuggable, breakpoints in C# work |

Vite proxies `/api/*` to the API, so the browser only ever talks to
port 52462 and there is no CORS setup to maintain. New server routes need to be added to
`apiProxyRoutes` in [vite.config.ts](YourElectionMap/yourelectionmap.client/vite.config.ts).

Other launch configurations: **Server: ASP.NET Core** (API alone — useful with
`YourElectionMap.Server.http` or the OpenAPI doc at `/openapi/v1.json`), **Client: Vite +
Edge/Chrome** (SPA alone), **Client: debug tests (Vitest)**, and **Server: attach to process**.
The requests in `YourElectionMap.Server.http` go to the real Census geocoder.

The Vite dev server runs as a VS Code background task, so it keeps running after the debug
session stops. Stop it from the terminal panel, or with *Tasks: Terminate Task*.

### Testing

<kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>P</kbd> > *Tasks: Run Test Task* runs everything, or use
the Testing panel to run and debug individual tests. From a terminal:

```powershell
cd YourElectionMap
dotnet test YourElectionMap.sln                   # xUnit: unit + in-memory integration tests
cd yourelectionmap.client
npm test                                          # vitest run
npm run test:watch                                # vitest in watch mode
```

`YourElectionMap.Server.Tests` uses `WebApplicationFactory<Program>` to host the real app in
memory; `Program` is declared `public partial` at the bottom of `Program.cs` to allow that. The
geocoder is stubbed, so no test reaches the internet, but the tests read the real district file
and need it generated first. The client tests replace MapLibre with a fake
(`src/test/fakeMaplibre.ts`), since jsdom has no WebGL, so they fetch no tiles either.

Run the client tests from a path with an uppercase drive letter (`D:\...`). From `d:\...`, Vitest
loads its modules twice and every suite fails in `src/test/setup.ts` before a test runs.

### Other tasks

*Tasks: Run Task* also offers `build`, `clean`, `publish`, `watch server` (hot reload without
the debugger), `client: install`, `client: dev server`, `client: build`, `client: lint`, and
`test: client (watch)`.

## Visual Studio

The solution still opens and runs from Visual Studio: <kbd>F5</kbd> starts the server, and
`Microsoft.AspNetCore.SpaProxy` launches the same Vite dev server on port 52462.
