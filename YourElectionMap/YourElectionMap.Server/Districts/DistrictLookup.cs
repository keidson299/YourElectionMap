namespace YourElectionMap.Server.Districts
{
    // What GET /api/districts returns. The client's twin is DistrictLookup in
    // yourelectionmap.client/src/districts/types.ts; change the two together. Names go
    // out camel-cased, and DateOnly as "2026-11-03".

    /// <param name="Location">[longitude, latitude].</param>
    /// <param name="Districts">Broad to local, and never empty: none found is a 404.</param>
    /// <param name="Election">Null for no election, no answer, or a lookup by location.</param>
    public sealed record DistrictLookup(
        string Label,
        double[] Location,
        IReadOnlyList<District> Districts,
        ElectionSummary? Election);

    /// <param name="Id">URL-safe and stable: "state-lower-44", or "county-37183" for a named district.</param>
    /// <param name="Office">"State House", or a named district's name: "Wake County".</param>
    /// <param name="Number">"44" or "65B"; null for a district known by name.</param>
    /// <param name="Area">The address's county, for the map chip.</param>
    /// <param name="Boundary">A GeoJSON MultiPolygon's coordinates.</param>
    public sealed record District(
        string Id,
        string Level,
        string Office,
        string? Number,
        string Detail,
        string? Area,
        double[][][][] Boundary);

    /// <param name="Candidates">Null when Google's answer holds no contests: unknown, not zero.</param>
    public sealed record ElectionSummary(DateOnly Date, int? Candidates, int? Measures);
}
