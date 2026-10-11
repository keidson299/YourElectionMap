namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// NAD83 / North Carolina State Plane (FIPS 3200, US survey feet), a Lambert
    /// conformal conic: the projection North Carolina's enacted plans are drawn in.
    /// Every parameter below is the one the plans' <c>.prj</c> files state; those count
    /// in metres, and the converter turns them into these feet.
    /// <para>
    /// Only YourElectionMap.Tools uses it, to thin the NC plans in their own space and
    /// then turn them into the degrees the district files hold. The server tests and
    /// sends everything in degrees.
    /// </para>
    /// </summary>
    public static class StatePlane
    {
        /// <summary>
        /// 96 feet to the map unit, so a mile is 55 units — the scale the design spec was
        /// drawn at, and the client's <c>MAP_UNITS_PER_MILE</c> in <c>src/map/frame.ts</c>.
        /// The converter thins North Carolina's plans in these units.
        /// </summary>
        public const double FeetPerMapUnit = 96;

        // GRS80, as the .prj's SPHEROID gives it.
        private const double SemiMajorMetres = 6378137.0;
        private const double InverseFlattening = 298.257222101;

        // UNIT["US survey foot", 0.304800609601219]
        private const double MetresPerFoot = 0.304800609601219;

        private const double FalseEastingFeet = 2_000_000.0;
        private const double FalseNorthingFeet = 0.0;
        private const double CentralMeridian = -79.0;
        private const double LatitudeOfOrigin = 33.75;
        private const double StandardParallel1 = 36.1666666666667;
        private const double StandardParallel2 = 34.3333333333333;

        private static readonly double Eccentricity = EccentricityOf(InverseFlattening);

        // The cone, fixed by the standard parallels.
        private static readonly double ConeConstant = ConeConstantOf();
        private static readonly double ScaleFactor = ScaleFactorOf();
        private static readonly double OriginRadius =
            SemiMajorMetres * ScaleFactor * Math.Pow(TangentOf(LatitudeOfOrigin), ConeConstant);

        /// <summary>
        /// A latitude and longitude in map units: x east, y <em>down</em>, the way SVG
        /// counts.
        /// </summary>
        public static (double X, double Y) ToMapUnits(double latitude, double longitude)
        {
            var (easting, northing) = ToStatePlaneFeet(latitude, longitude);

            return (easting / FeetPerMapUnit, -northing / FeetPerMapUnit);
        }

        /// <summary>The same point in the shapefiles' own units: feet, north up.</summary>
        public static (double Easting, double Northing) ToStatePlaneFeet(
            double latitude,
            double longitude)
        {
            var radius = SemiMajorMetres * ScaleFactor
                * Math.Pow(TangentOf(latitude), ConeConstant);
            var angle = ConeConstant * Radians(longitude - CentralMeridian);

            var east = radius * Math.Sin(angle);
            var north = OriginRadius - radius * Math.Cos(angle);

            return (
                FalseEastingFeet + east / MetresPerFoot,
                FalseNorthingFeet + north / MetresPerFoot);
        }

        /// <summary>
        /// The way back: a point in map units as a latitude and longitude, which is how
        /// every vertex of North Carolina's plans reaches the district file.
        /// </summary>
        public static (double Latitude, double Longitude) ToLatLon(double x, double y)
        {
            var east = (x * FeetPerMapUnit - FalseEastingFeet) * MetresPerFoot;
            var north = (-y * FeetPerMapUnit - FalseNorthingFeet) * MetresPerFoot;

            // The cone constant is positive in the northern hemisphere, so the radius is
            // too, and the angle is measured from the line down the central meridian.
            var fromApex = OriginRadius - north;
            var radius = Math.Sqrt(east * east + fromApex * fromApex);
            var angle = Math.Atan2(east, fromApex);

            var t = Math.Pow(radius / (SemiMajorMetres * ScaleFactor), 1 / ConeConstant);

            return (LatitudeOf(t), CentralMeridian + Degrees(angle / ConeConstant));
        }

        /// <summary>
        /// Snyder's t back to a latitude. There is no closed form on the ellipsoid, so it
        /// is iterated from the sphere's answer; it settles within a few rounds.
        /// </summary>
        private static double LatitudeOf(double t)
        {
            var latitude = Math.PI / 2 - 2 * Math.Atan(t);

            for (var round = 0; round < 15; round++)
            {
                var sin = Eccentricity * Math.Sin(latitude);
                var next = Math.PI / 2
                    - 2 * Math.Atan(t * Math.Pow((1 - sin) / (1 + sin), Eccentricity / 2));

                if (Math.Abs(next - latitude) < 1e-12)
                {
                    return Degrees(next);
                }

                latitude = next;
            }

            return Degrees(latitude);
        }

        private static double EccentricityOf(double inverseFlattening)
        {
            var flattening = 1 / inverseFlattening;

            return Math.Sqrt(2 * flattening - flattening * flattening);
        }

        private static double ConeConstantOf()
        {
            var t1 = TangentOf(StandardParallel1);
            var t2 = TangentOf(StandardParallel2);

            return Math.Log(RadiusFactorOf(StandardParallel1) / RadiusFactorOf(StandardParallel2))
                / Math.Log(t1 / t2);
        }

        private static double ScaleFactorOf() =>
            RadiusFactorOf(StandardParallel1)
            / (ConeConstant * Math.Pow(TangentOf(StandardParallel1), ConeConstant));

        /// <summary>Snyder's m: the radius of the parallel, as a fraction of the semi-major axis.</summary>
        private static double RadiusFactorOf(double degrees)
        {
            var latitude = Radians(degrees);
            var sin = Eccentricity * Math.Sin(latitude);

            return Math.Cos(latitude) / Math.Sqrt(1 - sin * sin);
        }

        /// <summary>Snyder's t: how far the latitude is from the pole, conformally.</summary>
        private static double TangentOf(double degrees)
        {
            var latitude = Radians(degrees);
            var sin = Eccentricity * Math.Sin(latitude);

            return Math.Tan(Math.PI / 4 - latitude / 2)
                / Math.Pow((1 - sin) / (1 + sin), Eccentricity / 2);
        }

        private static double Radians(double degrees) => degrees * Math.PI / 180;

        private static double Degrees(double radians) => radians * 180 / Math.PI;
    }
}
