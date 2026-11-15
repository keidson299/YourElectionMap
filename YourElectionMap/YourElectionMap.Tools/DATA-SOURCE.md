# Where the district boundaries come from

Every boundary on the site comes from the Census Bureau's TIGERweb map services. This tool turns
each state's layers into `YourElectionMap.Server/Districts/Data/<state>-districts.json`: rings in
longitude and latitude, thinned to 15 feet, written to five decimal places (about a metre). The
files are git-ignored; the command that builds them is in `CLAUDE.md`. TIGER data is public
domain, and the site credits it (`DataCredit` in the client).

## Downloads

Each state's layers are kept outside the repository, in
`D:\VisualStudio\GitProjects\YourElectionMap\Docs\Tiger\<state>`.

| Downloaded | States |
| --- | --- |
| 2026-10-11 | CA, NC, OR, SC, WA; AZ, ID, MT, UT, WY (their local layers 2026-10-12) |
| 2026-10-13 | CO, NM, NV, OK, TX |
| 2026-10-24 | AR, IA, KS, LA, MN, MO, MS, ND, NE, SD |
| 2026-10-25 | AL, CT, DE, FL, GA, IL, IN, KY, MA, MD, MI, NJ, NY, OH, PA, RI, TN, VA, WI, WV |
| 2026-10-27 | AK, HI, ME, NH, VT, every layer |

The county subdivisions of the town and township states (below) were downloaded on 2026-10-26,
and every folder's previous-vintage legislative layers on 2026-10-25, but for the five added on
2026-10-27.

## The source

TIGERweb's current layers are the January 1, 2026 vintage.

| Level | Service, layer | Saved as |
| --- | --- | --- |
| US House | `Legislative`, 0, 120th Congressional Districts | `cd.geojson` |
| State Senate | `Legislative`, 1, 2026 State Legislative Districts – Upper | `sldu.geojson` |
| State House / Assembly | `Legislative`, 2, 2026 State Legislative Districts – Lower | `sldl.geojson` |
| County | `State_County`, 1, Counties | `county.geojson` |
| City or town | `Places_CouSub_ConCity_SubMCD`, 4, Incorporated Places | `place.geojson` |
| Town or township | `Places_CouSub_ConCity_SubMCD`, 1, County Subdivisions | `cousub.geojson` |
| School district | `School`, 0, Unified School Districts | `unsd.geojson` |
| High school district | `School`, 1, Secondary School Districts | `scsd.geojson` |
| Elementary school district | `School`, 2, Elementary School Districts | `elsd.geojson` |

The legislative layers are required (Nebraska has no `sldl.geojson`); the local ones are
optional, and a state without one builds without it. `State_County`'s other county layers are
coarser copies. Census designated places (layer 5) are left out: nobody is elected to them.

Each layer is one request per state, in degrees (`outSR=4326`):

```
https://tigerweb.geo.census.gov/arcgis/rest/services/TIGERweb/<service>/MapServer/<layer>/query?where=STATE%3D%27<FIPS>%27&outFields=NAME,GEOID&returnGeometry=true&outSR=4326&f=geojson
```

The county subdivisions need `outFields=NAME,GEOID,FUNCSTAT,LSADC`. A layer too big for one answer
says `"exceededTransferLimit": true`; fetch it in pages with `resultOffset` and
`resultRecordCount` and join the features. Texas's unified school districts come back empty
rather than say so: fetch them 100 at a time with `orderByFields=GEOID` and check the total
against `returnCountOnly=true` (1,016). A state with no high school or elementary districts gets
empty files for them, which is fine.

## What the tool makes of them

- **Legislative districts** are numbered from the end of `NAME`: "State House District 38", a
  half or third, "4A", "1C", or, in Alaska's Senate, a letter. A state's one at-large seat,
  "Congressional District (at Large)", is numbered 1. The tool stops unless a layer holds exactly
  the districts `Tiger.cs` lists for it.
- **Named districts** (Massachusetts, Vermont, New Hampshire's House) have no number: the office
  is the name without its chamber, "Addison-1 District", "Belknap District 1", the detail is the
  chamber, and the id is the level and `GEOID`, `state-upper-25D25`.
- **Local rows** are governments known by name: the office is `NAME` ("Wake County", "Raleigh
  city"), there is no number, and the id is the level and `GEOID`, `county-37183`. A county's
  detail is the state's word for one: "Parish" in Louisiana, "Planning region" in Connecticut,
  "Borough or census area" in Alaska.
- **"Not defined" areas are left out.** A legislative layer's `<FIPS>ZZ` or `<FIPS>ZZZ` is water no
  district takes in, so a point there has no row for that level: Lake Pontchartrain in
  Louisiana's Senate, Illinois's part of Lake Michigan in all three, Wisconsin's lakes in both
  chambers, Connecticut's Long Island Sound and New Hampshire's coast in all three, and
  Massachusetts's bays in its House. The counties go on over the water. A school layer's "School
  District Not Defined" is land or water with no district, so a point there has no school row.
  The tests allow each of these only inside a box around it.
- **Towns and townships** are kept where they govern themselves, `FUNCSTAT` A (active) or B
  (sharing its work with another government), in the twenty states where they can: CT, IL, IN,
  KS, MA, ME, MI, MN, MO, ND, NE, NH, NJ, NY, OH, PA, RI, SD, VT and WI. The rest are left out: F,
  a city the places layer has again; C, a town that is one with its city, as Hartford; G, New
  York City's boroughs; I and N, inactive, as most of Missouri's townships; S, unorganized land.
  `LSADC` gives the detail: 43 "Town", 44, 45 and 49 "Township", 39 "Plantation"; any other type
  stops the tool. A city or village inside a town or township is listed after it.
- **Alaska's Near Islands**, past the 180th meridian, are kept 360° west, below -180, so its
  districts are one piece rather than the width of the globe.
- **No population**: TIGERweb's `POP100` is empty for these layers.

## State notes

Only what explains an entry in `Tiger.cs` or a row that looks wrong and is not.

- **Missouri** uses the 2022 US House map, TIGER's 119th Congress layer (`cd-119.geojson`): the
  courts kept the 2025 map, which layer 0 holds, out of 2026 pending Proposition A on the
  November 2026 ballot. If it passes, the 2025 map is used from 2028 and `Tiger.cs` should read
  `cd.geojson` again.
- **California, Texas, Utah and North Carolina** have new US House maps for 2026; each was
  confirmed in layer 0 against the 119th Congress layer.
- **Nebraska**'s one chamber is in TIGER's upper layer; its office is "Legislature" and its files
  have no `state-lower` rows.
- **Halves and thirds**: Minnesota halves all 67 Senate districts into House districts 1A to 67B,
  North Dakota halves District 4, South Dakota 26 and 28. Maryland elects delegates from halves
  of 12 districts and thirds of 6.
- **One set of districts in both layers**: Washington, Arizona, Idaho and New Jersey elect a
  senator and two representatives from each district, so their House rows repeat the Senate's
  outlines.
- **New Hampshire**'s House layer has none of the state's floterial districts, which TIGER does not
  hold; an address in one elects a representative the site does not show.
- **Alaska**'s 20 Senate districts are lettered A to T, each two House districts. **Hawaii** has no
  cities: Honolulu is a county.
- **Same county and city row**: St. Louis, Baltimore, Virginia's independent cities and Carson
  City are in no county, so TIGER has each in both layers; Juneau, Sitka and Anchorage are a
  borough and a city in one.
- **School districts** are unified in most states. Where they are not, a point can have a high
  school and an elementary row (AZ, CA, CT, IL, MA, ME, MT, NH, NJ, NY's Long Island, RI, VT, WI,
  a corner of Oregon, towns in TN and KY, one place each in TX, MN and GA), or an elementary
  district alone, with nothing over it in TIGER (OK, MO, MI, ND, MN, WY, VA).

## Are the plans current?

TIGERweb's labels do not say: its 120th Congress layer has been updated since the vintage date,
and `CDSESSN` and `LSY` read 120 and 2026 everywhere. Each folder holds the previous layers to
compare with, `cd-119.geojson` (layer 4) and `sldu-prev.geojson` and `sldl-prev.geojson` (layers 5
and 6). Comparing on a 0.05° grid on 2026-10-25 (2026-10-27 for AK, HI, ME, NH, VT), the current
US House layer had changed in Alabama (the 2023 map the Supreme Court allowed for 2026), Florida
(signed 2026-05-04), Louisiana (SB 121, signed 2026-05-29), Missouri (see above), North Carolina,
Ohio, Texas and Utah (their 2025 maps) and Tennessee (2026), and nowhere else. Of the legislative
layers only Michigan's Senate and Mississippi's Senate and House changed, both to the court-ordered
plans in use. Each was checked against news of the plans in use for 2026. Suits still open in
Louisiana, Tennessee and Alabama could change a map; checking again is a download of the three
legislative layers and the same comparison.

## How the files are checked

The converter has no tests of its own, since its input is not in the repository.
`DistrictDataTests` in the server's tests checks its output: every covered point on a grid over
each state finds one district per legislative level (but on the water above), one county, at most
one city or a town and a city in it, and school rows that do not overlap, and no point is in two
states but for a strip a few feet wide along a border, where each file was thinned on its own.
