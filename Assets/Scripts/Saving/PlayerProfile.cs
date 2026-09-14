using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace GildedFate.Saving
{
    [Serializable] public sealed class PlayerProfile
    {
        public int runsPlayed,wins,losses,highestDamage,mostBlock,cardsPlayed,enemiesDefeated,elitesDefeated,bossesDefeated,goldCollected,relicsCollected;
        public float fastestVictory;
        public List<string> completedEncounterReceipts=new();
        public List<string> completedRunIds=new();
        public bool RegisterCompletedRun(string id)
        {
            completedRunIds??=new List<string>();
            if(string.IsNullOrEmpty(id)||completedRunIds.Contains(id))return false;
            completedRunIds.Add(id);if(completedRunIds.Count>2048)completedRunIds.RemoveRange(0,1024);
            return true;
        }
        public float master=.8f,music=.7f,effects=.85f,ui=.8f,cardAnimationSpeed=1f; public int fpsLimit=120,antiAliasing=4,textureQuality=0;
        public bool fullscreen=true,vSync=true,screenShake=true,damageNumbers=true,tooltips=true,fastMode=false,reduceFlashing=false,reduceMotion=false,highContrastIntents=false,colorblindStatus=false;
        public bool largeCardText=false,largeIntents=false,largeEffectIcons=false,largeDamageNumbers=false,reducedVfx=false,highContrastUi=false;
    }
    public static class ProfileService
    {
        private static string FileName=>Path.Combine(SaveService.DirectoryPath,"gilded_fate_profile.json");
        public static PlayerProfile Load()=>AtomicSaveFile.TryRead(FileName,json=>JsonUtility.FromJson<PlayerProfile>(json),p=>p!=null&&p.runsPlayed>=0&&p.wins>=0&&p.losses>=0,out var profile,out _,out _)?profile:new PlayerProfile();
        public static bool Save(PlayerProfile p)
        {
            if(AtomicSaveFile.TryWrite(FileName,JsonUtility.ToJson(p,true),out var error))return true;
            Debug.LogWarning("Profile save failed: "+error);return false;
        }
    }
}
