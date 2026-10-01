// Trailer capture build only: presentation clock that follows Time.captureDeltaTime while a
// trailer is recording (so every animation renders at a locked frame rate), and the normal
// unscaled clock otherwise. prepare.sh rewrites Time.unscaledTime / unscaledDeltaTime /
// realtimeSinceStartup in the game scripts to these properties. This file is not rewritten.
namespace GildedFate.UI
{
    public static class GildedTrailerTime
    {
        public static bool Locked=>GildedTrailerDirector.Active;
        public static float Now=>Locked?UnityEngine.Time.time:UnityEngine.Time.unscaledTime;
        public static float Delta=>Locked?UnityEngine.Time.deltaTime:UnityEngine.Time.unscaledDeltaTime;
        public static float Realtime=>Locked?UnityEngine.Time.time:UnityEngine.Time.realtimeSinceStartup;
    }
}
