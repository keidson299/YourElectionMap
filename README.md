# YourElectionMap

An ASP.NET Core 9 API (`YourElectionMap.Server`) serving a React 19 + Vite SPA
(`yourelectionmap.client`), with an xUnit test project (`YourElectionMap.Server.Tests`).

## Developing in VS Code

### One-time setup

```powershell
dotnet dev-certs https --trust        # trust the local HTTPS certificate
cd YourElectionMap/yourelectionmap.client
npm install
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

Vite proxies `/api/*` and `/weatherforecast` to the API, so the browser only ever talks to
port 52462 and there is no CORS setup to maintain. New server routes need to be added to
`apiProxyRoutes` in [vite.config.ts](YourElectionMap/yourelectionmap.client/vite.config.ts).

Other launch configurations: **Server: ASP.NET Core** (API alone — useful with
`YourElectionMap.Server.http` or the OpenAPI doc at `/openapi/v1.json`), **Client: Vite +
Edge/Chrome** (SPA alone), and **Server: attach to process**.

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
memory; `Program` is declared `public partial` at the bottom of `Program.cs` to allow that.

### Other tasks

*Tasks: Run Task* also offers `build`, `clean`, `publish`, `watch server` (hot reload without
the debugger), `client: install`, `client: build` and `client: lint`.

## Visual Studio

The solution still opens and runs from Visual Studio: <kbd>F5</kbd> starts the server, and
`Microsoft.AspNetCore.SpaProxy` launches the same Vite dev server on port 52462.
