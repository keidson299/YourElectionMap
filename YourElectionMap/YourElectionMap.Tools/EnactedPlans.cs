using System.Globalization;
using YourElectionMap.Server.Districts;

namespace YourElectionMap.Tools
{
    /// <summary>
    /// North Carolina, from the General Assembly's enacted district plans, which carry
    /// each district's 2020 population. The plans are in NC State Plane: each ring is
    /// thinned there, in the site's map units, and only then turned into degrees.
    /// </summary>
    public static class EnactedPlans
    {
        /// <summary>US survey feet in a metre, exactly, by its definition.</summary>
        private const double FeetPerMetre = 3937.0 / 1200.0;

        private const string Source = "NCGA enacted maps";

        /// <summary>
        /// The three plans, where each sits under the folder given, and how many
        /// districts it must have.
        /// </summary>
        private static readonly Layer[] Layers =
        [
            new(
                Path.Combine("nc_cong_adopted_2025", "NCGA_CCM-2 - Shapefile", "CCM-2.shp"),
                "congressional", "U.S. House", "Federal", 14),
            new(
                Path.Combine("nc_sldu_adopted_2023", "SL 2023-146 Senate - Shapefile", "SL 2023-146.shp"),
                "state-upper", "State Senate", "State", 50),
            new(
                Path.Combine("nc_sldl_adopted_2023", "SL 2023-149 House - Shapefile", "SL 2023-149.shp"),
                "state-lower", "State House", "State", 120),
        ];

        public static Built Build(string source)
        {
            var districts = new List<BuiltDistrict>();

            foreach (var layer in Layers)
            {
                districts.AddRange(Build(layer, source));
            }

            return new Built("NC", Source, districts);
        }

        private static List<BuiltDistrict> Build(Layer layer, string source)
        {
            var path = Path.Combine(source, layer.Path);

            if (!File.Exists(path))
            {
                throw new InvalidDataException($"No such shapefile: {path}.");
            }

            CheckProjection(path);

            using var shapes = new ShapefileReader(path);
            var table = new DbaseReader(path);

            if (shapes.Count != table.Count)
            {
                throw new InvalidDataException(
                    $"{layer.Path}: {shapes.Count} shapes against {table.Count} records.");
            }

            var built = new List<BuiltDistrict>(shapes.Count);

            for (var feature = 0; feature < shapes.Count; feature++)
            {
                built.Add(Build(layer, table, feature, shapes.ReadRings(feature)));
            }

            Program.CheckAndSort(built, layer.Path, layer.Level, layer.Count);

            return built;
        }

        private static BuiltDistrict Build(Layer layer, DbaseReader table, int feature, List<double[]> parts)
        {
            var number = table.Text(feature, "DISTRICT");

            if (!int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new InvalidDataException(
                    $"{layer.Path}: feature {feature} has district \"{number}\".");
            }

            var population = table.Text(feature, "PL20AA_TOT");

            if (!int.TryParse(population, NumberStyles.None, CultureInfo.InvariantCulture, out var residents))
            {
                throw new InvalidDataException(
                    $"{layer.Path}: district {parsed} has population \"{population}\".");
            }

            var rings = new List<double[]>(parts.Count);

            foreach (var part in parts)
            {
                var thinned = Geometry.Thin(
                    ToMapUnits(part),
                    Program.StoredToleranceFeet / StatePlane.FeetPerMapUnit);

                rings.Add(ToDegrees(thinned));
            }

            if (rings.Count == 0)
            {
                throw new InvalidDataException($"{layer.Path}: district {parsed} has no shape.");
            }

            var plain = parsed.ToString(CultureInfo.InvariantCulture);

            return new BuiltDistrict(
                $"{layer.Level}-{plain}",
                layer.Level,
                layer.Office,
                plain,
                layer.Detail,
                residents,
                parts.Sum(part => part.Length / 2),
                rings);
        }

        /// <summary>
        /// The plans are in NC State Plane in metres. The precinct files the site used
        /// to be built from were the same projection in feet, so a plan in any other
        /// unit would land a few times too small and still look plausible: stop instead.
        /// </summary>
        private static void CheckProjection(string path)
        {
            var prj = Path.ChangeExtension(path, ".prj");

            if (!File.Exists(prj))
            {
                throw new InvalidDataException($"{path} has no .prj beside it.");
            }

            var text = File.ReadAllText(prj);

            // The projection's own unit is the last UNIT in the text; the earlier one
            // belongs to the geographic system underneath.
            var unit = text[text.LastIndexOf("UNIT[", StringComparison.Ordinal)..];

            if (!text.Contains("North_Carolina_FIPS_3200", StringComparison.Ordinal)
                || !unit.StartsWith("UNIT[\"Meter\"", StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"{prj} is not NC State Plane (FIPS 3200) in metres.");
            }
        }

        /// <summary>
        /// Metres to the map's own units, by way of the US survey feet those units are
        /// counted in, flipping north so that y counts downward as StatePlane expects.
        /// </summary>
        private static double[] ToMapUnits(double[] ring)
        {
            var converted = new double[ring.Length];

            for (var i = 0; i < ring.Length; i += 2)
            {
                converted[i] = ring[i] * FeetPerMetre / StatePlane.FeetPerMapUnit;
                converted[i + 1] = -ring[i + 1] * FeetPerMetre / StatePlane.FeetPerMapUnit;
            }

            return converted;
        }

        /// <summary>A ring in map units as a flat ring of [longitude, latitude].</summary>
        private static double[] ToDegrees(double[] ring)
        {
            var degrees = new double[ring.Length];

            for (var i = 0; i < ring.Length; i += 2)
            {
                var (latitude, longitude) = StatePlane.ToLatLon(ring[i], ring[i + 1]);

                degrees[i] = longitude;
                degrees[i + 1] = latitude;
            }

            return degrees;
        }

        private sealed record Layer(
            string Path,
            string Level,
            string Office,
            string Detail,
            int Count);
    }
}
