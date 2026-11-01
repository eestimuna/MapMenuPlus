using System;
using System.Collections.Generic;
using System.IO;

namespace MapMenuPlus
{
    /// <summary>A list of barcodes kept in a text file (favorites, recent).</summary>
    internal sealed class IdStore
    {
        private readonly List<string> _ids = new List<string>();
        private readonly string _path;
        private readonly int _max;

        internal IdStore(string path, int max)
        {
            _path = path;
            _max = max;
            try
            {
                if (_path != null && File.Exists(_path))
                    foreach (string line in File.ReadAllLines(_path))
                    {
                        string id = line.Trim();
                        if (id.Length > 0 && !Contains(id) && _ids.Count < _max) _ids.Add(id);
                    }
            }
            catch { }
        }

        internal bool Exists { get { try { return _path != null && File.Exists(_path); } catch { return false; } } }
        internal List<string> Ids { get { return new List<string>(_ids); } }

        internal bool Contains(string id)
        {
            foreach (string x in _ids) if (string.Equals(x, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>Recent list: move to the front. Returns true when the list changed.</summary>
        internal bool PushFront(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (_ids.Count > 0 && string.Equals(_ids[0], id, StringComparison.OrdinalIgnoreCase)) return false;
            _ids.RemoveAll(x => string.Equals(x, id, StringComparison.OrdinalIgnoreCase));
            _ids.Insert(0, id);
            if (_ids.Count > _max) _ids.RemoveRange(_max, _ids.Count - _max);
            Save();
            return true;
        }

        internal void SetAll(IEnumerable<string> ids)
        {
            _ids.Clear();
            foreach (string id in ids) if (!string.IsNullOrEmpty(id) && !Contains(id) && _ids.Count < _max) _ids.Add(id);
            Save();
        }

        private void Save()
        {
            try { if (_path != null) File.WriteAllLines(_path, _ids); } catch { }
        }
    }

    /// <summary>
    /// When each map was last played and how often (UserData\MapMenuPlus\played.txt, one line per map:
    /// barcode TAB unix-time TAB count). Gives "Recently Played" and "last played 3 days ago".
    /// </summary>
    internal sealed class PlayLog
    {
        private readonly Dictionary<string, long> _last = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _count = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly string _path;

        internal PlayLog(string path)
        {
            _path = path;
            try
            {
                if (_path != null && File.Exists(_path))
                    foreach (string line in File.ReadAllLines(_path))
                    {
                        string[] p = line.Split('\t');
                        long t;
                        int n;
                        if (p.Length < 2 || p[0].Length == 0 || !long.TryParse(p[1], out t)) continue;
                        _last[p[0]] = t;
                        _count[p[0]] = p.Length >= 3 && int.TryParse(p[2], out n) ? Math.Max(1, n) : 1;
                    }
            }
            catch { }
        }

        internal int Count { get { return _last.Count; } }

        internal void Played(string barcode, long unix)
        {
            if (string.IsNullOrEmpty(barcode)) return;
            _last[barcode] = unix;
            int n;
            _count[barcode] = _count.TryGetValue(barcode, out n) ? n + 1 : 1;
            Save();
        }

        internal long LastPlayed(string barcode)
        {
            long t;
            return barcode != null && _last.TryGetValue(barcode, out t) ? t : 0;
        }

        internal int Times(string barcode)
        {
            int n;
            return barcode != null && _count.TryGetValue(barcode, out n) ? n : 0;
        }

        /// <summary>Barcodes, last played first.</summary>
        internal List<string> Recent()
        {
            var l = new List<KeyValuePair<string, long>>(_last);
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            var ids = new List<string>(l.Count);
            foreach (var kv in l) ids.Add(kv.Key);
            return ids;
        }

        private void Save()
        {
            if (_path == null) return;
            try
            {
                var lines = new List<string>(_last.Count);
                foreach (var kv in _last)
                {
                    int n;
                    _count.TryGetValue(kv.Key, out n);
                    lines.Add(kv.Key + "\t" + kv.Value + "\t" + Math.Max(1, n));
                }
                File.WriteAllLines(_path, lines);
            }
            catch { }
        }
    }
}
