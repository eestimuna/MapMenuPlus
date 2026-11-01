using System;
using System.IO;
using MelonLoader.Utils;

namespace MapMenuPlus
{
    /// <summary>Tiny file logger: UserData\MapMenuPlus\log.txt (overwritten every game start).</summary>
    internal static class Log
    {
        private const int MaxLines = 8000;
        private static string _path;
        private static int _lines;
        private static readonly object Gate = new object();

        internal static void Init()
        {
            try
            {
                string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "MapMenuPlus");
                Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, "log.txt");
                File.WriteAllText(_path, "MapMenuPlus log, started " + DateTime.Now + Environment.NewLine);
            }
            catch { _path = null; }
        }

        internal static void Info(string msg) { Write("INFO  ", msg); }
        internal static void Warn(string msg) { Write("WARN  ", msg); }

        internal static void Error(string msg, Exception e)
        {
            Write("ERROR ", msg + (e != null ? " :: " + e : ""));
        }

        private static void Write(string level, string msg)
        {
            if (_path == null) return;
            try
            {
                lock (Gate)
                {
                    if (_lines++ >= MaxLines) return;
                    File.AppendAllText(_path, DateTime.Now.ToString("HH:mm:ss.fff") + " " + level + msg + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
