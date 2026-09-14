using System.Collections;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private bool ConfigureIdentityCapture(string mode)
        {
            if(!mode.StartsWith("identity-"))return false;
            combatTestInput=true;AudioListener.pause=true;
            if(mode=="identity-cards")
            {
                run.NewRun(HeroId.Vanguard,20260911);screen=ScreenMode.Collection;
                bindingCaptureCards=new[]{CardOrigin.Knight,CardOrigin.Arcane,CardOrigin.Reaper,CardOrigin.Wanderer}.SelectMany(origin=>new[]{Rarity.Common,Rarity.Uncommon,Rarity.Rare}.Select(rarity=>GameContent.Cards.First(c=>c.origin==origin&&c.rarity==rarity))).ToArray();return true;
            }
            if(mode=="identity-map"||mode=="identity-travel"||mode=="identity-inroom")
            {
                run.NewRun(HeroId.Vanguard,20260911);screen=ScreenMode.Map;
                foreach(var n in run.nodes){n.available=false;n.complete=false;}
                var at=run.nodes.Where(n=>n.floor==0).First();
                for(var i=0;i<5;i++){at.complete=true;at=run.nodes.First(n=>n.floor==at.floor+1&&(at.nextMask&(1<<n.lane))!=0);}
                var prior=run.nodes.First(n=>n.complete&&n.floor==4);run.floor=5;run.activeNodeFloor=4;run.activeNodeLane=prior.lane;
                foreach(var n in run.nodes.Where(n=>n.floor==5&&(prior.nextMask&(1<<n.lane))!=0))n.available=true;
                if(mode=="identity-inroom"){run.activeNodeFloor=at.floor;run.activeNodeLane=at.lane;}
                mapFocusFloor=-1;return true;
            }
            PrepareCombatCheck(mode=="identity-hexer"?"hex_strike":mode=="identity-reaper"?"soul":"executioners_cleave",5);
            combat.energy=3;
            if(mode is "identity-vanguard" or "identity-input")
            {
                var ids=new[]{"strike","executioners_cleave","defend","unbreakable_spirit","dazed_mind"};
                for(var i=0;i<5;i++){var card=GameContent.Find(ids[i]).Copy();card.instanceId=combat.hand[i].instanceId;combat.hand[i]=card;}
                combat.TakeEvents();RestoreCombatPresentation();
            }
            return true;
        }
        private IEnumerator RunIdentityChecks()
        {
            yield return new WaitForSecondsRealtime(.6f);
            CombatCheck(Resources.Load<Texture2D>("Art/Identity/RoomEmblems")!=null,"Room artifact atlas loads");
            CombatCheck(Resources.Load<Texture2D>("Art/Identity/EnergyVessels")!=null,"Energy vessel atlas loads");
            AuditCardFrameLanguage();
            CombatCheck(IdentityEnergyIndex(CardOrigin.Knight)==0&&IdentityEnergyIndex(CardOrigin.Arcane)==1&&IdentityEnergyIndex(CardOrigin.Reaper)==2,"Three character Energy identities map correctly");
            var strike=combat.hand[0];var heavy=combat.hand[1];var status=combat.hand[4];
            CombatCheck(combat.CanPlay(strike)&&!combat.PreviewCard(strike).conditionActive,"Ordinary playable card uses white state");
            CombatCheck(combat.CanPlay(heavy)&&combat.PreviewCard(heavy).conditionActive,"First Attack Heavy uses crimson state");
            CombatCheck(!combat.CanPlay(status),"Unplayable Status receives no gameplay light");
            combat.memory.attacksThisTurn=1;
            CombatCheck(!combat.PreviewCard(heavy).conditionActive,"Heavy ready state ends after another Attack");
            combat.energy=0;CombatCheck(!combat.CanPlay(heavy),"Unaffordable Heavy cannot glow even with an active condition");
            combat.memory.attacksThisTurn=0;combat.energy=3;
            var a=new Vector2(100,400);var b=new Vector2(260,180);
            CombatCheck(FateThreadPoint(a,b,0)==a&&FateThreadPoint(a,b,1)==b,"Fate Thread preserves exact graph endpoints");
            for(var i=0;i<=100;i++){var p=FateThreadPoint(a,b,i/100f);CombatCheck(p.y<=a.y&&p.y>=b.y,"Thread stays between connected floors "+i);}
            foreach(var word in new[]{"Dissipate","Dissipates","Dissipated","Dissipating","Energy","Soulbind"})CombatCheck(FormatCardRules(word).Contains(">"+word+"</color>"),"Canonical full keyword retained: "+word);
            ConfigureIdentityCapture("identity-map");
            var edges=run.nodes.Where(n=>n.complete).Sum(n=>run.nodes.Count(t=>t.complete&&t.floor==n.floor+1&&(n.nextMask&(1<<t.lane))!=0));
            CombatCheck(edges==4,"Only the four real traveled edges are braided");
            var snapshot=JsonUtility.ToJson(run);var restored=JsonUtility.FromJson<GildedFate.Map.RunModel>(snapshot);
            CombatCheck(restored.nodes.Count(n=>n.complete)==5&&restored.nodes.Count(n=>n.available)==run.nodes.Count(n=>n.available),"Route history and available connections survive serialization");
            Debug.Log("[Gilded Fate Visual Identity] "+combatInteractionChecks+" checks · "+combatInteractionFailures+" failures");
        }
    }
}
