// Section 6.4 step 2 — verification against the REAL shipped manifest file.
//
// WHY THIS EXISTS: SignatureTests passes a PRE-COMPUTED canonical byte array to the
// verifier. That proves Ed25519 works, but it bypasses PresetCatalog's own
// re-canonicalisation — the code that actually runs on device. A signature bug lived
// behind that gap and only surfaced on hardware: the signer emitted pretty-printed JSON,
// the client re-emitted it compact, and the bytes differed.
//
// These tests read the shipped file and drive the real canonicaliser.
using System;
using System.IO;
using NUnit.Framework;
using Gibi.AssetRuntime;
using UnityEngine;

namespace Gibi.Tests.EditMode
{
    public class GW_ASSET_002_ManifestCanonicalization
    {
        private const string PresetId = "asset_01J8ZQK5T7VN2MXR4WD6GHYAB3";

        private static string PresetDir =>
            Path.Combine(Application.streamingAssetsPath, "presets");

        private static byte[] Hex(string s)
        {
            var b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++)
                b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            return b;
        }

        [Test]
        public void Shipped_manifest_verifies_through_the_real_canonicalizer()
        {
            string manifestPath = Path.Combine(PresetDir, PresetId + ".manifest.json");
            string keysPath = Path.Combine(PresetDir, "trusted-keys.json");

            Assert.IsTrue(File.Exists(manifestPath), $"missing {manifestPath}");
            Assert.IsTrue(File.Exists(keysPath), $"missing {keysPath}");

            string json = File.ReadAllText(manifestPath);

            // Pull signature and keyId without depending on Unity's JSON handling of
            // fields we do not model.
            string sigHex = ExtractString(json, "signature");
            string keyId = ExtractString(json, "keyId");
            Assert.IsNotEmpty(sigHex, "manifest carries no signature");

            string keysJson = File.ReadAllText(keysPath);
            string pubHex = ExtractString(keysJson, "publicKeyHex");
            Assert.IsNotEmpty(pubHex, "trusted-keys.json carries no public key");

            var store = new PinnedKeyStore();
            store.Pin(keyId, Hex(pubHex));
            var verifier = new Ed25519Verifier(store);

            // THE POINT: canonicalise exactly as the device does.
            byte[] canonical = PresetCatalog.CanonicalizeExcludingSignature(json);

            Assert.IsTrue(verifier.Verify(canonical, Hex(sigHex), keyId),
                "The shipped manifest must verify through the client's own " +
                "re-canonicalisation, not merely through pre-computed bytes.");
        }

        [Test]
        public void Canonical_form_contains_no_whitespace_and_omits_the_signature()
        {
            string json = File.ReadAllText(Path.Combine(PresetDir, PresetId + ".manifest.json"));
            string canonical = System.Text.Encoding.UTF8.GetString(
                PresetCatalog.CanonicalizeExcludingSignature(json));

            Assert.IsFalse(canonical.Contains("\"signature\""),
                "A document cannot contain its own signature.");
            Assert.IsFalse(canonical.Contains("\n") || canonical.Contains("  "),
                "RFC 8785 canonical form carries no insignificant whitespace.");
        }

        [Test]
        public void Tampering_with_the_shipped_manifest_breaks_verification()
        {
            string dir = PresetDir;
            string json = File.ReadAllText(Path.Combine(dir, PresetId + ".manifest.json"));
            string keyId = ExtractString(json, "keyId");
            string sigHex = ExtractString(json, "signature");
            string pubHex = ExtractString(File.ReadAllText(Path.Combine(dir, "trusted-keys.json")),
                                          "publicKeyHex");

            var store = new PinnedKeyStore();
            store.Pin(keyId, Hex(pubHex));
            var verifier = new Ed25519Verifier(store);

            // Swap the declared species — a manifest edit that would otherwise let an
            // unapproved asset through section 6.2's allowlist.
            string tampered = json.Replace("\"species\":\"dog\"", "\"species\":\"cat\"");
            Assert.AreNotEqual(json, tampered, "tamper did not apply; test would pass vacuously");

            byte[] canonical = PresetCatalog.CanonicalizeExcludingSignature(tampered);
            Assert.IsFalse(verifier.Verify(canonical, Hex(sigHex), keyId));
        }

        private static string ExtractString(string json, string key)
        {
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) return null;
            i = json.IndexOf(':', i) + 1;
            while (i < json.Length && (json[i] == ' ' || json[i] == '"')) i++;
            int end = json.IndexOf('"', i);
            return end < 0 ? null : json.Substring(i, end - i);
        }
    }
}
