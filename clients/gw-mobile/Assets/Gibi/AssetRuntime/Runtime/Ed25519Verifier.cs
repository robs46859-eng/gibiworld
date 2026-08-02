// GW-ARCH-001 section 6.4 step 2 — Ed25519 signature verification against a PINNED key.
//
// Pure managed implementation (RFC 8032 Ed25519 verify) so the trust decision has no
// platform or plugin dependency and behaves identically on iOS, Android, and in EditMode
// tests. Verification only -- this code cannot sign, by construction.
//
// Section 6.1: "public key pinned and remotely revocable."
using System;
using System.Numerics;
using System.Security.Cryptography;

namespace Gibi.AssetRuntime
{
    public sealed class PinnedKeyStore
    {
        private readonly System.Collections.Generic.Dictionary<string, byte[]> _keys = new();

        public void Pin(string keyId, byte[] publicKey32)
        {
            if (publicKey32 == null || publicKey32.Length != 32)
                throw new ArgumentException("Ed25519 public key must be 32 bytes", nameof(publicKey32));
            _keys[keyId] = publicKey32;
        }

        /// <summary>Section 6.1: keys are remotely revocable. Revocation is immediate.</summary>
        public void Revoke(string keyId) => _keys.Remove(keyId);

        public bool IsPinned(string keyId) => keyId != null && _keys.ContainsKey(keyId);

        public bool TryGet(string keyId, out byte[] key) => _keys.TryGetValue(keyId, out key);
    }

    public sealed class Ed25519Verifier : ISignatureVerifier
    {
        private readonly PinnedKeyStore _store;
        public Ed25519Verifier(PinnedKeyStore store) { _store = store; }

        public bool IsPinnedKey(string keyId) => _store.IsPinned(keyId);

        public bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, string keyId)
        {
            if (!_store.TryGet(keyId, out var publicKey)) return false;
            if (signature.Length != 64) return false;
            try { return Ed25519.Verify(signature, message, publicKey); }
            catch { return false; }   // malformed input is a rejection, never an exception
        }
    }

    /// <summary>RFC 8032 Ed25519 verification over Curve25519 in Edwards form.</summary>
    internal static class Ed25519
    {
        private static readonly BigInteger Q =
            BigInteger.Pow(2, 255) - 19;
        private static readonly BigInteger L =
            BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");
        private static readonly BigInteger D =
            Mod(BigInteger.Parse("-121665") * Inv(121666));
        private static readonly BigInteger I =
            BigInteger.ModPow(2, (Q - 1) / 4, Q);

        private static BigInteger Mod(BigInteger x)
        {
            var r = x % Q;
            return r.Sign < 0 ? r + Q : r;
        }

        private static BigInteger Inv(BigInteger x) => BigInteger.ModPow(Mod(x), Q - 2, Q);

        // Extended coordinates (X, Y, Z, T)
        private static BigInteger[] Edwards(BigInteger[] p, BigInteger[] q)
        {
            BigInteger a = Mod((p[1] - p[0]) * (q[1] - q[0]));
            BigInteger b = Mod((p[1] + p[0]) * (q[1] + q[0]));
            BigInteger c = Mod(2 * p[3] * q[3] * D);
            BigInteger dd = Mod(2 * p[2] * q[2]);
            BigInteger e = b - a, f = dd - c, g = dd + c, h = b + a;
            return new[] { Mod(e * f), Mod(g * h), Mod(f * g), Mod(e * h) };
        }

        private static BigInteger[] ScalarMul(BigInteger[] p, BigInteger e)
        {
            var q = new BigInteger[] { 0, 1, 1, 0 };
            while (e > 0)
            {
                if (!e.IsEven) q = Edwards(q, p);
                p = Edwards(p, p);
                e >>= 1;
            }
            return q;
        }

        private static BigInteger RecoverX(BigInteger y, int sign)
        {
            BigInteger y2 = Mod(y * y);
            BigInteger u = Mod(y2 - 1);
            BigInteger v = Mod(D * y2 + 1);
            BigInteger x = Mod(u * Inv(v));
            x = BigInteger.ModPow(x, (Q + 3) / 8, Q);
            if (Mod(x * x * v - u) != 0) x = Mod(x * I);
            if (Mod(x * x * v - u) != 0) return BigInteger.MinusOne;
            if ((int)(x & 1) != sign) x = Q - x;
            return x;
        }

        private static BigInteger FromLe(ReadOnlySpan<byte> b)
        {
            Span<byte> tmp = stackalloc byte[b.Length + 1];
            b.CopyTo(tmp);
            tmp[b.Length] = 0;                      // force positive
            return new BigInteger(tmp.ToArray());
        }

        private static byte[] Encode(BigInteger[] p)
        {
            BigInteger zi = Inv(p[2]);
            BigInteger x = Mod(p[0] * zi), y = Mod(p[1] * zi);
            var outp = new byte[32];
            var yb = y.ToByteArray();
            Array.Copy(yb, outp, Math.Min(yb.Length, 32));
            outp[31] = (byte)((outp[31] & 0x7F) | (byte)((x & 1) << 7));
            return outp;
        }

        public static bool Verify(ReadOnlySpan<byte> sig, ReadOnlySpan<byte> msg,
                                  ReadOnlySpan<byte> pub)
        {
            var rBytes = sig.Slice(0, 32);
            BigInteger s = FromLe(sig.Slice(32, 32));
            if (s >= L) return false;               // non-canonical S is a rejection

            BigInteger ay = FromLe(pub) & ((BigInteger.One << 255) - 1);
            int aSign = (pub[31] >> 7) & 1;
            BigInteger ax = RecoverX(ay, aSign);
            if (ax.Sign < 0) return false;
            var A = new[] { ax, ay, BigInteger.One, Mod(ax * ay) };

            BigInteger ry = FromLe(rBytes) & ((BigInteger.One << 255) - 1);
            int rSign = (rBytes[31] >> 7) & 1;
            BigInteger rx = RecoverX(ry, rSign);
            if (rx.Sign < 0) return false;
            var R = new[] { rx, ry, BigInteger.One, Mod(rx * ry) };

            byte[] hashInput = new byte[64 + msg.Length];
            rBytes.CopyTo(hashInput);
            pub.CopyTo(hashInput.AsSpan(32));
            msg.CopyTo(hashInput.AsSpan(64));
            // Unity's .NET Standard 2.1 profile predates the static HashData helper.
            byte[] h;
            using (var sha = SHA512.Create()) h = sha.ComputeHash(hashInput);
            BigInteger k = FromLe(h) % L;

            // Base point
            BigInteger by = Mod(4 * Inv(5));
            BigInteger bx = RecoverX(by, 0);
            var B = new[] { bx, by, BigInteger.One, Mod(bx * by) };

            var lhs = ScalarMul(B, s);
            var rhs = Edwards(R, ScalarMul(A, k));

            return CryptographicOperations.FixedTimeEquals(Encode(lhs), Encode(rhs));
        }
    }
}
