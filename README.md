# YourElectionMap

Type an address and see the election districts you vote in, drawn on a map.

An ASP.NET Core 9 API (`YourElectionMap.Server`) serving a React 19 + Vite SPA
(`yourelectionmap.client`), with an xUnit test project (`YourElectionMap.Server.Tests`) and a
console project (`YourElectionMap.Tools`) that builds the district data.

The data is real and covers **Arizona, California, Idaho, Montana, North Carolina, Oregon,
South Carolina, Utah, Washington and Wyoming**: each state's US House, State Senate and State House or Assembly districts, and in
California, North Carolina, Oregon, South Carolina and Washington the county, city or town and
school district too, all from
the Census Bureau's TIGERweb, drawn with [MapLibre GL](https://maplibre.org/) over a street map from
[OpenFreeMap](https://openfreemap.org/), with the next election at the address — its date and how
many candidates and ballot measures are on the ballot — from Google's voter lookup. Four things
reach out to the internet: the server places addresses with the US Census geocoder and asks the
Google Civic Information API for the election, and the visitor's browser fetches the street map's
tiles from OpenFreeMap and the page's fonts from Google Fonts. There are no county commission,
city council or school board districts yet: the local rows are the governments as a whole. The plan is
[design/ElectionMapMainDesignPlanV5.md](design/ElectionMapMainDesignPlanV5.md).

## Developing in VS Code

### One-time setup

```powershell
dotnet dev-certs https --trust        # trust the local HTTPS certificate
cd YourElectionMap/yourelectionmap.client
npm install
```

**Generate the district files.** `YourElectionMap.Server/Districts/Data/` holds one file per
state, git-ignored, and the server will not start without at least one. Each is built from the
Census Bureau's TIGERweb district layers for that state, downloaded once as GeoJSON and kept
outside the repository. See [DATA-SOURCE.md](YourElectionMap/YourElectionMap.Tools/DATA-SOURCE.md)
for the download query and the folder layout the tool expects. From `YourElectionMap/`:

```powershell
# Once for each state: AZ, CA, ID, MT, NC, OR, SC, UT, WA and WY.
dotnet run --project YourElectionMap.Tools -- tiger <state> `
  "<folder holding the state's cd.geojson, sldu.geojson and sldl.geojson>" `
  YourElectionMap.Server\Districts\Data\<state>-districts.json
```

The folder can also hold the state's county, city and school district layers (`county`,
`place`, `unsd`, `scsd` and `elsd.geojson`); the tool adds each one it finds and skips any it
does not.

**Add a Google Civic Information API key (optional).** Without one the site works, but the
election footer under the district list stays hidden. In the Google Cloud console, create a
project, enable the *Google Civic Information API*, and create an API key restricted to that API
(the free quota is 25,000 requests a day, and each address looked up uses two: the election
list, then the voter lookup). Then, from `YourElectionMap/`:

```powershell
dotnet user-secrets set "GoogleCivic:ApiKey" "<your key>" --project YourElectionMap.Server
```

When deployed, set the environment variable `GoogleCivic__ApiKey` instead. Never put the key in
`appsettings*.json`.

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
The requests in `YourElectionMap.Server.http` go to the real Census geocoder, and to Google's
voter lookup when an API key is set.

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
geocoder and Google's voter lookup are stubbed, so no test reaches the internet, but the tests read the real district files
and need both generated first. The client tests replace MapLibre with a fake
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
