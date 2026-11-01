using System;
using System.Collections.Generic;
using System.Text;

namespace MapMenuPlus
{
    /// <summary>What mod.io says about one mod (one pack of maps): name, author, numbers, pictures.</summary>
    internal sealed class ModInfo
    {
        internal long Id;
        internal string Name, NameId, Summary, Author, ProfileUrl;
        internal long Downloads, Subscribers;
        internal int RatingPct = -1;          // % positive ratings, -1 = not rated yet
        internal int RatingsTotal;
        internal double Weighted;             // mod.io's weighted rating 0..1 (better for sorting than the plain %)
        internal string RatingText;           // "Very Positive", ...
        internal int Maturity;                // bits: 1 alcohol, 2 drugs, 4 violence, 8 explicit
        internal long DateAdded, DateUpdated, DateLive;   // unix seconds
        internal string Logo320, Logo640, Logo1280;
        internal long FileSize;
        internal readonly List<string> Gallery = new List<string>();        // screenshots, big (1280x720)
        internal readonly List<string> Tags = new List<string>();
        internal string Raw;                  // the JSON as mod.io sent it (saved in the cache)

        /// <summary>The "explicit" maturity flag: these pictures are hidden unless "Show 18+ pictures" is on.</summary>
        internal bool Explicit { get { return (Maturity & 8) != 0; } }

        /// <summary>Newest of the dates (for "Newest" sorting).</summary>
        internal long Newest { get { return Math.Max(DateLive, Math.Max(DateAdded, DateUpdated)); } }

        /// <summary>The picture for a map card (640x360, falls back to the other sizes).</summary>
        internal string CardPicture { get { return Logo640 ?? Logo1280 ?? Logo320; } }

        /// <summary>The pictures of the details view: the logo first, then the screenshots (big sizes).</summary>
        internal List<string> BigPictures()
        {
            var l = new List<string>();
            string logo = Logo1280 ?? Logo640 ?? Logo320;
            if (logo != null) l.Add(logo);
            foreach (string g in Gallery) if (g != null && !l.Contains(g)) l.Add(g);
            return l;
        }

        internal static ModInfo FromJson(JNode m, string raw)
        {
            if (m == null || !m.IsObj) return null;
            long id = m["id"].Long;
            if (id <= 0) return null;
            var i = new ModInfo
            {
                Id = id,
                Name = Clean(m["name"].Str),
                NameId = m["name_id"].Str,
                Summary = Clean(m["summary"].Str),
                ProfileUrl = m["profile_url"].Str,
                Maturity = m["maturity_option"].Int,
                DateAdded = m["date_added"].Long,
                DateUpdated = m["date_updated"].Long,
                DateLive = m["date_live"].Long,
                Raw = raw,
            };
            JNode by = m["submitted_by"];
            i.Author = Clean(by["username"].Str ?? by["name_id"].Str);

            JNode stats = m["stats"];
            i.Downloads = stats["downloads_total"].Long;
            i.Subscribers = stats["subscribers_total"].Long;
            i.RatingsTotal = stats["ratings_total"].Int;
            i.RatingText = stats["ratings_display_text"].Str;
            i.Weighted = stats["ratings_weighted_aggregate"].Num;
            if (stats["ratings_percentage_positive"].Exists && i.RatingsTotal > 0) i.RatingPct = Math.Max(0, Math.Min(100, stats["ratings_percentage_positive"].Int));

            JNode logo = m["logo"];
            i.Logo320 = Url(logo["thumb_320x180"].Str);
            i.Logo640 = Url(logo["thumb_640x360"].Str);
            i.Logo1280 = Url(logo["thumb_1280x720"].Str);
            if (i.Logo1280 == null) i.Logo1280 = Url(logo["original"].Str);

            foreach (JNode img in m["media"]["images"].Items)
            {
                string u = Url(img["thumb_1280x720"].Str) ?? Url(img["original"].Str) ?? Url(img["thumb_320x180"].Str);
                if (u != null && i.Gallery.Count < 12) i.Gallery.Add(u);
            }
            foreach (JNode t in m["tags"].Items)
            {
                string n = t["name"].Str;
                if (!string.IsNullOrEmpty(n)) i.Tags.Add(n);
            }
            i.FileSize = m["modfile"]["filesize"].Long;
            return i;
        }

        private static string Url(string u)
        {
            if (string.IsNullOrEmpty(u)) return null;
            return u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? u : null;
        }

        /// <summary>Text for the menu: no line breaks or tabs, no rich-text tags.</summary>
        internal static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = Text.StripTags(s);
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c) || char.IsControl(c)) { space = sb.Length > 0; continue; }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }

    /// <summary>Little text helpers (rich-text tags, comparing names, numbers like 12.3K).</summary>
    internal static class Text
    {
        /// <summary>Removes TextMeshPro rich-text tags like &lt;color=red&gt; (many map names have them).</summary>
        internal static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
            var sb = new StringBuilder(s.Length);
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '<')
                {
                    int end = s.IndexOf('>', i + 1);
                    if (end > i && end - i <= 40 && LooksLikeTag(s, i + 1, end)) { i = end + 1; continue; }
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString().Trim();
        }

        private static bool LooksLikeTag(string s, int from, int to)
        {
            if (from >= to) return false;
            int k = from;
            if (s[k] == '/') k++;
            if (k >= to) return false;
            return char.IsLetter(s[k]) || s[k] == '#';
        }

        /// <summary>For comparing names: lower case letters and digits only ("Big_Rooms Pack!" = "bigroomspack").</summary>
        internal static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = StripTags(s);
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            return sb.ToString();
        }

        /// <summary>1234 -> "1.2K", 1234567 -> "1.2M".</summary>
        internal static string Short(long n)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (n < 1000) return n.ToString(ci);
            if (n < 10000) return (n / 1000.0).ToString("0.0", ci) + "K";
            if (n < 1000000) return (n / 1000).ToString(ci) + "K";
            if (n < 10000000) return (n / 1000000.0).ToString("0.0", ci) + "M";
            return (n / 1000000).ToString(ci) + "M";
        }

        /// <summary>"3 Mar 2025" from unix seconds.</summary>
        internal static string Date(long unix)
        {
            if (unix <= 0) return "";
            try
            {
                DateTime d = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
                return d.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch { return ""; }
        }

        /// <summary>"today", "yesterday", "5 days ago", "3 weeks ago", ... from unix seconds.</summary>
        internal static string Ago(long unix, long now)
        {
            if (unix <= 0) return "";
            long d = (now - unix) / 86400;
            if (d <= 0) return "today";
            if (d == 1) return "yesterday";
            if (d < 14) return d + " days ago";
            if (d < 60) return (d / 7) + " weeks ago";
            if (d < 730) return (d / 30) + " months ago";
            return (d / 365) + " years ago";
        }
    }
}
