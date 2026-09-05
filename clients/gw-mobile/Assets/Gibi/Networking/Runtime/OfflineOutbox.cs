// GW-ARCH-003 DATA-05 & W16 — OfflineOutbox.
// Manages local durable event queuing, sequential replay, idempotency keys, and gap handling.
// Caps: 1,000 events, 7-day expiration.
// Backoff: 0.5/1/2/4s + 0-250ms jitter, max 4 retries.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Gibi.Core;

namespace Gibi.Networking
{
    public readonly struct OutboxEventRecord
    {
        public readonly string EventId;
        public readonly string SessionId;
        public readonly int Sequence;
        public readonly string EventType;
        public readonly string PayloadJson;
        public readonly string PayloadHash;
        public readonly string IdempotencyKey;
        public readonly long CreatedAtUtcMs;
        public readonly int RetryCount;

        public OutboxEventRecord(string eventId, string sessionId, int sequence, string eventType,
                                 string payloadJson, string idempotencyKey, long createdAtUtcMs,
                                 int retryCount = 0)
        {
            EventId = eventId;
            SessionId = sessionId;
            Sequence = sequence;
            EventType = eventType;
            PayloadJson = payloadJson ?? string.Empty;
            PayloadHash = ComputeSha256(PayloadJson);
            IdempotencyKey = idempotencyKey;
            CreatedAtUtcMs = createdAtUtcMs;
            RetryCount = retryCount;
        }

        public OutboxEventRecord WithIncrementedRetry()
            => new OutboxEventRecord(EventId, SessionId, Sequence, EventType, PayloadJson, IdempotencyKey, CreatedAtUtcMs, RetryCount + 1);

        public static string ComputeSha256(string input)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }
    }

    public sealed class OfflineOutbox
    {
        public const int MaxEventsCapacity = 1000;
        public const long MaxEventAgeMs = 7L * 24L * 60L * 60L * 1000L; // 7 days
        public const int MaxRetryAttempts = 4;

        private readonly List<OutboxEventRecord> _events = new List<OutboxEventRecord>();
        private readonly HashSet<string> _idempotencyKeys = new HashSet<string>(StringComparer.Ordinal);
        private int _lastAckedSequence = 0;

        public int Count => _events.Count;
        public int LastAckedSequence => _lastAckedSequence;

        public bool TryEnqueue(OutboxEventRecord record, long nowUtcMs)
        {
            if (_events.Count >= MaxEventsCapacity)
                PruneExpired(nowUtcMs);

            if (_events.Count >= MaxEventsCapacity)
                return false; // Outbox saturated

            // Deduplication by idempotency key
            if (_idempotencyKeys.Contains(record.IdempotencyKey))
                return false;

            _events.Add(record);
            _idempotencyKeys.Add(record.IdempotencyKey);
            return true;
        }

        public List<OutboxEventRecord> GetPendingBatch(int maxBatchSize = 50)
        {
            int count = Math.Min(maxBatchSize, _events.Count);
            return _events.GetRange(0, count);
        }

        public void AcknowledgeThrough(int sequence)
        {
            if (sequence <= _lastAckedSequence) return;
            _lastAckedSequence = sequence;

            _events.RemoveAll(e => e.Sequence <= sequence);
        }

        public void HandleGap(int nextExpectedSequence)
        {
            // If server reports a gap, drop acknowledged entries prior to expected
            if (nextExpectedSequence > _lastAckedSequence)
            {
                AcknowledgeThrough(nextExpectedSequence - 1);
            }
        }

        public void PruneExpired(long nowUtcMs)
        {
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                if (nowUtcMs - _events[i].CreatedAtUtcMs > MaxEventAgeMs || _events[i].RetryCount >= MaxRetryAttempts)
                {
                    _idempotencyKeys.Remove(_events[i].IdempotencyKey);
                    _events.RemoveAt(i);
                }
            }
        }

        public static float CalculateBackoffWithJitter(int attempt, float randomJitterSec)
        {
            float baseSec = attempt switch
            {
                0 => 0.5f,
                1 => 1.0f,
                2 => 2.0f,
                _ => 4.0f
            };
            return baseSec + Math.Min(0.25f, Math.Max(0f, randomJitterSec));
        }
    }
}
