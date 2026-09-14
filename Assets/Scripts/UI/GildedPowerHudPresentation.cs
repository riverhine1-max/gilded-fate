using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class PowerArrival
        {
            public string title;
            public float landsAt;
            public bool alreadyActive;
        }
        private readonly List<PowerArrival> powerArrivals=new();
        private Rect PlayerEffectArea(int count)
        {
            var hero=HeroPortraitRect;var size=profile.largeEffectIcons?52f:44f;
            var width=Mathf.Clamp(count*(size+5)-5,282,Mathf.Min(520,2*(hero.center.x-88)));
            return new Rect(hero.center.x-width*.5f,hero.yMax+48,width,size+2);
        }
        private Rect EffectCell(Rect area,int index,int count)
        {
            var size=profile.largeEffectIcons?52f:44f;
            var columns=EffectStripLayout.Columns(area.width,size,5);
            var shown=Mathf.Min(count,columns*EffectStripLayout.Rows(area.height,size,5));
            index=Mathf.Clamp(index,0,Mathf.Max(0,shown-1));
            return new Rect(EffectStripLayout.CellX(area.center.x,index,shown,columns,size,5),
                area.y+index/columns*(size+5),size,size);
        }
        private Rect PowerHudTarget(CardDef card)
        {
            var title=PowerIconCatalog.Title(card?.name);var chips=PlayerEffectChips();
            var index=chips.FindIndex(c=>PowerIconCatalog.Title(c.title)==title);
            // If the HUD is full the visible overflow slot owns the extra effects.
            // The destination always uses the same geometry as the real effect strip.
            return EffectCell(PlayerEffectArea(Mathf.Max(1,chips.Count)),index<0?chips.Count:index,Mathf.Max(1,chips.Count));
        }
        private float SchedulePowerArrival(CardDef card,Vector2 from,bool alreadyActive)
        {
            var delay=AnimationSeconds(.06f);var duration=AnimationSeconds(profile.reduceMotion?.12f:.42f);
            var end=Time.unscaledTime+delay+duration;var title=PowerIconCatalog.Title(card.name);
            powerArrivals.Add(new PowerArrival{title=title,landsAt=end,alreadyActive=alreadyActive});
            MoveCard(card,from,PowerHudTarget(card).center,duration,.85f,.10f,0,0,delay,absorb:true);
            powerPulseBeats.Add(("P:"+title,end));
            return delay+duration+.18f;
        }
        private float PowerArrivalOpacity(string title)
        {
            var alpha=1f;
            foreach(var arrival in powerArrivals)
                if(!arrival.alreadyActive&&arrival.title==PowerIconCatalog.Title(title))
                    alpha=Mathf.Min(alpha,Mathf.Clamp01((Time.unscaledTime-arrival.landsAt)/.12f));
            return alpha;
        }
        private bool DrawPowerHudIcon(Rect rect,string title)
        {
            if(!PowerIconCatalog.TryGet(title,out var icon))return false;
            var art=LoadAuthoredArt(icon.resource);if(!art)return false;
            DrawAtlasIcon(art,icon.tile,icon.columns,icon.rows,rect);return true;
        }
        private bool PowerIsDormant(string title)
        {
            var m=combat.memory;
            return PowerIconCatalog.Title(title) switch
            {
                "GILDED WARLORD"=>!combat.WarlordReady,
                "WAR MACHINE"=>m.warMachineUses>=m.warMachine,
                "UNBREAKABLE SPIRIT"=>presentedSpiritDirections==3,
                "ONSLAUGHT"=>m.onslaughtUsed,
                "INDOMITABLE"=>m.indomitableUsed,
                "SIGIL MASTERY"=>m.sigilMasteryUsed,
                "DEATH'S GAZE"=>m.deathsGazeUsed,
                "PERFECT FORM"=>m.firstAttackPlayed&&m.firstSkillPlayed,
                "DEATH'S EMBRACE"=>m.soulsPlayedThisTurn>=1,
                "GRAVEKEEPER"=>m.gravekeeperUsed,
                "ENDLESS HARVEST"=>m.soulsPlayedThisTurn>=2,
                "REAPER'S CALLING"=>m.reapersCallingUsed,
                _=>false
            };
        }
    }
}

