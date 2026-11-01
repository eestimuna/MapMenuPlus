using System;
using BoneLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(MapMenuPlus.MapMenuPlusMod), "MapMenuPlus", "1.0.0", "community", null)]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace MapMenuPlus
{
    /// <summary>
    /// Map Menu Plus: a new Levels page for BONELAB. Big picture cards with each map's mod.io picture, name, author,
    /// downloads and rating; a details view with all of the mod's screenshots, its description and PLAY; favorites,
    /// recently played, search, sort, SURPRISE ME. CLASSIC switches back to the game's own page.
    /// Open the menu (B / Y) -> Levels.
    /// </summary>
    public class MapMenuPlusMod : MelonMod
    {
        private const string Version = "1.0.0";

        private MapMenu _menu;
        private float _nextErrLog, _nextMenuTry;

        public override void OnInitializeMelon()
        {
            Log.Init();
            string dataDir = null;
            try
            {
                dataDir = System.IO.Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "MapMenuPlus");
                System.IO.Directory.CreateDirectory(dataDir);
            }
            catch { dataDir = null; }

            try { Settings.Load(); }
            catch (Exception e) { Log.Error("Loading the settings failed (using the defaults)", e); }
            Bank.Prepare();   // the menu's pictures are drawn in the background now

            _menu = new MapMenu(dataDir);
            Settings.Changed = () => { try { _menu.SettingsChanged(); } catch (Exception e) { Log.Error("Applying a setting failed", e); } };
            Settings.ClearPictures = () => _menu.ClearPictures();
            Settings.StatusText = () => _menu.StatusLine();

            ModIo.Enabled = Settings.UseModIo;
            string mods = null;
            try { mods = Levels.ModsFolder(); } catch { }
            ModIo.Start(dataDir, mods);
            ReadToken();

            Patches.Apply(HarmonyInstance);
            TryMenu();
            Hooking.OnLevelLoaded += (info) =>
            {
                try
                {
                    Log.Info("Level loaded: " + info.title + " (" + info.barcode + ").");
                    _menu.Reset();
                    Patches.Reset();
                    _menu.LevelLoaded(info.barcode, info.title);
                    ReadToken();
                    TryMenu();
                }
                catch (Exception e) { Log.Error("Level reset failed", e); }
            };

            Log.Info("MapMenuPlus " + Version + " initialised (mods folder: " + (mods ?? "not found") + ").");
            LoggerInstance.Msg("Loaded " + Version + ". Open the menu -> Levels. Log: UserData/MapMenuPlus/log.txt");
        }

        private static void ReadToken()
        {
            try
            {
                string where;
                string token = Levels.ReadToken(out where);
                ModIo.SetToken(token);
                Log.Info("mod.io login: " + (token != null ? "found (" + where + ")" : "not found - log in to mod.io in BONELAB for map pictures"));
            }
            catch (Exception e) { Log.Error("Reading the mod.io login failed", e); }
        }

        private void TryMenu()
        {
            try
            {
                if (!Settings.BuildMenu()) _nextMenuTry = Time.realtimeSinceStartup + 2f;
                else _nextMenuTry = 0f;
            }
            catch (Exception e)
            {
                Log.Error("BoneMenu page failed", e);
                _nextMenuTry = 0f;
            }
        }

        public override void OnUpdate()
        {
            float now = Time.realtimeSinceStartup;
            try
            {
                if (_nextMenuTry > 0f && now >= _nextMenuTry) TryMenu();
                _menu.Update(now, Patches.Find(now));
            }
            catch (Exception e)
            {
                if (now >= _nextErrLog)
                {
                    _nextErrLog = now + 5f;
                    Log.Error("Update failed", e);
                }
            }
        }

        public override void OnApplicationQuit()
        {
            try { Log.Info(ModIo.Summary()); } catch { }
            try { ModIo.Quit(); } catch { }
        }
    }
}
