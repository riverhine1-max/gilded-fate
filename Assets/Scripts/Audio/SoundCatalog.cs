namespace GildedFate.Audio
{
    // Semantic cues backed by licensed stock-recording edits, with three variations.
    public enum SoundCue
    {
        UiHover, UiConfirm, UiBack, UiDenied, CardDraw, CardPickup, CardReturn, CardDiscard, CardExhaust, CardShuffle,
        AttackSteel, AttackArcane, AttackReaper, EnemyAttack, HitSteel, HitArcane, HitReaper, HitHeavy, PlayerHurt,
        GainBlock, BlockHit, BlockBreak, Heal, Power, Burn, Debuff, Resonance, Energy,
        GoldCollect, GoldSpend, RewardReveal, RewardCard, RewardRelic, RewardShard, Binding, Fateweave, Upgrade,
        RemoveCard, Victory, Defeat, BossIntro, BossPhase, MapStep, EventReveal, EventClock, EventWhisper, EventThread, EndTurn, TurnStart
    }
    public readonly struct SoundSpec
    {
        public readonly float gain,interval,pitchSpread;
        public readonly int priority,limit;
        public readonly bool ui;
        public SoundSpec(float gain,float interval=.035f,int priority=4,int limit=3,float pitchSpread=.025f,bool ui=false)
        {this.gain=gain;this.interval=interval;this.priority=priority;this.limit=limit;this.pitchSpread=pitchSpread;this.ui=ui;}
    }
    public static class SoundCatalog
    {
        public const int Variations=3;
        public static SoundSpec Get(SoundCue cue)=>cue switch
        {
            SoundCue.UiHover=>new(.065f,.14f,1,1,.008f,true),
            SoundCue.UiConfirm or SoundCue.UiBack=>new(.24f,.065f,2,2,.015f,true),
            SoundCue.UiDenied=>new(.27f,.14f,3,1,.008f,true),
            SoundCue.CardDraw or SoundCue.CardDiscard=>new(.12f,.045f,2,4,.045f),
            SoundCue.CardPickup or SoundCue.CardReturn or SoundCue.CardShuffle=>new(.17f,.065f,2,3,.035f),
            SoundCue.AttackSteel or SoundCue.AttackArcane or SoundCue.AttackReaper or SoundCue.EnemyAttack=>new(.29f,.035f,5,3,.03f),
            SoundCue.GainBlock=>new(.52f,.045f,7,3,.018f),
            SoundCue.BlockHit=>new(.43f,.025f,7,3,.028f),
            SoundCue.BlockBreak or SoundCue.PlayerHurt or SoundCue.HitHeavy=>new(.55f,.025f,8,3,.024f),
            SoundCue.HitSteel or SoundCue.HitArcane or SoundCue.HitReaper=>new(.49f,.025f,7,3,.03f),
            SoundCue.GoldCollect=>new(.44f,.15f,7,2,.015f),
            SoundCue.GoldSpend=>new(.28f,.12f,5,2,.02f),
            SoundCue.Victory or SoundCue.Defeat or SoundCue.BossIntro or SoundCue.BossPhase=>new(.44f,.6f,10,1,0),
            SoundCue.RewardRelic or SoundCue.Fateweave or SoundCue.RewardReveal=>new(.4f,.25f,8,2,.008f),
            SoundCue.RewardCard or SoundCue.RewardShard or SoundCue.Binding or SoundCue.Upgrade=>new(.37f,.12f,7,2,.012f),
            SoundCue.Power or SoundCue.Heal=>new(.37f,.08f,6,2,.018f),
            SoundCue.Resonance=>new(.18f,.12f,4,2,.018f),
            SoundCue.Energy or SoundCue.Debuff or SoundCue.Burn=>new(.22f,.055f,4,3,.02f),
            SoundCue.EventWhisper or SoundCue.EventClock or SoundCue.EventThread=>new(.25f,.3f,3,1,.015f),
            _=>new(.30f,.075f,5,3,.02f)
        };
    }
}
