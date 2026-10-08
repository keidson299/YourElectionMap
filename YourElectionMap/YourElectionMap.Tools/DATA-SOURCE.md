# Where the district boundaries come from

`YourElectionMap.Server/Districts/Data/nc-districts.json` is generated from the 2024 North
Carolina precinct shapefiles by this tool. The shapefiles are not in the repository — they are
about 130MB — and live outside it, at
`D:\VisualStudio\GitProjects\YourElectionMap\Docs\MapShapeFiles\NC_2024_Precint_Bounds`.

## The source

North Carolina 2024 general election precinct-level results and boundaries, retrieved from the
Redistricting Data Hub on 2025-09-12. The boundaries are the NC State Board of Elections precinct
boundary file (`SBE_PRECINCTS_20250225`), with Wake and Henderson county boundaries from those
counties. The results are the NCSBE's historical election results data.

Projection: NAD83 / North Carolina State Plane, FIPS 3200, US survey feet — a Lambert conformal
conic (EPSG:2264). `YourElectionMap.Server/Districts/StatePlane.cs` carries the same parameters,
read off the `.prj`, so an address projects into the space these boundaries are drawn in.

## Two things to know before trusting it

**The data's README documents columns that are not there.** It lists `CONG_DIST`, `SLDU_DIST` and
`SLDL_DIST` fields holding each precinct's district. The `.dbf` files hold only `UNIQUE_ID`,
`COUNTYFP`, `PRECINCT`, `COUNTY` and one column per candidate. So this tool reads the district out
of the vote columns instead: a precinct with votes in `GCON07RROU` is in US House 7. Every precinct
in all three files points at exactly one district, which is what makes that sound.

**District boundaries here are precinct boundaries added together.** The data's own README says
precincts were split only where they recorded votes for candidates in more than one district, so
"district assignments for precincts may not perfectly correspond to the districts themselves,
particularly in large, uninhabited areas". An address near a district line in open country may
therefore be placed on the wrong side of it. This is why the page carries a source note naming the
data rather than presenting the boundaries as authoritative.

## Credit

The site must say that its map data comes from the Redistricting Data Hub, with a link to
https://redistrictingdatahub.org/. `DataCredit` in `yourelectionmap.client/src/components/` does
that, on Home and under the district list on the result page.

## Running it

From `YourElectionMap/`:

```powershell
dotnet run --project YourElectionMap.Tools -- `
  "D:\VisualStudio\GitProjects\YourElectionMap\Docs\MapShapeFiles\NC_2024_Precint_Bounds" `
  YourElectionMap.Server\Districts\Data\nc-districts.json
```

It prints a line per district — precincts in, vertices in, outline edges, rings, points out — and
refuses to write the file if any district dissolves to nothing. Two runs of the same input produce
the same bytes, so a re-run with no change leaves no diff.
