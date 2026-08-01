// GW-ARCH-001 section 5.1 — ANCHOR_LOCAL frame.
// NORMATIVE: Quaternions SHALL be normalized to absolute error <= 1e-4 before
// persistence; zero or NaN quaternions are rejected. (GW-AR-005)
// NORMATIVE: Course objects SHALL be within 75 metres of their site anchor.
using System;

namespace Gibi.Core
{
    public readonly struct AnchorLocalPose
    {
        public const double QuaternionNormEpsilon = 1e-4;
        public const float MaxAnchorRadiusM = 75f;

        public readonly float Px, Py, Pz;
        public readonly float Qx, Qy, Qz, Qw;

        private AnchorLocalPose(float px, float py, float pz, float qx, float qy, float qz, float qw)
        { Px = px; Py = py; Pz = pz; Qx = qx; Qy = qy; Qz = qz; Qw = qw; }

        /// <summary>
        /// Validating factory. This is the ONLY way to construct a persistable pose.
        /// Returns a failed Result rather than throwing so callers handle rejection explicitly.
        /// </summary>
        public static Result<AnchorLocalPose> Create(float px, float py, float pz,
                                                     float qx, float qy, float qz, float qw)
        {
            if (IsNotFinite(px) || IsNotFinite(py) || IsNotFinite(pz))
                return Result<AnchorLocalPose>.Fail("POSE_POSITION_NOT_FINITE");
            if (IsNotFinite(qx) || IsNotFinite(qy) || IsNotFinite(qz) || IsNotFinite(qw))
                return Result<AnchorLocalPose>.Fail("POSE_QUATERNION_NAN");

            double norm = Math.Sqrt((double)qx * qx + (double)qy * qy + (double)qz * qz + (double)qw * qw);
            if (norm <= 0.0)
                return Result<AnchorLocalPose>.Fail("POSE_QUATERNION_ZERO");
            if (Math.Abs(norm - 1.0) > QuaternionNormEpsilon)
                return Result<AnchorLocalPose>.Fail("POSE_QUATERNION_NOT_NORMALIZED");

            double radius = Math.Sqrt((double)px * px + (double)py * py + (double)pz * pz);
            if (radius > MaxAnchorRadiusM)
                return Result<AnchorLocalPose>.Fail("POSE_EXCEEDS_75M_FROM_ANCHOR");

            return Result<AnchorLocalPose>.Ok(new AnchorLocalPose(px, py, pz, qx, qy, qz, qw));
        }

        private static bool IsNotFinite(float v) => float.IsNaN(v) || float.IsInfinity(v);
    }
}
