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
        // ---- added settings (older profiles load these defaults) ----
        public int gameSpeed=0,windowMode=1,resolutionWidth=0,resolutionHeight=0,padPromptStyle=0;
        public float brightness=1f;
        public bool instantEnemyTurns=false,confirmEndTurn=true,muteInBackground=true,showPadPrompts=true,displayMigrated=false;
        public string lastSeenVersion="";
        // ---- meta progression ----
        public int[] heroMarks=new int[3];public int totalMarks;
        public int[] fateDebtUnlocked=new int[3];public int[] fateDebtBestWin={-1,-1,-1};
        public int[] winsByHero=new int[3];
        public int totalGilds,dailyRunsCompleted;
        public List<string> bossesDefeatedIds=new();
        public List<string> achievements=new();
        public List<RunRecord> runHistory=new();
        public List<DailyRecord> dailyRecords=new();
        public void EnsureMeta()
        {
            // Profiles from before the Window Mode setting only stored a fullscreen flag.
            if(!displayMigrated){windowMode=fullscreen?1:2;displayMigrated=true;}
            if(heroMarks==null||heroMarks.Length<3)heroMarks=new int[3];
            if(fateDebtUnlocked==null||fateDebtUnlocked.Length<3)fateDebtUnlocked=new int[3];
            if(fateDebtBestWin==null||fateDebtBestWin.Length<3)fateDebtBestWin=new[]{-1,-1,-1};
            if(winsByHero==null||winsByHero.Length<3)winsByHero=new int[3];
            bossesDefeatedIds??=new List<string>();achievements??=new List<string>();runHistory??=new List<RunRecord>();dailyRecords??=new List<DailyRecord>();
            lastSeenVersion??="";
        }
    }
    [Serializable] public sealed class RunRecord
    {
        public string date,hero,killedBy,dailyDate,seed;
        public bool victory;public int fateDebt,act,floor,score,seconds,highestHit,gilds,marks;
        public List<string> deck=new(),relics=new(),shards=new();
    }
    [Serializable] public sealed class DailyRecord
    {
        public string date,hero;public bool victory;public int score,floor;
    }
    public static class ProfileService
    {
        private static string FileName=>Path.Combine(SaveService.DirectoryPath,"gilded_fate_profile.json");
        public static PlayerProfile Load(){var p=LoadRaw();p.EnsureMeta();return p;}
        private static PlayerProfile LoadRaw()=>AtomicSaveFile.TryRead(FileName,json=>JsonUtility.FromJson<PlayerProfile>(json),p=>p!=null&&p.runsPlayed>=0&&p.wins>=0&&p.losses>=0,out var profile,out _,out _)?profile:new PlayerProfile();
        // The Playground and other sandboxes never write the real profile.
        public static bool Suspended;
        public static bool Save(PlayerProfile p)
        {
            if(Suspended)return true;
            if(AtomicSaveFile.TryWrite(FileName,JsonUtility.ToJson(p,true),out var error))return true;
            Debug.LogWarning("Profile save failed: "+error);return false;
        }
    }
}
