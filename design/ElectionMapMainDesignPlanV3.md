# Election Map Website — Design Plan V3

_As of 2026-09-28. This replaces [V2](ElectionMapMainDesignPlanV2.md) and, before it, [V1](ElectionMapMainDesignPlanV1.md); both stay as the record of what was intended when they were written. Slices 1 to 3 are built: the result page now fetches its lookups from the server, which answers with invented sample data until slice 4._

## Purpose

This website serves as a way for people to see exactly which election maps they are within. This stretches from federal election maps, through state maps, all the way down to municipal boundaries for elections like school board or city council members.

The site models six kinds of district, listed broad to local: U.S. House, State Senate, State House, County Commission, City Council and School Board. An address can fall in fewer than six, and the site never assumes it falls in all of them.

Three principles hold throughout:

- **Nonpartisan.** The site is not affiliated with any election authority, and says so on both pages.
- **No accounts, no stored personal data.** There is no login. The address is used only to look up its districts and is not kept.
- **Nothing remembered but the theme.** The only thing the site remembers is the visitor's light or dark choice, and only in their own browser.

## Status and roadmap

The work is split into numbered slices, and comments in the code refer to them by number.

| Slice                  | What it delivers                                                                                                 | Status  |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------- | ------- |
| 1. Home and foundation | Colour and type tokens, the light/dark theme, the address form with "use my current location", URL-based routing | Done    |
| 2. Result page         | The district rail, the map and the phone layout, all on an invented sample lookup                                | Done    |
| 3. Districts API       | A server endpoint returns the lookup and the page fetches it; designed loading, error and no-districts states    | Done    |
| 4. Real lookups        | Real districts for a real address, replacing the sample                                                          | Next    |
| 5. Real geometry       | District boundaries from Census TIGERweb, projected into the map in place of the hand-drawn stand-in             | Planned |
| Later                  | The election detail view; the data store and the job that keeps it current                                       | Planned |

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

The card holds the site name and tagline, the address field with a **Find My Maps** button, and a **Use my current location** button that asks the browser for its position.

### Validating the address

The client checks only that the field is not empty, and says so under the field if it is. Whether the text is a real US address is for the server's lookup to decide, since it has to place the address anyway.

That lookup runs before the page changes: the submit button shows a spinner and reads "Checking…", and the field stays readable. An address the server cannot place is said under the field the visitor typed it into, rather than on a result page with nothing to show — the same on Home and behind the result page's **Update**. If the browser cannot or will not share its location, the form says so and suggests typing an address instead.

The result page has all three states of its own as well, because a `/map` URL can be opened, shared or reloaded without passing through a form. A card over the empty survey sheet says that the lookup is running, that the address could not be matched, or that the lookup is not responding, the last with a **Try again** button.

### Result page

On a desktop the page has three parts:

- **Header** (68px tall): the site name, which links back to Home; the address, still editable, with an **Update** button; the nonpartisan note; and the theme toggle.
- **Rail** (400px wide, on the left): headed "N districts cover you", it lists every district covering the address, broad to local. The rows work as one radio group: one Tab stop, and arrow keys to move between rows.
- **Map**: the rest of the width.

Unless the URL names a district, the State House district is selected first, because it is drawn at neighbourhood scale and the Census publishes it reliably. If there is no State House district, the first district is selected.

The selected row is the boundary drawn solid and hatched on the map. Its neighbours in the list, one on each side where there are two, are drawn for context: the broader one dashed, the more local one dotted. Line style, not colour alone, tells the three apart, so the map still reads in greyscale print and for colour-blind visitors.

At the foot of the rail, under "Also for this address", sit the next election's date, the number of candidates on the ballot, and the number of ballot measures. This footer is left out when the address has no election information. While the lookup is invented, the rail carries a "Sample data" tag.

### Map

The map is hand-written SVG, not a mapping library. It is always centred on the address, and opens fitted to the selected district so that all of its edges are in view. From there the zoom buttons step in twice or out once; there is no panning or dragging. Line weights and the address pin keep their on-screen size at every zoom.

Over the map sit:

- a "Showing" chip naming the selected district, with its area and population when they are known
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

V1 planned for full information about the elections at an address: candidate names, political affiliation, candidate websites, propositions, the dates of the previous and upcoming elections, and published results once an election has passed. That is still the plan, as a detail view reached from the rail's footer. Today the footer shows only the summary: the next election's date and the candidate and measure counts. The detail view has not been designed yet.

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
- **No UI or map library.** Styling is plain CSS, and the map is hand-written SVG. Slice 5 projects real boundaries into the same drawing.
- **Design tokens.** Every size, colour and line weight comes from the build spec, [canvas4/Main.dc.html](canvas4/Main.dc.html). Colours are CSS custom properties with a matching dark set. The type is Fraunces for display and Inter for the interface, loaded from Google Fonts. The folders `canvas` to `canvas3` are the design rounds that led to the spec, and [canvas3/Notes.dc.html](canvas3/Notes.dc.html) records the decisions made along the way.
- **Tests and linting.** Vitest with Testing Library for tests, and oxlint for linting.

## The lookup contract

The result page renders one shape, whatever produced it. The server sends it as JSON, filled with an invented sample until slice 4. It is written twice, and the two halves are changed together: `DistrictLookup` in [types.ts](../YourElectionMap/yourelectionmap.client/src/districts/types.ts) is what the page reads, and the records in [DistrictLookup.cs](../YourElectionMap/YourElectionMap.Server/Districts/DistrictLookup.cs) are what the server writes. In plain terms, a lookup holds:

| Field     | What it holds                                                                                                                                                                                                                    |
| --------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| label     | The address as the page names it, such as "1420 Maple Street"                                                                                                                                                                    |
| location  | The address as a point on the map                                                                                                                                                                                                |
| districts | Each district covering the address: a stable id; its level; its office ("State House"); its unit and number ("District 44", "Ward 2"); a short detail line; the area it covers and its population, where known; and its boundary |
| election  | The next election's date and its candidate and measure counts, or nothing when there is no election information                                                                                                                  |
| sample    | Whether the districts are invented rather than looked up                                                                                                                                                                         |

The page relies on two rules. It never assumes six districts, and handles any number, including none. And a boundary is a list of rings rather than a single polygon, so real districts with islands or holes fit without changes.

## Server design & structure

The back end is built with C# on ASP.NET Core 9. As with the client, both have great familiarity and a wealth of resources to help address any issues that come up.

**Today** the server has one endpoint of its own, the districts lookup below, alongside OpenAPI in development and a fallback that serves the front end for any other path.

Its tests host the whole app in memory and call it over HTTP, reading the raw JSON, so routing and the field names and formats on the wire are covered too — that JSON is what the client reads, so it is where the contract is pinned down. A controller can also be instantiated directly where only its own logic is in question.

**The districts endpoint** is `GET /api/districts`, under the prefix the development proxy already forwards. It takes either an `address` (free text, as typed) or `lat` and `lon`: the same inputs the result page's URL carries, and the address wins if both are given. The address travels in the query string, as it already does in the page URL, and is not logged. For each request it:

1. Resolves the input to a point. An address is geocoded; coordinates are used as given.
2. If the address cannot be placed, or the point is outside the US, returns a no-match response, which the page shows as its designed no-districts state.
3. Finds every district whose boundary contains the point.
4. Attaches the election summary for the address.
5. Returns the lookup. If any step fails, it returns an HTTP error, which the page shows as its error state.

A no-match is a 404, which the client tells apart from any other error status, so that a server it cannot reach never reads as an address that does not exist. Until slice 4 those five steps are stood in for by `DistrictLookupService`, which hands back the same invented sample for any address with a house number and for any point inside the country, and no match otherwise.

Because districts are found from the point itself, an address in an unincorporated area simply has no city district, which the page already handles.

**Data.** District boundaries are planned to come from the Census Bureau's TIGERweb service (slice 5). Where the rest of the data comes from, and where it is stored, is still open (see below), with V1's plan as the starting point:

- A job runs on a fixed schedule, likely as a serverless function, and queries the relevant government offices for the most up-to-date election information. Maps are updated once a year, or manually for an out-of-season map change. Candidate information is updated daily or weekly. A serverless function is attractive because it is the easiest to schedule and cheaper to operate than a standalone server.
- The data is kept in a cloud database alongside that job. There are two options: a key-value store for the maps and a relational database for the election information, or a single key-value store for both. Two stores add complexity and are harder to maintain, but they give the text-based election information more structure and could save cost and space.

**Hosting.** The server and the built front end ship together as one Linux container; the project already has a Dockerfile. The host is still to be chosen, on cost.

**Privacy.** No store holds any user data; the only data stored is publicly available election information. The address a user types is used for the lookup and not kept. This keeps data security risks to a minimum, and means storage needs stay the same however many people use the site.
