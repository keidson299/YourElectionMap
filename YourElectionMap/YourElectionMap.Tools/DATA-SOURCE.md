# Where the district boundaries come from

The server reads one file per state from `YourElectionMap.Server/Districts/Data/`, each generated
by this tool and git-ignored. Every file holds its rings in longitude and latitude, to six decimal
places, thinned to 15 feet.

| File | State | Source | Kept outside the repository, under `D:\VisualStudio\GitProjects\YourElectionMap\Docs\` |
| --- | --- | --- | --- |
| `nc-districts.json` | North Carolina | The General Assembly's enacted plans | `MapShapeFiles` |
| `ca-districts.json` | California | The Census Bureau's TIGERweb | `Tiger\ca` |

# North Carolina

## The source

Three plans, each retrieved through the Redistricting Data Hub, which states that it did not modify
any of the data:

| Level | Plan | Enacted | RDH retrieved | Shapefile, under `MapShapeFiles` |
| --- | --- | --- | --- | --- |
| US House | NCGA CCM-2 | 2025-10-21 | 2025-10-27 | `nc_cong_adopted_2025/NCGA_CCM-2 - Shapefile/CCM-2.shp` |
| State Senate | Session Law 2023-146 (Senate Bill 758) | 2023-10-25 | 2023-12-06 | `nc_sldu_adopted_2023/SL 2023-146 Senate - Shapefile/SL 2023-146.shp` |
| State House | Session Law 2023-149 (House Bill 898) | 2023-10-25 | 2023-12-06 | `nc_sldl_adopted_2023/SL 2023-149 House - Shapefile/SL 2023-149.shp` |

Each plan's folder also holds the General Assembly's PDF maps, statistics packs and block
equivalency file. The PDF maps are the thing to check a result against.

The 2025 congressional plan replaced the one used in 2024, so a site still built from 2024 data
shows the wrong US House district for anyone the redraw moved.

## What the files hold

- **One feature per district**, numbered 1 to 14, 1 to 50 and 1 to 120 in the `DISTRICT` field,
  each a single closed ring. They are the districts themselves, not shapes assembled from smaller
  units.
- **`PL20AA_TOT`**, each district's 2020 census population, which the plans were drawn to
  equalise. The tool carries it into the file, and the page shows it on the map chip.
- **NC State Plane (FIPS 3200), in metres.** The `.prj` is NAD83 / North Carolina State Plane, a
  Lambert conformal conic, with its false easting of 609,601.22 metres. The site's map units are
  96 US survey feet, so the tool converts metres to US survey feet (× 3937/1200) and then to map
  units, thins each ring there, and converts what is kept to longitude and latitude with
  `YourElectionMap.Server/Districts/StatePlane.cs`, which carries the same projection parameters.

## Credit

The site must say that its map data comes from the Redistricting Data Hub, with a link to
https://redistrictingdatahub.org/. `DataCredit` in `yourelectionmap.client/src/components/` does
that, on Home and under the district list on the result page.

## Running it

From `YourElectionMap/`:

```powershell
dotnet run --project YourElectionMap.Tools -- nc `
  "D:\VisualStudio\GitProjects\YourElectionMap\Docs\MapShapeFiles" `
  YourElectionMap.Server\Districts\Data\nc-districts.json
```

It prints a line per district — points in, points kept after thinning to 15 feet, population —
and refuses to write the file if:

- a plan's `.prj` is not NC State Plane in metres;
- a shapefile's shapes and records disagree;
- a plan's districts are not exactly 1 to N;
- a `DISTRICT` or `PL20AA_TOT` value is not a whole number.

Two runs of the same plans produce the same bytes apart from the date in the header.

# California

## The source

The Census Bureau's TIGERweb map service, `TIGERweb/Legislative/MapServer`, downloaded on
2026-10-11. Its current layers are the January 1, 2026 vintage. TIGER data is public domain.

| Level | Layer | Saved as, under `Docs\Tiger\ca` |
| --- | --- | --- |
| US House | 0, 120th Congressional Districts | `cd.geojson` |
| State Senate | 1, 2026 State Legislative Districts – Upper | `sldu.geojson` |
| Assembly | 2, 2026 State Legislative Districts – Lower | `sldl.geojson` |
| (comparison only) | 4, 119th Congressional Districts | `cd-119.geojson` |

The 120th Congress's districts are the map California's voters approved in November 2025
(Proposition 50), used from the 2026 elections. Layer 4 is the map before it: the two place 34% of
a 0.02° grid over the state in different districts, which is how it was confirmed that layer 0
holds the new map. The tool does not read layer 4.

Each layer was fetched with one request, in longitude and latitude (`outSR=4326`):

```
https://tigerweb.geo.census.gov/arcgis/rest/services/TIGERweb/Legislative/MapServer/<layer>/query?where=STATE%3D%2706%27&outFields=NAME,GEOID&returnGeometry=true&outSR=4326&f=geojson
```

A layer too big for one answer comes back with `"exceededTransferLimit": true`. Fetch it in
pages with `resultOffset` and `resultRecordCount`, and join the features into one file.

## What the files hold

- **One feature per district**, named "Congressional District 7", "State Senate District 8" and
  "Assembly District 6": 52, 40 and 80 of them. A few are MultiPolygons, for islands. The tool
  takes the number from the end of `NAME`.
- **`GEOID`**, which starts with California's FIPS code, 06.
- **No population.** TIGERweb's `POP100` field is empty for these layers, so the file has none and
  the map chip names the district alone.
- **Longitude and latitude, NAD83.** The tool thins each ring to 15 feet, taken as degrees of
  latitude, which keeps slightly more than that east to west.

The three layers cover the state exactly once each: on a 0.02° grid, every point is in one
district of each level, with no gaps or overlaps.

## Credit

The site names the U.S. Census Bureau beside the Redistricting Data Hub, with a link to TIGERweb.
TIGER data needs no credit, but the site says where every boundary it draws comes from.

## Running it

From `YourElectionMap/`:

```powershell
dotnet run --project YourElectionMap.Tools -- tiger CA `
  "D:\VisualStudio\GitProjects\YourElectionMap\Docs\Tiger\ca" `
  YourElectionMap.Server\Districts\Data\ca-districts.json
```

It prints a line per district — points in, points kept — and refuses to write the file if:

- a layer says it was cut short (`exceededTransferLimit`);
- a feature's `GEOID` is not in California;
- a layer's districts are not exactly 1 to N;
- a coordinate is not a longitude and latitude, as a layer fetched without `outSR=4326` would be.

Another state needs its FIPS code, layer files, office names and district counts added to the
table in `Tiger.cs`.
