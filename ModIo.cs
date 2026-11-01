using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MapMenuPlus
{
    /// <summary>A pack of maps (one pallet) the menu wants mod.io info for. Plain data, made on the main thread.</summary>
    internal sealed class PackKey
    {
        internal string Barcode;          // pallet barcode
        internal string Title;            // pallet title, tags removed
        internal string Author;
        internal List<string> LevelTitles = new List<string>();
        internal long ListingModId;       // mod.io id from the game's own mod listing (0 = not known)
        internal string ListingThumb;     // a picture URL from the game's own mod listing, if it has one
    }

    /// <summary>
    /// Talks to mod.io on its own background thread (never the game's thread) and keeps everything in a cache on disk
    /// (UserData\MapMenuPlus\modio), so pictures show instantly the next time, even offline.
    ///
    ///  - Login: the same mod.io login BONELAB uses for its own mod browser (the token is read from the game's mod
    ///    settings file - the same way Fusion reads it for downloading lobby mods). Without it no info is fetched, but
    ///    cached pictures and pictures from the game's own mod listings still work.
    ///  - Which mod is a pack: the game's own mod listing (installed through the game), else its *.manifest file,
    ///    else a search on mod.io by the pack's / map's exact name (checked letter by letter).
    ///  - Info for up to 100 packs in ONE request (mods?id-in=...), at most ~1.5 requests per second, waits when
    ///    mod.io says "too many requests".
    ///  - Pictures: downloaded 3 at a time, the cards you look at first, then the next pages, then all the rest.
    /// </summary>
    internal static class ModIo
    {
        internal enum State { Starting, NoLogin, Working, Ready, LoginExpired, Offline, Disabled }

        internal static string ApiBase = "https://api.mod.io/v1/games/3809/";   // BONELAB = game 3809 (tests replace this)
        internal const double ApiGap = 1.0;                // seconds between two API requests (mod.io allows ~60 a minute)
        internal const double InfoMaxAge = 3 * 86400;      // info older than this is fetched again (download counts change)
        internal const double MissingRetry = 3 * 86400;    // "not on mod.io" is checked again after this
        internal const double SearchRetry = 7 * 86400;     // "no name match" is checked again after this

        private static readonly object Gate = new object();
        private static Thread _thread, _imgThread;
        private static volatile bool _quit, _ready;
        private static readonly AutoResetEvent Wake = new AutoResetEvent(false);
        private static readonly AutoResetEvent WakeImages = new AutoResetEvent(false);

        private static string _dir, _modsDir, _imgDir;
        private static volatile string _token;
        private static volatile bool _enabled = true;
        private static volatile State _state = State.Starting;
        private static volatile string _detail = "";

        private static readonly ConcurrentDictionary<long, ModInfo> Infos = new ConcurrentDictionary<long, ModInfo>();
        private static readonly ConcurrentDictionary<long, long> Missing = new ConcurrentDictionary<long, long>();      // id -> when checked
        private static readonly ConcurrentDictionary<string, long> PackIds = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, long> SearchTried = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, long> ManifestIds = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, string> Files = new ConcurrentDictionary<string, string>();   // url -> file
        private static readonly ConcurrentDictionary<string, byte> FailedUrls = new ConcurrentDictionary<string, byte>();
        private static readonly ConcurrentDictionary<string, byte> Busy = new ConcurrentDictionary<string, byte>();

        // guarded by Gate
        private static readonly Dictionary<string, PackKey> _packs = new Dictionary<string, PackKey>(StringComparer.OrdinalIgnoreCase);
        private static List<string> _visible = new List<string>(), _soon = new List<string>();
        private static readonly List<string> _background = new List<string>();
        private static readonly HashSet<string> _backgroundSet = new HashSet<string>();

        private static int _version, _imgVersion;
        private static double _nextApi, _nextImages;
        private static int _apiCalls, _imagesDone, _imageBytes, _searches;
        private static bool _manifestsScanned, _lenient;
        private static HttpClient _http, _httpLenient;
        private static readonly System.Diagnostics.Stopwatch Clock = System.Diagnostics.Stopwatch.StartNew();
        private static double Now { get { return Clock.Elapsed.TotalSeconds; } }
        private static long Unix { get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); } }

        // ---- main thread side ---------------------------------------------------------------------------------

        /// <summary>Starts the worker. dataDir = UserData\MapMenuPlus, modsDir = the game's mod folder (for *.manifest files).</summary>
        internal static void Start(string dataDir, string modsDir)
        {
            lock (Gate)
            {
                if (_thread != null) return;
                _dir = dataDir != null ? Path.Combine(dataDir, "modio") : null;
                _imgDir = _dir != null ? Path.Combine(_dir, "img") : null;
                _modsDir = modsDir;
                _quit = false;
                _thread = new Thread(Loop) { IsBackground = true, Name = "MapMenuPlus mod.io", Priority = ThreadPriority.BelowNormal };
                _thread.Start();
                _imgThread = new Thread(ImageLoop) { IsBackground = true, Name = "MapMenuPlus pictures", Priority = ThreadPriority.BelowNormal };
                _imgThread.Start();
            }
        }

        internal static void Quit()
        {
            _quit = true;
            Wake.Set();
            WakeImages.Set();
        }

        /// <summary>The mod.io login (null = not logged in). Can be called again any time (e.g. after logging in).</summary>
        internal static void SetToken(string token)
        {
            token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
            if (token == _token) return;
            _token = token;
            if (_state == State.LoginExpired || _state == State.NoLogin || _state == State.Starting) _state = token == null ? State.NoLogin : State.Working;
            Wake.Set();
        }

        internal static bool HasToken { get { return _token != null; } }

        /// <summary>Pictures + info from mod.io on/off (BoneMenu). Off = only what is already in the cache.</summary>
        internal static bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; Wake.Set(); WakeImages.Set(); }
        }

        internal static State Status { get { return _enabled ? _state : State.Disabled; } }
        internal static string Detail { get { return _detail; } }

        /// <summary>Goes up whenever new info / pack ids arrive (the menu redraws then).</summary>
        internal static int Version { get { return Volatile.Read(ref _version); } }
        /// <summary>Goes up whenever a new picture file is ready.</summary>
        internal static int ImageVersion { get { return Volatile.Read(ref _imgVersion); } }

        internal static int ApiCalls { get { return _apiCalls; } }
        internal static int ImagesDownloaded { get { return _imagesDone; } }

        /// <summary>The packs whose maps are in the menu (the worker finds their mod.io ids and info).</summary>
        internal static void Register(IEnumerable<PackKey> packs)
        {
            lock (Gate)
            {
                foreach (PackKey p in packs)
                    if (p != null && !string.IsNullOrEmpty(p.Barcode)) _packs[p.Barcode] = p;
            }
            Wake.Set();
        }

        /// <summary>The mod.io id of a pack: &gt;0 = found, -1 = not on mod.io, 0 = not known (yet).</summary>
        internal static long ModIdOf(string palletBarcode)
        {
            long id;
            return palletBarcode != null && PackIds.TryGetValue(palletBarcode, out id) ? id : 0;
        }

        internal static ModInfo InfoOf(long modId)
        {
            ModInfo i;
            return modId > 0 && Infos.TryGetValue(modId, out i) ? i : null;
        }

        /// <summary>The downloaded file of a picture, or null while it is not there (yet).</summary>
        internal static string FileOf(string url)
        {
            string f;
            return url != null && Files.TryGetValue(url, out f) ? f : null;
        }

        internal static bool Failed(string url) { return url != null && FailedUrls.ContainsKey(url); }

        /// <summary>A picture file turned out broken (could not be shown): forget it and do not download it again.</summary>
        internal static void MarkBroken(string url)
        {
            if (url == null) return;
            string f;
            if (Files.TryRemove(url, out f)) { try { File.Delete(f); } catch { } }
            FailedUrls[url] = 1;
        }

        /// <summary>
        /// The pictures wanted now: visible = on screen (first), soon = the next pages. Replaces the last call's lists.
        /// </summary>
        internal static void Want(List<string> visible, List<string> soon)
        {
            lock (Gate)
            {
                _visible = visible != null ? new List<string>(visible) : new List<string>();
                _soon = soon != null ? new List<string>(soon) : new List<string>();
            }
            WakeImages.Set();
        }

        /// <summary>Pictures to download some time (all map cards), after the wanted ones.</summary>
        internal static void WantLater(IEnumerable<string> urls)
        {
            lock (Gate)
            {
                foreach (string u in urls)
                    if (u != null && _backgroundSet.Add(u)) _background.Add(u);
            }
            WakeImages.Set();
        }

        /// <summary>BoneMenu "Download map info again": forgets the info + search results (pictures stay).</summary>
        internal static void ForgetInfo()
        {
            Infos.Clear();
            Missing.Clear();
            SearchTried.Clear();
            foreach (var kv in PackIds) if (kv.Value < 0) { long ignored; PackIds.TryRemove(kv.Key, out ignored); }
            try
            {
                if (_dir != null)
                {
                    string mods = Path.Combine(_dir, "mods");
                    if (Directory.Exists(mods)) foreach (string f in Directory.GetFiles(mods, "*.json")) File.Delete(f);
                    File.Delete(Path.Combine(_dir, "missing.txt"));
                    SaveIds();
                }
            }
            catch (Exception e) { Log.Warn("Clearing the mod.io cache: " + e.Message); }
            Interlocked.Increment(ref _version);
            if (_state == State.Ready) _state = State.Working;
            Wake.Set();
        }

        // ---- worker ------------------------------------------------------------------------------------------

        private static void Loop()
        {
            try { Setup(); }
            catch (Exception e) { Log.Error("mod.io: setting up failed", e); }
            _ready = true;
            while (!_quit)
            {
                bool busy = false;
                try { busy = Step(); }
                catch (Exception e)
                {
                    Log.Error("mod.io worker step failed", e);
                    Thread.Sleep(1000);
                }
                if (!busy) Wake.WaitOne(400);
            }
        }

        /// <summary>The picture downloads, on their own thread (they never wait for the slower info requests).</summary>
        private static void ImageLoop()
        {
            while (!_quit && !_ready) Thread.Sleep(100);
            while (!_quit)
            {
                bool busy = false;
                try { busy = _enabled && Now >= _nextImages && DownloadImages(); }
                catch (Exception e)
                {
                    Log.Error("mod.io picture step failed", e);
                    Thread.Sleep(1000);
                }
                if (!busy) WakeImages.WaitOne(400);
            }
        }

        private static void Setup()
        {
            _http = MakeClient(false);
            if (_dir != null)
            {
                Directory.CreateDirectory(_dir);
                Directory.CreateDirectory(_imgDir);
                Directory.CreateDirectory(Path.Combine(_dir, "mods"));
            }
            LoadCache();
            _ready = true;
            Interlocked.Increment(ref _version);
            Interlocked.Increment(ref _imgVersion);
        }

        private static HttpClient MakeClient(bool lenient)
        {
            var h = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            if (lenient) h.ServerCertificateCustomValidationCallback = (_, __, ___, ____) => true;
            var c = new HttpClient(h) { Timeout = TimeSpan.FromSeconds(20) };
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MapMenuPlus/1.0 (BONELAB mod)");
            c.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, image/*");
            return c;
        }

        /// <summary>One round of work. True when something was done (the next round starts at once).</summary>
        private static bool Step()
        {
            if (!_enabled) { return false; }
            if (!_manifestsScanned) { _manifestsScanned = true; ScanManifests(); }
            bool did = Resolve();

            // API work (needs the login)
            string token = _token;
            if (token == null) SetState(State.NoLogin, "");
            else if (_state != State.LoginExpired && Now >= _nextApi)
            {
                if (FetchInfos(token)) did = true;
                else if (Now >= _nextApi && SearchOne(token)) did = true;
                else if (_state == State.Working || _state == State.Starting || _state == State.NoLogin) SetState(State.Ready, "");
            }

            return did;
        }

        private static void SetState(State s, string detail)
        {
            if (_state != s || _detail != detail)
            {
                _state = s;
                _detail = detail ?? "";
                Interlocked.Increment(ref _version);
            }
        }

        /// <summary>Pack ids from the game's listing / the manifest files.</summary>
        private static bool Resolve()
        {
            bool changed = false;
            List<PackKey> packs;
            lock (Gate) packs = new List<PackKey>(_packs.Values);
            foreach (PackKey p in packs)
            {
                long known;
                if (PackIds.TryGetValue(p.Barcode, out known) && known > 0) continue;
                long id = p.ListingModId > 0 ? p.ListingModId : 0;
                long m;
                if (id <= 0 && ManifestIds.TryGetValue(p.Barcode, out m)) id = m;
                if (id > 0)
                {
                    PackIds[p.Barcode] = id;
                    changed = true;
                }
                if (p.ListingThumb != null)
                    lock (Gate) if (_backgroundSet.Add(p.ListingThumb)) _background.Add(p.ListingThumb);
            }
            if (changed)
            {
                SaveIds();
                Interlocked.Increment(ref _version);
            }
            return false;   // cheap: not counted as work
        }

        // ---- info ---------------------------------------------------------------------------------------------

        private static bool FetchInfos(string token)
        {
            long unix = Unix;
            var need = new List<long>();
            foreach (var kv in PackIds)
            {
                long id = kv.Value;
                if (id <= 0 || need.Contains(id)) continue;
                long when;
                if (Missing.TryGetValue(id, out when) && unix - when < MissingRetry) continue;
                ModInfo info;
                if (Infos.TryGetValue(id, out info) && unix - FetchedAt(id) < InfoMaxAge) continue;
                need.Add(id);
                if (need.Count >= 100) break;
            }
            if (need.Count == 0) return false;

            SetState(State.Working, "map info");
            string url = ApiBase + "mods?id-in=" + string.Join(",", need) + "&_limit=100";
            int code;
            string body = ApiGet(url, token, out code);
            if (body == null) return code != 0;
            JNode root;
            if (!Json.TryParse(body, out root)) { Log.Warn("mod.io: broken answer for " + need.Count + " mods"); _nextApi = Now + 30; return true; }
            var got = new HashSet<long>();
            foreach (JNode m in root["data"].Items)
            {
                ModInfo info = ModInfo.FromJson(m, Json.Raw(body, m));
                if (info == null) continue;
                Store(info);
                got.Add(info.Id);
            }
            Log.Info("mod.io: info for " + got.Count + " of " + need.Count + " packs.");
            // not in the list (hidden, deleted, or the list leaves them out): asked one by one
            foreach (long id in need)
            {
                if (got.Contains(id)) continue;
                if (_quit || Now < _nextApi - 5) break;
                string one = ApiGet(ApiBase + "mods/" + id, token, out code);
                JNode m;
                if (one != null && Json.TryParse(one, out m))
                {
                    ModInfo info = ModInfo.FromJson(m, one);
                    if (info != null) { Store(info); continue; }
                }
                bool gone = code == 404 || code == 410 || code == 403 || one != null;   // one != null: answered, but not a mod
                if (gone)
                {
                    Missing[id] = Unix;
                    SaveMissing();
                }
                if (code == 401 || code == 429 || code == 0) break;
            }
            Interlocked.Increment(ref _version);
            return true;
        }

        private static void Store(ModInfo info)
        {
            Infos[info.Id] = info;
            long ignored;
            Missing.TryRemove(info.Id, out ignored);
            if (_dir == null || info.Raw == null) return;
            try { File.WriteAllText(Path.Combine(_dir, "mods", info.Id + ".json"), info.Raw, Encoding.UTF8); }
            catch (Exception e) { Log.Warn("mod.io: saving info " + info.Id + ": " + e.Message); }
        }

        private static long FetchedAt(long id)
        {
            if (_dir == null) return Unix;
            try
            {
                string f = Path.Combine(_dir, "mods", id + ".json");
                if (!File.Exists(f)) return 0;
                return new DateTimeOffset(File.GetLastWriteTimeUtc(f)).ToUnixTimeSeconds();
            }
            catch { return 0; }
        }

        /// <summary>A pack the game knows no mod.io id for: search mod.io for its exact name (one pack per call).</summary>
        private static bool SearchOne(string token)
        {
            PackKey pick = null;
            long unix = Unix;
            lock (Gate)
            {
                foreach (PackKey p in _packs.Values)
                {
                    long id;
                    if (PackIds.TryGetValue(p.Barcode, out id) && id != 0) continue;
                    long when;
                    if (SearchTried.TryGetValue(p.Barcode, out when) && unix - when < SearchRetry) continue;
                    pick = p;
                    break;
                }
            }
            if (pick == null) return false;

            SetState(State.Working, "finding packs");
            var names = new List<string>();
            AddName(names, pick.Title);
            foreach (string t in pick.LevelTitles) AddName(names, t);
            long found = 0;
            int code = 0;
            foreach (string name in names)
            {
                string q = name.Replace("*", " ").Replace("%", " ").Trim();
                if (q.Length < 2) continue;
                string body = ApiGet(ApiBase + "mods?name-lk=" + Uri.EscapeDataString(q) + "&_limit=10", token, out code);
                if (body == null) { if (code == 0 || code == 429 || code == 401) return true; continue; }
                JNode root;
                if (Json.TryParse(body, out root)) found = PickMatch(root["data"], names, pick.Author);
                if (found > 0) break;
            }
            if (found <= 0 && names.Count > 0)
            {
                string body = ApiGet(ApiBase + "mods?_q=" + Uri.EscapeDataString(names[0]) + "&_limit=20", token, out code);
                if (body == null && (code == 0 || code == 429 || code == 401)) return true;
                JNode root;
                if (body != null && Json.TryParse(body, out root)) found = PickMatch(root["data"], names, pick.Author);
            }
            _searches++;
            SearchTried[pick.Barcode] = Unix;
            PackIds[pick.Barcode] = found > 0 ? found : -1;
            Log.Info("mod.io: pack \"" + pick.Title + "\" " + (found > 0 ? "found by name: mod " + found : "not found by name."));
            SaveIds();
            Interlocked.Increment(ref _version);
            return true;
        }

        private static void AddName(List<string> names, string n)
        {
            n = Text.StripTags(n);
            if (string.IsNullOrWhiteSpace(n)) return;
            n = n.Trim();
            foreach (string x in names) if (Text.Norm(x) == Text.Norm(n)) return;
            if (Text.Norm(n).Length >= 2) names.Add(n);
        }

        /// <summary>
        /// The mod in a search result whose name (or url name) is EXACTLY one of ours (ignoring case, spaces and signs).
        /// Several: the one by the pack's author, else the most downloaded. 0 = no match.
        /// </summary>
        internal static long PickMatch(JNode data, List<string> names, string author)
        {
            var want = new HashSet<string>();
            foreach (string n in names) { string k = Text.Norm(n); if (k.Length > 0) want.Add(k); }
            string a = Text.Norm(author);
            long best = 0, bestDl = -1;
            bool bestAuthor = false;
            foreach (JNode m in data.Items)
            {
                long id = m["id"].Long;
                if (id <= 0) continue;
                if (!want.Contains(Text.Norm(m["name"].Str)) && !want.Contains(Text.Norm(m["name_id"].Str))) continue;
                string by = Text.Norm(m["submitted_by"]["username"].Str);
                bool am = a.Length > 0 && by.Length > 0 && (a == by || a.Contains(by) || by.Contains(a));
                long dl = m["stats"]["downloads_total"].Long;
                if (best == 0 || (am && !bestAuthor) || (am == bestAuthor && dl > bestDl))
                {
                    best = id;
                    bestDl = dl;
                    bestAuthor = am;
                }
            }
            return best;
        }

        /// <summary>One GET to the API with the login. null = failed (code = HTTP status, 0 = no connection).</summary>
        private static string ApiGet(string url, string token, out int code)
        {
            code = 0;
            double wait = _nextApi - Now;
            if (wait > 0 && wait < 3) Thread.Sleep((int)(wait * 1000));
            _nextApi = Now + ApiGap;
            Interlocked.Increment(ref _apiCalls);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
                        using (HttpResponseMessage res = Client().SendAsync(req).GetAwaiter().GetResult())
                        {
                            code = (int)res.StatusCode;
                            string body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            if (res.IsSuccessStatusCode)
                            {
                                if (_state == State.Offline) SetState(State.Working, "");
                                return body;
                            }
                            if (code == 401)
                            {
                                SetState(State.LoginExpired, "");
                                Log.Warn("mod.io: the login was refused (401) - log in to mod.io again in BONELAB.");
                            }
                            else if (code == 429)
                            {
                                double after = 60;
                                try
                                {
                                    if (res.Headers.RetryAfter != null)
                                    {
                                        if (res.Headers.RetryAfter.Delta.HasValue) after = res.Headers.RetryAfter.Delta.Value.TotalSeconds;
                                        else if (res.Headers.RetryAfter.Date.HasValue) after = (res.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds;
                                    }
                                }
                                catch { }
                                after = Math.Max(5, Math.Min(600, after));
                                _nextApi = Now + after;
                                Log.Info("mod.io: too many requests, waiting " + after.ToString("0") + " s.");
                            }
                            else if (code >= 500) _nextApi = Now + 30;
                            else if (code != 404 && code != 410) Log.Info("mod.io: HTTP " + code + " for " + Short(url));
                            return null;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (attempt == 0 && !_lenient && IsTls(e))
                    {
                        _lenient = true;
                        Log.Warn("mod.io: the secure connection check failed (" + e.GetBaseException().Message + "), using the same relaxed check Fusion uses.");
                        continue;
                    }
                    code = 0;
                    _nextApi = Now + 30;
                    _nextImages = Now + 30;
                    SetState(State.Offline, "");
                    Log.Info("mod.io: no connection (" + e.GetBaseException().Message + "), trying again in 30 s.");
                    return null;
                }
            }
            return null;
        }

        private static HttpClient Client()
        {
            if (!_lenient) return _http;
            if (_httpLenient == null) _httpLenient = MakeClient(true);
            return _httpLenient;
        }

        private static bool IsTls(Exception e)
        {
            for (Exception x = e; x != null; x = x.InnerException)
                if (x is AuthenticationException) return true;
            return false;
        }

        private static string Short(string url)
        {
            int q = url.IndexOf('?');
            return q > 0 ? url.Substring(0, q) : url;
        }

        // ---- pictures -----------------------------------------------------------------------------------------

        private static bool DownloadImages()
        {
            var batch = new List<string>();
            List<string> visible, soon, background;
            lock (Gate)
            {
                visible = _visible;               // replaced (never changed) by Want: safe to read outside the lock
                soon = _soon;
                background = new List<string>(_background);
            }
            Collect(visible, batch, 3);
            Collect(soon, batch, 3);
            Collect(background, batch, 3);
            if (batch.Count == 0) return false;
            var tasks = new List<Task<bool>>();
            foreach (string u in batch)
            {
                Busy[u] = 1;
                tasks.Add(Task.Run(() => DownloadOne(u)));
            }
            try { Task.WaitAll(tasks.ToArray()); } catch { }
            foreach (string u in batch) { byte ignored; Busy.TryRemove(u, out ignored); }
            Interlocked.Increment(ref _imgVersion);
            if (_state == State.Ready || _state == State.NoLogin) Interlocked.Increment(ref _version);
            return true;
        }

        private static void Collect(List<string> from, List<string> into, int max)
        {
            foreach (string u in from)
            {
                if (into.Count >= max) return;
                if (u == null || Files.ContainsKey(u) || FailedUrls.ContainsKey(u) || Busy.ContainsKey(u) || into.Contains(u)) continue;
                string f = CachedFile(u);
                if (f != null) { Files[u] = f; continue; }
                into.Add(u);
            }
        }

        private static bool DownloadOne(string url)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    using (HttpResponseMessage res = Client().GetAsync(url).GetAwaiter().GetResult())
                    {
                        if (!res.IsSuccessStatusCode)
                        {
                            int code = (int)res.StatusCode;
                            if (code == 404 || code == 403 || code == 410) FailedUrls[url] = 1;
                            else _nextImages = Now + 10;
                            return false;
                        }
                        byte[] bytes = res.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                        string ext = Kind(bytes);
                        if (ext == null || bytes.Length > 12 * 1024 * 1024)
                        {
                            FailedUrls[url] = 1;
                            Log.Info("mod.io: picture is not a JPG/PNG (" + bytes.Length + " bytes): " + url);
                            return false;
                        }
                        string file = _imgDir != null ? Path.Combine(_imgDir, Hash(url) + ext) : null;
                        if (file != null)
                        {
                            string tmp = file + ".part";
                            File.WriteAllBytes(tmp, bytes);
                            if (File.Exists(file)) File.Delete(file);
                            File.Move(tmp, file);
                            Files[url] = file;
                        }
                        Interlocked.Increment(ref _imagesDone);
                        Interlocked.Add(ref _imageBytes, bytes.Length);
                        return true;
                    }
                }
                catch (Exception e)
                {
                    if (attempt == 0 && !_lenient && IsTls(e)) { _lenient = true; continue; }
                    _nextImages = Now + 15;
                    Log.Info("mod.io: picture download failed (" + e.GetBaseException().Message + ").");
                    return false;
                }
            }
            return false;
        }

        /// <summary>".jpg" / ".png" from the first bytes, null for anything the game cannot show (GIF, WebP, ...).</summary>
        internal static string Kind(byte[] b)
        {
            if (b == null || b.Length < 8) return null;
            if (b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return ".jpg";
            if (b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return ".png";
            return null;
        }

        /// <summary>A short stable file name for a URL.</summary>
        internal static string Hash(string url)
        {
            using (var sha = System.Security.Cryptography.SHA1.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(url));
                var sb = new StringBuilder(24);
                for (int i = 0; i < 12; i++) sb.Append(h[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static string CachedFile(string url)
        {
            if (_imgDir == null) return null;
            string h = Hash(url);
            string f = Path.Combine(_imgDir, h + ".jpg");
            if (File.Exists(f)) return f;
            f = Path.Combine(_imgDir, h + ".png");
            return File.Exists(f) ? f : null;
        }

        // ---- cache on disk ------------------------------------------------------------------------------------

        private static void LoadCache()
        {
            if (_dir == null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int infos = 0, ids = 0;
            try
            {
                foreach (string f in Directory.GetFiles(Path.Combine(_dir, "mods"), "*.json"))
                {
                    try
                    {
                        string raw = File.ReadAllText(f, Encoding.UTF8);
                        JNode m;
                        if (!Json.TryParse(raw, out m)) continue;
                        ModInfo i = ModInfo.FromJson(m, raw);
                        if (i != null) { Infos[i.Id] = i; infos++; }
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.Warn("mod.io cache (info): " + e.Message); }
            try
            {
                string f = Path.Combine(_dir, "ids.txt");
                if (File.Exists(f))
                    foreach (string line in File.ReadAllLines(f))
                    {
                        string[] p = line.Split('\t');
                        long id, when;
                        if (p.Length < 2 || !long.TryParse(p[1], out id) || p[0].Length == 0) continue;
                        if (id < 0)
                        {
                            if (p.Length >= 3 && long.TryParse(p[2], out when)) SearchTried[p[0]] = when;
                            if (p.Length >= 3 && long.TryParse(p[2], out when) && Unix - when < SearchRetry) PackIds[p[0]] = -1;
                        }
                        else if (id > 0) { PackIds[p[0]] = id; ids++; }
                    }
                f = Path.Combine(_dir, "missing.txt");
                if (File.Exists(f))
                    foreach (string line in File.ReadAllLines(f))
                    {
                        string[] p = line.Split('\t');
                        long id, when;
                        if (p.Length >= 2 && long.TryParse(p[0], out id) && long.TryParse(p[1], out when)) Missing[id] = when;
                    }
            }
            catch (Exception e) { Log.Warn("mod.io cache (ids): " + e.Message); }
            int pics = 0;
            long bytes = 0;
            try
            {
                var all = new List<FileInfo>(new DirectoryInfo(_imgDir).GetFiles());
                foreach (FileInfo fi in all)
                {
                    if (fi.Name.EndsWith(".part", StringComparison.OrdinalIgnoreCase)) { try { fi.Delete(); } catch { } continue; }
                    pics++;
                    bytes += fi.Length;
                }
                // keep the picture cache below ~400 MB: the oldest go first
                if (bytes > 400L * 1024 * 1024)
                {
                    all.Sort((a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                    foreach (FileInfo fi in all)
                    {
                        if (bytes <= 300L * 1024 * 1024) break;
                        try { bytes -= fi.Length; fi.Delete(); pics--; } catch { }
                    }
                }
            }
            catch { }
            sw.Stop();
            Log.Info("mod.io cache: " + infos + " packs with info, " + ids + " known pack ids, " + pics + " pictures (" + (bytes / (1024 * 1024)) + " MB), read in "
                     + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms.");
        }

        private static readonly object SaveGate = new object();

        private static void SaveIds()
        {
            if (_dir == null) return;
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in PackIds)
                {
                    long when;
                    SearchTried.TryGetValue(kv.Key, out when);
                    sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\t').Append(when).Append('\n');
                }
                lock (SaveGate) File.WriteAllText(Path.Combine(_dir, "ids.txt"), sb.ToString());
            }
            catch { }
        }

        private static void SaveMissing()
        {
            if (_dir == null) return;
            try
            {
                var sb = new StringBuilder();
                foreach (var kv in Missing) sb.Append(kv.Key).Append('\t').Append(kv.Value).Append('\n');
                lock (SaveGate) File.WriteAllText(Path.Combine(_dir, "missing.txt"), sb.ToString());
            }
            catch { }
        }

        /// <summary>
        /// The game writes a *.manifest file (JSON) for every installed pack; for packs from mod.io it holds the mod
        /// id. Read once at the start (background), as a second way to find the id.
        /// </summary>
        private static void ScanManifests()
        {
            if (string.IsNullOrEmpty(_modsDir) || !Directory.Exists(_modsDir)) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int files = 0, found = 0;
            try
            {
                var list = new List<string>(Directory.GetFiles(_modsDir, "*.manifest", SearchOption.TopDirectoryOnly));
                foreach (string d in Directory.GetDirectories(_modsDir))
                    try { list.AddRange(Directory.GetFiles(d, "*.manifest", SearchOption.TopDirectoryOnly)); } catch { }
                foreach (string f in list)
                {
                    if (files++ > 3000) break;
                    try
                    {
                        if (new FileInfo(f).Length > 2 * 1024 * 1024) continue;
                        JNode root;
                        if (!Json.TryParse(File.ReadAllText(f), out root)) continue;
                        string barcode;
                        long id;
                        if (ManifestId(root, out barcode, out id)) { ManifestIds[barcode] = id; found++; }
                    }
                    catch { }
                }
            }
            catch (Exception e) { Log.Warn("Reading the mod manifests failed: " + e.Message); }
            sw.Stop();
            Log.Info("Mod manifests: " + files + " read, " + found + " with a mod.io id (" + sw.Elapsed.TotalMilliseconds.ToString("0") + " ms).");
        }

        /// <summary>The pallet barcode and mod.io id in a manifest's JSON (key names searched loosely).</summary>
        internal static bool ManifestId(JNode root, out string barcode, out long id)
        {
            barcode = null;
            id = 0;
            if (root == null || !root.IsObj) return false;
            foreach (var kv in root.Props)
            {
                string k = kv.Key.ToLowerInvariant();
                if ((k == "palletbarcode" || k == "pallet") && kv.Value.K == JNode.Kind.Str) barcode = kv.Value.S;
            }
            if (barcode == null)
            {
                JNode b = Find(root, "barcode", 2);
                if (b != null && b.K == JNode.Kind.Str) barcode = b.S;
                else if (b != null && b.IsObj && b["_id"].K == JNode.Kind.Str) barcode = b["_id"].S;
                else if (b != null && b.IsObj && b["id"].K == JNode.Kind.Str) barcode = b["id"].S;
            }
            JNode mid = Find(root, "modid", 6);
            if (mid != null) id = mid.Long;
            return !string.IsNullOrEmpty(barcode) && id > 0;
        }

        private static JNode Find(JNode n, string lowerKey, int depth)
        {
            if (n == null || depth < 0) return null;
            if (n.IsObj)
            {
                foreach (var kv in n.Props)
                    if (kv.Key.ToLowerInvariant() == lowerKey && (kv.Value.K == JNode.Kind.Num || kv.Value.K == JNode.Kind.Str || kv.Value.IsObj)) return kv.Value;
                foreach (var kv in n.Props)
                {
                    JNode r = Find(kv.Value, lowerKey, depth - 1);
                    if (r != null) return r;
                }
            }
            else if (n.IsArr)
                foreach (JNode c in n.Items)
                {
                    JNode r = Find(c, lowerKey, depth - 1);
                    if (r != null) return r;
                }
            return null;
        }

        /// <summary>For the log at the end.</summary>
        internal static string Summary()
        {
            return "mod.io: " + Status + ", " + _apiCalls + " requests, " + Infos.Count + " packs with info, " + _searches + " name searches, "
                   + _imagesDone + " pictures downloaded (" + (_imageBytes / 1024) + " KB).";
        }
    }
}
