using UnityEngine;
#if GILDED_STEAM
using Steamworks;
#endif

namespace GildedFate.Saving
{
    // Thin Steam bridge. The game works without Steam; achievements are always
    // tracked in the profile. To connect Steam:
    //   1. Import Steamworks.NET (github.com/rlabrecque/Steamworks.NET).
    //   2. Put your App ID in steam_appid.txt next to the built .exe.
    //   3. Player Settings > Scripting Define Symbols: add GILDED_STEAM.
    // Every call below is a no-op until then.
    public static class GildedSteam
    {
#if GILDED_STEAM
        private static bool ready;
        public static void Init(){try{ready=SteamAPI.Init();}catch(System.Exception e){Debug.LogWarning("[Gilded Fate Steam] "+e.Message);ready=false;}}
        public static void Tick(){if(ready)SteamAPI.RunCallbacks();}
        public static void Shutdown(){if(ready)SteamAPI.Shutdown();ready=false;}
        public static void Unlock(string id){if(!ready)return;SteamUserStats.SetAchievement(id);SteamUserStats.StoreStats();}
        public static bool Available=>ready;
#else
        public static void Init(){}
        public static void Tick(){}
        public static void Shutdown(){}
        public static void Unlock(string id){}
        public static bool Available=>false;
#endif
    }
}
