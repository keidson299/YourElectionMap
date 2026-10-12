# Election Map Website — Design Plan V5

_As of 2026-10-11. This replaces V1 to V4, which are in the repository's git history as the record of what was intended when they were written. The districts are drawn over a real street map: the boundaries alone showed the shape of a district but not where it falls, and seeing which side of which street the line runs down is the point of the site._

## Purpose

This website serves as a way for people to see exactly which election maps they are within. This stretches from federal election maps, through state maps, all the way down to municipal boundaries for elections like school board or city council members.

The site models six kinds of district, listed broad to local: U.S. House, State Senate, State House, County Commission, City Council and School Board. An address can fall in fewer than six, and the site never assumes it falls in all of them. **Today it has data for the first three**, and only in ten states, Arizona, California, Idaho, Montana, North Carolina, Oregon, South Carolina, Utah, Washington and Wyoming. For the local three it has, in California and North Carolina, the governments themselves rather than their electoral districts: the county, the city or town, and the school district, each named rather than numbered. The page is built so that all of this is a matter of how many rows the rail has, not a special case.

Three principles hold throughout:

- **Nonpartisan.** The site is not affiliated with any election authority, and says so on both pages.
- **No accounts, no stored personal data.** There is no login. The address is used only to look up its districts and is not kept. Outside services do see parts of a visit, and the **Privacy** section below says which.
- **Nothing remembered but the theme.** The only thing the site remembers is the visitor's light or dark choice, and only in their own browser.

## Status and roadmap

The work is split into numbered slices, and comments in the code refer to them by number.

| Slice                     | What it delivers                                                                                                                       | Status  |
| ------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- | ------- |
| 1. Home and foundation    | Colour and type tokens, the light/dark theme, the address form with "use my current location", URL-based routing                       | Done    |
| 2. Result page            | The district rail, the map and the phone layout, all on an invented sample lookup                                                       | Done    |
| 3. Districts API          | A server endpoint returns the lookup and the page fetches it; designed loading, error and no-districts states                          | Done    |
| 4. Real North Carolina    | Real districts and real boundaries for a real address: geocoding, and the state's US House, State Senate and State House districts     | Done    |
| 5. Street map             | The result map becomes a real, draggable street map with the districts drawn over it                                                    | Done    |
| 6. Enacted district maps  | The three levels built from the General Assembly's own plans, including the 2025 congressional map, with each district's population (dropped in slice 9: TIGER has none) | Done    |
| 7. Election information   | The next election's date and the number of candidates and ballot measures, from Google's voter lookup, which brings the rail's footer back | Done    |
| 8. California             | California's US House, State Senate and Assembly districts from the Census Bureau's TIGER data, the first state not from its own enacted plans | Done    |
| 9. One source             | North Carolina's districts from TIGER too, the same as California's, which retires the enacted-shapefile converter and its projection  | Done    |
| 10. Local governments     | The county, city or town and school district covering the address, by name, from TIGER, with the county's name on the map chip; California and North Carolina first. The election is now asked for by ID from Google's election list | Done    |
| Next                      | The local layers for the other eight states                                                                                              | Next    |
| Later                     | More states, by the same TIGER pipeline; County Commission, City Council and School Board districts inside the local governments; the election detail view, with voting places | Planned |

**Deployment is the last step, not a slice.** It comes once the slices that should be live are done. With the general election on 3 November 2026 and the current maps and election information in place, it can come now.

Two changes from V4's order. Slice 5 is new: the street map had been left out of every plan so far, and the site does not do its job without it. And election information came before the other three levels, because its data can come ready-made from an outside service (see **Outside data**), while local district boundaries have no statewide source.

## How it works

The site has two parts: a React front end that the user interacts with, and a C# back end that does the lookups. They ship as one app. The ASP.NET Core server hosts the API and also serves the built front end, so the browser talks to a single origin for everything but the street map's tiles. In development the two run as separate servers joined by a proxy; [CLAUDE.md](../CLAUDE.md) and the [README](../README.md) cover that setup.

A visit goes like this:

1. On the Home page the user types an address, or asks the browser for their current location.
2. The site looks that up, saying "Checking…" on the button, and then moves to the result page, whose URL carries what is being looked up: `/map?address=…` or `/map?lat=…&lon=…`.
3. The result page lists every district covering that place and draws one of them on the map. Picking a different district adds `&district=…` to the URL.

The URL is the source of truth for what is looked up and which district is shown, so any result can be bookmarked or shared and reopens as it was. A lookup from Home adds a history entry. A new address typed on the result page, or a different district, replaces the current entry, so Back always returns to Home rather than stepping through every lookup and district.

## Front end

### Home

A card sits on the left of the page, over a map that fills the background. The map is decorative: a hand-drawn "survey sheet" of district outlines on a coordinate grid, with one district hatched and a pin on it. It is not a map of the US and shows no real place. A veil fades from the card's edge to nothing by 76% of the page width, so the grid stays readable behind the card. On a phone the card drops to the bottom of the screen and its controls stack full width.

The card holds the site name and tagline, the address field with a **Find My Maps** button, and a **Use my current location** button that asks the browser for its position. Under them, the card names the states whose addresses it can answer for now — better said before someone types than after.

The tagline ("plus the candidates and dates that come with them") and the card's last line, "Also included: candidates · ballot measures · election dates", promise the election information the rail's footer gives, where Google has it for the address.

### Validating the address

The client checks only that the field is not empty, and says so under the field if it is; the field also stops at 200 characters, the server's own limit. Whether the text is an address the site can answer for is the server's lookup to decide, since it has to place the address anyway.

That lookup runs before the page changes: the submit button shows a spinner and reads "Checking…", and the field stays readable. An address the server cannot answer for is said under the field the visitor typed it into, rather than on a result page with nothing to show — the same on Home and behind the result page's **Update**. If the browser cannot or will not share its location, the form says so and suggests typing an address instead.

There are two reasons an address comes back with nothing, and **the page does not try to tell them apart**: it could not be placed at all, or it was placed somewhere the site has no districts for. One message covers both and names the real limit — the states the site covers so far — because that is the likelier of the two and the one a visitor can act on.

A lookup that gets no answer at all is a third case, and is kept apart from those two: the server or the geocoder behind it did not respond, which says nothing about the address. The form says the lookup is not responding and to try again in a moment, without flagging the field. The page gives up waiting after fifteen seconds.

The result page has all three states of its own as well, because a `/map` URL can be opened, shared or reloaded without passing through a form. A card over the empty survey sheet says that the lookup is running, that the address could not be placed, or that the lookup is not responding, the last with a **Try again** button.

### Result page

On a desktop the page has three parts:

- **Header** (68px tall): the site name, which links back to Home; the address, still editable, with an **Update** button; the nonpartisan note; and the theme toggle.
- **Rail** (400px wide, on the left): headed "N districts cover you", it lists every district covering the address, broad to local. The rows work as one radio group: one Tab stop, and arrow keys to move between rows.
- **Map**: the rest of the width.

Unless the URL names a district, the State House district is selected first, because every covered address has one and it is drawn at something like neighbourhood scale. A city or school district can be more local, but many addresses have neither, and a county is often broader than a congressional district. If there is no State House district, the first district is selected.

The selected row is the boundary drawn solid and hatched on the map. Its neighbours in the list, one on each side where there are two, are drawn for context: the broader one dashed, the more local one dotted. Line style, not colour alone, tells the three apart, so the map still reads in greyscale print and for colour-blind visitors.

Neighbours in the list can differ a great deal in size — a State Senate district beside a State House district, a county beside a city — so those context boundaries are often much larger than the selection and run off the edge of the view. That is left as it is: a line leaving the frame is a true thing to draw, and the legend names what each one is.

At the foot of the rail, under "Also for this address", sit the next election's date, the number of candidates on the ballot, and the number of ballot measures. They come from Google's voter lookup (see **Outside data**). The footer is left out when there is no election to show: Google has none for the address, did not answer, or the lookup was by location, which gives Google no address to ask about. Google's answer can name the election without the ballot — it leaves the contests out when two elections share a day — and then the footer gives the date alone rather than claim "0 candidates".

Voting places, which slice 7 first set out to include, wait for the election detail view, where there is room to say which place is for which day.

In the corner of the rail, the rail names the data the boundaries were drawn from: "U.S. Census Bureau TIGER", in every state. Districts are redrawn, and North Carolina's, California's and Utah's congressional districts were redrawn in 2025, so the page says which maps it is showing.

### Map

The map is a real street map, drawn with **MapLibre GL** from **OpenFreeMap**'s OpenStreetMap tiles. It shows streets, and their names once the view is close enough to read them — nothing else from the tiles, no buildings, water or points of interest. The streets are drawn in the spec's road colour on the spec's paper, so the page keeps its look, and the map thins itself as it widens: a congressional district's frame shows highways, a State House district's shows every street.

It opens centred on the address and fitted to the selected district, so that all of its edges are in view, and never closer than the spec's design frame: 11.3 miles across on a desktop, about right as the closest view of a State House district. From there the visitor can drag, scroll or pinch to any part of the state and down to street level. The `+` and `−` buttons step by the spec's half again a press. Picking another district refits the view to it; a new address opens a new map. The map does not rotate or tilt.

The pin and the address's label ride with the address as the map moves. The scale bar's distances run from a tenth of a mile to fifty, because a congressional district is a hundred miles wide and a street corner is much less.

The boundaries are sent at the full detail the server holds, not simplified for the opening view. A voter zooms in to see which side of a street the line runs down, and a line thinned for a hundred-mile view would put it on the wrong side.

The tile provider requires a credit, which sits in the map's corner as a small expandable note. The graticule of the old survey sheet is gone from the result map: the streets replace it. The waiting and no-match pages still draw the empty sheet.

Over the map sit:

- a "Showing" chip naming the selected district, and under it on a desktop the address's county, where the state's file has counties ("State House · District 38" over "Wake County"); the county's own chip names it alone. TIGER has no population, so the chip shows none
- a legend keying the three line styles
- a scale bar
- the address's label beside its pin

The drawing itself is hidden from screen readers. Everything it shows that matters is said in text by those overlays and by the rail. A browser that cannot draw the map at all (it needs WebGL) is told so where the map would be, and still gets the district list.

### Phone (860px and narrower)

The map moves to the top third of the screen and the district list becomes a sheet under it. The sheet's handle expands it to take most of the screen, leaving the map just tall enough for its chip and zoom buttons. Picking a district collapses the sheet again, so the new boundary is in view.

The legend becomes a pill counting the boundaries drawn, and the chip uses a shorter district name. The election footer shrinks to one line: the date and the candidate count.

### Theme

The theme follows the device's light or dark setting until the user picks one with the toggle (system → light → dark). An explicit choice is remembered in the browser and applied before the page first paints. The street map follows the theme as it changes.

### Election details (later slice)

V1 planned for full information about the elections at an address: candidate names, political affiliation, candidate websites, propositions, the dates of the previous and upcoming elections, and published results once an election has passed. That is still the plan, as a detail view reached from the rail's footer. The footer came back in slice 7; the detail view, with the polling place and early-voting sites, has not been designed yet.

### Accessibility

These decisions were made at design time and carried into the build:

- Every field has a label, hidden visually but not absent, and submits through a real form, so Enter works.
- Boundaries are told apart by line style, not by colour alone.
- On a page change, focus moves to the new page's heading so a screen reader announces where it landed, and the browser tab's title names the address.
- Controls are large enough to use by touch.

## Client design & structure

The front end is built with React 19 and TypeScript, bundled by Vite. React was chosen mainly due to my familiarity with it, as well as it being widely used and well suited to a project of this size. The app's runtime dependencies are React and MapLibre GL:

- **No router.** There are two routes, so a small wrapper over the browser's History API is enough.
- **One map library, loaded only when needed.** MapLibre GL draws the result map. It is most of the app's weight, so it loads with the first result rather than with Home. The map's whole look is built in code from the design tokens rather than fetched from the tile provider, so the provider is one setting to change.
- **No UI library.** Styling is plain CSS. Home's decorative board and the waiting sheet are still hand-written SVG.
- **Design tokens.** Every size, colour and line weight comes from the build spec, [canvas4/Main.dc.html](canvas4/Main.dc.html). Colours are CSS custom properties with a matching dark set. The type is Fraunces for display and Inter for the interface, loaded from Google Fonts. The design rounds that led to the spec, `canvas` to `canvas3`, are in the git history.
- **Tests and linting.** Vitest with Testing Library for tests, and oxlint for linting. The tests run without WebGL, so they replace MapLibre with a fake that records what the page tells it.

## The lookup contract

The result page renders one shape, whatever produced it. The server sends it as JSON. It is written twice, and the two halves are changed together: `DistrictLookup` in [types.ts](../YourElectionMap/yourelectionmap.client/src/districts/types.ts) is what the page reads, and the records in [DistrictLookup.cs](../YourElectionMap/YourElectionMap.Server/Districts/DistrictLookup.cs) are what the server writes. In plain terms, a lookup holds:

| Field     | What it holds                                                                                                                                                                                                                    |
| --------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| label     | The address as the page names it: the street line as typed, such as "16 W Jones St", or "Your location"                                                                                                                         |
| location  | The address as longitude and latitude                                                                                                                                                                                            |
| districts | Each district covering the address: a stable id; its level; its office ("State House"), or for a county, city or school district its name ("Wake County"); its unit and number ("District 44", "Ward 2"), both empty for a named row; a short detail line ("Federal", "County"); the address's county for the map chip, where known; and its boundary |
| election  | The next election's date and its candidate and measure counts, or nothing when there is no election information. Either count can be missing on its own, when the election data names the election but not the ballot           |
| source    | What the boundaries were drawn from, for the rail to name                                                                                                                                                                        |

The page relies on two rules. It never assumes six districts, and handles any number, including none. And a boundary is the coordinates of a GeoJSON MultiPolygon — polygons, each an outer ring and then its holes — so districts with islands or holes are drawn as one, and the map can be handed them as they are.

## Server design & structure

The back end is built with C# on ASP.NET Core 9. As with the client, both have great familiarity and a wealth of resources to help address any issues that come up.

**Today** the server has one endpoint of its own, the districts lookup below, alongside OpenAPI in development and a fallback that serves the front end for any other path.

Its tests host the whole app in memory and call it over HTTP, reading the raw JSON, so routing and the field names and formats on the wire are covered too — that JSON is what the client reads, so it is where the contract is pinned down. The geocoder and Google's voter lookup are stubbed in those tests, so nothing in the suite reaches the internet; everything else about them is real, including every district file.

**The districts endpoint** is `GET /api/districts`, under the prefix the development proxy already forwards. It takes either an `address` (free text, as typed) or `lat` and `lon`: the same inputs the result page's URL carries, and the address wins if both are given. The address travels in the query string, as it already does in the page URL, and is not logged. For each request it:

1. Resolves the input to a point. An address is geocoded; coordinates are used as given. An address with no state can match in several places, so each match is tried in the geocoder's order and the first one inside a state the site covers wins.
2. Finds every district whose boundary contains the point, broad to local, in longitude and latitude, the coordinates the district files hold.
3. Names what that state's boundaries were drawn from.
4. For a typed address, reads Google's list of coming elections, picks the soonest that is nationwide or in the address's state, and asks Google's voter lookup about the address for that election, sending the geocoder's spelling of the match it used, which names the state.
5. Returns the lookup, with each boundary as GeoJSON polygons, the election summary if there is one, and the source of the boundaries named. A browser that accepts compressed responses gets one: boundaries at full detail are tens of kilobytes of coordinates, and more for the largest districts.

It answers in one of four ways:

- **200** with the lookup, which always holds at least one district.
- **404** when the address cannot be placed, or the point is in none of the districts — the no-match the page has a designed state for.
- **503** when the geocoder did not answer, so the page can offer to try again rather than doubt the address.
- **400** for a query it cannot use: neither an address nor both coordinates, coordinates off the Earth, or an address over 200 characters. Only a hand-edited URL can send one, so the client treats it as a no-match.

The client reads 404 and 400 as no-match and every other failure, a 503 included, as an error, so that a server or geocoder it cannot reach never reads as an address that does not exist.

Because districts are found from the point itself, an address in an unincorporated area simply has no city row, and one where the Census defines no school district has no school row, which the page already handles.

**Data.** Every boundary comes from the Census Bureau's TIGERweb: its Legislative layers for the 120th Congress's US House districts and the 2026 state legislative districts, and its county, incorporated place and school district layers for the local rows. There is one file per state, each built by hand with a console project, `YourElectionMap.Tools`, and read by the server at startup:

- **North Carolina, `nc-districts.json` (about fifteen megabytes):** the General Assembly's 2025 congressional plan (CCM-2, enacted 21 October 2025) and its 2023 State Senate and State House plans (Session Laws 2023-146 and 2023-149), and its 100 counties, 552 cities and towns and 118 school districts, all of them county or city systems serving every grade. Until slice 9 the districts came from the enacted plans' own shapefiles, retrieved through the Redistricting Data Hub.
- **California, `ca-districts.json` (about nineteen megabytes):** the map California's voters approved in November 2025, the State Senate and Assembly districts, and its 58 counties, 483 cities and towns, and 974 school districts. Where California has no unified district, an address has a high school district and an elementary district inside it, and the rail lists both, high school first.
- **Oregon, `or-districts.json`, South Carolina, `sc-districts.json`, and Washington, `wa-districts.json` (three to five megabytes each):** their current plans. Washington elects a senator and two representatives from each of its 49 legislative districts, so its State House districts are its State Senate districts.
- **Arizona, Idaho, Montana, Utah and Wyoming (`az`, `id`, `mt`, `ut` and `wy-districts.json`, two to five megabytes each):** their current plans, with Utah's court-ordered congressional map of November 2025. Arizona and Idaho elect their House two to a Senate district, as Washington does. Wyoming's one House seat is at large, and the page calls it District 1.

The other eight states' files have no local rows yet. TIGER carries no population.

The local rows are the governments as a whole, not the districts their boards are elected from: a voter in Wake County votes for its commission, but from one of its commission districts, which TIGER does not hold. A row is named as the Census names it ("Wake County", "Raleigh city", "Wake County Schools") and identified by its Census code, which is stable across vintages and safe in the page's URL. A city's name carries its legal kind, so a few read oddly ("Crescent City city"); the Census also has the bare name, should that be preferred.

The sources are not in the repository and not read at runtime. The files are generated rather than checked in — they are git-ignored — so a fresh copy of the repository needs the sources and a run of the tool for each state before the server will start or its data tests pass; [DATA-SOURCE.md](../YourElectionMap/YourElectionMap.Tools/DATA-SOURCE.md) records where they came from. Every file holds its boundaries in longitude and latitude, thinned to fifteen feet. Three things about that are worth stating as design decisions rather than implementation detail:

- **The boundaries are the districts themselves.** TIGER holds one shape per district. Slice 4 had to build districts by adding up 2024 precincts, which approximated the lines in open country and showed the US House map the 2025 plan replaced. Before North Carolina was switched to TIGER, its layers were checked against the enacted plans on a grid over the state: they place the same districts everywhere but within a few feet of a line, which is why one national source can be trusted for every state.
- **It is generated once, not computed per request.** Nothing about those boundaries changes between redistrictings, so converting them at startup or per lookup would be work repeated for no reason.
- **Which district an address is in is answered from the same boundaries the page draws**, at the same detail. That keeps one source of truth, and means the only thing standing between an address and an answer is a point-in-polygon test. The test runs in longitude and latitude, the coordinates TIGER, the files and the page's map all use, so nothing is projected anywhere.

An address is placed by the **US Census geocoder** (`geocoding.geo.census.gov`, free and keyless). It is one of the two calls the server makes out to the internet. A geocoder that answers with no match is "could not place it", the same 404 as an address that does not exist. A geocoder that is down, gives up after eight seconds, or answers with something that is not JSON is a different matter: that says nothing about the address, so it is a 503 and the page offers to try again.

The other call is **Google's Civic Information API**, for the election, in two requests. The first reads its list of elections (`civicinfo/v2/elections`), which sends nothing about the visitor, and picks the soonest that has not passed and is either nationwide or in the address's state, leaving out Google's permanent "VIP Test Election". The second asks the voter lookup (`civicinfo/v2/voterinfo`) about the address for that election by its ID: asked without one, Google answers "Election unknown", even weeks before a general election. An election for one county of the state counts too, so for an address outside that county Google answers with nothing, and the footer is left out until that election has passed. Both need an API key, which the server reads from its configuration (`GoogleCivic:ApiKey`: user secrets in development, an environment variable when deployed) and sends in a request header rather than the URL. The election is extra to the districts, so these calls can never fail a lookup: no key, no coming election, a refusal, four seconds passing across the two requests, or an answer that cannot be read all mean the lookup goes out without an election and the footer is left out. A fresh copy of the repository has no key and runs that way. An election is shown until its day has ended in every state the site covers, which is the Pacific time zone.

**Hosting.** The server and the built front end ship together as one Linux container; the project already has a Dockerfile. The host is still to be chosen, on cost. Because the district files are not in the repository, the image carries them only when they have been generated into the build context first; an image built from a fresh clone has no districts and the server will not start.

## Outside data

Building every data source locally widens the project too far, so outside services are now an accepted way to fill a gap. Each one is weighed on cost, on whether it needs an account or key, and on what it sees of a visit. These were checked on 2026-10-07:

- **Street map tiles: OpenFreeMap.** Free, with no key, no account, no cookies and no limit on use, and a credit required. It offers no service guarantee, so the provider is one setting to change; Stadia Maps' free plan is the fallback.
- **Election information (slice 7, in use): the Google Civic Information API's election list and voter information lookup.** Free with an API key, up to 25,000 lookups a day; each address lookup on the site uses two, one of each. For an address in a supported election, it returns early voting and drop-off locations and the contests and candidates on that ballot. The data comes through the Voting Information Project from state and local election officials. The State Board of Elections' own candidate lists and polling-place files are the fallback and the cross-check. Google's separate representatives lookup was shut down in April 2025, so who holds each seat would come from elsewhere, such as Open States.
- **District boundaries (in use for every state since slice 9): Census TIGERweb.** Free, keyless and public domain. Its Legislative service holds every state's US House and state legislative districts, with the current plans as of its January 2026 vintage, in longitude and latitude. It is downloaded once per state and converted, not called per lookup. It has no population. The site credits the U.S. Census Bureau for its map data, linked to TIGERweb.
- **Local jurisdictions (slice 10, in use in California and North Carolina): Census TIGERweb.** Its county, incorporated place and school district layers give each local government's outline, downloaded and converted the same way as the legislative ones. County commission, city council and school board electoral districts have no statewide source: counties publish their own (Wake and Mecklenburg among them), so they would come county by county, or from a paid national service such as Cicero or BallotReady.

## Privacy

No store holds any user data; the only data stored is publicly available election information. The address a user types is used for the lookup and not kept. Nothing is logged, and the lookup's answer is sent `no-store` so that nothing between the server and the browser keeps the address it echoes back. This keeps data security risks to a minimum, and means storage needs stay the same however many people use the site.

What leaves the site, and to whom:

- **The address goes to the US Census geocoder** to be placed, from the server.
- **The street map's tiles come from OpenFreeMap**, fetched by the visitor's own browser. OpenFreeMap therefore sees the visitor's IP address and which part of the map they look at, which is roughly where the address is. It does not see the address itself.
- **The address goes to Google's voter lookup** for its election, from the server, spelled as the Census geocoder matched it. Google sees the address and the server, not the visitor or their IP address. A lookup by location sends Google nothing.
- **Fonts come from Google Fonts**, which sees the visitor's IP address.
