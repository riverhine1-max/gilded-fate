using System.Collections.Generic;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool IsRunInspectionPaused=>hudNavigationIndex>=0||combatHudInspectActive||routeInspectionOpen||screen==ScreenMode.Collection&&viewingRunDeck
            ||screen==ScreenMode.Settings||screen==ScreenMode.Combat&&(combatPauseOpen||pileOpen>=0||inspectedCard!=null)
            ||screen==ScreenMode.Map&&mapPauseOpen||runPauseOpen;
        private readonly List<(float time,SigilKind[] slots)> sigilStateBeats=new();
        private readonly List<(float time,int slot)> sigilPulseBeats=new();
        private readonly List<(float time,int value)> resonanceBeats=new();
        private readonly List<(float time,CombatEvent fact)> statusVisualBeats=new();
        private SigilKind[] presentedSigils=System.Array.Empty<SigilKind>();
        private int resonanceTarget;
        private float playerHealthFill,enemyHealthFill,playerHealGlow,enemyHealGlow;
        private bool drawingDisabledHandCard;
        private float shardFrameShatterAt=-1;
        private readonly float[] shardSourceY={52,296,542,780};
        private readonly float[] shardApertureTop={104,350,597,840};
        private readonly float[] shardApertureBottom={187,433,679,928};

        private void ResetMasterPolishPlayback()
        {
            InvalidateEnemyIntents();
            combatHudInspectActive=false;combatHudFocusKey=null;combatHudTargets.Clear();
            retaliateReturns.Clear();sigilStateBeats.Clear();sigilPulseBeats.Clear();resonanceBeats.Clear();statusVisualBeats.Clear();relicPulseBeats.Clear();
            presentedSigils=combat?.sigils.ToArray()??System.Array.Empty<SigilKind>();
            resonanceTarget=combat?.resonance??0;shardFrameShatterAt=-1;
            playerHealthFill=combat?.player.hp??0;enemyHealthFill=combat?.enemy.hp??0;playerHealGlow=enemyHealGlow=0;
        }
        private void UpdateHealingPresentation(float dt)
        {
            playerHealGlow=Mathf.Max(0,playerHealGlow-dt*2.7f);enemyHealGlow=Mathf.Max(0,enemyHealGlow-dt*2.7f);
            if(displayedPlayerHp>playerHealthFill+.1f)playerHealGlow=.6f;
            if(displayedEnemyHp>enemyHealthFill+.1f)enemyHealGlow=.6f;
            playerHealthFill=HealthFillMotion.Step(playerHealthFill,displayedPlayerHp,dt,profile.reduceMotion);
            enemyHealthFill=HealthFillMotion.Step(enemyHealthFill,displayedEnemyHp,dt,profile.reduceMotion);
            if(GroupCombat)foreach(var enemy in opponentVisuals)
            {
                enemy.healGlow=Mathf.Max(0,enemy.healGlow-dt*2.7f);
                if(enemy.hp>enemy.fill+.1f)enemy.healGlow=.6f;
                enemy.fill=HealthFillMotion.Step(enemy.fill,enemy.hp,dt,profile.reduceMotion);
            }
        }
        private void ScheduleMasterPolishReceipt(CombatEvent fact,float at)
        {
            if(fact.sigils!=null)sigilStateBeats.Add((at,fact.sigils));
            if(fact.sigilSlot>=0&&fact.kind==CombatEventKind.Status&&fact.label.StartsWith("TRIGGER:SIGIL "))
                sigilPulseBeats.Add((at,fact.sigilSlot));
            if(fact.kind==CombatEventKind.Resonance)resonanceBeats.Add((at,fact.amount));
        }
        private void UpdateMasterPolishPlayback(float now)
        {
            while(statusVisualBeats.Count>0&&statusVisualBeats[0].time<=now)
            {
                var fact=statusVisualBeats[0].fact;statusVisualBeats.RemoveAt(0);
                PlayHexerStatus(fact);PlayVanguardStatus(fact);PlayReaperStatus(fact);
                var positive=fact.playerSide&&fact.amount>0;
                var effect=fact.label=="BURN"?2:positive?run.hero==HeroId.Vanguard?4:run.hero==HeroId.Reaper?5:3:3;
                if(fact.playerSide){playerVfxIndex=effect;playerVfxTime=.32f;heroBuff=1;}
                else if(!GroupCombat){enemyVfxIndex=effect;enemyVfxTime=.32f;foeBuff=1;}
            }
            while(sigilStateBeats.Count>0&&sigilStateBeats[0].time<=now)
            {presentedSigils=sigilStateBeats[0].slots;sigilStateBeats.RemoveAt(0);}
            while(resonanceBeats.Count>0&&resonanceBeats[0].time<=now)
            {resonanceTarget=resonanceBeats[0].value;resonanceBeats.RemoveAt(0);resonancePulse=1;}
            sigilPulseBeats.RemoveAll(b=>now>b.time+.26f);
            relicPulseBeats.RemoveAll(b=>now>b.time+.38f);
            retaliateReturns.RemoveAll(b=>now>=b.landAt);
            if(!combatBusy&&sigilStateBeats.Count==0)
            {
                if(!presentedSigils.SequenceEqual(combat.sigils))presentedSigils=combat.sigils.ToArray();
                if(resonanceBeats.Count==0)resonanceTarget=combat.resonance;
            }
        }
        private Rect HexerSigilArea
        {
            get
            {
                var hero=HeroPortraitRect;
                var available=Mathf.Min(408,Mathf.Max(304,(hero.center.x-112)*2));
                return new Rect(hero.center.x-available*.5f,hero.y-85,available,62);
            }
        }
        private void DrawHexerResonance()
        {
            var area=HexerResonanceArea;var pulse=profile.reduceMotion?0:resonancePulse;
            var bob=profile.reduceMotion?0:Mathf.Sin(Time.unscaledTime*1.4f)*1.5f*(1-pulse);
            var size=40*(1+pulse*.14f);var icon=new Rect(area.x+(40-size)*.5f,area.y+bob+(40-size)*.5f,size,size);
            if(!resonanceRune)resonanceRune=CreateCardUiPolygon("Resonance split prism",96,128,new[]{new Vector2(.5f,0),new Vector2(.87f,.5f),new Vector2(.5f,1),new Vector2(.12f,.5f)},true);
            DrawCardUiShape(icon,resonanceRune,new Color(.81f,.68f,1f));
            DrawCardUiShape(InsetCardRect(icon,5),resonanceRune,new Color(.16f,.09f,.25f));
            DrawCardUiShape(InsetCardRect(icon,12),resonanceRune,new Color(1f,.85f,.50f));
            var number=new Rect(area.x+44,area.y+bob,area.width-44,40);
            var style=ReadableStyle(22+Mathf.RoundToInt(pulse*3),true);style.alignment=TextAnchor.MiddleLeft;
            style.normal.textColor=Color.Lerp(new Color(.97f,.94f,1f),new Color(1f,.85f,.50f),profile.reduceFlashing?0:pulse);
            // Keep the integer, not a rounded float (large counts lose precision).
            GUI.Label(number,resonanceTarget.ToString(),style);
            GUI.Label(new Rect(area.x,area.yMax+1,area.width,16),"RESONANCE",new GUIStyle(footerStyle){fontSize=10,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.84f,.79f,.9f)}});
            var detail=resonanceTarget+" Resonance.\n"+RuleKeywords.First(k=>k.term=="Resonance").detail;
            RegisterCombatHudTarget("hero:resonance",1,area,"RESONANCE",detail);
            if(CombatInspectionAllowed&&area.Contains(combatPointer))SetCombatEffectTooltip("RESONANCE",detail,area.center);
        }
        private Texture2D resonanceRune;
        private Rect HexerResonanceArea=>new(HeroPortraitRect.xMax+18,HeroPortraitRect.yMax-46,Mathf.Max(90,48+resonanceTarget.ToString().Length*17),42);
        private Rect SigilSlotRect(int index,float visible=-1)
        {
            var area=HexerSigilArea;var size=SigilLayout.Size(combat.SigilCapacity,area.width);
            var center=SigilLayout.Center(index,visible<0?visibleSigilSlots:visible,size,area.center.x);
            return new Rect(center-size*.5f,area.y,size,size);
        }
        private static Color SigilAccent(SigilKind kind)=>kind switch
        {
            SigilKind.Ember=>new Color(1f,.55f,.28f),SigilKind.Hex=>new Color(.78f,.53f,1f),
            SigilKind.Ruin=>new Color(1f,.36f,.34f),SigilKind.Wither=>new Color(.64f,.79f,.38f),
            SigilKind.Grave=>new Color(.38f,.86f,.75f),SigilKind.Mirror=>new Color(.95f,.96f,.93f),
            _=>new Color(.49f,.8f,1f)
        };
        private Vector2 ShardHealthOrigin=>new Vector2(HeroPortraitRect.xMax+16,HeroPortraitRect.yMax+19);

        private void DrawFateHealthTreatment(Rect hp)
        {
            var active=run.shards.FirstOrDefault(s=>s.active&&s.id==combat.activeShardId);
            var shattering=shardFrameShatterAt>=0?Time.unscaledTime-shardFrameShatterAt:-1;
            if(active==null&&(shattering<0||shattering>1))return;
            var art=LoadAuthoredArt("Art/UI/MasterPolish/FateHealth");if(!art)return;
            var state=shattering>=0?3:active.activeFractured?2:active.uses>=2?1:0;
            var fade=shattering<0?1:1-Mathf.SmoothStep(0,1,shattering);
            // Three horizontal slices: endcaps retain their proportions; only the
            // quiet thread rails stretch. Actual transparent aperture is aligned to HP.
            var top=shardApertureTop[state];var bottom=shardApertureBottom[state];
            var scale=hp.height/(bottom-top);
            var sourceTop=shardSourceY[state];var sourceHeight=190f;
            var topOffset=(top-sourceTop)*scale;
            var dest=new Rect(hp.x-120*scale,hp.y-topOffset,hp.width+240*scale,sourceHeight*scale);
            var old=GUI.color;GUI.color=new Color(1,1,1,fade);
            DrawFateHealthSlice(art,new Rect(dest.x,dest.y,120*scale,dest.height),new Rect(0,sourceTop,120,sourceHeight));
            DrawFateHealthSlice(art,new Rect(hp.x,dest.y,hp.width,dest.height),new Rect(120,sourceTop,1296,sourceHeight));
            DrawFateHealthSlice(art,new Rect(hp.xMax,dest.y,120*scale,dest.height),new Rect(1416,sourceTop,120,sourceHeight));
            GUI.color=old;
            if(shattering>=0)return;
            var pulse=0f;foreach(var flight in shardFlights)
                if(!flight.shatter&&Time.unscaledTime>=flight.start)
                    pulse=Mathf.Max(pulse,Mathf.Clamp01(1-(Time.unscaledTime-flight.start)/.20f));
            var breath=profile.reduceMotion||profile.reduceFlashing?0:(Mathf.Sin(shimmer*1.4f)+1)*.06f;
            var tint=state==2?new Color(1f,.57f,.23f):new Color(.93f,.76f,.43f);
            var opacity=(.14f+breath+pulse*.56f)*fade;
            // Tiny motion outside the HP window; never draw through its numerals.
            var head=Mathf.Repeat(shimmer*.075f,1);
            if(!profile.reducedVfx&&!profile.reduceMotion)
            {
                var x=hp.x+hp.width*head;var length=8+pulse*12;
                DrawLine(new Vector2(x,hp.y-4),new Vector2(Mathf.Min(hp.xMax,x+length),hp.y-4),new Color(tint.r,tint.g,tint.b,opacity),1.5f);
            }
            if(pulse>0)DrawLine(new Vector2(hp.xMax+7,hp.y-4),new Vector2(hp.xMax+21,hp.center.y),new Color(tint.r,tint.g,tint.b,opacity),2);
            var inspect=new Rect(hp.xMax+2,hp.y-11,30,hp.height+22);
            if(CombatInspectionAllowed&&inspect.Contains(combatPointer))
            {
                var def=WorldContent.FateShards.FirstOrDefault(s=>s.id==active.id);
                if(def!=null)SetCombatEffectTooltip(def.name.ToUpperInvariant(),
                    (state==2?"FRACTURED":state==1?"WEAKENED · SECOND USE":"STABLE")+"\n"+(active.activeFractured?def.fracturedText:def.stableText),inspect.center);
            }
        }
        private static void DrawFateHealthSlice(Texture2D texture,Rect destination,Rect pixels)
        {
            GUI.DrawTextureWithTexCoords(destination,texture,new Rect(pixels.x/texture.width,
                1-(pixels.y+pixels.height)/texture.height,pixels.width/texture.width,pixels.height/texture.height),true);
        }
    }
}
