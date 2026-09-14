using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    [Serializable] public sealed class RelicCombatMemory
    {
        public List<string> turnUsed=new(),combatUsed=new(),turnBuffs=new(),combatBuffs=new(),mechanics=new();
        public List<int> unwritten=new(),foresightCards=new(),ruptureOwners=new(),ruptureMasks=new(),knellOwners=new();
        public int extraDraws,exhausted,turnExhausted,attackHits,plays,turnPlays,turnAttacks,typeMask,costMask,originMask,chimeraMask,sequence,lastType=-1,fateType=-1,destined=-1,adaptationType=-1;
        public string paradoxSource="";
        public bool paradoxReady;
        public RelicCombatMemory Copy()
        {
            var c=(RelicCombatMemory)MemberwiseClone();c.turnUsed=new(turnUsed);c.combatUsed=new(combatUsed);c.turnBuffs=new(turnBuffs);c.combatBuffs=new(combatBuffs);c.mechanics=new(mechanics);
            c.unwritten=new(unwritten);c.foresightCards=new(foresightCards);c.ruptureOwners=new(ruptureOwners);c.ruptureMasks=new(ruptureMasks);c.knellOwners=new(knellOwners);return c;
        }
    }
    public sealed partial class CombatState
    {
        private RelicCombatMemory RM=>memory.relicExpansion??=new();
        private bool ValidateRelicMemory()
        {
            var r=RM;
            if(r.turnUsed==null||r.combatUsed==null||r.turnBuffs==null||r.combatBuffs==null||r.mechanics==null||r.unwritten==null||r.foresightCards==null||r.ruptureOwners==null||r.ruptureMasks==null||r.knellOwners==null)return false;
            if(r.sequence<0||r.sequence>2||r.lastType< -1||r.lastType>4||r.fateType< -1||r.fateType>2||r.adaptationType< -1||r.adaptationType>4||r.destined< -1||r.destined>nextInstanceId)return false;
            if(new[]{r.extraDraws,r.exhausted,r.turnExhausted,r.attackHits,r.plays,r.turnPlays,r.turnAttacks}.Any(n=>n<0)||r.ruptureOwners.Count!=r.ruptureMasks.Count||r.ruptureOwners.Count>4||r.unwritten.Count>3)return false;
            if(r.ruptureOwners.Any(n=>n<0||n>=EnemyCount)||r.ruptureMasks.Any(n=>n<0||n>255)||r.knellOwners.Any(n=>n<0||n>=EnemyCount))return false;
            if(r.unwritten.Concat(r.foresightCards).Any(n=>n<=0||n>nextInstanceId))return false;
            if(AllOwnedCards().Any(c=>c==null||c.perfectedGrowth<0||c.perfectedCostReduction<0||c.perfectedCostReduction>20))return false;
            return pendingPlay==null||pendingPlay.relicRuptureChecked!=null&&pendingPlay.relicRuptureTargets!=null&&pendingPlay.relicRuptureChecked.Concat(pendingPlay.relicRuptureTargets).All(n=>n>=1&&n<=EnemyCount);
        }
        [NonSerialized] private int repeatedTriggerDepth,relicDamageDepth;
        private bool HasRelic(string id)=>relics.Contains(id);
        private bool RelicOnce(string id,bool combatWide=false)
        {
            if(!HasRelic(id))return false;var used=combatWide?RM.combatUsed:RM.turnUsed;
            if(used.Contains(id))return false;used.Add(id);return true;
        }
        private void RelicTrigger(string id,Action effect)=>TriggerEffect("relic_"+id,()=>{Emit(CombatEventKind.RelicTrigger,1,true,null,id);effect();});
        private void RelicGrant(string source,string id,int n,CombatEffectDuration duration=CombatEffectDuration.Combat)
            =>RelicTrigger(source,()=>GrantEffect(player,id,n,source,duration));
        private int TakeRelicEffect(string id,bool one=false)
        {
            var e=player.effects.FirstOrDefault(x=>x.id==id);if(e==null)return 0;
            var n=one?1:e.value;TriggerEffect("consume_"+id,()=>{e.value-=n;if(e.value<=0)player.effects.Remove(e);Emit(CombatEventKind.Status,-n,true,null,id.Replace('_',' ').ToUpperInvariant());});return n;
        }
        private static int BitCount(int mask){var n=0;while(mask!=0){n+=mask&1;mask>>=1;}return n;}
        private bool Temporary(CardDef c)=>c!=null&&(c.temporary||IsSoul(c));
        private bool Started(CardDef c)=>R.startingIds.Contains(c.instanceId);
        private int OriginBit(CardDef c,bool bridge=false)
        {
            if(Temporary(c)||!Started(c))return bridge?8:16;
            if(bridge){if(c.origin==CardOrigin.Wanderer)return 2;return c.hero==hero?1:4;}
            return c.origin switch{CardOrigin.Knight=>1,CardOrigin.Arcane=>2,CardOrigin.Reaper=>4,CardOrigin.Wanderer=>8,_=>16};
        }
        private void InitRelicExpansion()
        {
            repeatedTriggerDepth=relicDamageDepth=0;
            if(HasRelic("tome_of_the_unwritten"))RelicTrigger("tome_of_the_unwritten",()=>{var pool=draw.ToList();for(var i=0;i<3&&pool.Count>0;i++){var c=pool[NextRandom(pool.Count)];pool.Remove(c);RM.unwritten.Add(c.instanceId);}GrantEffect(player,"unwritten",RM.unwritten.Count,"tome_of_the_unwritten",CombatEffectDuration.Combat);});
            if(HasRelic("fate_die"))RelicTrigger("fate_die",()=>{RM.fateType=NextRandom(3);GrantEffect(player,"fate_die",RM.fateType+1,"fate_die",CombatEffectDuration.Combat);});
        }
        private void ResetRelicTurn()
        {
            RM.turnUsed.Clear();RM.turnBuffs.Clear();RM.mechanics.Clear();RM.extraDraws=RM.turnExhausted=RM.attackHits=RM.turnPlays=RM.turnAttacks=RM.typeMask=RM.costMask=RM.originMask=RM.chimeraMask=0;
            RM.lastType=-1;RM.destined=-1;RM.paradoxReady=false;RM.paradoxSource="";RM.ruptureOwners.Clear();RM.ruptureMasks.Clear();
            foreach(var id in new[]{"fate_convergence_progress","many_paths_progress","chimera_progress","paradox"})ConsumeEffect(player,id);
        }
        private void RelicOpeningResources(ref int drawCount)
        {
            if(HasRelic("eternal_core"))RelicTrigger("eternal_core",()=>energy++);
            if(HasRelic("crown_of_insight")){drawCount+=2;RelicTrigger("crown_of_insight",()=>{});}
            if(turn%3==0&&HasRelic("golden_hourglass")){drawCount+=2;RelicTrigger("golden_hourglass",()=>energy+=2);}
            if(turn==1&&HasRelic("fates_lantern")){drawCount+=3;RelicTrigger("fates_lantern",()=>energy++);}
            if(turn==1&&HasRelic("gilded_chalice"))RelicTrigger("gilded_chalice",()=>{GainStrength(2);GainFortify(2);});
            if(HasRelic("crown_of_plenty"))RelicTrigger("crown_of_plenty",()=>GainBlock(6));
        }
        private void RelicOpeningHand()
        {
            if(HasRelic("broken_compass")&&hand.Count>0)RelicTrigger("broken_compass",()=>{RM.destined=hand[NextRandom(hand.Count)].instanceId;GrantEffect(player,"destined",1,"broken_compass",CombatEffectDuration.TurnEnd);});
            if(HasRelic("thread_of_eternity")){var pool=hand.Where(c=>!c.upgraded&&GameContent.HasUpgradePreview(c)).ToArray();if(pool.Length>0)RelicTrigger("thread_of_eternity",()=>{var c=pool[NextRandom(pool.Length)];var i=hand.IndexOf(c);hand[i]=GameContent.Upgrade(c);Emit(CombatEventKind.Status,1,true,hand[i],"COMBAT UPGRADE");});}
        }
        private int RelicCost(CardDef c,int cost)
        {
            cost-=c.perfectedCostReduction;
            if(RM.unwritten.Contains(c.instanceId))cost--;
            if(RM.foresightCards.Contains(c.instanceId))cost--;
            if(RM.fateType==(int)c.kind&&!RM.combatUsed.Contains("fate_die"))cost--;
            if(c.kind==CardKind.Power)cost-=EffectValue(player,"ribbon_discount");
            cost-=EffectValue(player,"destined_discount");
            if(EffectValue(player,"many_paths_free")>0)cost=0;
            return cost;
        }
        private int ReserveBonus(CardDef c,string id,bool applicable=true,bool one=false)=>applicable?TakeRelicEffect(id,one):0;
        private void BeforeRelicPlay(CardDef c)
        {
            var p=pendingPlay;var damage=GivesDamage(c);var block=GivesBlock(c);var applicable=damage||block;var printed=PrintedCost(c);
            p.relicOriginMask=OriginBit(c);p.relicBridgeOrigins=OriginBit(c,true);
            if(RM.fateType==(int)c.kind&&RelicOnce("fate_die",true))RelicTrigger("fate_die",()=>ConsumeEffect(player,"fate_die"));
            RM.foresightCards.Remove(c.instanceId);
            if(c.kind==CardKind.Power)TakeRelicEffect("ribbon_discount");TakeRelicEffect("destined_discount");TakeRelicEffect("many_paths_free",true);
            if(c.kind==CardKind.Attack){p.relicDamage+=TakeRelicEffect("relic_next_attack");p.relicDamagePercent+=25*TakeRelicEffect("ferocity",true);p.relicPhantom=TakeRelicEffect("phantom_edge",true)>0;}
            var flat=ReserveBonus(c,"cracked_ready",applicable&&printed>=2)+ReserveBonus(c,"bloodthread_ready",applicable)+ReserveBonus(c,"preparation",applicable&&printed<=1,true)*5;
            if(damage)p.relicDamage+=flat;else if(block)p.relicBlock+=flat;
            if(c.kind==CardKind.Skill&&block)p.relicBlock+=TakeRelicEffect("gauntlet_ready");
            var hollow=TakeRelicEffect("hollow_crown_ready");if(damage)p.relicDamage+=hollow;if(block)p.relicBlock+=hollow;if(!applicable)p.relicDrawAfter+=hollow/5;
            if(!Started(c)&&RelicOnce("hollow_coin"))RelicTrigger("hollow_coin",()=>{if(damage)p.relicDamage+=4;else if(block)p.relicBlock+=4;});
            if(Temporary(c)&&RelicOnce("spirit_fang",true))RelicTrigger("spirit_fang",()=>{if(damage)p.relicDamage+=5;else if(block)p.relicBlock+=5;});
            if(EffectValue(player,"adaptation")>0&&RM.adaptationType!=(int)c.kind&&applicable){TakeRelicEffect("adaptation",true);if(damage)p.relicDamagePercent+=50;else p.relicBlockPercent+=50;}
            if(TakeRelicEffect("chimera",true)>0){p.relicDamagePercent+=25;p.relicBlockPercent+=25;p.relicOriginMask=31;p.relicBridgeOrigins=15;}
            // The first Attack and Skill have independent reservations for the same relic.
            if(HasRelic("sovereign_seal")&&c.kind is CardKind.Attack or CardKind.Skill&&!RM.turnUsed.Contains("seal_"+c.kind)){RM.turnUsed.Add("seal_"+c.kind);RelicTrigger("sovereign_seal",()=>{if(c.kind==CardKind.Attack)p.relicDamage+=8;else if(block)p.relicBlock+=8;});}
            RM.plays++;if(RM.plays%6==0&&HasRelic("hollow_hourglass")){p.relicEcho=true;RelicGrant("hollow_hourglass","echoed",1,CombatEffectDuration.TurnEnd);}
            if(c.perfected&&c.kind is CardKind.Attack or CardKind.Skill)c.perfectedGrowth++;
        }
        private void AfterRelicPlay(CardDef c,PendingCardPlay p)
        {
            RM.turnPlays++;if(c.kind==CardKind.Power)RelicBuff(c.id);if(c.kind==CardKind.Attack)RM.turnAttacks++;
            RM.typeMask|=1<<(int)c.kind;RM.costMask|=1<<Math.Min(2,PrintedCost(c));RM.originMask|=p.relicBridgeOrigins;RM.chimeraMask|=p.relicOriginMask;
            if(p.relicPhantom&&!IsOver)TriggerEffect("phantom_edge",()=>RelicDamage(4,"PHANTOM EDGE"));
            if(p.relicDrawAfter>0)Draw(p.relicDrawAfter,false);
            if(c.perfected&&c.kind==CardKind.Power)c.perfectedCostReduction=Math.Min(PrintedCost(c),c.perfectedCostReduction+1);
            if(PrintedCost(c)==0&&RelicOnce("cracked_hourglass"))RelicGrant("cracked_hourglass","cracked_ready",4);
            if((RM.typeMask&3)==3&&RelicOnce("war_torn_ribbon"))RelicGrant("war_torn_ribbon","ribbon_discount",1,CombatEffectDuration.TurnEnd);
            if(c.kind==CardKind.Attack&&PrintedCost(c)>=2&&HasRelic("dented_gauntlet"))RelicGrant("dented_gauntlet","gauntlet_ready",4,CombatEffectDuration.TurnEnd);
            if(RM.turnPlays>=3&&RelicOnce("bloodstained_thread"))RelicGrant("bloodstained_thread","bloodthread_ready",4);
            if(RM.turnAttacks>=3&&RelicOnce("bloodglass_shard"))RelicGrant("bloodglass_shard","ferocity",1);
            if(RM.costMask==7&&RelicOnce("gilded_loop"))RelicTrigger("gilded_loop",()=>Draw(2,false));
            if(!Started(c)&&RelicOnce("severed_quill")&&hand.Count>0)RelicTrigger("severed_quill",()=>Discount(hand[NextRandom(hand.Count)],1));
            if(!Started(c)&&RelicOnce("spectral_needle"))RelicGrant("spectral_needle","phantom_edge",1);
            if(PrintedCost(c)>=2&&RelicOnce("golden_scarab"))RelicGrant("golden_scarab","preparation",1);
            if(RM.destined==c.instanceId){RM.destined=-1;ConsumeEffect(player,"destined");RelicGrant("broken_compass","destined_discount",1,CombatEffectDuration.TurnEnd);}
            if(HasRelic("the_golden_cycle")){var expected=RM.sequence;if((int)c.kind==expected){RM.sequence++;if(RM.sequence==3){RM.sequence=0;RelicTrigger("the_golden_cycle",()=>{energy++;Emit(CombatEventKind.Energy,energy,true);});}}else RM.sequence=0;}
            if(BitCount(RM.originMask)>=3&&RelicOnce("crown_of_many_paths"))RelicGrant("crown_of_many_paths","many_paths_free",1,CombatEffectDuration.TurnEnd);
            if(BitCount(RM.chimeraMask)>=2&&RelicOnce("soul_of_the_chimera"))RelicGrant("soul_of_the_chimera","chimera",1);
            if(RM.lastType==(int)c.kind&&RelicOnce("broken_law")){RM.adaptationType=(int)c.kind;RelicGrant("broken_law","adaptation",1);}RM.lastType=(int)c.kind;
            if(RM.turnPlays>=5&&RelicOnce("crown_of_echoes"))RelicGrant("crown_of_echoes","reverberation",1);
            if(RM.turnPlays>=5&&RelicOnce("endless_tome"))RelicTrigger("endless_tome",()=>Draw(2,false));
        }
        private void RelicDrawn(CardDef c,bool normal)
        {
            if(EffectValue(player,"foresight")>0){TakeRelicEffect("foresight",true);RM.foresightCards.Add(c.instanceId);}
            if(normal)return;RM.extraDraws++;
            if(RelicOnce("frayed_cord"))RelicGrant("frayed_cord","relic_next_attack",3);
            if(RM.extraDraws>=3&&RelicOnce("veilglass"))RelicGrant("veilglass","foresight",1);
        }
        private bool RelicPreventExhaust(CardDef c)
        {
            if(Temporary(c)||EffectValue(player,"afterlife")<=0)return false;
            TakeRelicEffect("afterlife",true);discard.Add(c);Emit(CombatEventKind.Discard,1,true,c,"AFTERLIFE");return true;
        }
        private void RelicExhausted(CardDef c)
        {
            RM.exhausted++;RM.turnExhausted++;RelicMechanic("Exhaust");
            if(RelicOnce("iron_thread"))RelicTrigger("iron_thread",()=>GainBlock(3));
            if(RM.turnExhausted>=2&&RelicOnce("grave_dust"))RelicTrigger("grave_dust",()=>Draw(1,false));
            if(RM.exhausted%3==0&&HasRelic("funeral_bell"))RelicTrigger("funeral_bell",()=>{GrantEffect(player,"relic_next_attack",5,"funeral_bell",CombatEffectDuration.Combat);Draw(1,false);});
            if(RM.exhausted%4==0&&HasRelic("grave_lantern"))RelicGrant("grave_lantern","afterlife",1);
            if(!Temporary(c)&&HasRelic("hollow_crown"))RelicGrant("hollow_crown","hollow_crown_ready",5);
        }
        private void RelicBuff(string id)
        {
            if(!RM.turnBuffs.Contains(id))RM.turnBuffs.Add(id);if(!RM.combatBuffs.Contains(id))RM.combatBuffs.Add(id);RelicMechanic("Buff");
            if(RelicOnce("chain_of_opposites"))RelicGrant("chain_of_opposites","opposites_ready",1,CombatEffectDuration.TurnEnd);
            if(RM.turnBuffs.Count>=2&&RelicOnce("bent_warhorn"))RelicTrigger("bent_warhorn",()=>GainBlock(3));
            if(RM.turnBuffs.Count>=2&&RelicOnce("blacksteel_clasp"))RelicGrant("blacksteel_clasp","relic_next_attack",6);
            if(RM.combatBuffs.Count>=3&&HasRelic("iron_reliquary")){RM.combatBuffs.Clear();RelicGrant("iron_reliquary","resolve",1);}
        }
        private int RelicDebuffAmount(int amount)=>amount+(amount>0?TakeRelicEffect("opposites_ready"):0);
        private void RelicDebuffApplied()
        {
            RelicMechanic("Debuff");
            if(RelicOnce("omen_nail"))RelicTrigger("omen_nail",()=>GainBlock(2));
            if(RelicOnce("black_rose"))RelicTrigger("black_rose",()=>GrantEffect(enemy,"wither",1,"black_rose",CombatEffectDuration.Combat));
            if(DebuffNames(enemy).Distinct().Count()<3)return;
            if(RelicOnce("mirror_thorn"))RelicTrigger("mirror_thorn",()=>{RelicDamage(8,"MIRROR THORN");Draw(1,false);});
            if(HasRelic("mourning_bell")&&!RM.knellOwners.Contains(EnemyContextIndex)){RM.knellOwners.Add(EnemyContextIndex);RelicTrigger("mourning_bell",()=>GrantEffect(enemy,"death_knell",1,"mourning_bell",CombatEffectDuration.Combat));}
        }
        private void RelicMechanic(string category)
        {
            if(RM.mechanics.Contains(category))return;RM.mechanics.Add(category);
            if(RM.mechanics.Count>=3&&RelicOnce("fates_convergence"))RelicTrigger("fates_convergence",()=>{Draw(2,false);energy++;Emit(CombatEventKind.Energy,energy,true);});
        }
        private void RelicSigilActivated(){RelicMechanic("Sigil");if(RelicOnce("ritual_chalk",true))RelicTrigger("ritual_chalk",()=>Draw(1,false));}
        private void RelicGenerated(CardDef c){c.temporary=true;RelicMechanic("Temporary");}
        private void RelicRetrieved(CardDef c){if(RelicOnce("graveyard_key",true))RelicTrigger("graveyard_key",()=>Discount(c,1));}
        private int RelicRetainedBlock(int retained)
        {
            if(retained==0&&player.block>0&&EffectValue(player,"resolve")>0){TakeRelicEffect("resolve",true);return CeilPercent(player.block,50);}return retained;
        }
        private void RelicDamage(int amount,string label)
        {
            relicDamageDepth++;try{DamageEnemyRaw(amount,label);}finally{relicDamageDepth--;}
        }
        private void RelicDamageDealt(int amount,string category,bool attack,bool condemnedBonus=false)
        {
            if(amount<=0)return;
            var owner=EnemyContextIndex;
            if(RelicOnce("executioners_chain",true))RelicTrigger("executioners_chain",()=>GrantEffect(enemy,"condemned",1,"executioners_chain",CombatEffectDuration.Combat));
            var condemned=enemy.effects.FirstOrDefault(e=>e.id=="condemned");
            if(condemned!=null&&!condemnedBonus){condemned.value++;if(condemned.value>5){condemned.value=1;TriggerEffect("condemned",()=>RelicDamage(12,"CONDEMNED"));}}
            if(attack){RM.attackHits++;if(RM.attackHits>=4&&RelicOnce("glass_needle"))RelicTrigger("glass_needle",()=>OnRandomLivingEnemy(()=>ApplyEnemyDebuff(EffectKind.Vulnerable,1)));}
            else if(RelicOnce("ashen_needle"))RelicTrigger("ashen_needle",()=>ApplyEnemyDebuff(EffectKind.Weak,1));
            if(DebuffNames(enemy).Any()&&RelicOnce("smoldering_wick")){var other=Enumerable.Range(0,EnemyCount).Where(i=>i!=owner&&IsLivingTarget(i)).ToArray();if(other.Length>0)RelicTrigger("smoldering_wick",()=>{SaveEnemyContext();LoadEnemyContext(other[NextRandom(other.Length)]);RelicDamage(3,"SMOLDERING WICK");SaveEnemyContext();LoadEnemyContext(owner);});}
            if(!attack&&EffectValue(enemy,"death_knell")>0)TriggerEffect("death_knell",()=>{ConsumeEffect(enemy,"death_knell");RelicDamage(amount,"DEATH KNELL");},false);
            if(HasRelic("black_star_of_ruin"))
            {
                var bit=category switch{"Attack"=>1,"Burn"=>2,"Retaliate"=>4,"Death's Echo"=>8,"Relic"=>16,"Temporary"=>32,_=>64};
                var i=RM.ruptureOwners.IndexOf(owner);if(i<0){RM.ruptureOwners.Add(owner);RM.ruptureMasks.Add(bit);i=RM.ruptureMasks.Count-1;}else RM.ruptureMasks[i]|=bit;
                if(BitCount(RM.ruptureMasks[i])>=3&&RM.ruptureMasks[i]<128){RM.ruptureMasks[i]|=128;RelicTrigger("black_star_of_ruin",()=>GrantEffect(enemy,"rupture",1,"black_star_of_ruin",CombatEffectDuration.Combat));}
            }
        }
        private int RelicAttackAmount(CardDef c,int amount)
        {
            var p=pendingPlay;if(p!=null&&p.card==c){amount+=p.relicDamage+(c.perfected&&c.kind==CardKind.Attack?c.perfectedGrowth:0);amount=CeilPercent(amount,100+p.relicDamagePercent);if(c.kind==CardKind.Attack){var key=EnemyContextIndex+1;if(!p.relicRuptureChecked.Contains(key)){p.relicRuptureChecked.Add(key);if(ConsumeEffect(enemy,"rupture")>0)p.relicRuptureTargets.Add(key);}if(p.relicRuptureTargets.Contains(key))amount=CeilPercent(amount,150);}}return amount;
        }
        private int RelicBlockAmount(CardDef c,int amount){var p=pendingPlay;return p!=null&&p.card==c?CeilPercent(amount+p.relicBlock+(c.perfected&&c.kind==CardKind.Skill?c.perfectedGrowth:0),100+p.relicBlockPercent):amount;}
        private int EnemyBuffAfterWither(int amount)
        {if(amount<=0||EffectValue(enemy,"wither")<=0)return amount;var reduction=ConsumeEffect(enemy,"wither");return Math.Max(0,amount-reduction);}
        private int TriggerRepeats(string id,bool playerSide)
        {
            if(repeatedTriggerDepth>0||id.StartsWith("consume_")||id is "retaliate"||id.StartsWith("relic_crown_of_echoes")||id.StartsWith("relic_paradox_engine"))return 0;
            var causedByCard=pendingPlay!=null||GameContent.Find(id)?.kind==CardKind.Power||id.StartsWith("sigil_")||id is "deaths_echo" or "mark_of_the_grave" or "battle_temper" or "iron_blood";
            var repeats=0;
            if(RM.paradoxReady&&id!=RM.paradoxSource){RM.paradoxReady=false;ConsumeEffect(player,"paradox");repeats++;}
            else if(causedByCard&&RelicOnce("paradox_engine")){RM.paradoxSource=id;RM.paradoxReady=true;Emit(CombatEventKind.RelicTrigger,1,true,null,"paradox_engine");GrantEffect(player,"paradox",1,"paradox_engine",CombatEffectDuration.TurnEnd);}
            if(causedByCard&&EffectValue(player,"reverberation")>0){TakeRelicEffect("reverberation",true);repeats++;}
            return repeats;
        }
        public string RelicProgress(string id)=>id switch
        {
            "funeral_bell"=>$"{RM.exhausted%3}/3 Exhausts", "grave_lantern"=>$"{RM.exhausted%4}/4 Exhausts", "hollow_hourglass"=>$"{RM.plays%6}/6 cards",
            "the_golden_cycle"=>$"{RM.sequence}/3 · next: {(CardKind)RM.sequence}","fates_convergence"=>$"{Math.Min(3,RM.mechanics.Count)}/3 · {string.Join(", ",RM.mechanics)}",
            "crown_of_many_paths"=>$"{BitCount(RM.originMask)}/3 origins", "soul_of_the_chimera"=>$"{BitCount(RM.chimeraMask)}/2 origins",
            "tome_of_the_unwritten"=>$"{RM.unwritten.Count} Unwritten cards", "fate_die"=>RM.combatUsed.Contains(id)?"USED":$"{(CardKind)RM.fateType} · READY",
            _=>RM.turnUsed.Contains(id)?"USED THIS TURN":RM.combatUsed.Contains(id)?"USED THIS COMBAT":""
        };
        public string RelicCardModification(CardDef c)
        {
            if(c==null)return "";var lines=new List<string>();
            if(c.perfected)lines.Add(c.kind==CardKind.Power?$"Perfected Power: each play permanently reduces its cost by 1 (minimum 0). Current reduction: {c.perfectedCostReduction}. Source: Perfected Thread. Permanent for this run.":$"Perfected {c.kind}: each play permanently adds +1 {(c.kind==CardKind.Attack?"damage":"Block")} to each applicable instance, then Exhausts. Current growth: +{c.perfectedGrowth}. Source: Perfected Thread. Permanent for this run.");
            if(RM.unwritten.Contains(c.instanceId))lines.Add("Unwritten: costs 1 less for this entire combat. Source: Tome of the Unwritten.");
            if(RM.foresightCards.Contains(c.instanceId))lines.Add("Foresight: costs 1 less until played.");
            if(RM.destined==c.instanceId)lines.Add("Destined: playing this card this turn makes your next card cost 1 less this turn.");
            return string.Join("\n",lines);
        }
    }
}
