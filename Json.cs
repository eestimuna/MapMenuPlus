using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MapMenuPlus
{
    /// <summary>
    /// A read-only JSON value (object, array, text, number, true/false, null). Missing keys give an empty value instead
    /// of an error, so "m["logo"]["thumb_640x360"].Str" is simply null when the field is not there.
    /// Own small parser (tested here), so the mod needs no JSON library at all.
    /// </summary>
    internal sealed class JNode
    {
        internal enum Kind { Null, Bool, Num, Str, Arr, Obj }

        internal Kind K;
        internal string S;
        internal double N;
        internal bool B;
        internal List<JNode> A;
        internal Dictionary<string, JNode> O;
        internal int Start, End;   // where this value is in the text (to keep its raw text)

        internal static readonly JNode Missing = new JNode { K = Kind.Null };

        internal bool Exists { get { return this != Missing && K != Kind.Null; } }
        internal bool IsObj { get { return K == Kind.Obj; } }
        internal bool IsArr { get { return K == Kind.Arr; } }

        internal JNode this[string key]
        {
            get
            {
                JNode v;
                return K == Kind.Obj && key != null && O.TryGetValue(key, out v) ? v : Missing;
            }
        }

        internal JNode this[int i] { get { return K == Kind.Arr && i >= 0 && i < A.Count ? A[i] : Missing; } }

        internal int Count { get { return K == Kind.Arr ? A.Count : (K == Kind.Obj ? O.Count : 0); } }

        internal IEnumerable<JNode> Items { get { return K == Kind.Arr ? (IEnumerable<JNode>)A : Array.Empty<JNode>(); } }

        internal IEnumerable<KeyValuePair<string, JNode>> Props
        {
            get { return K == Kind.Obj ? (IEnumerable<KeyValuePair<string, JNode>>)O : Array.Empty<KeyValuePair<string, JNode>>(); }
        }

        /// <summary>Text (numbers and true/false as text too); null for objects, arrays, null and missing.</summary>
        internal string Str
        {
            get
            {
                switch (K)
                {
                    case Kind.Str: return S;
                    case Kind.Num: return N.ToString("R", CultureInfo.InvariantCulture);
                    case Kind.Bool: return B ? "true" : "false";
                    default: return null;
                }
            }
        }

        internal double Num
        {
            get
            {
                if (K == Kind.Num) return N;
                double d;
                if (K == Kind.Str && double.TryParse(S, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
                if (K == Kind.Bool) return B ? 1 : 0;
                return 0;
            }
        }

        internal long Long { get { double d = Num; return d > long.MaxValue ? long.MaxValue : d < long.MinValue ? long.MinValue : (long)d; } }
        internal int Int { get { double d = Num; return d > int.MaxValue ? int.MaxValue : d < int.MinValue ? int.MinValue : (int)d; } }
        internal bool Bool { get { return K == Kind.Bool ? B : Num != 0; } }
    }

    internal static class Json
    {
        private const int MaxDepth = 64;

        /// <summary>Parses JSON text. Throws FormatException on broken text.</summary>
        internal static JNode Parse(string text)
        {
            if (text == null) throw new FormatException("no text");
            int i = 0;
            JNode n = Value(text, ref i, 0);
            Skip(text, ref i);
            if (i != text.Length) throw new FormatException("text after the JSON value at " + i);
            return n;
        }

        internal static bool TryParse(string text, out JNode node)
        {
            try { node = Parse(text); return true; }
            catch { node = JNode.Missing; return false; }
        }

        /// <summary>The raw text of a value (to save one mod of a list on its own).</summary>
        internal static string Raw(string text, JNode n)
        {
            if (n == null || n == JNode.Missing || n.End <= n.Start) return null;
            return text.Substring(n.Start, n.End - n.Start);
        }

        private static void Skip(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') i++;
                else break;
            }
        }

        private static JNode Value(string s, ref int i, int depth)
        {
            if (depth > MaxDepth) throw new FormatException("too deep");
            Skip(s, ref i);
            if (i >= s.Length) throw new FormatException("unexpected end");
            int start = i;
            char c = s[i];
            JNode n;
            if (c == '{') n = Obj(s, ref i, depth);
            else if (c == '[') n = Arr(s, ref i, depth);
            else if (c == '"') n = new JNode { K = JNode.Kind.Str, S = Str(s, ref i) };
            else if (c == 't' && Word(s, ref i, "true")) n = new JNode { K = JNode.Kind.Bool, B = true };
            else if (c == 'f' && Word(s, ref i, "false")) n = new JNode { K = JNode.Kind.Bool, B = false };
            else if (c == 'n' && Word(s, ref i, "null")) n = new JNode { K = JNode.Kind.Null };
            else if (c == '-' || (c >= '0' && c <= '9')) n = new JNode { K = JNode.Kind.Num, N = Number(s, ref i) };
            else throw new FormatException("unexpected '" + c + "' at " + i);
            n.Start = start;
            n.End = i;
            return n;
        }

        private static bool Word(string s, ref int i, string w)
        {
            if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) return false;
            i += w.Length;
            return true;
        }

        private static JNode Obj(string s, ref int i, int depth)
        {
            var n = new JNode { K = JNode.Kind.Obj, O = new Dictionary<string, JNode>(StringComparer.Ordinal) };
            i++;   // {
            Skip(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return n; }
            while (true)
            {
                Skip(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("expected a key at " + i);
                string key = Str(s, ref i);
                Skip(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("expected ':' at " + i);
                i++;
                n.O[key] = Value(s, ref i, depth + 1);
                Skip(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end in an object");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return n; }
                throw new FormatException("expected ',' or '}' at " + i);
            }
        }

        private static JNode Arr(string s, ref int i, int depth)
        {
            var n = new JNode { K = JNode.Kind.Arr, A = new List<JNode>() };
            i++;   // [
            Skip(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return n; }
            while (true)
            {
                n.A.Add(Value(s, ref i, depth + 1));
                Skip(s, ref i);
                if (i >= s.Length) throw new FormatException("unexpected end in a list");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return n; }
                throw new FormatException("expected ',' or ']' at " + i);
            }
        }

        private static string Str(string s, ref int i)
        {
            i++;   // "
            StringBuilder sb = null;
            int runStart = i;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"')
                {
                    string result = sb == null ? s.Substring(runStart, i - runStart) : sb.Append(s, runStart, i - runStart).ToString();
                    i++;
                    return result;
                }
                if (c == '\\')
                {
                    if (sb == null) sb = new StringBuilder();
                    sb.Append(s, runStart, i - runStart);
                    i++;
                    if (i >= s.Length) break;
                    char e = s[i];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 >= s.Length) throw new FormatException("broken \\u at " + i);
                            int code;
                            if (!int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                throw new FormatException("broken \\u at " + i);
                            sb.Append((char)code);
                            i += 4;
                            break;
                        default: throw new FormatException("unknown escape \\" + e + " at " + i);
                    }
                    i++;
                    runStart = i;
                    continue;
                }
                i++;
            }
            throw new FormatException("text without an end");
        }

        private static double Number(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            while (i < s.Length)
            {
                char c = s[i];
                if ((c >= '0' && c <= '9') || c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-') i++;
                else break;
            }
            double d;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("broken number at " + start);
            return d;
        }
    }
}
