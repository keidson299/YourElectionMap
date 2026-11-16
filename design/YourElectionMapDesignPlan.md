# YourElectionMap — Design Plan

## Purpose

The site shows people exactly which election districts they are in, from federal down to local, drawn over a real street map so they can see which side of which street a line runs down.

It models six kinds of district, broad to local: U.S. House, State Senate, State House, County Commission, City Council and School Board. **It has data for the first three in all fifty states**, but not Washington, D.C., or the territories; a state with a one-chamber legislature has two. For the local three it has the governments rather than their electoral districts: the county, the town or township where those govern themselves, the city or town, and the school district, each named rather than numbered. An address can fall in fewer than six, and the page handles any number of rows.

Four principles hold throughout:

- **Nonpartisan.** The site is not affiliated with any election authority, and says so on both pages.
- **No accounts, no stored personal data.** The address is used for the lookup and not kept. **Privacy** below lists what outside services see.
- **Nothing remembered but the theme**, and only in the visitor's own browser.
- **Small and direct over general.** This is a one-person project for a small audience. Code is written for the site it is, and size, in code and in what the browser downloads, is a cost.

## How it was built

| Phase                   | What it delivered                                                                                                                           |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| 1. Design               | Four rounds of page design, ending in the build spec, [canvas4/Main.dc.html](canvas4/Main.dc.html)                                          |
| 2. Home and foundation  | Colour and type tokens, the light/dark theme, the address form with "use my current location", URL-based routing                            |
| 3. Result page          | The district rail, the map and the phone layout, on an invented sample lookup                                                               |
| 4. Districts API        | The server endpoint and the page fetching it, with waiting, no-match and error states                                                       |
| 5. Real data, one state | Geocoding and real legislative districts for one state, first built from precincts, then from the state's enacted plans; the converter tool |
| 6. Street map           | A real, draggable street map under the districts                                                                                            |
| 7. Election information | The next election's date and its candidate and measure counts, from Google, in the rail's footer                                            |
| 8. One national source  | Every boundary from Census TIGER instead of each state's own files, retiring the enacted-plan converter, its projection and populations     |
| 9. Local governments    | The county, city or town and school district covering the address, from TIGER                                                               |
| 10. All fifty states    | The pipeline run state by state, adding named, lettered and half districts, uncovered water, and towns and townships; the move to .NET 10   |
| 11. Cleanup             | A review of every project for bloat: trimmed tests and comments, a smaller wire format and data files, and docs checked against the code    |

## How it works

A React front end and a C# back end ship as one app: the ASP.NET Core server hosts the API and serves the built front end, so the browser talks to one origin for everything but map tiles and fonts. In development they run as two servers joined by a proxy; [CLAUDE.md](../CLAUDE.md) and the [README](../README.md) cover that.

1. On Home the user types an address or shares their location.
2. The site looks it up ("Checking…" on the button), then moves to `/map?address=…` or `/map?lat=…&lon=…`.
3. The result page lists every covering district and draws one. Picking another adds `&district=…`.

The URL decides what is looked up and shown, so any result can be bookmarked or shared. A lookup from Home adds a history entry; a new address or district on the result page replaces it, so Back always returns to Home.

## Front end

### Home

A card sits on the left over a decorative "survey sheet": hand-drawn district outlines on a grid, one hatched with a pin. It shows no real place. A veil fades from the card's edge to nothing by 76% of the page width. On a phone the card drops to the bottom and its controls stack full width.

The card holds the site name and tagline, the address field with **Find My Maps**, **Use my current location**, and a note that it covers the fifty states. The tagline and the card's last line, "Also included: candidates · ballot measures · election dates", promise the election information the rail's footer gives.

### Validating the address

The client checks only that the field is not empty; the field also stops at 200 characters, the server's limit. Whether the address is usable is the server's call, since it has to place it anyway.

The lookup runs before the page changes, and a failure is shown under the field it came from, on Home and behind the result page's **Update**. A refused or unavailable location suggests typing an address.

An address that could not be placed and one placed outside the covered states get **one message**, naming the fifty states and suggesting adding the city or ZIP code. A lookup that gets no answer is kept apart, since it says nothing about the address: the form says to try again in a moment, without flagging the field. The page stops waiting after fifteen seconds.

The result page has the same three states, since a `/map` URL can be opened directly: a card over the empty survey sheet says the lookup is running, found nothing, or is not responding (with **Try again**).

### Result page

On a desktop:

- **Header** (68px): the site name linking Home, the editable address with **Update**, the nonpartisan note, and the theme toggle.
- **Rail** (400px, left): "N districts cover you", broad to local, as one radio group (one Tab stop, arrow keys between rows).
- **Map**: the rest of the width.

Unless the URL names one, the State House district is selected first: almost every address has one, at neighbourhood scale. Where the legislature has one chamber, its district is selected; otherwise the first.

The selection is drawn solid and hatched. Its list neighbours, one each side where possible, are drawn for context, the broader dashed and the more local dotted, so line style rather than colour tells them apart. Context boundaries are often far larger than the selection and run off the view; that is left as it is, and the legend names them.

Rows sort broad to local, then by number. An elementary school district follows the high school district over it, and a city or village follows the town or township it is in.

The rail's footer, "Also for this address", gives the next election's date and its candidate and measure counts from Google. It is left out when there is no election, Google did not answer, or the lookup was by location. When Google names the election without the ballot, the footer gives the date alone.

A tag in the rail's corner names the source, "U.S. Census Bureau TIGER", and the rail and Home credit the Census Bureau with a link to TIGERweb.

### Map

A street map drawn with **MapLibre GL** from **OpenFreeMap**'s OpenStreetMap tiles. It shows streets and their names, place names, highway numbers, water and parks, and nothing else from the tiles, so nothing competes with the districts. Colours come from the spec, every name sits under the district lines, and detail thins as the view widens.

It opens centred on the address and fitted to the selected district, never closer than the spec's frame (11.3 miles across on a desktop). The visitor can drag, scroll or pinch anywhere in the state down to street level; `+` and `−` step by half again. Picking a district refits the view. The map does not rotate or tilt. The scale bar runs from a tenth of a mile to fifty.

Boundaries are sent at full detail, not simplified for the opening view, so a voter zoomed to a street sees the line on the right side of it.

Over the map sit a "Showing" chip naming the selection (and on a desktop the address's county), a legend, a scale bar, the address's label by its pin, and the tile provider's required credit. The drawing is hidden from screen readers, since the overlays and rail say everything that matters. A browser without WebGL is told so and still gets the list.

### Phone (860px and narrower)

The map takes the top third and the list becomes a sheet under it, which its handle expands to most of the screen. Picking a district collapses it. The legend becomes a pill counting the boundaries, the chip shortens, and the footer shrinks to the date and candidate count.

### Theme

The theme follows the device until the user picks one (system → light → dark). A choice is remembered and applied before first paint, and the map follows it.

### Accessibility

- Every field has a label, hidden visually, and submits through a real form.
- Boundaries are told apart by line style, not colour alone.
- On a page change, focus moves to the new heading, and the tab title names the address.
- Controls are large enough for touch.

## Client design & structure

React 19 and TypeScript, bundled by Vite, chosen for familiarity and fit. The runtime dependencies are React and MapLibre GL:

- **No router.** Two routes need only a small wrapper over the History API.
- **MapLibre loads with the first result**, not with Home, as it is most of the app's weight. Its stylesheet is replaced by the few rules the map needs, and the map's look is built in code from the design tokens, so the tile provider is one setting.
- **No UI library.** Plain CSS; Home's board and the waiting sheet are hand-written SVG.
- **Design tokens** from the build spec, [canvas4/Main.dc.html](canvas4/Main.dc.html), as CSS custom properties with a dark set. Fraunces and Inter come from Google Fonts. Earlier design rounds are kept beside the spec.
- **Vitest and Testing Library** for tests, with MapLibre faked since jsdom has no WebGL; **oxlint** for linting.

## The lookup contract

The page renders one JSON shape, written twice and changed together: `DistrictLookup` in [types.ts](../YourElectionMap/yourelectionmap.client/src/districts/types.ts) and the records in [DistrictLookup.cs](../YourElectionMap/YourElectionMap.Server/Districts/DistrictLookup.cs).

| Field     | What it holds                                                                                                                                                      |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| label     | The street line as typed, or "Your location"                                                                                                                       |
| location  | Longitude and latitude                                                                                                                                             |
| districts | Each covering district: id, level, office (or name, for a named row), number ("44", "65B", empty for a named row), detail line, the address's county, and boundary |
| election  | The next election's date and its candidate and measure counts, or nothing. Either count can be missing when the data names the election but not the ballot         |

The page never assumes six districts, and a boundary is a GeoJSON MultiPolygon's coordinates, so islands and holes draw as one.

Most legislative districts are numbered, some with a letter for a half or third; a few legislatures letter or name theirs, and a named row's office is its name over its chamber. Local rows are always named as the Census names them ("… County", "… city"). Each id is the level plus the number or Census code, stable across vintages and safe in a URL.

## Server design & structure

C# on ASP.NET Core 10, chosen for the same reasons as the client. One endpoint, plus a fallback serving the front end; the controller does the work, with no service layer.

Tests host the whole app in memory and read the raw JSON, which pins down the contract. The geocoder and Google are stubbed, so nothing reaches the internet; everything else is real, including every district file.

**`GET /api/districts`** takes an `address` or `lat` and `lon` (the address wins if both are given). The address is not logged. It:

1. Resolves the input to a point. An address is geocoded, and the first match inside a covered state wins.
2. Finds every district containing the point, broad to local.
3. For a typed address, picks the soonest coming election that is nationwide or in the address's state from Google's list, and asks Google's voter lookup about the address for it.
4. Returns the lookup, compressed when the browser accepts it.

It answers **200** with at least one district, **404** when the address cannot be placed or is in no district, **503** when the geocoder did not answer, and **400** for an unusable query (no address or coordinates, coordinates off the Earth, or an address over 200 characters). The client treats 404 and 400 as no-match and anything else as an error, so an outage never reads as a bad address.

Because districts are found from the point, an address with no city, no school district, or on water TIGER leaves out of a level simply has no row for it.

**Data.** Every boundary comes from Census TIGERweb: its legislative layers, and its county, county subdivision, place and school district layers. Each state's layers are downloaded by hand and converted by `YourElectionMap.Tools` into one git-ignored file per state, which the server loads at startup. The tool stops if a legislative layer does not hold exactly the districts expected for that state, and keeps towns and townships only where TIGER marks them as governments. The [README](../README.md) and [DATA-SOURCE.md](../YourElectionMap/YourElectionMap.Tools/DATA-SOURCE.md) cover the downloads and exceptions.

Files hold longitude and latitude, thinned to fifteen feet and written to five decimal places. The design decisions behind that:

- **The boundaries are the districts themselves**, not assembled from precincts.
- **Converted once, not per request**, since boundaries change only with redistricting.
- **Lookup uses the boundaries the page draws**, at the same detail, by a point-in-polygon test in longitude and latitude. Nothing is projected.
- **Each district is thinned on its own**, so neighbours overlap or miss by a few feet. Of two districts of one kind the point is deeper in wins, and an empty statewide level takes the nearest district within fifty feet. The geocoder places an address about twenty feet off the street's centreline, clear of that error where a line follows a street.

**The US Census geocoder** (free, keyless) is one of the server's two outside calls. No match is a 404; a geocoder that is down, takes over eight seconds or answers with something other than JSON is a 503.

**Google's Civic Information API** is the other, in two requests: the election list, which sends nothing about the visitor and is cached for an hour, then the voter lookup for the chosen election by ID (without an ID Google answers "Election unknown"). The key comes from configuration (`GoogleCivic:ApiKey`) and goes in a header, never the URL. These calls can never fail a lookup: no key, no election, a refusal, four seconds passing or an unreadable answer all mean no footer. An election is shown until its day ends in every state, at midnight Pacific time.

**Hosting.** One Linux container from the project's Dockerfile; the host is still to be chosen, on cost. The image needs the district files generated into the build context first, or the server will not start.

## Outside data

Outside services are weighed on cost, keys and what they see of a visit (checked 2026-10-07):

- **Street map tiles: OpenFreeMap.** Free, no key, no cookies, no usage limit, credit required. No service guarantee, so Stadia Maps' free plan is the fallback.
- **Elections: Google Civic Information API.** Free with a key, 25,000 requests a day; each lookup uses two. Data comes from election officials through the Voting Information Project, and also includes candidates and voting locations. Google's representatives lookup shut down in April 2025, so officeholders would come from elsewhere, such as Open States.
- **Boundaries: Census TIGERweb.** Free, keyless, public domain, January 2026 vintage, with no population. Downloaded once per state, and credited on the site.
- **Local electoral districts: no source.** Counties and cities publish their own, so they would come one by one or from a paid service such as Cicero or BallotReady.

## Privacy

Nothing about a visit is stored or logged, and the lookup is sent `no-store` so nothing in between keeps the address it echoes back. What leaves the site:

- **The Census geocoder** gets the address, from the server.
- **OpenFreeMap** gets tile requests from the visitor's browser, so it sees their IP address and roughly where they are looking, but not the address.
- **Google's voter lookup** gets the address as the geocoder matched it, from the server, not the visitor's IP. A lookup by location sends Google nothing.
- **Google Fonts** sees the visitor's IP address.
