// GW-ARCH-003 PET-03, AI-01 & W17 — Intent Envelope Validator and NullIntentSource.
// AI-01: Validates that any AI intent source matches schema version 2, catalog revision 2,
// allowlisted intents, matching pet and context revisions, target existence, and valid expiration.
// PET-03: NullIntentSource provides a clean zero-overhead local fallback when no model is attached.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gibi.Core;

namespace Gibi.AI
{
    public interface IIntentSource
    {
        Task<Result<AiIntentEnvelope>> RequestIntentAsync(AiIntentContext context, CancellationToken ct);
    }

    public sealed class NullIntentSource : IIntentSource
    {
        public Task<Result<AiIntentEnvelope>> RequestIntentAsync(AiIntentContext context, CancellationToken ct)
        {
            // Null intent source immediately returns fallback; local deterministic policy handles selection
            return Task.FromResult(Result<AiIntentEnvelope>.Fail("NULL_INTENT_SOURCE_INACTIVE"));
        }
    }

    public readonly struct AiIntentContext
    {
        public readonly string RequestId;
        public readonly string PetId;
        public readonly int ContextRevision;
        public readonly int CatalogRevision;
        public readonly string[] AvailableTargetIds;

        public AiIntentContext(string requestId, string petId, int contextRevision,
                               int catalogRevision, string[] availableTargetIds)
        {
            RequestId = requestId;
            PetId = petId;
            ContextRevision = contextRevision;
            CatalogRevision = catalogRevision;
            AvailableTargetIds = availableTargetIds ?? Array.Empty<string>();
        }
    }

    public readonly struct AiIntentEnvelope
    {
        public readonly int SchemaVersion;
        public readonly string RequestId;
        public readonly string PetId;
        public readonly int ContextRevision;
        public readonly int CatalogRevision;
        public readonly string Intent;
        public readonly string TargetId;
        public readonly DateTime ExpiresAt;

        public AiIntentEnvelope(int schemaVersion, string requestId, string petId,
                                int contextRevision, int catalogRevision, string intent,
                                string targetId, DateTime expiresAt)
        {
            SchemaVersion = schemaVersion;
            RequestId = requestId;
            PetId = petId;
            ContextRevision = contextRevision;
            CatalogRevision = catalogRevision;
            Intent = intent;
            TargetId = targetId;
            ExpiresAt = expiresAt;
        }
    }

    public static class IntentEnvelopeValidator
    {
        public const int ExpectedSchemaVersion = 2;
        public const int ExpectedCatalogRevision = 2;

        private static readonly HashSet<string> AllowedIntents = new HashSet<string>(StringComparer.Ordinal)
        {
            "CALM_IDLE","SETTLE","REST","STRETCH","SHAKE_OFF","SCAN_AROUND","YAWN",
            "GREET","SEEK_PROXIMITY","INVITE_PLAY","FOLLOW_GAZE","LEAN_IN","CHECK_IN","PRESENT_ITEM",
            "INSPECT_OBJECT","CURIOUS_SNIFF","NUDGE_OBJECT","RETRIEVE","GUARD_ITEM","ABANDON_ITEM",
            "SEEK_SHADE","AVOID_SURFACE","ORIENT_TO_SOUND",
            "SOFTEN","LIE_NEAR","REST_HEAD",
            "ANTICIPATE_CUE","OFFER_KNOWN_TRICK","RESET_POSTURE"
        };

        public static Result<AiIntentEnvelope> Validate(
            AiIntentEnvelope envelope,
            AiIntentContext context,
            DateTime nowUtc)
        {
            if (envelope.SchemaVersion != ExpectedSchemaVersion)
                return Result<AiIntentEnvelope>.Fail($"SCHEMA_VERSION_MISMATCH_{envelope.SchemaVersion}");

            if (envelope.CatalogRevision != ExpectedCatalogRevision)
                return Result<AiIntentEnvelope>.Fail($"CATALOG_REVISION_MISMATCH_{envelope.CatalogRevision}");

            if (!string.Equals(envelope.PetId, context.PetId, StringComparison.Ordinal))
                return Result<AiIntentEnvelope>.Fail("PET_ID_MISMATCH");

            if (envelope.ContextRevision != context.ContextRevision)
                return Result<AiIntentEnvelope>.Fail("CONTEXT_REVISION_STALE");

            if (string.IsNullOrEmpty(envelope.Intent) || !AllowedIntents.Contains(envelope.Intent))
                return Result<AiIntentEnvelope>.Fail($"UNALLOWLISTED_INTENT_{envelope.Intent}");

            if (!string.IsNullOrEmpty(envelope.TargetId))
            {
                bool targetFound = false;
                if (context.AvailableTargetIds != null)
                {
                    for (int i = 0; i < context.AvailableTargetIds.Length; i++)
                    {
                        if (string.Equals(context.AvailableTargetIds[i], envelope.TargetId, StringComparison.Ordinal))
                        {
                            targetFound = true;
                            break;
                        }
                    }
                }
                if (!targetFound)
                    return Result<AiIntentEnvelope>.Fail($"UNKNOWN_TARGET_ID_{envelope.TargetId}");
            }

            if (envelope.ExpiresAt <= nowUtc)
                return Result<AiIntentEnvelope>.Fail("INTENT_EXPIRED");

            return Result<AiIntentEnvelope>.Ok(envelope);
        }
    }
}
