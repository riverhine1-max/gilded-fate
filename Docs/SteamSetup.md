# Steam setup: cloud saves and achievements

The game runs without Steam. Achievements are tracked in the profile, and the Steam calls do nothing until you complete steps 2–3 below.

## 1. Cloud saves (Steam Auto-Cloud, no code needed)

Do this in Steamworks: App Admin → Cloud → Auto-Cloud.

1. Set the byte quota to 10 MB and the file count to 20.
2. Add a Root Path:

   | Field | Value |
   |---|---|
   | Root | `WinAppDataLocalLow` |
   | Subdirectory | `Gilded Fate/Gilded Fate` |
   | Pattern | `gilded_fate_*.json*` |
   | OS | Windows |

   The subdirectory is your Unity Company Name / Product Name. If you change either in Player Settings, update it here too.
3. (Optional) Add root overrides for macOS and Linux if you ship those builds.
4. Publish the changes.

Each save is written with a `.bak` backup next to it. The pattern above includes the backups.

## 2. Steamworks.NET (achievements)

1. Import Steamworks.NET from https://github.com/rlabrecque/Steamworks.NET (Unity package).
2. Create `steam_appid.txt` next to the built `.exe`. It holds only your App ID. Keep it for testing; Steam supplies the ID for store builds.
3. In Player Settings → Other Settings → Scripting Define Symbols, add `GILDED_STEAM`.

`Assets/Scripts/Saving/GildedSteam.cs` then starts Steam, unlocks achievements and syncs any achievements the player earned before Steam was connected.

## 3. Achievement API names

In Steamworks → Stats & Achievements, create one achievement per ID in `Docs/MetaProgression.md`. The ID (for example `ACH_FIRST_ASCENT`) is the API Name.

Display names and descriptions are in `Assets/Scripts/Core/AchievementCatalog.cs`. Steam needs a 256×256 icon, plus a greyed-out locked version, for each achievement.

## 4. Store and community links

Fill in these constants at the top of `Assets/Scripts/UI/GildedMetaProgress.cs`:

```csharp
SteamPageUrl = "https://store.steampowered.com/app/YOUR_APP_ID/";
DiscordUrl   = "";
FeedbackUrl  = "";
```

A main-menu link only appears once its URL is set. Also bump `GameVersion` for each release. When it changes, players see the "What's New" panel once.
