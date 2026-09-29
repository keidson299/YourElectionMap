namespace YourElectionMap.Server.Districts
{
    // What GET /api/districts returns. The client's twin of these types is
    // DistrictLookup in yourelectionmap.client/src/districts/types.ts; change the
    // two together. Names go out camel-cased by the web JSON defaults, and
    // DateOnly as "2026-11-03", which is what the client reads.

    /// <param name="Label">The address as the page names it: "1420 Maple Street".</param>
    /// <param name="Location">The address as a point on the map: [x, y] in map units.</param>
    /// <param name="Districts">
    /// Every district covering the address, broad to local. Never empty: a lookup
    /// that finds none is a 404 instead.
    /// </param>
    /// <param name="Election">The next election's summary, or null when there is none.</param>
    /// <param name="Sample">True while the districts are invented rather than looked up.</param>
    public sealed record DistrictLookup(
        string Label,
        double[] Location,
        IReadOnlyList<District> Districts,
        ElectionSummary? Election,
        bool Sample);

    /// <param name="Id">URL-safe and stable, because the page URL carries it: "state-lower-44".</param>
    /// <param name="Level">One of the client's DISTRICT_LEVELS: "state-lower".</param>
    /// <param name="Office">"State House".</param>
    /// <param name="Unit">"District", "Ward" or "Zone".</param>
    /// <param name="Number">"44".</param>
    /// <param name="Detail">The rail's second line: "Federal", "Greenlake County".</param>
    /// <param name="Area">The place named on the map chip, or null.</param>
    /// <param name="Population">Residents, or null where that is not known.</param>
    /// <param name="Boundary">
    /// Rings of [x, y] points in map units, rather than one polygon, so districts
    /// with islands or holes fit unchanged.
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
        double[][][] Boundary);

    public sealed record ElectionSummary(DateOnly Date, int Candidates, int Measures);
}
