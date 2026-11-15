# YourElectionMap

Type a US address, or share your location, and YourElectionMap lists the districts you vote in
and draws each one on a street map, so you can see which side of which street its line runs down.

For any address in the fifty states (not Washington, D.C., or the territories) it shows:

- **Legislative districts:** US House, State Senate, and State House (or Assembly, General
  Assembly or House of Delegates, as the state calls it). Nebraska's one-chamber Legislature
  takes the place of the last two.
- **Local governments:** the county (a parish in Louisiana, a planning region in Connecticut, a
  borough or census area in Alaska); the town or township, in the twenty states where they govern
  themselves; the city or town; and the school district, or a high school district and an
  elementary district where a state splits them.
- **The next election:** its date and how many candidates and ballot measures are on the ballot,
  when Google has them for the address.

The result page lists the districts beside the map, or in a sheet under it on a phone. The selected
district is drawn hatched, and its neighbours in the list are drawn dashed and dotted for context.
The URL holds the address and the selected district, so a result can be bookmarked or shared.

Not covered yet: the districts that county commissions, city councils and school boards are
elected from (the local rows are the governments as a whole), and New Hampshire's floterial House
districts, which TIGER does not have.

Where the data comes from:

| What                     | From                                                                                            | When                                                                            |
| ------------------------ | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- |
| Boundaries               | The Census Bureau's [TIGERweb](https://tigerweb.geo.census.gov/tigerwebmain/TIGERweb_main.html) | Downloaded once per state and converted into a file the server loads at startup |
| Where the address is     | The US Census geocoder                                                                          | Each lookup, from the server                                                    |
| The next election        | Google's Civic Information API (needs an API key; the site works without one)                   | Each lookup by address, from the server                                         |
| Street map and its fonts | [OpenFreeMap](https://openfreemap.org/), drawn with [MapLibre GL](https://maplibre.org/)        | From the visitor's browser                                                      |
| Page fonts               | Google Fonts                                                                                    | From the visitor's browser                                                      |

There are no accounts, and nothing about a visit is stored. The browser remembers only the light
or dark theme.

The solution, in `YourElectionMap/`:

| Project                        | What it is                                                                                                    |
| ------------------------------ | ------------------------------------------------------------------------------------------------------------- |
| `YourElectionMap.Server`       | ASP.NET Core 10 API with one endpoint, `GET /api/districts`. It also serves the built client                  |
| `yourelectionmap.client`       | React 19 + TypeScript front end, bundled by Vite ([README](YourElectionMap/yourelectionmap.client/README.md)) |
| `YourElectionMap.Server.Tests` | xUnit tests that run the whole app in memory, with no internet                                                |
| `YourElectionMap.Tools`        | Console app that turns TIGER downloads into the district files                                                |

The design plan is [design/ElectionMapMainDesignPlanV5.md](design/ElectionMapMainDesignPlanV5.md).

## Running it

You need the .NET 10 SDK and Node.js with npm (the Dockerfile installs Node 24). Commands run from
`YourElectionMap/` unless noted.

### 1. Download each state's TIGER layers

Make one folder per state, outside the repository, and save each layer into it as GeoJSON. Each
layer takes one query to TIGERweb, filtered by the state's two-digit FIPS code and returned in
degrees:

```
https://tigerweb.geo.census.gov/arcgis/rest/services/TIGERweb/<service>/MapServer/<layer>/query?where=STATE%3D%27<FIPS>%27&outFields=NAME,GEOID&returnGeometry=true&outSR=4326&f=geojson
```

| Save as          | Service                        | Layer | Needed                                                                             |
| ---------------- | ------------------------------ | ----- | ---------------------------------------------------------------------------------- |
| `cd.geojson`     | `Legislative`                  | 0     | Yes                                                                                |
| `sldu.geojson`   | `Legislative`                  | 1     | Yes                                                                                |
| `sldl.geojson`   | `Legislative`                  | 2     | Yes, except Nebraska                                                               |
| `county.geojson` | `State_County`                 | 1     | Optional                                                                           |
| `cousub.geojson` | `Places_CouSub_ConCity_SubMCD` | 1     | Optional; for town and township states, with `outFields=NAME,GEOID,FUNCSTAT,LSADC` |
| `place.geojson`  | `Places_CouSub_ConCity_SubMCD` | 4     | Optional                                                                           |
| `unsd.geojson`   | `School`                       | 0     | Optional                                                                           |
| `scsd.geojson`   | `School`                       | 1     | Optional                                                                           |
| `elsd.geojson`   | `School`                       | 2     | Optional                                                                           |

Missouri also needs `cd-119.geojson` (`Legislative`, layer 4): its 2022 US House map, which the
courts kept in place for 2026.

TIGERweb stops a large answer at its record limit and marks it `"exceededTransferLimit": true`.
Fetch such a layer in pages with `resultOffset` and `resultRecordCount` and join the features. The
tool refuses a file that was cut short. Texas's unified school districts need special handling;
[DATA-SOURCE.md](YourElectionMap/YourElectionMap.Tools/DATA-SOURCE.md) explains it, and records
when each state was downloaded and how to check that the plans are still current.

### 2. Build the district files

`YourElectionMap.Tools` reads one state's folder and writes
`YourElectionMap.Server/Districts/Data/<state>-districts.json`. Each district's boundary is thinned
to 15 feet and written to five decimal places. The files are git-ignored. The server will not start
without at least one, and the tests need all fifty. To build them all:

```powershell
foreach ($state in 'ak', 'al', 'ar', 'az', 'ca', 'co', 'ct', 'de', 'fl',
           'ga', 'hi', 'ia', 'id', 'il', 'in', 'ks', 'ky', 'la',
           'ma', 'md', 'me', 'mi', 'mn', 'mo', 'ms', 'mt', 'nc',
           'nd', 'ne', 'nh', 'nj', 'nm', 'nv', 'ny', 'oh', 'ok',
           'or', 'pa', 'ri', 'sc', 'sd', 'tn', 'tx', 'ut', 'va',
           'vt', 'wa', 'wi', 'wv', 'wy') {
  dotnet run --project YourElectionMap.Tools -- tiger $state `
    "<folder of TIGER downloads>\$state" `
    "YourElectionMap.Server\Districts\Data\$state-districts.json"
}
```

Each run prints the district count and file size. The tool skips an optional layer that is missing.
It stops without writing if a legislative layer is missing, or if a layer does not hold exactly the
districts `Tiger.cs` expects for that state. Rebuild a state when its plan changes.

### 3. Set up once

```powershell
dotnet dev-certs https --trust        # trust the local HTTPS certificate
cd yourelectionmap.client
npm install
```

**Google Civic Information API key (optional).** Without one, the site works but the election
footer stays hidden. In the Google Cloud console, enable the _Google Civic Information API_ and
create an API key restricted to it. The free quota is 25,000 requests a day, and each address
lookup uses two. Then:

```powershell
dotnet user-secrets set "GoogleCivic:ApiKey" "<your key>" --project YourElectionMap.Server
```

When deployed, set the environment variable `GoogleCivic__ApiKey` instead. Never put the key in
`appsettings*.json`.

### 4. Build and test

```powershell
dotnet build YourElectionMap.sln
dotnet test YourElectionMap.sln       # several minutes: it checks a grid over every state's file
cd yourelectionmap.client
npm test                              # vitest
npm run lint                          # oxlint
npm run build                         # type-check and bundle
```

No test reaches the internet: the geocoder and Google are stubbed on the server, and MapLibre is
faked on the client. Run the client scripts from a path with an uppercase drive letter
(`D:\...`). From `d:\...`, Vitest loads its modules twice and every suite fails in
`src/test/setup.ts`.

### 5. Launch

**VS Code.** Open `YourElectionMap/YourElectionMap.code-workspace`, install the recommended
extensions (C# Dev Kit and Vitest Explorer), press <kbd>F5</kbd> and pick **Full stack (Edge)** or
**Full stack (Chrome)**. That starts:

| Piece            | URL                                            | Notes                              |
| ---------------- | ---------------------------------------------- | ---------------------------------- |
| Vite dev server  | https://localhost:52462                        | The URL to browse, with hot reload |
| ASP.NET Core API | https://localhost:7132 (http://localhost:5026) | C# breakpoints work                |

Vite proxies `/api` to the API, so the browser talks only to port 52462. A new server route must be
added to `apiProxyRoutes` in
[vite.config.ts](YourElectionMap/yourelectionmap.client/vite.config.ts). Vite runs as a background
task and keeps running after debugging stops; end it with _Tasks: Terminate Task_. Other launch
configurations run the server or client alone, debug the Vitest tests, or attach to a process.
`YourElectionMap.Server/YourElectionMap.Server.http` has sample requests, which go to the real
geocoder.

**Visual Studio, or the command line.** Run the server's `https` profile, with <kbd>F5</kbd> or:

```powershell
dotnet run --project YourElectionMap.Server --launch-profile https
```

`Microsoft.AspNetCore.SpaProxy` starts the same Vite dev server, and the site is at https://localhost:52462.

**Deploying.** `dotnet publish YourElectionMap.Server/YourElectionMap.Server.csproj` builds the
client into the server's `wwwroot`, so one app serves both. `YourElectionMap.Server/Dockerfile`
builds a Linux image from the `YourElectionMap/` folder:

```powershell
docker build -f YourElectionMap.Server/Dockerfile -t yourelectionmap .
```

Build the district files first. The image takes them from the build context, and without them the
server will not start.
