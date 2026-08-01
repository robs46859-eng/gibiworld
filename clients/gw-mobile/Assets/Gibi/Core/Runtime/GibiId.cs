// GW-ARCH-001 section 10.2 — "All public IDs SHALL be opaque, globally unique,
// prefixed identifiers. Sequential database IDs SHALL NOT cross the API boundary."
using System;
using System.Text.RegularExpressions;

namespace Gibi.Core
{
    public static class GibiId
    {
        // Crockford base32 ULID body, 26 chars, excluding I, L, O, U.
        private const string Body = "[0-9A-HJKMNP-TV-Z]{26}";

        private static readonly Regex Pet     = new Regex($"^pet_{Body}$",   RegexOptions.Compiled);
        private static readonly Regex Asset   = new Regex($"^asset_{Body}$", RegexOptions.Compiled);
        private static readonly Regex Site    = new Regex($"^site_{Body}$",  RegexOptions.Compiled);
        private static readonly Regex Course  = new Regex($"^crs_{Body}$",   RegexOptions.Compiled);
        private static readonly Regex Run     = new Regex($"^run_{Body}$",   RegexOptions.Compiled);
        private static readonly Regex Spatial = new Regex($"^spo_{Body}$",   RegexOptions.Compiled);

        public static bool IsPet(string s)     => s != null && Pet.IsMatch(s);
        public static bool IsAsset(string s)   => s != null && Asset.IsMatch(s);
        public static bool IsSite(string s)    => s != null && Site.IsMatch(s);
        public static bool IsCourse(string s)  => s != null && Course.IsMatch(s);
        public static bool IsRun(string s)     => s != null && Run.IsMatch(s);
        public static bool IsSpatial(string s) => s != null && Spatial.IsMatch(s);

        /// <summary>Rejects any purely numeric identifier reaching the client boundary.</summary>
        public static bool LooksLikeSequentialDbId(string s)
            => !string.IsNullOrEmpty(s) && long.TryParse(s, out _);
    }
}
