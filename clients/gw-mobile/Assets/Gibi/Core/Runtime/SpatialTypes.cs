// GW-ARCH-003 Section 2 & AR-01, AR-04, SYS-01 — Core spatial, command and measurement types.
// Provider-neutral value records, ports and contracts with no SDK-specific dependencies.
using System;
using UnityEngine;

namespace Gibi.Core
{
    /// <summary>
    /// AR-01: Replaces numeric placeholders with explicit Known or Unknown states.
    /// Fabricated numeric minimums or NaNs cannot pass eligibility gates.
    /// </summary>
    public readonly struct SpatialMeasurement<T> : IEquatable<SpatialMeasurement<T>>
    {
        public readonly bool IsKnown;
        public readonly T Value;
        public readonly string Source;
        public readonly float AgeS;
        public readonly string UnknownReason;

        private SpatialMeasurement(bool isKnown, T value, string source, float ageS, string unknownReason)
        {
            IsKnown = isKnown;
            Value = value;
            Source = source ?? string.Empty;
            AgeS = ageS;
            UnknownReason = unknownReason ?? string.Empty;
        }

        public static SpatialMeasurement<T> Known(T value, string source = "provider", float ageS = 0f)
            => new SpatialMeasurement<T>(true, value, source, ageS, string.Empty);

        public static SpatialMeasurement<T> Unknown(string reason)
            => new SpatialMeasurement<T>(false, default, string.Empty, float.PositiveInfinity, reason ?? "MEASUREMENT_UNAVAILABLE");

        public bool Equals(SpatialMeasurement<T> other)
            => IsKnown == other.IsKnown &&
               Equals(Value, other.Value) &&
               string.Equals(Source, other.Source, StringComparison.Ordinal) &&
               Mathf.Approximately(AgeS, other.AgeS) &&
               string.Equals(UnknownReason, other.UnknownReason, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SpatialMeasurement<T> other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = IsKnown.GetHashCode();
                hash = (hash * 397) ^ (Value != null ? Value.GetHashCode() : 0);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(Source);
                hash = (hash * 397) ^ AgeS.GetHashCode();
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(UnknownReason);
                return hash;
            }
        }

        public override string ToString()
            => IsKnown ? $"Known({Value} from {Source}, age={AgeS:F2}s)" : $"Unknown({UnknownReason})";
    }

    public enum CancelReason
    {
        None,
        SafetyStop,
        PlayerInterrupt,
        TrackingLoss,
        SessionEnd,
        PetSwitch,
        Timeout,
        ObstacleBlocked,
        UserMovedAway,
    }

    public enum PlayerCommand
    {
        None,
        Fetch,
        Come,
        Sit,
        Home,
        Pet,
        Pause,
    }

    public enum CommandStatus
    {
        Accepted,
        Busy,
        InvalidTarget,
        NotTracked,
        UnsupportedCapability,
        StaleRevision,
        Cancelled,
    }

    public readonly struct CommandResult
    {
        public readonly CommandStatus Status;
        public readonly ActionToken Token;
        public readonly string FailureCode;

        public bool Succeeded => Status == CommandStatus.Accepted;

        public CommandResult(CommandStatus status, ActionToken token, string failureCode = null)
        {
            Status = status;
            Token = token;
            FailureCode = failureCode;
        }

        public static CommandResult Accept(ActionToken token)
            => new CommandResult(CommandStatus.Accepted, token, null);

        public static CommandResult Reject(CommandStatus status, string failureCode)
            => new CommandResult(status, ActionToken.None, failureCode);
    }

    public readonly struct AgentEnvelope
    {
        public readonly float RadiusM;
        public readonly float HeightM;
        public readonly float StrideM;

        public AgentEnvelope(float radiusM, float heightM, float strideM)
        {
            RadiusM = Mathf.Max(0.05f, radiusM);
            HeightM = Mathf.Max(0.10f, heightM);
            StrideM = Mathf.Max(0.05f, strideM);
        }

        public static readonly AgentEnvelope DefaultPet = new AgentEnvelope(0.20f, 0.50f, 0.40f);
    }

    public enum PathStatus
    {
        Invalid,
        Planned,
        Failed,
        Blocked,
    }

    public readonly struct PathResult
    {
        public readonly PathStatus Status;
        public readonly int GeometryRevision;
        public readonly Vector3[] Waypoints;
        public readonly float LengthM;

        public bool Succeeded => Status == PathStatus.Planned && Waypoints != null && Waypoints.Length > 0;

        public PathResult(PathStatus status, int geometryRevision, Vector3[] waypoints, float lengthM)
        {
            Status = status;
            GeometryRevision = geometryRevision;
            Waypoints = waypoints ?? Array.Empty<Vector3>();
            LengthM = lengthM;
        }

        public static PathResult Fail(PathStatus status, int geometryRevision)
            => new PathResult(status, geometryRevision, Array.Empty<Vector3>(), 0f);

        public static PathResult Success(int geometryRevision, Vector3[] waypoints, float lengthM)
            => new PathResult(PathStatus.Planned, geometryRevision, waypoints, lengthM);
    }

    public interface INavigationQuery
    {
        PathResult TryPlan(Vector3 start, Vector3 goal, AgentEnvelope agent, int geometryRevision);
    }
}
