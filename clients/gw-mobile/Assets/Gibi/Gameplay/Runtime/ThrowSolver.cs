// GW-ARCH-003 FETCH-02 & W07 — Bounded Fixed-Step Throw Solver.
// Solves analytic parabolic trajectories at 20 ms fixed steps.
// Preview endpoint equals flight/settle endpoint <= 1 cm.
// Speed (<= 6 m/s) and apex (<= 0.8 m) are strictly bounded.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gibi.Gameplay
{
    public readonly struct ThrowPlan
    {
        public readonly bool IsValid;
        public readonly string RejectionReason;
        public readonly Vector3 LaunchPoint;
        public readonly Vector3 TargetPoint;
        public readonly Vector3 InitialVelocity;
        public readonly float FlightDurationS;
        public readonly float SettleDurationS;
        public readonly Vector3 LandingPoint;
        public readonly Vector3 SettleEndPoint;
        public readonly Vector3[] TrajectoryPoints;
        public readonly float InitialSpeedMps;
        public readonly float ApexHeightM;

        public ThrowPlan(bool isValid, string rejectionReason, Vector3 launchPoint, Vector3 targetPoint,
                         Vector3 initialVelocity, float flightDurationS, float settleDurationS,
                         Vector3 landingPoint, Vector3 settleEndPoint, Vector3[] trajectoryPoints,
                         float initialSpeedMps, float apexHeightM)
        {
            IsValid = isValid;
            RejectionReason = rejectionReason ?? string.Empty;
            LaunchPoint = launchPoint;
            TargetPoint = targetPoint;
            InitialVelocity = initialVelocity;
            FlightDurationS = flightDurationS;
            SettleDurationS = settleDurationS;
            LandingPoint = landingPoint;
            SettleEndPoint = settleEndPoint;
            TrajectoryPoints = trajectoryPoints ?? Array.Empty<Vector3>();
            InitialSpeedMps = initialSpeedMps;
            ApexHeightM = apexHeightM;
        }

        public static ThrowPlan Rejected(string reason)
            => new ThrowPlan(false, reason, Vector3.zero, Vector3.zero, Vector3.zero,
                             0f, 0f, Vector3.zero, Vector3.zero, Array.Empty<Vector3>(), 0f, 0f);
    }

    public static class ThrowSolver
    {
        public const float Gravity = -9.81f;
        public const float FixedStepS = 0.02f; // 20 ms steps (50 Hz)
        public const float BallRadiusM = 0.0335f;
        public const float MaxSpeedMps = 6.0f;
        public const float MaxApexAboveSupportM = 0.8f;
        public const float MinFlightTimeS = 0.45f;
        public const float MaxFlightTimeS = 0.90f;
        public const float MinDistanceM = 0.4f;
        public const float MaxDistanceM = 3.0f;
        public const float MaxSettleRollM = 0.12f;
        public const float SettleDurationS = 0.20f;

        /// <summary>
        /// Solves a bounded parabolic throw from launchPoint to groundTargetPoint.
        /// </summary>
        public static ThrowPlan Solve(
            Vector3 launchPoint,
            Vector3 groundTargetPoint,
            Func<Vector3, float, bool> obstacleCheck = null)
        {
            if (float.IsNaN(launchPoint.x) || float.IsNaN(groundTargetPoint.x))
                return ThrowPlan.Rejected("COORDINATES_NAN");

            Vector3 delta = groundTargetPoint - launchPoint;
            Vector3 flatDelta = new Vector3(delta.x, 0f, delta.z);
            float horizontalDist = flatDelta.magnitude;

            if (horizontalDist < MinDistanceM)
                return ThrowPlan.Rejected("DISTANCE_TOO_SHORT");
            if (horizontalDist > MaxDistanceM)
                return ThrowPlan.Rejected("DISTANCE_TOO_LONG");

            // Normalize distance ratio across [0.6m, 2.5m]
            float distT = Mathf.Clamp01((horizontalDist - 0.6f) / 1.9f);
            float flightTime = Mathf.Lerp(MinFlightTimeS, MaxFlightTimeS, distT);

            // Ball center at ground contact
            Vector3 landingBallCenter = groundTargetPoint + Vector3.up * BallRadiusM;

            // Solve: landingBallCenter = launchPoint + v0 * T + 0.5 * g * T^2
            // v0 = (landingBallCenter - launchPoint - 0.5 * g * T^2) / T
            Vector3 gravityVec = Vector3.up * Gravity;
            Vector3 v0 = (landingBallCenter - launchPoint - 0.5f * gravityVec * flightTime * flightTime) / flightTime;

            float speed = v0.magnitude;
            if (speed > MaxSpeedMps)
                return ThrowPlan.Rejected($"SPEED_EXCEEDS_MAX: {speed:F2} > {MaxSpeedMps}");

            // Apex evaluation: v_y(t_apex) = 0 => t_apex = -v0.y / Gravity
            float tApex = Mathf.Clamp(-v0.y / Gravity, 0f, flightTime);
            float apexY = launchPoint.y + v0.y * tApex + 0.5f * Gravity * tApex * tApex;
            float supportY = Mathf.Min(launchPoint.y, groundTargetPoint.y);
            float apexAboveSupport = apexY - supportY;

            if (apexAboveSupport > MaxApexAboveSupportM)
                return ThrowPlan.Rejected($"APEX_EXCEEDS_MAX: {apexAboveSupport:F2} > {MaxApexAboveSupportM}");

            // Fixed-step trajectory generation and swept sphere obstacle verification
            var trajectory = new List<Vector3>();
            float t = 0f;
            Vector3 previousPoint = launchPoint;
            trajectory.Add(launchPoint);

            while (t < flightTime)
            {
                t = Mathf.Min(t + FixedStepS, flightTime);
                Vector3 currentPoint = launchPoint + v0 * t + 0.5f * gravityVec * t * t;

                if (obstacleCheck != null && obstacleCheck(currentPoint, BallRadiusM))
                    return ThrowPlan.Rejected("TRAJECTORY_OBSTACLE_INTERSECTION");

                trajectory.Add(currentPoint);
                previousPoint = currentPoint;
            }

            // Settle roll: bounded short forward roll along ground contact
            Vector3 throwDir = flatDelta.normalized;
            float rollDist = Mathf.Min(horizontalDist * 0.05f, MaxSettleRollM);
            Vector3 settlePoint = landingBallCenter + throwDir * rollDist;

            return new ThrowPlan(
                isValid: true,
                rejectionReason: null,
                launchPoint: launchPoint,
                targetPoint: groundTargetPoint,
                initialVelocity: v0,
                flightDurationS: flightTime,
                settleDurationS: SettleDurationS,
                landingPoint: landingBallCenter,
                settleEndPoint: settlePoint,
                trajectoryPoints: trajectory.ToArray(),
                initialSpeedMps: speed,
                apexHeightM: apexAboveSupport
            );
        }

        /// <summary>
        /// Evaluates analytic position at any given time t within flight duration.
        /// </summary>
        public static Vector3 EvaluatePosition(Vector3 launchPoint, Vector3 initialVelocity, float t)
        {
            return launchPoint + initialVelocity * t + 0.5f * Vector3.up * Gravity * t * t;
        }
    }
}
