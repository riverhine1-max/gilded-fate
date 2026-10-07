using System;
using System.Linq;

namespace GildedFate.Combat
{
    // Decides which enemy icon (if any) belongs with a given intent, counter or forecast. Pure data rules with no
    // Unity dependency, so the same answer is used in real runs, the Playground and the test harness. The icons
    // are supporting marks: every number, counter and move name is still drawn as text next to them.
    public static class EnemyIconRules
    {
        public const string Command="Intent_Command",AuctionLot="Intent_AuctionLot",Repeat="Intent_Repeat",ThronebreakerCharge="Intent_ThronebreakerCharge",
            WorldBreak="Intent_WorldBreak",Eruption="Intent_Eruption";
        public const string BonusGold="Mechanic_BonusGold",Reserve="Mechanic_Reserve",Growth="Mechanic_Growth",Prediction="Mechanic_Prediction",OrbitPlate="Mechanic_OrbitPlate",
            Momentum="Mechanic_Momentum",Judgment="Mechanic_Judgment",Sentence="Mechanic_Sentence",Toll="Mechanic_Toll",Echo="Mechanic_Echo",Split="Mechanic_Split",
            Fracture="Mechanic_Fracture",DualPossibility="Mechanic_DualPossibility",RoyalOrder="Mechanic_RoyalOrder",Siege="Mechanic_Siege",WorldBreakCharge="Mechanic_WorldBreakCharge",
            BurrowWarning="Mechanic_BurrowWarning",FormChange="Mechanic_FormChange",PreparedAttack="Mechanic_PreparedAttack";
        public const string SeizedGold="Status_SeizedGold";

        // Every icon file that ships with the enemy icon set, with the folder it lives in.
        public static readonly string[] Intents={Command,AuctionLot,Repeat,ThronebreakerCharge,WorldBreak,Eruption};
        public static readonly string[] Mechanics={BonusGold,Reserve,Growth,Prediction,OrbitPlate,Momentum,Judgment,Sentence,Toll,Echo,Split,Fracture,DualPossibility,RoyalOrder,Siege,WorldBreakCharge,BurrowWarning,FormChange,PreparedAttack};
        public static readonly string[] Statuses={SeizedGold};
        public static string Folder(string icon)=>icon.StartsWith("Intent_")?"Intents":icon.StartsWith("Status_")?"Statuses":"Mechanics";
        public static string ResourcePath(string icon)=>"Art/UI/EnemyIcons/"+Folder(icon)+"/"+icon;

        private static bool Is(string id,string suffix)=>id!=null&&id.EndsWith(suffix,StringComparison.Ordinal);

        // The icon shown for one action of an enemy's intent (null = keep the ordinary intent artwork).
        // `move` is the move the enemy is about to use; `index` is the position of the action inside that intent.
        public static string IntentIcon(string enemyId,string move,EnemyActionType type,int index)
        {
            if(type==EnemyActionType.Command)return Command;
            switch(move)
            {
                case "nt_wb_break":return type==EnemyActionType.Attack?WorldBreak:null;
                case "nt_dc_eruption":return type==EnemyActionType.Attack?Eruption:null;
                case "gt_charge":return type==EnemyActionType.Attack?ThronebreakerCharge:null;
                case "gr_open_bidding":return index==0?AuctionLot:null;
                case "fr_repeat":case "fr_dup_repeat":case "fr_um_replay":return index==0?Repeat:null;
            }
            // Actions that spend or build one of an enemy's own resources wear that resource's mark.
            switch(type)
            {
                case EnemyActionType.Growth:case EnemyActionType.GrowthSet:case EnemyActionType.GrowthAlly:case EnemyActionType.PlantSeed:return Growth;
                case EnemyActionType.PlateSpend:case EnemyActionType.PlateGain:return OrbitPlate;
                case EnemyActionType.Momentum:case EnemyActionType.MomentumReset:return Momentum;
                case EnemyActionType.ReserveSpend:case EnemyActionType.ReserveAll:case EnemyActionType.ReserveGain:case EnemyActionType.ReserveSet:return Reserve;
                case EnemyActionType.BonusConsume:return BonusGold;
            }
            return null;
        }

        // The icon drawn beside an enemy's counter (null = no icon; Heat keeps its own existing Heat icon).
        public static string MechanicIcon(string enemyId,string label)
        {
            if(string.IsNullOrEmpty(label)||label=="HEAT")return null;
            if(label.StartsWith("BONUS GOLD",StringComparison.Ordinal))return BonusGold;
            if(label.StartsWith("RESERVE",StringComparison.Ordinal)||label.StartsWith("ROYAL RESERVE",StringComparison.Ordinal))return Reserve;
            if(enemyId!=null&&enemyId.StartsWith("hollowwood_",StringComparison.Ordinal)&&(label=="GROWTH"||label.EndsWith("· GROWTH",StringComparison.Ordinal)))return Growth;
            if(label=="ORBIT PLATES")return OrbitPlate;
            if(label=="MOMENTUM"&&Is(enemyId,"fallen_comet"))return Momentum;
            if(Is(enemyId,"_confessor")||Is(enemyId,"oathbreaker_priest"))return Judgment;
            if(Is(enemyId,"final_bishop")&&label.StartsWith("PHASE 2",StringComparison.Ordinal))return Judgment;
            if(Is(enemyId,"execution_herald")&&label.StartsWith("SENTENCE ",StringComparison.Ordinal))return Sentence;
            if(Is(enemyId,"nameless_seer")&&label=="SENTENCE PREPARED")return PreparedAttack;
            if(label=="TOLL")return Toll;
            if(label.Contains("PENDING ECHO")||label=="NO ECHO")return Echo;
            if(Is(enemyId,"splitling")||Is(enemyId,"split_sovereign"))return Split;
            if(Is(enemyId,"thronebreaker")&&label.StartsWith("SIEGE",StringComparison.Ordinal))return Siege;
            if(Is(enemyId,"elite_worldbreaker")&&label.StartsWith("CHARGE",StringComparison.Ordinal))return WorldBreakCharge;
            if(Is(enemyId,"elite_deepcrawler")&&label.StartsWith("BURROWED",StringComparison.Ordinal))return BurrowWarning;
            if(Is(enemyId,"phase_beast")||Is(enemyId,"living_icon")||Is(enemyId,"crown_duelmaster")||Is(enemyId,"pale_chimera")||Is(enemyId,"elite_wayfarer"))
                return label.StartsWith("FORM",StringComparison.Ordinal)||label.StartsWith("STANCE",StringComparison.Ordinal)||label.StartsWith("STATE",StringComparison.Ordinal)?FormChange:null;
            if(Is(enemyId,"shifting_husk")&&(label=="ARMORED"||label=="EXPOSED"))return FormChange;
            return null;
        }

        // The icon on the header of a forecast panel (Future Intent, Possible Next Actions, Upcoming Lot ...).
        public static string ForecastIcon(string enemyId,string title)
        {
            if(string.IsNullOrEmpty(title))return null;
            if(title.StartsWith("UPCOMING LOT",StringComparison.Ordinal))return AuctionLot;
            if(title.StartsWith("UPCOMING REPLAY",StringComparison.Ordinal))return Repeat;
            if(enemyId!=null&&enemyId.StartsWith("fractured_realm_",StringComparison.Ordinal))return title.StartsWith("POSSIBLE",StringComparison.Ordinal)?DualPossibility:null;
            if(enemyId!=null&&enemyId.StartsWith("shattered_observatory_",StringComparison.Ordinal))return Prediction;
            return null;
        }

        // Short, plain-language tooltip line for an icon. Never exposes implementation names.
        public static string Tip(string icon)
        {
            switch(icon)
            {
                case Command:return "Orders one random living Minion it owns to repeat its last action.";
                case AuctionLot:return "A Lot is up for bidding. It resolves on a later action.";
                case Repeat:return "Repeats a previous completed action.";
                case ThronebreakerCharge:return "Siege complete. The Thronebreaker charges at full force.";
                case WorldBreak:return "WORLD BREAK. One enormous hit, announced a full turn ahead.";
                case Eruption:return "The Deepcrawler bursts out of the ground. It never left the fight.";
                case BonusGold:return "Extra reward for beating this enemy. Not your Gold; it can't be stolen.";
                case Reserve:return "Stored resource that powers its stronger action.";
                case Growth:return "Builds to 3. At full Growth it may use a stronger Bloom action.";
                case Prediction:return "A future action is shown ahead of time.";
                case OrbitPlate:return "Orbit Plates absorb attention before the enemy is hurt. They are consumed and rebuilt.";
                case Momentum:return "Builds as it keeps moving. At full Momentum its next strike hits harder.";
                case Judgment:return "Judges how you played last turn and answers accordingly.";
                case Sentence:return "Countdown. At zero the Sentence is carried out.";
                case Toll:return "Each Toll brings the great strike closer.";
                case Echo:return "A stored effect repeats on a future action.";
                case Split:return "Can break into fragments, which are Minions of the original.";
                case Fracture:return "Enemies here repeat, split and echo their actions.";
                case DualPossibility:return "Exactly two actions are possible next.";
                case RoyalOrder:return "A formation theme, not a buff. Throne enemies act in ranks.";
                case Siege:return "Counts down as the ram advances. At zero, the Thronebreaker charges.";
                case WorldBreakCharge:return "Charging World Break. It strikes when the charge completes.";
                case BurrowWarning:return "Burrowed; Eruption is next. It can still be attacked.";
                case FormChange:return "Cycles between named forms. The current form is shown in words.";
                case PreparedAttack:return "A major attack is prepared for next.";
                case SeizedGold:return "Holding your Gold. Defeating it returns the Gold.";
            }
            return null;
        }
    }

    public sealed partial class CombatState
    {
        // Icon for each action of enemy `index`'s current intent, using the move the enemy is about to use.
        private void AssignIntentIcons(int index,System.Collections.Generic.List<EnemyIntentAction> actions)
        {
            var move=MindAt(index)?.planned??"";var id=EnemyIdAt(index);
            for(var i=0;i<actions.Count;i++)
            {
                var icon=EnemyIconRules.IntentIcon(id,move,actions[i].type,i);
                actions[i].iconKey=icon;
                if(icon!=null)
                {
                    var tip=EnemyIconRules.Tip(icon);
                    if(!string.IsNullOrEmpty(tip)&&actions[i].detail!=null&&!actions[i].detail.Contains(tip))actions[i].detail+="\n"+tip;
                }
            }
        }
        // Icon for the counter pill of enemy `index` (null when the counter has none).
        public string CounterIcon(int index)
        {
            if(!WildCounter(index,out var label,out _,out _))return null;
            return EnemyIconRules.MechanicIcon(EnemyIdAt(index),label);
        }
        public string ForecastIcon(int index)
        {
            if(!WildForecast(index,out var title,out _,out _))return null;
            return EnemyIconRules.ForecastIcon(EnemyIdAt(index),title);
        }
    }
}
