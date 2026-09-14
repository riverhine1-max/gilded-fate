using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    [Serializable] public sealed class RemainingCardMemory
    {
        public int normalDrawn,typesPlayed,hiddenTypes,hiddenPotential,soulDominion,goldenThread,accessibleSigils=7;
        public int additionalGuaranteedDraws,turnStartCards;
        public bool goldenUsed,hiddenPowerUsed;
        public string hiddenRevealedNames="";
        public List<int> startingIds=new(),discountIds=new(),discounts=new(),reflectedIds=new(),boundIds=new(),boundRates=new(),boundBonuses=new(),sparkSlots=new(),imprintCosts=new(),imprintDamage=new();
        public RemainingCardMemory Copy(){var c=(RemainingCardMemory)MemberwiseClone();c.startingIds=new(startingIds);c.discountIds=new(discountIds);c.discounts=new(discounts);c.reflectedIds=new(reflectedIds);c.boundIds=new(boundIds);c.boundRates=new(boundRates);c.boundBonuses=new(boundBonuses);c.sparkSlots=new(sparkSlots);c.imprintCosts=new(imprintCosts);c.imprintDamage=new(imprintDamage);return c;}
    }
    [Serializable] public sealed class RemainingCardChoice
    {
        public int stage,slot,source,selectedDebuff,count,souls;
        public List<int> eligible=new();
        public RemainingCardChoice Copy(){var c=(RemainingCardChoice)MemberwiseClone();c.eligible=new(eligible);return c;}
    }
    public sealed partial class CombatState
    {
        private RemainingCardMemory R=>memory.remaining??=new();
        private bool ValidateRemainingMemory()
        {
            var r=memory.remaining;if(r==null)return true;
            if(r.additionalGuaranteedDraws<0||r.additionalGuaranteedDraws>r.normalDrawn||r.turnStartCards<0||r.turnStartCards>cardsPlayed)return false;
            return r.startingIds!=null&&r.discountIds!=null&&r.discounts!=null&&r.reflectedIds!=null&&r.boundIds!=null&&r.boundRates!=null&&r.boundBonuses!=null&&r.sparkSlots!=null&&r.imprintCosts!=null&&r.imprintDamage!=null&&r.discountIds.Count==r.discounts.Count&&r.boundIds.Count==r.boundRates.Count&&r.boundIds.Count==r.boundBonuses.Count&&r.imprintCosts.Count==r.imprintDamage.Count&&r.boundRates.All(x=>x>0)&&r.boundBonuses.All(x=>x>=0)&&r.discounts.All(x=>x>=0)&&r.imprintCosts.All(x=>x>=0)&&r.imprintDamage.All(x=>x>0)&&r.accessibleSigils>=0&&r.accessibleSigils<=127;
        }
        private bool ValidateRemainingChoice()
        {
            if(!IsRemainingCard(pendingPlay.card)||pendingPlay.remaining==null||pendingPlay.remaining.eligible==null)return false;
            var q=pendingPlay.remaining;if(q.stage<0||q.stage>1||q.source<0||q.source>=EnemyCount||q.count<0||q.count>3||q.souls<0||q.souls>q.count)return false;
            if(pendingPlay.card.id=="sigil_mutation"&&q.stage==1&&(q.slot<0||q.slot>=sigils.Count))return false;
            if(pendingPlay.card.id=="arcane_contagion"&&(q.selectedDebuff<0||q.selectedDebuff>3))return false;
            return q.eligible.Distinct().Count()==q.eligible.Count&&q.eligible.All(id=>AllOwnedCards().Any(c=>c.instanceId==id));
        }
        private RemainingCardChoice RC=>pendingPlay.remaining??=new();
        public static bool IsRemainingCard(CardDef c)=>c!=null&&(GameContent.MajorHexerCardIds.Contains(c.id)||GameContent.MajorReaperCardIds.Contains(c.id)||GameContent.MajorWandererCardIds.Contains(c.id));
        private static bool RemainingTargets(CardDef c)=>c!=null&&c.id is "spreading_flame" or "shared_misfortune" or "arcane_contagion" or "unstable_hex" or "reapers_mark" or "mark_of_the_grave" or "grave_execution" or "shared_fate" or "borrowed_strength" or "shared_burden";
        private static bool IsBadCard(CardDef c)=>c.origin is CardOrigin.Curse or CardOrigin.Status;
        private static bool GivesDamage(CardDef c)=>c.kind==CardKind.Attack||IsSoul(c);
        private static bool GivesBlock(CardDef c)=>c.effect==EffectKind.Block||c.text.IndexOf("gain",StringComparison.OrdinalIgnoreCase)>=0&&c.text.Contains("Block");
        private int BoundBonus(CardDef c){var i=R.boundIds.IndexOf(c.instanceId);return i<0?0:R.boundBonuses[i];}
        public string CombatCardModification(CardDef c){var i=c==null?-1:R.boundIds.IndexOf(c.instanceId);return i<0?"":$"Soulbound: +{R.boundBonuses[i]} {(GivesDamage(c)?"damage":"Block")}. Each Soul adds {R.boundRates[i]} for this combat.";}
        private int RemainingCost(CardDef c,int cost)
        {
            var i=R.discountIds.IndexOf(c.instanceId);if(i>=0)cost-=R.discounts[i];
            if((R.hiddenTypes&2)!=0&&c.kind==CardKind.Skill&&!GivesBlock(c))cost--;
            if((R.hiddenTypes&4)!=0&&c.kind==CardKind.Power&&!R.hiddenPowerUsed)cost--;
            return cost;
        }
        private void Discount(CardDef c,int amount){var i=R.discountIds.IndexOf(c.instanceId);if(i<0){R.discountIds.Add(c.instanceId);R.discounts.Add(amount);}else R.discounts[i]+=amount;Emit(CombatEventKind.Status,amount,true,c,"COST REDUCED THIS TURN");}
        private void StartRemainingTurn()
        {
            R.additionalGuaranteedDraws=R.normalDrawn=R.typesPlayed=R.hiddenTypes=0;R.hiddenPowerUsed=R.goldenUsed=false;R.discountIds.Clear();R.discounts.Clear();R.reflectedIds.Clear();R.sparkSlots.Clear();
            if(R.hiddenPotential>0)
            {
                R.hiddenRevealedNames=string.Join(", ",draw.Take(R.hiddenPotential).Select(c=>c.name));ExpansionPowerPulse("hidden_potential");foreach(var c in draw.Take(R.hiddenPotential).ToArray()){R.hiddenTypes|=c.kind==CardKind.Attack?1:c.kind==CardKind.Skill?2:c.kind==CardKind.Power?4:0;Emit(CombatEventKind.Status,1,true,c,"HIDDEN POTENTIAL · "+c.name+" · "+c.kind.ToString().ToUpperInvariant());}
                // The permanent Power HUD owns the revealed names and active type summary.
            }
        }
        private void BeforeRemainingPlay(CardDef c)
        {
            if(R.reflectedIds.Remove(c.instanceId)){pendingPlay.copiesRemaining++;Emit(CombatEventKind.Status,1,true,c,"FATE'S REFLECTION");}
            if(c.kind==CardKind.Power)R.hiddenPowerUsed=true;
            var fortune=EffectValue(player,"passing_fortune");if(fortune>0&&(GivesDamage(c)||GivesBlock(c)))TriggerEffect("passing_fortune",()=>{ConsumeEffect(player,"passing_fortune");pendingPlay.remainingBonus+=fortune;});
            if(R.goldenThread>0&&!R.goldenUsed&&!R.startingIds.Contains(c.instanceId))
            {R.goldenUsed=true;ExpansionPowerPulse("golden_thread");if(R.goldenThread>1)pendingPlay.remainingBonus+=3;Draw(1,false);}
        }
        private void AfterRemainingCard(CardDef c)
        {
            R.typesPlayed|=c.kind==CardKind.Attack?1:c.kind==CardKind.Skill?2:c.kind==CardKind.Power?4:0;
            var printed=PrintedCost(c);for(var i=0;i<R.imprintCosts.Count;i++)if(printed==R.imprintCosts[i]){ExpansionPowerPulse("gilded_imprint");DamageRandomEnemy(R.imprintDamage[i],"GILDED IMPRINT");}
        }
        private int PrintedCost(CardDef c){var d=GameContent.Find(c.id);return d==null?c.cost:(c.upgraded?GameContent.Upgrade(d):d).cost;}
        private int RemainingDamageBonus(CardDef c)=>BoundBonus(c)+(pendingPlay?.remainingBonus??0)+((R.hiddenTypes&1)!=0&&c.kind==CardKind.Attack?5:0);
        private int RemainingBlockBonus(CardDef c)=>c==null?0:(GivesDamage(c)?0:BoundBonus(c)+(pendingPlay?.remainingBonus??0))+((R.hiddenTypes&2)!=0&&c.kind==CardKind.Skill?5:0);
        // Keep other draw triggers unchanged; opening-hand draws are excluded only
        // from effects whose rule explicitly counts additional draws.
        public int AdditionalCardsDrawnThisTurn()=>Math.Max(0,memory.cardsDrawnThisTurn-R.normalDrawn+R.additionalGuaranteedDraws);
        private void OnRemainingDraw(CardDef c,bool normal)
        {
            if(normal)R.normalDrawn++;if(phase!=CombatPhase.Player)return;
            if(EffectValue(player,"deaths_echo")>0)TriggerEffect("deaths_echo",()=>DamageRandomEnemy(EffectValue(player,"deaths_echo"),"DEATH'S ECHO"));
            ForEachLivingEnemy(()=>{if(EffectValue(enemy,"mark_of_the_grave")>0)TriggerEffect("mark_of_the_grave",()=>GrantEffect(enemy,"gravemark",EffectValue(enemy,"mark_of_the_grave"),"mark_of_the_grave",CombatEffectDuration.Combat),false);});
            if(R.soulDominion>0&&drawingFromSoul&&!IsSoul(c)){ExpansionPowerPulse("soul_dominion");AddSouls(1,R.soulDominion>1?SoulDestination.Draw:SoulDestination.Discard);}
        }
        private void OnRemainingSoul()
        {
            for(var i=0;i<R.boundIds.Count;i++){R.boundBonuses[i]+=R.boundRates[i];var c=AllOwnedCards().FirstOrDefault(x=>x.instanceId==R.boundIds[i]);if(c!=null)Emit(CombatEventKind.Status,R.boundRates[i],true,c,"SOULBOUND");}
        }
        private void OnMarkedLost()
        {
            if(EffectValue(enemy,"unstable_hex")<=0)return;var n=ConsumeEffect(enemy,"unstable_hex");ForEachLivingEnemy(()=>ApplyEnemyDebuff(EffectKind.Weak,n));
        }
        private IEnumerable<string> DebuffNames(FighterState f)
        {
            if(f.burn>0)yield return "BURN";if(f.marked>0)yield return "MARKED";if(f.weak>0)yield return "WEAK";if(f.vulnerable>0)yield return "VULNERABLE";if(f.frail>0)yield return "FRAIL";
            foreach(var e in f.effects??new())if(e.id is "reaped" or "gravemark" or "unstable_hex" or "mark_of_the_grave" or "rupture" or "condemned" or "wither" or "death_knell")yield return e.id.ToUpperInvariant();
        }
        private int DebuffAmount(FighterState f,string n)=>n switch{"BURN"=>f.burn,"MARKED"=>f.marked,"WEAK"=>f.weak,"VULNERABLE"=>f.vulnerable,"FRAIL"=>f.frail,_=>EffectValue(f,n.ToLowerInvariant())};
        private void RemoveDebuff(FighterState f,string n,int v){switch(n){case "BURN":f.burn-=v;break;case "MARKED":f.marked-=v;if(f==enemy)OnMarkedLost();break;case "WEAK":f.weak-=v;break;case "VULNERABLE":f.vulnerable-=v;break;case "FRAIL":f.frail-=v;break;}Emit(CombatEventKind.Status,-v,f==player,null,n);}
        private void AddDebuff(string n,int v,string source)
        {
            if(v<=0)return;switch(n){case "BURN":ApplyBurn(v);break;case "MARKED":ApplyMarked(v);break;case "WEAK":ApplyEnemyDebuff(EffectKind.Weak,v);break;case "VULNERABLE":ApplyEnemyDebuff(EffectKind.Vulnerable,v);break;case "FRAIL":enemy.frail+=v;Emit(CombatEventKind.Status,v,false,null,n);break;default:GrantEffect(enemy,n.ToLowerInvariant(),v,source,CombatEffectDuration.Combat);break;}
        }
        private void CopyDebuffs(FighterState source,int amount,string cardId){foreach(var n in DebuffNames(source).ToArray())AddDebuff(n,Math.Min(amount,DebuffAmount(source,n)),cardId);}
        private void AllHits(CardDef c,int value,int count=1){for(var hit=0;hit<count&&!IsOver;hit++){var h=hit;ForEachLivingEnemy(()=>DamageEnemy(value+c.permanentDamageBonus,c,h,count));}}
        private void BeginRemainingCards(IEnumerable<CardDef> eligible){RC.eligible=eligible.Select(c=>c.instanceId).ToList();pendingPlay.choice=RC.eligible.Count==0?CardChoiceKind.None:CardChoiceKind.ExpansionCard;}
        private IReadOnlyList<CardDef> RemainingChoiceCards=>AllOwnedCards().Where(c=>RC.eligible.Contains(c.instanceId)&&c!=pendingPlay.card).ToArray();
        private void BeginRemainingOptions(int stage){RC.stage=stage;pendingPlay.choice=CardChoiceKind.ExpansionOption;if(RemainingChoiceOptions.Count==0)pendingPlay.choice=CardChoiceKind.None;}
        private IReadOnlyList<string> RemainingChoiceOptions
        {
            get
            {
                switch(pendingPlay.card.id)
                {
                    case "arcane_contagion":return RC.stage==0?new[]{"BURN","MARKED","WEAK","VULNERABLE"}.Where(n=>DebuffAmount(EnemyAt(RC.source),n)>0).ToArray():Enumerable.Range(0,EnemyCount).Where(i=>i!=RC.source&&IsLivingTarget(i)).Select(i=>$"{i+1} · {WorldContent.Enemies.First(e=>e.id==EnemyIdAt(i)).name}").ToArray();
                    case "forbidden_transfusion":return new[]{"STRENGTH","FORTIFY","RESONANCE"};
                    case "spirit_feast":return new[]{"EXHAUST ANOTHER CARD","FINISH"};
                    case "sigil_mutation":if(RC.stage==1)return Enum.GetValues(typeof(SigilKind)).Cast<SigilKind>().Where(s=>(R.accessibleSigils&(1<<(int)s))!=0&&s!=sigils[RC.slot]).Select(s=>s.ToString().ToUpperInvariant()+" SIGIL").ToArray();break;
                }
                return sigils.Select((s,i)=>$"{i+1} · {s.ToString().ToUpperInvariant()} SIGIL").ToArray();
            }
        }
        private bool ResolveRemainingCard(CardDef c)
        {
            if(!IsRemainingCard(c))return false;var v=c.value;var s=c.secondary;ApplySpecialBase(c,ref v,ref s,memory.attacksThisTurn==0);var source=EnemyContextIndex;var snap=enemy.Copy();
            switch(c.id)
            {
                case "spreading_flame":var burn=enemy.burn*v/100;ForEachLivingEnemy(()=>{if(EnemyContextIndex!=source)ApplyBurn(burn);});break;
                case "hexwave":ForEachLivingEnemy(()=>ApplyMarked(c.upgraded||sigils.Contains(SigilKind.Hex)?2:1));break;
                case "ritual_spark":case "sigil_mutation":case "ritual_collapse":BeginRemainingOptions(0);break;
                case "cursed_flame":var extra=hand.Any(x=>x.origin==CardOrigin.Curse)?2:1;ForEachLivingEnemy(()=>ApplyBurn(v*extra));break;
                case "shared_misfortune":ForEachLivingEnemy(()=>{if(EnemyContextIndex!=source)CopyDebuffs(snap,v,c.id);});break;
                case "arcane_fracture":var debuffed=0;ForEachLivingEnemy(()=>{if(DebuffNames(enemy).Any())debuffed++;DamageEnemy(v+c.permanentDamageBonus,c);});GainResonance(debuffed);break;
                case "sigil_shift":if(sigils.Count>0){ShiftRemainingSigil();for(var n=0;n<v;n++)ActivateSigil(sigils.Count-1);}break;
                case "forbidden_exchange":case "forbidden_transfusion":BeginRemainingCards(hand.Where(IsBadCard));break;
                case "burning_reflection":var types=DebuffNames(enemy).Count();DamageEnemy(v+c.permanentDamageBonus,c);ApplyBurn(types*s);break;
                case "arcane_contagion":RC.source=source;if(LivingEnemyCount>1)BeginRemainingOptions(0);break;
                case "dark_premonition":DrawPremonition(v);break;
                case "resonant_wave":var spent=Math.Min(2,resonance);resonance-=spent;Emit(CombatEventKind.Resonance,resonance,true);AllHits(c,v,1+spent);break;
                case "unstable_hex":ApplyMarked(v);GrantEffect(enemy,c.id,s,c.id,CombatEffectDuration.Combat);break;
                case "sigil_of_ruin":case "sigil_of_withering":case "grave_inscription":case "mirror_ritual":var kind=c.id=="sigil_of_ruin"?SigilKind.Ruin:c.id=="sigil_of_withering"?SigilKind.Wither:c.id=="grave_inscription"?SigilKind.Grave:SigilKind.Mirror;var slot=CreateSigil(kind);if(c.upgraded&&c.id is "sigil_of_ruin" or "sigil_of_withering")ActivateSigil(slot);if(c.id=="grave_inscription"&&!c.upgraded)AddRandomCurse(false);break;
                case "deaths_echo":GrantEffect(player,c.id,v,c.id,CombatEffectDuration.TurnEnd);break;
                case "reapers_mark":GrantEffect(enemy,"reaped",v,c.id,CombatEffectDuration.Combat);break;
                case "death_spiral":AllHits(c,v,1+Math.Min(3,AdditionalCardsDrawnThisTurn()/s));break;
                case "spirit_feast":RC.count=RC.souls=0;if(hand.Count>0)BeginRemainingOptions(0);break;
                case "haunted_blade":DamageEnemy(v+c.permanentDamageBonus+Math.Max(0,memory.cardsDrawnThisTurn-R.normalDrawn)*s,c);break;
                case "soul_infusion":BeginRemainingCards(hand.Where(x=>!IsSoul(x)));break;
                case "mark_of_the_grave":GrantEffect(enemy,c.id,1,c.id,CombatEffectDuration.TurnEnd);break;
                case "grave_execution":var marks=ConsumeEffect(enemy,"gravemark");for(var n=0;n<marks&&!IsOver&&enemy.hp>0;n++)DamageEnemy(v+c.permanentDamageBonus,c,n,marks);break;
                case "soul_dominion":R.soulDominion=Math.Max(R.soulDominion,v);GrantEffect(player,c.id,v,c.id,CombatEffectDuration.Combat);break;
                case "the_last_harvest":var victims=hand.ToArray();var souls=victims.Count(IsSoul);foreach(var x in victims){hand.Remove(x);ExhaustCard(x);}AllHits(c,v,victims.Length);Draw(souls,false);break;
                case "gilded_toss":var firstCard=cardsPlayed==0;DamageEnemy(v+c.permanentDamageBonus,c);if(firstCard)Draw(1,false);break;
                case "shared_fate":var other=RandomOtherEnemy(source);if(other>=0)OnEnemy(other,()=>CopyDebuffs(snap,v,c.id));break;
                case "borrowed_strength":GainStrength(Math.Max(0,enemy.strength)*v/100,true);break;
                case "threadcutter":case "twist_of_fate":case "fates_reflection":case "gilded_imprint":BeginRemainingCards(hand);break;
                case "golden_opportunity":var old=hand.Select(x=>x.instanceId).ToArray();Draw(2,false);BeginRemainingCards(hand.Where(x=>!old.Contains(x.instanceId)));break;
                case "turn_the_blade":var repeat=DebuffNames(enemy).Any()?2:1;for(var n=0;n<repeat&&enemy.hp>0;n++)DamageEnemy(v+c.permanentDamageBonus,c,n,repeat);break;
                case "passing_fortune":GrantEffect(player,c.id,v,c.id,CombatEffectDuration.TurnEnd);break;
                case "scattered_threads":var allTypes=new HashSet<string>();ForEachLivingEnemy(()=>{foreach(var n in DebuffNames(enemy))allTypes.Add(n);});AllHits(c,v+allTypes.Count*s);break;
                case "second_chance":BeginRemainingCards(discard);break;
                case "shared_burden":var transferLeft=v;foreach(var n in DebuffNames(player).Where(n=>n is "BURN" or "MARKED" or "WEAK" or "VULNERABLE" or "FRAIL").ToArray()){var amount=Math.Min(transferLeft,DebuffAmount(player,n));transferLeft-=amount;RemoveDebuff(player,n,amount);AddDebuff(n,amount,c.id);}break;
                case "golden_thread":R.goldenThread=Math.Max(R.goldenThread,v);GrantEffect(player,c.id,v,c.id,CombatEffectDuration.Combat);break;
                case "perfect_sequence":var mask=R.typesPlayed|1;var count=(mask&1)+((mask>>1)&1)+((mask>>2)&1);for(var n=0;n<3&&enemy.hp>0;n++)DamageEnemy(v+c.permanentDamageBonus+count*2,c,n,3);break;
                case "hidden_potential":R.hiddenPotential=Math.Max(R.hiddenPotential,v);Emit(CombatEventKind.Status,v,true,c,"POWER");break;
            }
            return true;
        }
        private void DrawPremonition(int count)
        {
            var before=hand.Select(c=>c.instanceId).ToArray();Draw(count,false);var fresh=hand.Where(c=>!before.Contains(c.instanceId)&&IsBadCard(c)).ToArray();var budget=1000;
            while(fresh.Length>0&&budget-->0){foreach(var c in fresh){hand.Remove(c);ExhaustCard(c);}before=hand.Select(c=>c.instanceId).ToArray();Draw(fresh.Length,false);fresh=hand.Where(c=>!before.Contains(c.instanceId)&&IsBadCard(c)).ToArray();}
        }
        private void FinishRemainingChoice(){if(!AwaitingChoice)ContinueCardResolution();}
        private bool ChooseRemainingCard(CardDef selected)
        {
            var c=pendingPlay.card;pendingPlay.choice=CardChoiceKind.None;
            switch(c.id)
            {
                case "forbidden_exchange":case "forbidden_transfusion":hand.Remove(selected);ExhaustCard(selected);if(c.id=="forbidden_transfusion")BeginRemainingOptions(0);else{var soul=Instance(c.upgraded?GameContent.Upgrade(GameContent.Find("soul")):GameContent.Find("soul"));RelicGenerated(soul);if(hand.Count<12){hand.Add(soul);Emit(CombatEventKind.Draw,1,true,soul,"SOUL");}else{discard.Add(soul);Emit(CombatEventKind.Discard,1,true,soul,"HAND FULL");}}break;
                case "threadcutter":hand.Remove(selected);ExhaustCard(selected);Draw(c.value,false);break;
                case "spirit_feast":hand.Remove(selected);ExhaustCard(selected);RC.count++;if(IsSoul(selected))RC.souls++;GainBlock(c.value,true,c);if(RC.count<3&&hand.Count>0)BeginRemainingOptions(0);else FinishFeast();break;
                case "soul_infusion":var b=R.boundIds.IndexOf(selected.instanceId);if(b<0){R.boundIds.Add(selected.instanceId);R.boundRates.Add(c.value);R.boundBonuses.Add(0);}else R.boundRates[b]+=c.value;Emit(CombatEventKind.Status,c.value,true,selected,"SOULBOUND");break;
                case "golden_opportunity":Discount(selected,c.value);break;
                case "second_chance":discard.Remove(selected);Discount(selected,1);if(c.upgraded&&hand.Count<12){hand.Add(selected);Emit(CombatEventKind.Draw,1,true,selected,"RETURN");}else{draw.Add(selected);Emit(CombatEventKind.Shuffle,1,true,selected,"TOP OF DRAW");}break;
                case "twist_of_fate":hand.Remove(selected);draw.Add(selected);Shuffle(draw);Emit(CombatEventKind.Shuffle,1,true,selected);memory.makeNextDrawFree++;Draw(1,false);break;
                case "fates_reflection":if(!R.reflectedIds.Contains(selected.instanceId))R.reflectedIds.Add(selected.instanceId);Emit(CombatEventKind.Status,1,true,selected,"FATE'S REFLECTION");break;
                case "gilded_imprint":var cost=PrintedCost(selected);R.imprintCosts.Add(cost);R.imprintDamage.Add(c.value);GrantEffect(player,"imprint_"+cost,c.value,c.id,CombatEffectDuration.Combat);break;
            }
            FinishRemainingChoice();return true;
        }
        private void FinishFeast(){if(RC.souls>0){energy++;Emit(CombatEventKind.Energy,energy,true);}pendingPlay.choice=CardChoiceKind.None;}
        private bool ChooseRemainingOption(string option)
        {
            var c=pendingPlay.card;var index=Array.IndexOf(RemainingChoiceOptions.ToArray(),option);pendingPlay.choice=CardChoiceKind.None;
            switch(c.id)
            {
                case "spirit_feast":if(index==0)BeginRemainingCards(hand);else FinishFeast();break;
                case "forbidden_transfusion":if(index==0)GainStrength(c.value);else if(index==1)GainFortify(c.value);else GainResonance(c.value);break;
                case "arcane_contagion":if(RC.stage==0){RC.selectedDebuff=Array.IndexOf(new[]{"BURN","MARKED","WEAK","VULNERABLE"},option);BeginRemainingOptions(1);}else{var dest=int.Parse(option.Substring(0,option.IndexOf(' ')))-1;var n=new[]{"BURN","MARKED","WEAK","VULNERABLE"}[RC.selectedDebuff];var amount=Math.Min(c.value,DebuffAmount(EnemyAt(RC.source),n));OnEnemy(RC.source,()=>RemoveDebuff(enemy,n,amount));OnEnemy(dest,()=>AddDebuff(n,amount,c.id));}break;
                case "ritual_spark":ActivateSigil(index);R.sparkSlots.Add(index);break;
                case "ritual_collapse":var repeats=(int)sigils[index]>=3?c.value:2;for(var i=0;i<repeats;i++)ActivateSigil(index);RemoveRemainingSigil(index);Emit(CombatEventKind.Status,1,true,c,"SIGIL SHATTER");break;
                case "sigil_mutation":if(RC.stage==0){RC.slot=index;BeginRemainingOptions(1);}else{sigils[RC.slot]=(SigilKind)Enum.Parse(typeof(SigilKind),option.Replace(" SIGIL",""),true);ActivateSigil(RC.slot);ActivateSigil(RC.slot);}break;
            }
            FinishRemainingChoice();return true;
        }
        private void RemoveRemainingSigil(int index){sigils.RemoveAt(index);R.sparkSlots=R.sparkSlots.Where(i=>i!=index).Select(i=>i>index?i-1:i).ToList();}
        private void ShiftRemainingSigil(){var kind=sigils[0];var boosted=R.sparkSlots.Count(i=>i==0);RemoveRemainingSigil(0);sigils.Add(kind);for(var i=0;i<boosted;i++)R.sparkSlots.Add(sigils.Count-1);}
        private void SigilEffect(SigilKind kind,int index,int multiplier)
        {
            int Scale(int n)=>Math.Max(1,(n*multiplier+99)/100);
            switch(kind)
            {
                case SigilKind.Ember:ApplyBurn(Scale(2));break;
                case SigilKind.Hex:ApplyMarked(Scale(1));break;
                case SigilKind.Echo:memory.echoArmed+=Scale(1);break;
                case SigilKind.Ruin:ForEachLivingEnemy(()=>DamageEnemyRaw(Scale(5),"RUIN SIGIL"));break;
                case SigilKind.Wither:ForEachLivingEnemy(()=>ApplyEnemyDebuff(EffectKind.Weak,Scale(1)));break;
                case SigilKind.Grave:for(var i=0;i<Scale(1);i++){var bad=hand.FirstOrDefault(IsBadCard);if(bad==null)break;hand.Remove(bad);ExhaustCard(bad);Draw(1,false);}break;
                case SigilKind.Mirror:if(index>0&&sigils[index-1]!=SigilKind.Mirror)SigilEffect(sigils[index-1],index-1,multiplier);break;
            }
        }
    }
}
