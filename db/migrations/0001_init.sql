-- GW-ARCH-001 section 10.2 — Core tables.
-- Migrations are FORWARD-ONLY (section 10.1). Deployment uses expand/migrate/contract
-- phases so client and server never require simultaneous cutover (section 16).
-- All timestamps are timestamptz in UTC.

BEGIN;

CREATE EXTENSION IF NOT EXISTS postgis;

-- Opaque prefixed public IDs. Sequential DB ids SHALL NOT cross the API boundary.
CREATE DOMAIN gibi_id AS text
  CHECK (VALUE ~ '^[a-z]{3,6}_[0-9A-HJKMNP-TV-Z]{26}$');

CREATE TYPE asset_issuer   AS ENUM ('PAWSOME3D','GIBIWORLD_PRESET');
CREATE TYPE asset_status   AS ENUM ('DRAFT','VALIDATING','PUBLISHED','REVOKED');
CREATE TYPE entitle_status AS ENUM ('ACTIVE','EXPIRED','REVOKED');
CREATE TYPE course_status  AS ENUM ('DRAFT','PUBLISHED','DISABLED');
CREATE TYPE run_result     AS ENUM ('FINISHED','ABANDONED','INVALIDATED');
CREATE TYPE species_t      AS ENUM ('dog','cat','rabbit','guinea_pig','ferret','miniature_pig');

-- Section 0: launch accounts SHALL be age 13 or older. Under-13 remains DISABLED
-- until verifiable parental consent and child privacy review ship.
CREATE TYPE birth_band_t   AS ENUM ('UNDER_13','13_17','18_PLUS');

CREATE TABLE users (
  id             bigserial PRIMARY KEY,          -- internal only, never exposed
  user_id        gibi_id UNIQUE NOT NULL,
  auth_subject   text UNIQUE NOT NULL,           -- external IdP subject; no password hashes
  birth_band     birth_band_t NOT NULL,
  status         text NOT NULL DEFAULT 'ACTIVE',
  locale         text NOT NULL DEFAULT 'en-US',
  revision       bigint NOT NULL DEFAULT 1,
  created_at     timestamptz NOT NULL DEFAULT now(),
  deleted_at     timestamptz,
  -- Enforces the section 0 age gate at the storage layer, not just in code.
  CONSTRAINT users_age_gate CHECK (birth_band <> 'UNDER_13' OR status = 'DISABLED')
);

CREATE TABLE pet_assets (
  id             bigserial PRIMARY KEY,
  pet_asset_id   gibi_id UNIQUE NOT NULL,
  issuer         asset_issuer NOT NULL,
  source_id      text,
  version        integer NOT NULL CHECK (version >= 1),
  species        species_t NOT NULL,
  digest         text NOT NULL CHECK (digest ~ '^sha256:[a-f0-9]{64}$'),
  manifest_json  jsonb NOT NULL,
  key_id         text NOT NULL,
  status         asset_status NOT NULL DEFAULT 'DRAFT',
  revision       bigint NOT NULL DEFAULT 1,
  created_at     timestamptz NOT NULL DEFAULT now(),
  -- Published versions are immutable; a change is a new version row.
  UNIQUE (pet_asset_id, version)
);
CREATE UNIQUE INDEX pet_assets_digest_uq ON pet_assets (digest);

CREATE TABLE pet_entitlements (
  id             bigserial PRIMARY KEY,
  user_id        gibi_id NOT NULL REFERENCES users(user_id),
  pet_asset_id   gibi_id NOT NULL,
  version        integer NOT NULL,
  status         entitle_status NOT NULL DEFAULT 'ACTIVE',
  granted_at     timestamptz NOT NULL DEFAULT now(),
  revoked_at     timestamptz,
  UNIQUE (user_id, pet_asset_id, version),
  CONSTRAINT entitlement_revoked_has_time
    CHECK (status <> 'REVOKED' OR revoked_at IS NOT NULL)
);

CREATE TABLE pets (
  id               bigserial PRIMARY KEY,
  pet_id           gibi_id UNIQUE NOT NULL,
  user_id          gibi_id NOT NULL REFERENCES users(user_id),
  pet_asset_id     gibi_id NOT NULL,
  asset_version    integer NOT NULL,
  display_name     text NOT NULL CHECK (char_length(display_name) BETWEEN 1 AND 24),
  personality_seed bigint NOT NULL,
  state_revision   bigint NOT NULL DEFAULT 1,
  created_at       timestamptz NOT NULL DEFAULT now(),
  deleted_at       timestamptz
);
CREATE INDEX pets_user_idx ON pets (user_id) WHERE deleted_at IS NULL;

CREATE TABLE pet_state (
  pet_id        gibi_id PRIMARY KEY REFERENCES pets(pet_id),
  bond          integer NOT NULL DEFAULT 0 CHECK (bond BETWEEN 0 AND 100),
  energy        integer NOT NULL DEFAULT 100 CHECK (energy BETWEEN 0 AND 100),
  training_json jsonb NOT NULL DEFAULT '{}'::jsonb,
  revision      bigint NOT NULL DEFAULT 1,
  updated_at    timestamptz NOT NULL DEFAULT now()
);

COMMIT;
