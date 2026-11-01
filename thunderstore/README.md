# Map Menu Plus

**See what a map looks like before you load it.** Map Menu Plus replaces BONELAB's Levels page with a menu of big picture cards. Each card shows the map's picture from mod.io, its name, author, downloads and rating.

## Features

- **Picture cards**: 9 maps per page, each with its mod.io thumbnail, name, author, download count and rating. A card lifts and lights up when you point at it.
- **Details page**: click a card to see a big picture and **all of the mod's screenshots**. Flip through them with the arrows, or let them change by themselves. It also shows the author, pack, last update, downloads, % liked and the description.
  - **PLAY** starts the map.
  - **FAVORITE** saves it to your favorites.
  - It shows how often and how long ago you played it.
- **Library**: All Maps, Favorites, Recently Played, Not Played Yet, Mod Maps and BONELAB Maps, each with a count.
- **Search** by map name, pack or author, using an on-screen keyboard.
- **Sort** by A-Z, Popular (downloads), Top Rated, Newest or Last Played.
- **SURPRISE ME** opens a random map from the list you are looking at.
- **YOU ARE IN** shows the map you are playing right now.
- **Fusion**: as a lobby guest the button says **REQUEST MAP** and the host gets your request, the same as the game's own page does.
- **CLASSIC** switches back to the game's own Levels page. A **NEW MAP MENU** button brings this menu back.
- BONELAB's own maps get a coloured card with their name and a BONELAB badge.

## mod.io pictures

The pictures and info come from mod.io, through the **same mod.io login BONELAB already uses** for its mod browser (and that Fusion uses to download lobby mods).

- If you have ever downloaded a mod inside the game, you are already logged in.
- If the menu says *"Log in to mod.io in BONELAB"*, log in once in the game's mod.io browser.
- The login is only used to **read** public info about the maps you have installed. Nothing on your account is changed.
- Pictures and info are saved on your PC (`UserData/MapMenuPlus/modio`), so they show instantly next time, even offline.
- Mods marked *explicit* on mod.io show "18+ picture hidden" unless you turn on **Show 18+ pictures**.
- Maps installed by hand (not through the game) are looked up on mod.io by their exact name. Most are found.

## Settings

Open the menu, then **Preferences -> BoneMenu -> Map Menu Plus**:

| Setting | What it does |
|---|---|
| New map menu | On / off (same as the CLASSIC button) |
| Pictures + info from mod.io | Off = no internet use, coloured cards only |
| Show 18+ pictures | Off by default |
| Check mod.io connection | Shows whether you are logged in and how many maps were found |
| Download map info again | Gets fresh downloads, ratings and pictures from mod.io |

## Requirements

- MelonLoader
- BoneLib
- Fusion is **not** needed, but works with it.

## Problems?

The log is at `UserData/MapMenuPlus/log.txt`. It says what was found and what went wrong.
