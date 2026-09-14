using System;

namespace GildedFate.Combat
{
    // One identity for the card-to-HUD journey, its resting icon and future pulses.
    // No gameplay state or card definitions are stored in this presentation catalog.
    public static class PowerIconCatalog
    {
        public readonly struct Icon
        {
            public readonly string resource;
            public readonly int tile,columns,rows;
            public Icon(string resource,int tile,int columns,int rows)
            {this.resource=resource;this.tile=tile;this.columns=columns;this.rows=rows;}
        }
        private static readonly string[] Legacy={"BATTLE TEMPER","IRON BLOOD","LIVING ARMOR","WAR MACHINE","UNBREAKABLE SPIRIT","PATIENT WARRIOR","RETRIBUTION","HOLD THE LINE","RELENTLESS","ONSLAUGHT","INDOMITABLE","SIGIL MASTERY","GRAND CONVERGENCE","ASHES","DEATH'S GAZE","EMBRACE THE VOID","DAMNATION","BATTLE RHYTHM","RESERVE ENERGY","TACTICAL ADVANTAGE","RESOURCEFUL","OVERFLOW","CHAIN REACTION","AGAINST ALL ODDS","PERFECT FORM"};
        private static readonly string[] Reaper={"DEATH'S EMBRACE","DEATH MARCH","GRAVEKEEPER","ENDLESS HARVEST","DEATH INCARNATE","GRIM ASCENSION","SOULBOUND TOME","ETERNAL SOULS","REAPER'S CALLING","SOUL DOMINION"};
        private static readonly string[] Martial={"GILDED WARLORD","UNMOVABLE","RELENTLESS CONQUEST","BRAND OF RUIN","BEYOND THE VEIL"};
        public static string Title(string name)=>(name??"").Trim().TrimEnd('+').ToUpperInvariant();
        public static bool TryGet(string title,out Icon icon)
        {
            title=Title(title);var i=Array.IndexOf(Reaper,title);
            if(i>=0){icon=new Icon("Art/UI/MasterPolish/ReaperPowers",i,4,3);return true;}
            i=Array.IndexOf(Legacy,title);
            if(i>=0){icon=new Icon("Art/CombatReadabilityAtlas_64",16+i,8,8);return true;}
            i=Array.IndexOf(Martial,title);
            if(i>=0){icon=new Icon("Art/Powers/MartialOccult",i,3,2);return true;}
            i=title=="GOLDEN THREAD"?7:title=="HIDDEN POTENTIAL"?9:-1;
            if(i>=0){icon=new Icon("Art/Powers/RemainingExpansion",i,4,3);return true;}
            icon=default;return false;
        }
    }
}

