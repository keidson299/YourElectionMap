# Election Map Website — Design Plan V4

_As of 2026-10-06. This replaces [V3](ElectionMapMainDesignPlanV3.md) and, before it, [V2](ElectionMapMainDesignPlanV2.md) and [V1](ElectionMapMainDesignPlanV1.md); all three stay as the record of what was intended when they were written. The districts are real: the site now answers a North Carolina address with the three districts that cover it, drawn from real boundaries._

## Purpose

This website serves as a way for people to see exactly which election maps they are within. This stretches from federal election maps, through state maps, all the way down to municipal boundaries for elections like school board or city council members.

The site models six kinds of district, listed broad to local: U.S. House, State Senate, State House, County Commission, City Council and School Board. An address can fall in fewer than six, and the site never assumes it falls in all of them. **Today it has data for the first three**, and only in North Carolina; the page is built so that this is a matter of how many rows the rail has, not a special case.

Three principles hold throughout:

- **Nonpartisan.** The site is not affiliated with any election authority, and says so on both pages.
- **No accounts, no stored personal data.** There is no login. The address is used only to look up its districts and is not kept. It is sent to the US Census geocoder to be placed, and nowhere else.
- **Nothing remembered but the theme.** The only thing the site remembers is the visitor's light or dark choice, and only in their own browser.

## Status and roadmap

The work is split into numbered slices, and comments in the code refer to them by number.

| Slice                      | What it delivers                                                                                                                   | Status  |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- | ------- |
| 1. Home and foundation     | Colour and type tokens, the light/dark theme, the address form with "use my current location", URL-based routing                   | Done    |
| 2. Result page             | The district rail, the map and the phone layout, all on an invented sample lookup                                                   | Done    |
| 3. Districts API           | A server endpoint returns the lookup and the page fetches it; designed loading, error and no-districts states                      | Done    |
| 4. Real North Carolina     | Real districts and real boundaries for a real address: geocoding, and the state's US House, State Senate and State House districts | Done    |
| 5. The other three levels  | County Commission, City Council and School Board, which need boundary data the project does not have yet                           | Next    |
| 6. Election information    | The next election's date, candidates and ballot measures, which brings the rail's footer back                                       | Planned |
| Later                      | More states; the election detail view; the data store and the job that keeps it current                                             | Planned |

Slice 4 absorbed what V3 had as slice 5, "real geometry". The precinct shapefiles it is built from carry the district boundaries as well as the district assignments, so one slice delivered both, and Census TIGERweb is no longer the plan — see **Data** below.

## How it works

The site has two parts: a React front end that the user interacts with, and a C# back end that does the lookups. They ship as one app. The ASP.NET Core server hosts the API and also serves the built front end, so the browser talks to a single origin and there is no cross-origin setup. In development the two run as separate servers joined by a proxy; [CLAUDE.md](../CLAUDE.md) and the [README](../README.md) cover that setup.

A visit goes like this:

1. On the Home page the user types an address, or asks the browser for their current location.
2. The site looks that up, saying "Checking…" on the button, and then moves to the result page, whose URL carries what is being looked up: `/map?address=…` or `/map?lat=…&lon=…`.
3. The result page lists every district covering that place and draws one of them on the map. Picking a different district adds `&district=…` to the URL.

The URL is the source of truth for what is looked up and which district is shown, so any result can be bookmarked or shared and reopens as it was. A lookup from Home adds a history entry. A new address typed on the result page, or a different district, replaces the current entry, so Back always returns to Home rather than stepping through every lookup and district.

## Front end

### Home

A card sits on the left of the page, over a map that fills the background. The map is decorative: a hand-drawn "survey sheet" of district outlines on a coordinate grid, with one district hatched and a pin on it. It is not a map of the US and shows no real place. A veil fades from the card's edge to nothing by 76% of the page width, so the grid stays readable behind the card. On a phone the card drops to the bottom of the screen and its controls stack full width.

The card holds the site name and tagline, the address field with a **Find My Maps** button, and a **Use my current location** button that asks the browser for its position. Under them, where the privacy note already sat, the card says that North Carolina addresses are the ones it can answer for now — better said before someone types than after.

The tagline ("plus the candidates and dates that come with them") and the card's last line, "Also included: candidates · ballot measures · election dates", still promise the election information that arrives in slice 6. Until then they describe the site as it is meant to be rather than as it is.

### Validating the address

The client checks only that the field is not empty, and says so under the field if it is; the field also stops at 200 characters, the server's own limit. Whether the text is an address the site can answer for is the server's lookup to decide, since it has to place the address anyway.

That lookup runs before the page changes: the submit button shows a spinner and reads "Checking…", and the field stays readable. An address the server cannot answer for is said under the field the visitor typed it into, rather than on a result page with nothing to show — the same on Home and behind the result page's **Update**. If the browser cannot or will not share its location, the form says so and suggests typing an address instead.

There are two reasons an address comes back with nothing, and **the page does not try to tell them apart**: it could not be placed at all, or it was placed somewhere the site has no districts for. One message covers both and names the real limit — that the site covers North Carolina so far — because that is the likelier of the two and the one a visitor can act on.

A lookup that gets no answer at all is a third case, and is kept apart from those two: the server or the geocoder behind it did not respond, which says nothing about the address. The form says the lookup is not responding and to try again in a moment, without flagging the field. The page gives up waiting after fifteen seconds.

The result page has all three states of its own as well, because a `/map` URL can be opened, shared or reloaded without passing through a form. A card over the empty survey sheet says that the lookup is running, that the address could not be placed, or that the lookup is not responding, the last with a **Try again** button.

### Result page

On a desktop the page has three parts:

- **Header** (68px tall): the site name, which links back to Home; the address, still editable, with an **Update** button; the nonpartisan note; and the theme toggle.
- **Rail** (400px wide, on the left): headed "N districts cover you", it lists every district covering the address, broad to local. The rows work as one radio group: one Tab stop, and arrow keys to move between rows.
- **Map**: the rest of the width.

Unless the URL names a district, the State House district is selected first, because it is the most local boundary the data has and it is drawn at something like neighbourhood scale. If there is no State House district, the first district is selected.

The selected row is the boundary drawn solid and hatched on the map. Its neighbours in the list, one on each side where there are two, are drawn for context: the broader one dashed, the more local one dotted. Line style, not colour alone, tells the three apart, so the map still reads in greyscale print and for colour-blind visitors.

With three nested levels rather than six, those context boundaries are often much larger than the selection and run off the edge of the view. That is left as it is: a line leaving the frame is a true thing to draw, and the legend names what each one is.

At the foot of the rail, under "Also for this address", sit the next election's date, the number of candidates on the ballot, and the number of ballot measures. This footer is left out when the address has no election information, **which is every address today** — there is no election data yet, so nobody sees it until slice 6.

In the corner of the rail, where the "Sample data" tag used to sit while the districts were invented, the rail now names the data the boundaries were drawn from: "NC 2024 precincts". It is not decoration. These boundaries are precinct shapes added together, and the data's own documentation warns that a precinct's district assignment may not follow the district exactly in large unpopulated areas, so the page says which data drew the line rather than presenting it as the last word.

### Map

The map is hand-written SVG, not a mapping library. It is always centred on the address, and opens fitted to the selected district so that all of its edges are in view. From there the zoom buttons step in twice or out once; there is no panning or dragging. Line weights and the address pin keep their on-screen size at every zoom.

Everything is drawn in **map units**: one unit is 96 feet, so 55 units make a mile. That number was chosen so the spec's sizes keep their meaning — the 620-unit design frame is 11.3 miles across, about right as the closest view of a State House district, and the spec's 92px scale bar still reads one mile at it. The scale bar's distances now run up to fifty miles, because a congressional district is a hundred miles wide and fitting one takes the frame out that far.

Keeping the address centred means a district whose address sits near one end is fitted by widening the frame on both sides, so it can appear smaller and off to one side. That is the price of the pin always being in the middle of the map, and it is worth looking at again once there is more than one state.

The paper and graticule follow the view rather than sitting at fixed coordinates, because a real address can be anywhere in the state. Nothing else is drawn under the boundaries: the hand-drawn river, roads and mesh of district outlines belong to Home's decorative board, and on a map of real places they would be claiming those places have them.

Over the map sit:

- a "Showing" chip naming the selected district, with its area and population when they are known — neither is in this data, so the chip shows the name alone
- a legend keying the three line styles
- a scale bar
- the address's label beside its pin

The drawing itself is hidden from screen readers. Everything it shows that matters is said in text by those overlays and by the rail.

### Phone (860px and narrower)

The map moves to the top third of the screen and the district list becomes a sheet under it. The sheet's handle expands it to take most of the screen, leaving the map just tall enough for its chip and zoom buttons. Picking a district collapses the sheet again, so the new boundary is in view.

The legend becomes a pill counting the boundaries drawn, and the chip uses a shorter district name. The election footer shrinks to one line: the date and the candidate count.

### Theme

The theme follows the device's light or dark setting until the user picks one with the toggle (system → light → dark). An explicit choice is remembered in the browser and applied before the page first paints.

### Election details (later slice)

V1 planned for full information about the elections at an address: candidate names, political affiliation, candidate websites, propositions, the dates of the previous and upcoming elections, and published results once an election has passed. That is still the plan, as a detail view reached from the rail's footer. The footer itself comes back in slice 6 with the summary — the next election's date and the candidate and measure counts — and the detail view after it. Neither has been designed yet.

With that view there will be three screens: Home, the result page, and election details. There is still no login.

### Accessibility

These decisions were made at design time and carried into the build:

- Every field has a label, hidden visually but not absent, and submits through a real form, so Enter works.
- Boundaries are told apart by line style, not by colour alone.
- On a page change, focus moves to the new page's heading so a screen reader announces where it landed, and the browser tab's title names the address.
- Controls are large enough to use by touch.

## Client design & structure

The front end is built with React 19 and TypeScript, bundled by Vite. React was chosen mainly due to my familiarity with it, as well as it being widely used and well suited to a project of this size. The app's only runtime dependencies are React itself:

- **No router.** There are two routes, so a small wrapper over the browser's History API is enough.
- **No UI or map library.** Styling is plain CSS, and the map is hand-written SVG. Real boundaries are drawn by the same code that drew the stand-in, because the server sends them in the units that drawing already worked in.
- **Design tokens.** Every size, colour and line weight comes from the build spec, [canvas4/Main.dc.html](canvas4/Main.dc.html). Colours are CSS custom properties with a matching dark set. The type is Fraunces for display and Inter for the interface, loaded from Google Fonts. The folders `canvas` to `canvas3` are the design rounds that led to the spec, and [canvas3/Notes.dc.html](canvas3/Notes.dc.html) records the decisions made along the way.
- **Tests and linting.** Vitest with Testing Library for tests, and oxlint for linting.

## The lookup contract

The result page renders one shape, whatever produced it. The server sends it as JSON. It is written twice, and the two halves are changed together: `DistrictLookup` in [types.ts](../YourElectionMap/yourelectionmap.client/src/districts/types.ts) is what the page reads, and the records in [DistrictLookup.cs](../YourElectionMap/YourElectionMap.Server/Districts/DistrictLookup.cs) are what the server writes. In plain terms, a lookup holds:

| Field     | What it holds                                                                                                                                                                                                                    |
| --------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| label     | The address as the page names it: the street line as typed, such as "16 W Jones St", or "Your location"                                                                                                                         |
| location  | The address as a point on the map, in map units                                                                                                                                                                                  |
| districts | Each district covering the address: a stable id; its level; its office ("State House"); its unit and number ("District 44", "Ward 2"); a short detail line; the area it covers and its population, where known; and its boundary |
| election  | The next election's date and its candidate and measure counts, or nothing when there is no election information                                                                                                                  |
| source    | What the boundaries were drawn from, for the rail to name                                                                                                                                                                        |

The page relies on two rules. It never assumes six districts, and handles any number, including none. And a boundary is a list of rings rather than a single polygon, so districts with islands or holes fit without changes — which they now do.

## Server design & structure

The back end is built with C# on ASP.NET Core 9. As with the client, both have great familiarity and a wealth of resources to help address any issues that come up.

**Today** the server has one endpoint of its own, the districts lookup below, alongside OpenAPI in development and a fallback that serves the front end for any other path.

Its tests host the whole app in memory and call it over HTTP, reading the raw JSON, so routing and the field names and formats on the wire are covered too — that JSON is what the client reads, so it is where the contract is pinned down. The geocoder is stubbed in those tests, so nothing in the suite reaches the internet; everything else about them is real, including the whole district file.

**The districts endpoint** is `GET /api/districts`, under the prefix the development proxy already forwards. It takes either an `address` (free text, as typed) or `lat` and `lon`: the same inputs the result page's URL carries, and the address wins if both are given. The address travels in the query string, as it already does in the page URL, and is not logged. For each request it:

1. Resolves the input to a point. An address is geocoded; coordinates are used as given. An address with no state can match in several places, so each match is tried in the geocoder's order and the first one inside North Carolina wins.
2. Projects that point into the map's own coordinates.
3. Finds every district whose boundary contains it, broad to local.
4. Returns the lookup, with no election summary and the source of the boundaries named.

It answers in one of four ways:

- **200** with the lookup, which always holds at least one district.
- **404** when the address cannot be placed, or the point is in none of the districts — the no-match the page has a designed state for.
- **503** when the geocoder did not answer, so the page can offer to try again rather than doubt the address.
- **400** for a query it cannot use: neither an address nor both coordinates, coordinates off the Earth, or an address over 200 characters. Only a hand-edited URL can send one, so the client treats it as a no-match.

The client reads 404 and 400 as no-match and every other failure, a 503 included, as an error, so that a server or geocoder it cannot reach never reads as an address that does not exist.

Because districts are found from the point itself, an address in an unincorporated area will simply have no city district once there is city data, which the page already handles.

**Data.** The boundaries come from the North Carolina State Board of Elections' 2024 precinct shapefiles, retrieved through the Redistricting Data Hub. They are not in the repository — they are about 130MB — and they are not read at runtime either. A console project, `YourElectionMap.Tools`, is run by hand to turn them into one four-megabyte file, `nc-districts.json`, which the server reads at startup. That file is generated rather than checked in — it is git-ignored — so a fresh copy of the repository needs the shapefiles and a run of the tool before the server will start or its data tests pass; [DATA-SOURCE.md](../YourElectionMap/YourElectionMap.Tools/DATA-SOURCE.md) records where they came from and what they can be trusted to say. Three things about that are worth stating as design decisions rather than implementation detail:

- **The district boundaries are precinct boundaries added together.** The files give each precinct's district, not each district's shape, so the shapes are dissolved out of the precincts. The data's own documentation warns that a precinct's district assignment may not follow the district exactly in large unpopulated areas, so an address near a district line in open country may be placed on the wrong side of it. This is why the page credits the data instead of presenting the line as authoritative.
- **It is generated once, not computed per request.** Nothing about those boundaries changes between redistrictings, so dissolving them at startup or per lookup would be work repeated for no reason.
- **Which district an address is in is answered from the same boundaries the page draws**, held at finer detail than it draws them. That keeps one source of truth, and means the only thing standing between an address and an answer is a point-in-polygon test.

An address is placed by the **US Census geocoder** (`geocoding.geo.census.gov`, free and keyless). It is the one call this app makes out to the internet. The same agency's view of the ground underlies the Census-derived parts of the boundary data, so an address and a district line agree about where they are. A geocoder that answers with no match is "could not place it", the same 404 as an address that does not exist. A geocoder that is down, gives up after eight seconds, or answers with something that is not JSON is a different matter: that says nothing about the address, so it is a 503 and the page offers to try again.

Where the **election** data will come from, and where it is stored, is still open, with V1's plan as the starting point:

- A job runs on a fixed schedule, likely as a serverless function, and queries the relevant government offices for the most up-to-date election information. Maps are updated once a year, or manually for an out-of-season map change. Candidate information is updated daily or weekly. A serverless function is attractive because it is the easiest to schedule and cheaper to operate than a standalone server.
- The data is kept in a cloud database alongside that job. There are two options: a key-value store for the maps and a relational database for the election information, or a single key-value store for both. Two stores add complexity and are harder to maintain, but they give the text-based election information more structure and could save cost and space.

The boundaries themselves no longer need either: a generated file that ships with the app is simpler than a store, and stays simpler as long as it fits.

**Hosting.** The server and the built front end ship together as one Linux container; the project already has a Dockerfile. The host is still to be chosen, on cost. Because the district file is not in the repository, the image carries it only when it has been generated into the build context first; an image built from a fresh clone has no districts and the server will not start.

**Privacy.** No store holds any user data; the only data stored is publicly available election information. The address a user types is used for the lookup and not kept. It does leave the server once, to the Census geocoder, which is the one thing a visitor should know that V3 did not have to say. Nothing is logged, and the lookup's answer is sent `no-store` so that nothing between the server and the browser keeps the address it echoes back. This keeps data security risks to a minimum, and means storage needs stay the same however many people use the site.
