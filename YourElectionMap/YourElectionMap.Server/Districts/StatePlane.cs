namespace YourElectionMap.Server.Districts
{
    /// <summary>
    /// The one projection this site uses: NAD83 / North Carolina State Plane
    /// (FIPS 3200, US survey feet), a Lambert conformal conic. Every parameter below
    /// is read straight off the <c>.prj</c> file that came with the precinct
    /// shapefiles, so the points this produces land in the same space as their
    /// boundaries.
    /// <para>
    /// Latitudes and longitudes arrive as WGS84, from the browser or the geocoder, and
    /// are used as if they were NAD83. The two differ by about a metre in this part of
    /// the world, against a map whose boundaries are thinned to tens of feet.
    /// </para>
    /// <para>
    /// The map the client draws works in <em>map units</em> rather than feet: one unit
    /// is <see cref="FeetPerMapUnit"/> feet, chosen so that 55 units make a mile and
    /// every size in the design spec keeps its meaning.
    /// </para>
    /// </summary>
    public static class StatePlane
    {
        /// <summary>
        /// 96 feet to the map unit, so a mile is 55 units — the scale the drawing was
        /// designed at, and still the client's <c>SURVEY_UNITS_PER_MILE</c>.
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

        // The cone the projection wraps the state in. Constant for these parallels, so
        // it is worked out once rather than per request.
        private static readonly double ConeConstant = ConeConstantOf();
        private static readonly double ScaleFactor = ScaleFactorOf();
        private static readonly double OriginRadius =
            SemiMajorMetres * ScaleFactor * Math.Pow(TangentOf(LatitudeOfOrigin), ConeConstant);

        /// <summary>
        /// A latitude and longitude as the map's own point: x east, y <em>down</em>, in
        /// map units. South is positive y because that is the direction SVG counts in,
        /// so north ends up where a reader expects it without any further flipping.
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
        /// Back to latitude and longitude. Nothing in the app needs this — it is here
        /// so a test can project a point and get it back, which is the only check on
        /// the maths that does not need an outside authority.
        /// </summary>
        public static (double Latitude, double Longitude) ToLatitudeLongitude(
            double easting,
            double northing)
        {
            var east = (easting - FalseEastingFeet) * MetresPerFoot;
            var north = (northing - FalseNorthingFeet) * MetresPerFoot;

            var fromOrigin = OriginRadius - north;
            var radius = Math.Sign(ConeConstant)
                * Math.Sqrt(east * east + fromOrigin * fromOrigin);
            var angle = Math.Atan2(east, fromOrigin);

            var tangent = Math.Pow(
                radius / (SemiMajorMetres * ScaleFactor),
                1 / ConeConstant);

            // Snyder's iteration for the latitude whose tangent this is. It settles in
            // three or four passes; ten is a generous ceiling.
            var latitude = Math.PI / 2 - 2 * Math.Atan(tangent);

            for (var pass = 0; pass < 10; pass++)
            {
                var sin = Eccentricity * Math.Sin(latitude);
                var next = Math.PI / 2 - 2 * Math.Atan(
                    tangent * Math.Pow((1 - sin) / (1 + sin), Eccentricity / 2));

                if (Math.Abs(next - latitude) < 1e-12)
                {
                    latitude = next;
                    break;
                }

                latitude = next;
            }

            return (
                Degrees(latitude),
                Degrees(angle / ConeConstant) + CentralMeridian);
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
