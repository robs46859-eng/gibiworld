-- GW-ARCH-001 section 0 (age gate) and section 13.2 (data classification).
-- Implements docs/design/age-assurance-and-consent.md / ADR-010.

BEGIN;

CREATE TYPE consent_method AS ENUM (
  'CARD_MICROTRANSACTION', 'SIGNED_FORM_UPLOAD', 'GOVERNMENT_ID', 'STAFF_VIDEO_CALL'
);
CREATE TYPE consent_status AS ENUM ('PENDING', 'GRANTED', 'WITHDRAWN', 'EXPIRED');

CREATE TABLE guardian_consents (
  id                  bigserial PRIMARY KEY,
  consent_id          gibi_id UNIQUE NOT NULL,
  child_user_id       gibi_id NOT NULL REFERENCES users(user_id),
  guardian_contact    text NOT NULL,
  status              consent_status NOT NULL DEFAULT 'PENDING',
  method              consent_method,
  document_version    text NOT NULL,
  granted_at          timestamptz,
  withdrawn_at        timestamptz,
  expires_at          timestamptz,
  created_at          timestamptz NOT NULL DEFAULT now(),

  -- Only the FACT of verification is retained. Never the card, the signature image, the
  -- ID document, or any other evidence (design section 4).
  CONSTRAINT consent_granted_has_method_and_time
    CHECK (status <> 'GRANTED' OR (method IS NOT NULL AND granted_at IS NOT NULL)),
  CONSTRAINT consent_withdrawn_has_time
    CHECK (status <> 'WITHDRAWN' OR withdrawn_at IS NOT NULL)
);
CREATE UNIQUE INDEX guardian_consents_active_uq
  ON guardian_consents (child_user_id) WHERE status = 'GRANTED';

-- Section 0: "Under-13 accounts SHALL remain disabled until verifiable parental consent."
-- Migration 0001 enforced the age gate with a CHECK. That constraint cannot see another
-- table, so it is replaced by a trigger that admits an under-13 account ONLY when a
-- GRANTED consent exists. Enforcing this in the database means no service bug, no admin
-- action, and no future migration can produce an active under-13 account without consent.
ALTER TABLE users DROP CONSTRAINT IF EXISTS users_age_gate;

CREATE OR REPLACE FUNCTION enforce_under13_requires_consent() RETURNS trigger AS $$
BEGIN
  IF NEW.birth_band = 'UNDER_13' AND NEW.status = 'ACTIVE' THEN
    IF NOT EXISTS (
      SELECT 1 FROM guardian_consents
       WHERE child_user_id = NEW.user_id
         AND status = 'GRANTED'
         AND (expires_at IS NULL OR expires_at > now())
    ) THEN
      RAISE EXCEPTION
        'under-13 account % cannot be ACTIVE without current verifiable guardian consent (section 0)',
        NEW.user_id
        USING ERRCODE = 'check_violation';
    END IF;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE CONSTRAINT TRIGGER users_under13_consent_gate
  AFTER INSERT OR UPDATE ON users
  DEFERRABLE INITIALLY IMMEDIATE
  FOR EACH ROW EXECUTE FUNCTION enforce_under13_requires_consent();

-- Withdrawing consent must DISABLE the account, never silently degrade it (design s6).
CREATE OR REPLACE FUNCTION disable_child_on_consent_withdrawal() RETURNS trigger AS $$
BEGIN
  IF NEW.status IN ('WITHDRAWN','EXPIRED') AND OLD.status = 'GRANTED' THEN
    UPDATE users SET status = 'DISABLED'
     WHERE user_id = NEW.child_user_id AND birth_band = 'UNDER_13';
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER consent_withdrawal_disables_child
  AFTER UPDATE ON guardian_consents
  FOR EACH ROW EXECUTE FUNCTION disable_child_on_consent_withdrawal();

-- Care profiles (ADR-009). Stored against the PET as behaviour parameters, so reading the
-- database reveals a pet configured for calmer movement -- not a statement about a person.
-- Section 13.2: Confidential. NEVER egressed to the AI provider or telemetry.
CREATE TABLE pet_care_profiles (
  pet_id       gibi_id PRIMARY KEY REFERENCES pets(pet_id),
  profile_bits integer NOT NULL DEFAULT 0,
  set_by       text NOT NULL DEFAULT 'GUARDIAN',
  updated_at   timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT care_profile_bits_range CHECK (profile_bits BETWEEN 0 AND 31)
);

COMMENT ON TABLE pet_care_profiles IS
  'ADR-009 behavioural accommodations. MUST NOT be included in any AI provider request '
  'or telemetry export. No diagnosis, condition, or free text is ever stored here.';

COMMIT;
