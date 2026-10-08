namespace YourElectionMap.Server.Districts
{
    // What GET /api/districts returns. The client's twin of these types is
    // DistrictLookup in yourelectionmap.client/src/districts/types.ts; change the
    // two together. Names go out camel-cased by the web JSON defaults, and
    // DateOnly as "2026-11-03", which is what the client reads.

    /// <param name="Label">The address as the page names it: "1420 Maple Street".</param>
    /// <param name="Location">The address as [longitude, latitude].</param>
    /// <param name="Districts">
    /// Every district covering the address, broad to local. Never empty: a lookup
    /// that finds none is a 404 instead.
    /// </param>
    /// <param name="Election">
    /// The next election's summary, from Google's voter lookup, or null when there is
    /// none to show: no election, no answer, or a lookup by location.
    /// </param>
    /// <param name="Source">
    /// What the boundaries were drawn from, for the page to name: "NCGA enacted maps".
    /// Null when there is nothing to credit.
    /// </param>
    public sealed record DistrictLookup(
        string Label,
        double[] Location,
        IReadOnlyList<District> Districts,
        ElectionSummary? Election,
        string? Source);

    /// <param name="Id">URL-safe and stable, because the page URL carries it: "state-lower-44".</param>
    /// <param name="Level">One of the client's DISTRICT_LEVELS: "state-lower".</param>
    /// <param name="Office">"State House".</param>
    /// <param name="Unit">"District", "Ward" or "Zone".</param>
    /// <param name="Number">"44".</param>
    /// <param name="Detail">The rail's second line: "Federal", "Greenlake County".</param>
    /// <param name="Area">The place named on the map chip, or null.</param>
    /// <param name="Population">Residents, or null where that is not known.</param>
    /// <param name="Boundary">
    /// The coordinates of a GeoJSON MultiPolygon: polygons, each an outer ring and
    /// then its holes, each ring closed [longitude, latitude] points. The page's map
    /// fills by that structure, so islands and holes come out right.
    /// </param>
    public sealed record District(
        string Id,
        string Level,
        string Office,
        string Unit,
        string Number,
        string Detail,
        string? Area,
        int? Population,
        double[][][][] Boundary);

    /// <param name="Date">Election Day: "2026-11-03".</param>
    /// <param name="Candidates">
    /// Candidates across every contest on the ballot, or null when Google's answer
    /// holds no contests, so the page never says "0 candidates" about a ballot it has
    /// not seen.
    /// </param>
    /// <param name="Measures">Referenda on the ballot, or null on the same terms.</param>
    public sealed record ElectionSummary(DateOnly Date, int? Candidates, int? Measures);
}
