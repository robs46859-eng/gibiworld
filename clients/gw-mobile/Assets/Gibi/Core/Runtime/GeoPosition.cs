// GW-ARCH-001 section 5.1 — Coordinate frames.
// NORMATIVE: Authoritative geographic values SHALL remain float64 on server and in
// transport. They SHALL NOT be stored in Unity Vector3.
// This type exists so that a geographic coordinate is structurally incapable of being
// assigned into a float-precision Unity vector.
using System;

namespace Gibi.Core
{
    /// <summary>WGS84 position. Authority: world service. Use: discovery, distance, audit.</summary>
    public readonly struct GeoPosition : IEquatable<GeoPosition>
    {
        public readonly double LatitudeDeg;
        public readonly double LongitudeDeg;
        public readonly double AltitudeM;

        public GeoPosition(double latitudeDeg, double longitudeDeg, double altitudeM = 0.0)
        {
            if (double.IsNaN(latitudeDeg) || latitudeDeg < -90.0 || latitudeDeg > 90.0)
                throw new ArgumentOutOfRangeException(nameof(latitudeDeg));
            if (double.IsNaN(longitudeDeg) || longitudeDeg < -180.0 || longitudeDeg > 180.0)
                throw new ArgumentOutOfRangeException(nameof(longitudeDeg));
            LatitudeDeg = latitudeDeg;
            LongitudeDeg = longitudeDeg;
            AltitudeM = altitudeM;
        }

        /// <summary>Haversine great-circle distance in metres, computed entirely in float64.</summary>
        public double DistanceMetres(in GeoPosition other)
        {
            const double R = 6371008.8; // IUGG mean earth radius
            double dLat = (other.LatitudeDeg - LatitudeDeg) * Math.PI / 180.0;
            double dLon = (other.LongitudeDeg - LongitudeDeg) * Math.PI / 180.0;
            double a1 = Math.Sin(dLat * 0.5);
            double a2 = Math.Sin(dLon * 0.5);
            double lat1 = LatitudeDeg * Math.PI / 180.0;
            double lat2 = other.LatitudeDeg * Math.PI / 180.0;
            double a = a1 * a1 + Math.Cos(lat1) * Math.Cos(lat2) * a2 * a2;
            return 2.0 * R * Math.Asin(Math.Min(1.0, Math.Sqrt(a)));
        }

        public bool Equals(GeoPosition o) =>
            LatitudeDeg.Equals(o.LatitudeDeg) && LongitudeDeg.Equals(o.LongitudeDeg) && AltitudeM.Equals(o.AltitudeM);
        public override bool Equals(object o) => o is GeoPosition g && Equals(g);
        public override int GetHashCode() => HashCode.Combine(LatitudeDeg, LongitudeDeg, AltitudeM);
    }
}
