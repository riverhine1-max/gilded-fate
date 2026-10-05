using System;
using GildedFate.Core;

namespace GildedFate.Map
{
    // Per-run meta state: Fate Debt modifiers, Daily Run identity and the small
    // counters used for run history, scoring and achievements. Plain fields so
    // JsonUtility saves them; older saves load with Fate Debt 0 and no daily.
    public sealed partial class RunModel
    {
        public int fateDebt,fateDebtMask;
        public string dailyDate="";
        public int elitesThisRun,bossesThisRun,highestHitThisRun,gildsThisRun,mostBlockThisRun;
        public bool sandbox;
        // Act themes rolled at run start (index 0 = Act 1). Older saves have none and keep the Vault roster.
        public System.Collections.Generic.List<string> actThemes=new();
        public string lastEncounterId="";
        public string ThemeForAct(int forAct)=>actThemes!=null&&forAct>=1&&forAct<=actThemes.Count?actThemes[forAct-1]??"":ActThemes.Vault;
        public string CurrentTheme=>ThemeForAct(act);
        public void RollActThemes(){actThemes=new();for(var a=1;a<=3;a++)actThemes.Add(ActThemes.Roll(a,seed));}
        // Themed elite for a map node (stable for the node) and the theme's boss.
        public string ThemedEliteFor(int nodeFloor,int nodeLane)
        {
            var pool=ThemeRosters.Elites(CurrentTheme);if(pool.Length==0)return "";var hash=unchecked((uint)seed*2654435761u^(uint)(act*7349+nodeFloor*397+nodeLane*71));hash^=hash>>16;
            return pool[hash%(uint)pool.Length];
        }
        public string ThemedBoss=>ThemeRosters.Boss(CurrentTheme);

        public bool IsDaily=>!string.IsNullOrEmpty(dailyDate);
        public bool Debt(int modifier)=>modifier>=1&&modifier<=FateDebt.Count&&(fateDebtMask&(1<<(modifier-1)))!=0;
        public float RestHealFraction=>Debt(5)?.2f:.3f;
        public int RunFloorNumber=>(Math.Max(1,Math.Min(3,act))-1)*FloorCount+floor+1;

        public void ResetRunMeta()
        {
            fateDebt=fateDebtMask=0;dailyDate="";sandbox=false;RollActThemes();lastEncounterId="";
            elitesThisRun=bossesThisRun=highestHitThisRun=gildsThisRun=mostBlockThisRun=0;
        }
        public void CopyRunMeta(RunModel source)
        {
            if(source==null)return;
            fateDebt=source.fateDebt;fateDebtMask=source.fateDebtMask;dailyDate=source.dailyDate??"";sandbox=source.sandbox;
            actThemes=new System.Collections.Generic.List<string>(source.actThemes??new());lastEncounterId=source.lastEncounterId??"";
            elitesThisRun=source.elitesThisRun;bossesThisRun=source.bossesThisRun;highestHitThisRun=source.highestHitThisRun;
            gildsThisRun=source.gildsThisRun;mostBlockThisRun=source.mostBlockThisRun;
        }
        // Starting penalties. Call once, right after NewRun.
        public void ApplyFateDebtStart()
        {
            if(Debt(9)){maxHp=(int)Math.Round(maxHp*.9);hp=maxHp;}
            if(Debt(4))
            {
                var curses=new System.Collections.Generic.List<string>();
                foreach(var c in GameContent.Cards)if(c.origin==CardOrigin.Curse&&c.id!="doom")curses.Add(c.id);
                if(curses.Count>0)AddCard(curses[(int)((uint)seed%(uint)curses.Count)]);
            }
            SyncLegacyDeck();
        }
    }
}
