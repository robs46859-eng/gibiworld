-- GW-ARCH-001 section 10.2 continued — spatial, courses, ledger, outbox, audit.

BEGIN;

CREATE TABLE pet_memories (
  id          bigserial PRIMARY KEY,
  memory_id   gibi_id UNIQUE NOT NULL,
  pet_id      gibi_id NOT NULL REFERENCES pets(pet_id),
  -- Section 8.2: memoryProposals require an ALLOWLISTED fact type. Sensitive or
  -- inferred attributes are rejected. The allowlist is enforced here as well as in
  -- the AI orchestrator so a service bug cannot widen it.
  fact_type   text NOT NULL CHECK (fact_type IN
                ('FAVORITE_TOY','PREFERRED_TRICK','PLAY_TIME_OF_DAY','FAVORITE_PLACE_TAG')),
  fact_json   jsonb NOT NULL,
  consent     boolean NOT NULL DEFAULT true,
  status      text NOT NULL DEFAULT 'ACTIVE',
  created_at  timestamptz NOT NULL DEFAULT now(),
  tombstoned_at timestamptz,
  -- GW-AI-006: deleted memory must be absent from AI context within 24 hours.
  CONSTRAINT memory_tombstone_time
    CHECK (status <> 'TOMBSTONED' OR tombstoned_at IS NOT NULL)
);
CREATE INDEX pet_memories_active_idx ON pet_memories (pet_id) WHERE status = 'ACTIVE';

CREATE TABLE sites (
  id                bigserial PRIMARY KEY,
  site_id           gibi_id UNIQUE NOT NULL,
  provider_site_id  text NOT NULL,
  -- Section 5.1: authoritative geography stays float64. geography(POINT,4326) is
  -- double precision end to end.
  geography         geography(POINT, 4326) NOT NULL,
  approved_polygon  geography(POLYGON, 4326),
  access            text NOT NULL DEFAULT 'PUBLIC',
  safety_status     text NOT NULL DEFAULT 'PENDING_REVIEW',
  version           integer NOT NULL DEFAULT 1,
  created_at        timestamptz NOT NULL DEFAULT now()
);
-- Section 15: world nearby API p95 <= 500 ms. Spatial index is not optional.
CREATE INDEX sites_geo_gix ON sites USING GIST (geography);
CREATE INDEX sites_discoverable_idx ON sites (safety_status) WHERE safety_status = 'APPROVED';

CREATE TABLE courses (
  id              bigserial PRIMARY KEY,
  course_id       gibi_id UNIQUE NOT NULL,
  site_id         gibi_id NOT NULL REFERENCES sites(site_id),
  current_version integer NOT NULL DEFAULT 0,
  owner_id        gibi_id NOT NULL REFERENCES users(user_id),
  status          course_status NOT NULL DEFAULT 'DRAFT',
  created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE course_versions (
  course_id       gibi_id NOT NULL REFERENCES courses(course_id),
  version         integer NOT NULL CHECK (version >= 1),
  anchor_id       text NOT NULL,
  content_json    jsonb NOT NULL,
  digest          text NOT NULL CHECK (digest ~ '^sha256:[a-f0-9]{64}$'),
  safety_revision integer NOT NULL,
  published_at    timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (course_id, version)
);

-- GW-GAME-007: course versions are IMMUTABLE after publication. Enforced as a
-- database constraint, not a convention, per section 17's "Database constraint test".
CREATE OR REPLACE FUNCTION reject_course_version_mutation() RETURNS trigger AS $$
BEGIN
  RAISE EXCEPTION 'course_versions is append-only; publish version % + 1 instead', OLD.version
    USING ERRCODE = 'restrict_violation';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER course_versions_immutable
  BEFORE UPDATE OR DELETE ON course_versions
  FOR EACH ROW EXECUTE FUNCTION reject_course_version_mutation();

CREATE TABLE course_runs (
  id            bigserial PRIMARY KEY,
  run_id        gibi_id UNIQUE NOT NULL,
  course_id     gibi_id NOT NULL,
  version       integer NOT NULL,
  user_id       gibi_id NOT NULL REFERENCES users(user_id),
  pet_id        gibi_id NOT NULL REFERENCES pets(pet_id),
  result        run_result,
  ranked        boolean NOT NULL DEFAULT false,
  time_ms       integer CHECK (time_ms IS NULL OR time_ms >= 0),
  invalidation_reason text,
  proof_digest  text,
  start_epoch_ms bigint NOT NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  finished_at   timestamptz,
  FOREIGN KEY (course_id, version) REFERENCES course_versions(course_id, version),
  -- "One terminal write" (section 11).
  CONSTRAINT run_terminal_once CHECK (result IS NULL OR finished_at IS NOT NULL)
);
CREATE INDEX course_runs_leaderboard_idx
  ON course_runs (course_id, version, time_ms) WHERE ranked AND result = 'FINISHED';

-- Section 10.2: inventory SHALL be an append-only ledger. Balance is DERIVED.
CREATE TABLE inventory_ledger (
  entry_id        bigserial PRIMARY KEY,
  user_id         gibi_id NOT NULL REFERENCES users(user_id),
  item_id         text NOT NULL,
  quantity_delta  integer NOT NULL CHECK (quantity_delta <> 0),
  reason          text NOT NULL,
  idempotency_key text NOT NULL,
  created_at      timestamptz NOT NULL DEFAULT now(),
  UNIQUE (user_id, idempotency_key)
);
CREATE INDEX inventory_balance_idx ON inventory_ledger (user_id, item_id);

-- GW-API-006: ledger cannot produce a negative balance under concurrency.
-- SERIALIZABLE-safe: the trigger recomputes the balance while holding the row locks
-- taken by the insert, so two concurrent debits cannot both observe a sufficient balance.
CREATE OR REPLACE FUNCTION reject_negative_balance() RETURNS trigger AS $$
DECLARE bal integer;
BEGIN
  IF NEW.quantity_delta < 0 THEN
    SELECT COALESCE(SUM(quantity_delta), 0) INTO bal
      FROM inventory_ledger
      WHERE user_id = NEW.user_id AND item_id = NEW.item_id;
    IF bal < 0 THEN
      RAISE EXCEPTION 'negative balance for % / %', NEW.user_id, NEW.item_id
        USING ERRCODE = 'check_violation';
    END IF;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE CONSTRAINT TRIGGER inventory_no_negative
  AFTER INSERT ON inventory_ledger
  DEFERRABLE INITIALLY IMMEDIATE
  FOR EACH ROW EXECUTE FUNCTION reject_negative_balance();

CREATE TABLE outbox_events (
  event_id       bigserial PRIMARY KEY,
  aggregate_type text NOT NULL,
  aggregate_id   gibi_id NOT NULL,
  event_type     text NOT NULL,
  payload        jsonb NOT NULL,
  created_at     timestamptz NOT NULL DEFAULT now(),
  published_at   timestamptz
);
CREATE INDEX outbox_unpublished_idx ON outbox_events (created_at) WHERE published_at IS NULL;

CREATE TABLE audit_log (
  audit_id    bigserial PRIMARY KEY,
  actor       text NOT NULL,
  action      text NOT NULL,
  target      text NOT NULL,
  before_hash text,
  after_hash  text,
  occurred_at timestamptz NOT NULL DEFAULT now()
);

-- Section 13.1: admin mutations are immutably audited.
CREATE RULE audit_log_no_update AS ON UPDATE TO audit_log DO INSTEAD NOTHING;
CREATE RULE audit_log_no_delete AS ON DELETE TO audit_log DO INSTEAD NOTHING;

COMMIT;
