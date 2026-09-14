using System;
using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;

namespace GildedFate.Combat
{
    [Serializable] public sealed class CombatOpponent
    {
        public string id,intentLabel,mechanicTitle,mechanicText;
        public FighterState fighter=new();
        public int baseDamage,intentValue,intentHits=1;
        public IntentKind intent;
        public CombatOpponent Copy(){var c=(CombatOpponent)MemberwiseClone();c.fighter=fighter.Copy();return c;}
    }
    public sealed partial class CombatState
    {
        public List<CombatOpponent> opponents=new();
        public int enemyContextIndex;
        public int EnemyContextIndex=>enemyContextIndex;
        public int EnemyCount=>opponents?.Count>0?opponents.Count:1;
        public bool AnyEnemyAlive=>opponents?.Count>0?opponents.Any(e=>e.fighter.hp>0):enemy.hp>0;
        public FighterState EnemyAt(int index)=>opponents?.Count>0?opponents[index].fighter:enemy;
        public string EnemyIdAt(int index)=>opponents?.Count>0?opponents[index].id:enemyId;
        public bool IsLivingTarget(int index)=>index>=0&&index<EnemyCount&&EnemyAt(index).hp>0;
        private void SaveEnemyContext()
        {
            if(opponents==null||opponents.Count==0)return;
            var e=opponents[enemyContextIndex];e.fighter=enemy;e.id=enemyId;e.baseDamage=enemyBaseDamage;
            e.intent=intent;e.intentHits=intentHits;e.intentValue=intentValue;e.intentLabel=intentLabel;
            e.mechanicTitle=mechanicTitle;e.mechanicText=mechanicText;
        }
        private void LoadEnemyContext(int index)
        {
            if(opponents==null||opponents.Count==0){enemyContextIndex=0;return;}
            var e=opponents[index];enemyContextIndex=index;enemy=e.fighter;enemyId=e.id;enemyBaseDamage=e.baseDamage;
            intent=e.intent;intentHits=e.intentHits;intentValue=e.intentValue;intentLabel=e.intentLabel;
            mechanicTitle=e.mechanicTitle;mechanicText=e.mechanicText;
        }
        public T InspectEnemy<T>(int index,Func<T> read)
        {
            SaveEnemyContext();var previous=enemyContextIndex;LoadEnemyContext(index);
            try{return read();}finally{LoadEnemyContext(previous);}
        }
        private void ForEachLivingEnemy(Action action)
        {
            SaveEnemyContext();var previous=enemyContextIndex;
            try{for(var i=0;i<EnemyCount;i++){if(!IsLivingTarget(i))continue;LoadEnemyContext(i);action();SaveEnemyContext();}}
            finally{LoadEnemyContext(previous);}
        }
        private void OnRandomLivingEnemy(Action action)
        {
            var living=Enumerable.Range(0,EnemyCount).Where(IsLivingTarget).ToArray();if(living.Length==0)return;
            SaveEnemyContext();var previous=enemyContextIndex;LoadEnemyContext(living[NextRandom(living.Length)]);
            try{action();SaveEnemyContext();}finally{LoadEnemyContext(previous);}
        }
        private void InitializeOpponentGroup(string[] ids)
        {
            opponents=new();enemyContextIndex=0;if(ids==null||ids.Length<=1)return;
            if(ids.Length>4)throw new ArgumentException("Encounters support at most four enemies.");
            foreach(var id in ids)
            {
                var def=Array.Find(WorldContent.Enemies,e=>e.id==id);
                if(def==null||def.boss||def.elite)throw new ArgumentException("Only authored normal enemies may join a group.");
                var hp=EncounterContent.GroupHp(def.hp);
                opponents.Add(new CombatOpponent{id=id,baseDamage=def.baseDamage,fighter=new FighterState{hp=hp,maxHp=hp}});
            }
            LoadEnemyContext(0);
        }
        private void PlanAllEnemyIntents(){ForEachLivingEnemy(PlanIntent);}
        public int TotalIntendedDamage=>Enumerable.Range(0,EnemyCount).Where(IsLivingTarget)
            .Sum(i=>InspectEnemy(i,()=>IntentDealsDamage?IntentDisplayValue*intentHits:0));
        public bool Play(CardDef card,int targetIndex)
        {
            if(!CanPlay(card)||RequiresEnemyTarget(card)&&!IsLivingTarget(targetIndex))return false;
            if(IsLivingTarget(targetIndex)){SaveEnemyContext();LoadEnemyContext(targetIndex);}
            return Play(card);
        }
        private static bool AffectsAllEnemies(CardDef card)=>card!=null&&card.kind!=CardKind.Power&&
            ((card.text??"").IndexOf("all enemies",StringComparison.OrdinalIgnoreCase)>=0);
        private static bool AffectsRandomEnemy(CardDef card)=>card!=null&&
            ((card.text??"").IndexOf("random enemy",StringComparison.OrdinalIgnoreCase)>=0);
        private void DamageRandomEnemy(int amount,string label)=>OnRandomLivingEnemy(()=>DamageEnemyRaw(amount,label));
        private void ResolveCardTargets(CardDef card)
        {
            if(ResolveMajorVanguardCard(card))return;
            if(ResolveRemainingCard(card))return;
            if(AffectsAllEnemies(card))ForEachLivingEnemy(()=>ResolveCard(card));
            else if(AffectsRandomEnemy(card))OnRandomLivingEnemy(()=>ResolveCard(card));
            else if(!RequiresEnemyTarget(card)&&enemy.hp<=0&&AnyEnemyAlive)
            {
                SaveEnemyContext();var previous=enemyContextIndex;LoadEnemyContext(Enumerable.Range(0,EnemyCount).First(IsLivingTarget));
                try{ResolveCard(card);SaveEnemyContext();}finally{LoadEnemyContext(previous);}
            }
            else ResolveCard(card);
        }
    }
}
