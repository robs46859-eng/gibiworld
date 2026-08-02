// Section 6.4 step 2 — Ed25519 verification against a pinned key.
// Cross-implementation: signature produced by Python's `cryptography`, verified by the
// managed C# implementation. Mostly negative tests — the failure mode is ACCEPTING
// something forged, not rejecting something valid.
using System;
using NUnit.Framework;
using Gibi.AssetRuntime;

namespace Gibi.Tests.EditMode
{
    public class GW_ASSET_001_SignatureVerification
    {
        private static byte[] Hex(string s)
        {
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }

        private static (Ed25519Verifier v, byte[] msg, byte[] sig) Setup()
        {
            var store = new PinnedKeyStore();
            store.Pin(SignatureVectors.KeyId, Hex(SignatureVectors.PublicKeyHex));
            return (new Ed25519Verifier(store),
                    Convert.FromBase64String(SignatureVectors.CanonicalManifestBase64),
                    Hex(SignatureVectors.SignatureHex));
        }

        [Test]
        public void Real_preset_signature_verifies_against_the_pinned_key()
        {
            var (v, msg, sig) = Setup();
            Assert.IsTrue(v.Verify(msg, sig, SignatureVectors.KeyId),
                "A signature made by the Python signer must verify in the C# client.");
        }

        [Test]
        public void A_single_flipped_manifest_byte_invalidates_the_signature()
        {
            var (v, msg, sig) = Setup();
            msg[msg.Length / 2] ^= 0x01;
            Assert.IsFalse(v.Verify(msg, sig, SignatureVectors.KeyId));
        }

        [Test]
        public void A_tampered_signature_is_rejected()
        {
            var (v, msg, sig) = Setup();
            sig[0] ^= 0xFF;
            Assert.IsFalse(v.Verify(msg, sig, SignatureVectors.KeyId));
        }

        [Test]
        public void An_unpinned_key_id_is_rejected_even_with_a_valid_signature()
        {
            var (_, msg, sig) = Setup();
            var empty = new Ed25519Verifier(new PinnedKeyStore());
            Assert.IsFalse(empty.IsPinnedKey(SignatureVectors.KeyId));
            Assert.IsFalse(empty.Verify(msg, sig, SignatureVectors.KeyId),
                "Section 6.4 step 1: an unknown key ID rejects before render.");
        }

        [Test]
        public void Revoking_a_key_takes_effect_immediately()
        {
            var store = new PinnedKeyStore();
            store.Pin(SignatureVectors.KeyId, Hex(SignatureVectors.PublicKeyHex));
            var v = new Ed25519Verifier(store);
            var msg = Convert.FromBase64String(SignatureVectors.CanonicalManifestBase64);
            var sig = Hex(SignatureVectors.SignatureHex);

            Assert.IsTrue(v.Verify(msg, sig, SignatureVectors.KeyId));
            store.Revoke(SignatureVectors.KeyId);
            Assert.IsFalse(v.Verify(msg, sig, SignatureVectors.KeyId),
                "Section 6.1: pinned keys are remotely revocable.");
        }

        [Test]
        public void Malformed_signature_length_is_a_rejection_not_an_exception()
        {
            var (v, msg, _) = Setup();
            Assert.IsFalse(v.Verify(msg, new byte[10], SignatureVectors.KeyId));
            Assert.IsFalse(v.Verify(msg, Array.Empty<byte>(), SignatureVectors.KeyId));
        }

        [Test]
        public void Wrong_key_cannot_validate_another_keys_signature()
        {
            var (_, msg, sig) = Setup();
            var store = new PinnedKeyStore();
            var wrong = new byte[32];
            for (int i = 0; i < 32; i++) wrong[i] = (byte)(i * 7 + 1);
            store.Pin(SignatureVectors.KeyId, wrong);
            Assert.IsFalse(new Ed25519Verifier(store).Verify(msg, sig, SignatureVectors.KeyId));
        }
    }
}
