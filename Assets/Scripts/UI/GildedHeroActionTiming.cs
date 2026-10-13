using System.Collections;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool HeroVideoActionPlaying=>HexerVideoEnabled&&(hexerVideos?.ActionPlaying??false)||
            VanguardVideoEnabled&&(vanguardVideos?.ActionPlaying??false)||ReaperVideoEnabled&&(reaperVideos?.ActionPlaying??false);
        private bool HeroVideoActionReady=>HexerVideoEnabled&&(hexerVideos?.ActionReady??false)||
            VanguardVideoEnabled&&(vanguardVideos?.ActionReady??false)||ReaperVideoEnabled&&(reaperVideos?.ActionReady??false);
        private bool HeroVideoNeedsRecovery=>HeroVideoActionPlaying&&
            !(HexerVideoEnabled&&hexerVideos?.ActionName=="Defeat"||VanguardVideoEnabled&&vanguardVideos?.ActionName=="Defeat"||ReaperVideoEnabled&&reaperVideos?.ActionName=="Defeat");
        private IEnumerator WaitForHeroVideoFrame()
        {
            var remaining=10f;
            while(HeroVideoActionPlaying&&!HeroVideoActionReady&&remaining>0)
            {yield return null;if(!IsRunInspectionPaused)remaining-=Time.unscaledDeltaTime;}
        }
        private float HeroVideoSecondsToPose()
        {
            if(HexerVideoEnabled&&hexerVideos?.ActionReady==true)return hexerVideos.SecondsToPose(HeroActionTiming.PoseSeconds("Hexer",hexerVideos.ActionName));
            if(VanguardVideoEnabled&&vanguardVideos?.ActionReady==true)return vanguardVideos.SecondsToPose(HeroActionTiming.PoseSeconds("Vanguard",vanguardVideos.ActionName));
            if(ReaperVideoEnabled&&reaperVideos?.ActionReady==true)return reaperVideos.SecondsToPose(HeroActionTiming.PoseSeconds("Reaper",reaperVideos.ActionName));
            return 0;
        }
        private float HeroVideoPoseDelay(string hero,string clip,float seconds)
        {
            var duration=hero=="Vanguard"?VanguardVideoCatalog.Find(clip)?.Duration??3:hero=="Hexer"?HexerVideoCatalog.Find(clip)?.Duration??3:ReaperVideoCatalog.Find(clip)?.Duration??3;
            var speed=Mathf.Clamp(duration/Mathf.Max(.4f,seconds),.1f,10f);
            return HeroActionTiming.PoseSeconds(hero,clip)/speed;
        }
        private IEnumerator WaitForHeroVideoRecovery()
        {
            var remaining=10f;
            while(HeroVideoNeedsRecovery&&remaining>0)
            {yield return null;if(!IsRunInspectionPaused)remaining-=Time.unscaledDeltaTime;}
        }
    }
}
