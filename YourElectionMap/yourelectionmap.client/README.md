# yourelectionmap.client

The YourElectionMap front end: React 19 and TypeScript, bundled by Vite. Its runtime dependencies
are React and MapLibre GL — no router and no UI library. The result map is MapLibre over
OpenFreeMap's street tiles, and the styling is plain CSS.

It has two pages: Home (the address form over a decorative survey map) and the result page at
`/map?address=…` or `/map?lat=…&lon=…`, which lists the districts covering that place and draws
one of them. It gets them from the server's `GET /api/districts`, which Vite proxies to the
ASP.NET Core API in development. See the [repository README](../../README.md) for running the two
together.

## Scripts

```powershell
npm run dev          # Vite on https://localhost:52462 (proxies /api to the API)
npm test             # vitest run
npm run test:watch   # vitest in watch mode
npm run lint         # oxlint, configured in .oxlintrc.json
npm run build        # tsc -b && vite build
```

Run these from a path with an uppercase drive letter (`D:\...`). From `d:\...`, Vitest loads its
modules twice and every suite fails in `src/test/setup.ts` before a test runs.

## Layout of `src/`

| Folder | What is in it |
| --- | --- |
| `pages/` | `HomePage` and `MapPage` |
| `components/` | The address form, header, theme toggle and Home's decorative map; `result/` holds the rail, the map and what sits over it |
| `districts/` | `types.ts`, the lookup contract shared with the server's `DistrictLookup.cs`; `lookups.ts`, the whole data layer; selection and labels |
| `map/` | The MapLibre style, the hatch, and `camera.ts`: where the map opens, how far it can be dragged, and the scale bar |
| `routing/` | A small wrapper over the History API; the URL decides what is looked up and shown |
| `hooks/`, `theme/` | The media query hook; the light/dark theme |
| `test/` | Test setup, a fake districts API, a fake MapLibre, and a six-level sample lookup |

Sizes, colours and line weights come from the build spec,
[design/canvas4/Main.dc.html](../../design/canvas4/Main.dc.html).
