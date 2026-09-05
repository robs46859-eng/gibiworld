-- GW-ARCH-003 DATA-04 & W15 — Companion dwellings, sessions, events, idempotency, preferences, and asset-version integrity.
-- Migrations are FORWARD-ONLY. Deployment uses expand/migrate/contract phases.

BEGIN;

-- 1. Asset version integrity correction:
-- Remove the conflicting single-column uniqueness on pet_asset_id so (pet_asset_id, version)
-- permits multiple published/draft versions of an asset without ID collision.
ALTER TABLE pet_assets DROP CONSTRAINT IF EXISTS pet_assets_pet_asset_id_key;

-- Ensure composite uniqueness exists
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint
    WHERE conrelid = 'pet_assets'::regclass AND conname = 'pet_assets_composite_version_uq'
  ) THEN
    ALTER TABLE pet_assets ADD CONSTRAINT pet_assets_composite_version_uq UNIQUE (pet_asset_id, version);
  END IF;
END $$;

-- 2. Selected dwelling per pet:
-- Durable preference only. Does NOT store local room coordinates, anchor transforms, or floor polygons.
CREATE TABLE pet_dwellings (
  pet_id          gibi_id PRIMARY KEY REFERENCES pets(pet_id),
  catalog_id      text NOT NULL,
  catalog_version integer NOT NULL CHECK (catalog_version >= 1),
  style_json      jsonb NOT NULL DEFAULT '{}'::jsonb,
  revision        bigint NOT NULL DEFAULT 1,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);

-- 3. Companion play sessions:
-- Tracks solo unranked AR fetch and companion play sessions.
CREATE TABLE companion_play_sessions (
  session_id      gibi_id PRIMARY KEY,
  pet_id          gibi_id NOT NULL REFERENCES pets(pet_id),
  mode            text NOT NULL DEFAULT 'LOCAL_UNRANKED',
  client_version  text NOT NULL,
  ruleset_version integer NOT NULL DEFAULT 1,
  next_sequence   integer NOT NULL DEFAULT 1,
  status          text NOT NULL DEFAULT 'ACTIVE',
  started_at      timestamptz NOT NULL DEFAULT now(),
  finished_at     timestamptz
);
CREATE INDEX companion_sessions_pet_idx ON companion_play_sessions (pet_id, status);

-- 4. Companion play events:
-- Append-only event ledger for unranked companion actions.
-- Enforces (session_id, event_sequence) strict uniqueness to detect gaps or duplicate submissions.
CREATE TABLE companion_play_events (
  id              bigserial PRIMARY KEY,
  session_id      gibi_id NOT NULL REFERENCES companion_play_sessions(session_id),
  event_id        gibi_id UNIQUE NOT NULL,
  event_sequence  integer NOT NULL CHECK (event_sequence >= 1),
  event_type      text NOT NULL,
  payload_json    jsonb NOT NULL,
  body_hash       text NOT NULL,
  created_at      timestamptz NOT NULL DEFAULT now(),
  UNIQUE (session_id, event_sequence)
);

-- 5. Idempotency records:
-- Scoped to auth subject + method + route + idempotency key.
-- Retained >= 24 h to guarantee idempotent mutation processing.
CREATE TABLE idempotency_records (
  id              bigserial PRIMARY KEY,
  auth_subject    text NOT NULL,
  method          text NOT NULL,
  route           text NOT NULL,
  idempotency_key text NOT NULL,
  request_hash    text NOT NULL,
  response_status integer NOT NULL,
  response_body   jsonb NOT NULL,
  expires_at      timestamptz NOT NULL,
  created_at      timestamptz NOT NULL DEFAULT now(),
  UNIQUE (auth_subject, method, route, idempotency_key)
);
CREATE INDEX idempotency_expiry_idx ON idempotency_records (expires_at);

-- 6. Pet preferences:
-- Allowlisted personalization and accessibility settings.
-- Rejects free-form inferred psychological traits.
CREATE TABLE pet_preferences (
  pet_id              gibi_id PRIMARY KEY REFERENCES pets(pet_id),
  favorite_toy        text,
  preferred_trick     text,
  play_time_of_day    text,
  favorite_place_tag  text,
  reduced_motion      boolean NOT NULL DEFAULT false,
  revision            bigint NOT NULL DEFAULT 1,
  updated_at          timestamptz NOT NULL DEFAULT now()
);

COMMIT;
