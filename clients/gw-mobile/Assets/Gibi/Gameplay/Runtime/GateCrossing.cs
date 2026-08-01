// GW-ARCH-001 section 9.2 — "Gate crossing SHALL use swept-volume intersection
// between the pet root capsule and ordered gate plane, NOT frame-point containment."
// GW-GAME-003: ranked gate crossing uses swept volume and exact order.
//
// Frame-point containment fails at speed: at 3.8 m/s and 60 fps the pet root moves
// 63 mm per frame, so a thin gate plane can be stepped over entirely between samples.
// Sweeping the segment between successive fixed steps closes that hole.
using System;

namespace Gibi.Gameplay
{
    public readonly struct Vec3
    {
        public readonly double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public double Dot(Vec3 o) => X * o.X + Y * o.Y + Z * o.Z;
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    }

    /// <summary>A gate is a bounded plane: centre, unit normal, half-width, height.</summary>
    public readonly struct GatePlane
    {
        public readonly int Order;
        public readonly Vec3 Centre;
        public readonly Vec3 Normal;     // unit
        public readonly Vec3 RightAxis;  // unit, in-plane horizontal
        public readonly double HalfWidthM;
        public readonly double HeightM;

        public GatePlane(int order, Vec3 centre, Vec3 normal, Vec3 rightAxis,
                         double halfWidthM, double heightM)
        { Order = order; Centre = centre; Normal = normal; RightAxis = rightAxis;
          HalfWidthM = halfWidthM; HeightM = heightM; }
    }

    public sealed class GateCrossingDetector
    {
        public const double CorridorMinWidthM  = 0.8;
        public const double CorridorMinHeightM = 1.5;

        private int _nextExpectedOrder;
        public int NextExpectedOrder => _nextExpectedOrder;
        public bool OutOfOrderDetected { get; private set; }

        /// <summary>
        /// Test the swept segment from previous to current root position against a gate,
        /// inflated by the pet capsule radius. Returns true on a valid forward crossing.
        /// </summary>
        public static bool SweptCrosses(in GatePlane gate, Vec3 prev, Vec3 curr, double capsuleRadiusM)
        {
            Vec3 toPrev = prev - gate.Centre;
            Vec3 toCurr = curr - gate.Centre;

            double dPrev = toPrev.Dot(gate.Normal);
            double dCurr = toCurr.Dot(gate.Normal);

            // Inflate the plane by the capsule radius so a grazing body still counts.
            if (dPrev > capsuleRadiusM && dCurr > capsuleRadiusM) return false;
            if (dPrev < -capsuleRadiusM && dCurr < -capsuleRadiusM) return false;

            // Require an actual sign change (a real crossing, not resting on the plane).
            if (Math.Sign(dPrev) == Math.Sign(dCurr)) return false;

            // Parametric point of intersection along the swept segment.
            double denom = dPrev - dCurr;
            if (Math.Abs(denom) < 1e-9) return false;
            double t = dPrev / denom;
            if (t < 0.0 || t > 1.0) return false;

            Vec3 hit = prev + (curr - prev) * t;
            Vec3 local = hit - gate.Centre;

            // Must pass through the gate aperture, not around the post or over the bar.
            double lateral = Math.Abs(local.Dot(gate.RightAxis));
            if (lateral > gate.HalfWidthM + capsuleRadiusM) return false;

            double vertical = local.Y;
            if (vertical < -capsuleRadiusM || vertical > gate.HeightM + capsuleRadiusM) return false;

            return true;
        }

        /// <summary>
        /// Feed one fixed-step movement. Returns the gate order crossed, or -1.
        /// Any crossing that is not the next expected order flags the run out-of-order,
        /// which the finish endpoint converts to UNRANKED.
        /// </summary>
        public int Observe(GatePlane[] orderedGates, Vec3 prev, Vec3 curr, double capsuleRadiusM)
        {
            for (int i = 0; i < orderedGates.Length; i++)
            {
                if (!SweptCrosses(orderedGates[i], prev, curr, capsuleRadiusM)) continue;

                if (orderedGates[i].Order != _nextExpectedOrder)
                {
                    OutOfOrderDetected = true;
                    return orderedGates[i].Order;
                }
                _nextExpectedOrder++;
                return orderedGates[i].Order;
            }
            return -1;
        }

        public void Reset() { _nextExpectedOrder = 0; OutOfOrderDetected = false; }
    }
}
