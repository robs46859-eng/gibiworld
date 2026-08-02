// Minimal top-level JSON field splitter for RFC 8785 re-canonicalization.
//
// Unity's JsonUtility cannot round-trip arbitrary JSON while preserving exact value
// formatting, and exact bytes are what a signature covers. This walks the raw text and
// returns each top-level key with its value SUBSTRING VERBATIM, so numbers, arrays, and
// nested objects are reproduced exactly as the signer emitted them.
//
// Scope is deliberately narrow: it handles the manifests our own signer produces. It is
// not a general JSON parser and must not be used as one.
using System.Collections.Generic;

namespace Gibi.AssetRuntime
{
    internal static class JsonFlattener
    {
        public static IEnumerable<(string key, string value)> TopLevelFields(string json)
        {
            int i = 0;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '{') yield break;
            i++;

            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i < json.Length && json[i] == '}') yield break;
                if (i >= json.Length || json[i] != '"') yield break;

                string key = ReadString(json, ref i);

                SkipWs(json, ref i);
                if (i >= json.Length || json[i] != ':') yield break;
                i++;
                SkipWs(json, ref i);

                int start = i;
                SkipValue(json, ref i);
                string value = json.Substring(start, i - start).Trim();

                yield return (key, value);

                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',') { i++; continue; }
                yield break;
            }
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++;
        }

        private static string ReadString(string s, ref int i)
        {
            i++; // opening quote
            var sb = new System.Text.StringBuilder();
            while (i < s.Length && s[i] != '"')
            {
                if (s[i] == '\\' && i + 1 < s.Length) { sb.Append(s[i]).Append(s[i + 1]); i += 2; continue; }
                sb.Append(s[i]); i++;
            }
            i++; // closing quote
            return sb.ToString();
        }

        private static void SkipValue(string s, ref int i)
        {
            if (i >= s.Length) return;

            if (s[i] == '"') { ReadString(s, ref i); return; }

            if (s[i] == '{' || s[i] == '[')
            {
                char open = s[i], close = open == '{' ? '}' : ']';
                int depth = 0;
                bool inStr = false;
                while (i < s.Length)
                {
                    char c = s[i];
                    if (inStr)
                    {
                        if (c == '\\') { i += 2; continue; }
                        if (c == '"') inStr = false;
                    }
                    else
                    {
                        if (c == '"') inStr = true;
                        else if (c == open) depth++;
                        else if (c == close) { depth--; if (depth == 0) { i++; return; } }
                    }
                    i++;
                }
                return;
            }

            while (i < s.Length && s[i] != ',' && s[i] != '}' && s[i] != ']') i++;
        }
    }
}
