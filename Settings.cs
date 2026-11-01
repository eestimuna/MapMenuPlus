using System;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using MelonLoader;
using UnityEngine;

namespace MapMenuPlus
{
    /// <summary>
    /// Settings (UserData\MelonPreferences.cfg, section [MapMenuPlus]) and the BoneMenu page
    /// (open the menu -> Preferences -> BoneMenu -> Map Menu Plus).
    /// </summary>
    internal static class Settings
    {
        internal static bool NewMenu = true;
        internal static bool UseModIo = true;
        internal static bool ShowMature;

        internal static Action Changed;          // the menu redraws
        internal static Action ClearPictures;    // "Download pictures again"
        internal static Func<string> StatusText; // "Check mod.io"

        private static MelonPreferences_Category _cat;
        private static MelonPreferences_Entry<bool> _newMenu, _modio, _mature;
        private static bool _built;

        internal static void Load()
        {
            _cat = MelonPreferences.CreateCategory("MapMenuPlus");
            _newMenu = _cat.CreateEntry("NewMapMenu", true);
            _modio = _cat.CreateEntry("PicturesFromModIo", true);
            _mature = _cat.CreateEntry("Show18PlusPictures", false);
            NewMenu = _newMenu.Value;
            UseModIo = _modio.Value;
            ShowMature = _mature.Value;
        }

        internal static void SetNewMenu(bool on)
        {
            NewMenu = on;
            if (_newMenu != null) { _newMenu.Value = on; Save(); }
        }

        private static void Save()
        {
            try { _cat.SaveToFile(false); } catch (Exception e) { Log.Warn("Saving the settings failed: " + e.Message); }
        }

        /// <summary>The BoneMenu page. False if BoneMenu is not ready yet (tried again later).</summary>
        internal static bool BuildMenu()
        {
            if (_built) return true;
            if (Page.Root == null) return false;
            _built = true;
            Page page = Page.Root.CreatePage("Map Menu Plus", new Color(0.26f, 0.6f, 1f, 1f));
            page.CreateBool("New map menu", new Color(0.26f, 0.6f, 1f, 1f), NewMenu, v =>
            {
                SetNewMenu(v);
                Log.Info("BoneMenu: new map menu = " + v);
                if (Changed != null) Changed();
            });
            page.CreateBool("Pictures + info from mod.io", Color.green, UseModIo, v =>
            {
                UseModIo = v;
                _modio.Value = v;
                Save();
                ModIo.Enabled = v;
                Log.Info("BoneMenu: mod.io = " + v);
                if (Changed != null) Changed();
            });
            page.CreateBool("Show 18+ pictures", new Color(1f, 0.45f, 0.45f, 1f), ShowMature, v =>
            {
                ShowMature = v;
                _mature.Value = v;
                Save();
                Log.Info("BoneMenu: show 18+ pictures = " + v);
                if (Changed != null) Changed();
            });
            page.CreateFunction("Check mod.io connection", Color.yellow, () =>
            {
                string s = StatusText != null ? StatusText() : ModIo.Summary();
                Log.Info("BoneMenu: check: " + s);
                Tell("Map Menu Plus", s, 6f);
            });
            page.CreateFunction("Download map info again", new Color(1f, 0.6f, 0.2f, 1f), () =>
            {
                ModIo.ForgetInfo();
                if (ClearPictures != null) ClearPictures();
                Log.Info("BoneMenu: map info cleared, downloading again.");
                Tell("Map Menu Plus", "Map info is downloaded again from mod.io.", 3f);
            });
            Log.Info("BoneMenu page \"Map Menu Plus\" added.");
            return true;
        }

        internal static void Tell(string title, string message, float seconds)
        {
            try
            {
                Notifier.Send(new Notification
                {
                    Title = title,
                    Message = message,
                    ShowTitleOnPopup = true,
                    PopupLength = seconds,
                    Type = NotificationType.Information,
                });
            }
            catch (Exception e) { Log.Warn("Notification failed: " + e.Message); }
        }
    }
}
