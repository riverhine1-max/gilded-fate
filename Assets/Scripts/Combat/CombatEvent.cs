using GildedFate.Core;

namespace GildedFate.Combat
{
    public enum CombatPhase { Player, Enemy, Finished, EnemyResolved }
    public enum CardChoiceKind { None, DiscardFromHand, ReturnFromDiscard, ExhaustFromHand, ExhaustCurseFromHand, ExhaustSoulFromHand, ReturnAttackFromExhaust, ReturnSkillFromExhaust, AdaptMode, SigilMode, BuffToDouble, SigilSlot, BuffToGain, ExpansionCard, ExpansionOption }

    [System.Serializable]
    public sealed class PendingCardPlay
    {
        public CardDef card;
        public RemainingCardChoice remaining=new();
        public int remainingBonus;
        public int relicDamage,relicBlock,relicDamagePercent,relicBlockPercent,relicDrawAfter,relicPaidCost,relicOriginMask,relicBridgeOrigins;
        public bool relicEcho,relicPhantom,relicPrimaryRepeat,soulPlayRegistered;
        public bool relentlessPulseUsed;
        public System.Collections.Generic.List<int> relicRuptureChecked=new(),relicRuptureTargets=new();
        public int copiesRemaining,resolvedCopies;
        public int choiceFollowupDraw,choiceFollowupEnergy,choiceFollowupSigil=-1,choiceFollowupValue;
        public bool gilded,effectStarted;
        public bool shardFirstAttack,shardHeavyAttack,shardBastionTriggered;
        public int shardAttackBonus,shardDrawAfter,shardBlockTotal;
        public int additionalAttackCount,attackEffectBonus,attackCountReceipt=1;
        public CardChoiceKind choice;
        public PendingCardPlay Copy(){var copy=(PendingCardPlay)MemberwiseClone();copy.card=card?.Copy();copy.remaining=(remaining??new()).Copy();copy.relicRuptureChecked=new(relicRuptureChecked??new());copy.relicRuptureTargets=new(relicRuptureTargets??new());return copy;}
    }
    public enum CombatEventKind { Draw, Discard, Exhaust, Shuffle, CardResolved, Damage, Block, Heal, Status, Energy, Resonance, PlayerTurn, EnemyTurn, Death, ShardTrigger, EnemyAction, StateSnapshot, RelicTrigger }
    public enum CombatCardDestination { None, Hand, Draw, Discard, Exhaust, Deck }

    // Facts emitted by the rules, not commands that can apply an effect twice.
    public sealed class CombatEvent
    {
        public CombatEventKind kind;
        public CardDef card;
        public CombatCardDestination destination;
        public bool generatedCard;
        public int handIndex=-1,handCount;
        public int amount;
        public bool playerSide;
        public string label;
        // Post-event receipts for sequential HUD playback; never reapplied as rules.
        public bool hasVitals;
        public FighterState playerStatuses,enemyStatuses;
        public int playerRetaliation,spiritDirectionsUsed;
        public int hitId,enemyIndex;
        // Presentation-only receipts: slot ownership and ordered Sigil state.
        public int sigilSlot=-1;
        public SigilKind[] sigils;
        public int playerHp,enemyHp,playerBlock,enemyBlock;
        public CombatEvent(CombatEventKind kind,int amount=0,bool playerSide=false,CardDef card=null,string label="")
        {this.kind=kind;this.amount=amount;this.playerSide=playerSide;this.card=card;this.label=label;destination=kind==CombatEventKind.Draw?CombatCardDestination.Hand:CombatCardDestination.None;}
    }
}
