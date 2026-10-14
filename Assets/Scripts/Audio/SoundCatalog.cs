namespace GildedFate.Audio
{
    // Semantic cues backed by licensed stock-recording edits, with three variations.
    public enum SoundCue
    {
        UiHover, UiConfirm, UiBack, UiDenied, CardDraw, CardPickup, CardReturn, CardDiscard, CardExhaust, CardShuffle,
        AttackSteel, AttackArcane, AttackReaper, EnemyAttack, HitSteel, HitArcane, HitReaper, HitHeavy, PlayerHurt,
        GainBlock, BlockHit, BlockBreak, Heal, Power, Burn, Debuff, Resonance, Energy,
        GoldCollect, GoldSpend, RewardReveal, RewardCard, RewardRelic, RewardShard, Binding, Fateweave, Upgrade,
        RemoveCard, Victory, Defeat, BossIntro, BossPhase, MapStep, EventReveal, EventClock, EventWhisper, EventThread, EndTurn, TurnStart,
        // Added with the rebuilt sound set (Audio/SFX_Forged). The original set has no clips for these; see SoundCatalog.OriginalFallback.
        IntroAwaken, IntroDoor, IntroFlash, IntroStreaks, IntroClash, IntroCrown, IntroShimmer, EnemyDeath, CardPlay, CritHit
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
        /// <summary>
        /// The original effects set's equivalent of a cue: the cue itself when the original set has it, the sound the game used before for the cues added with the
        /// rebuilt set, or null when the original game made no sound there (so choosing ORIGINAL in Settings keeps the game exactly as it was).
        /// </summary>
        public static SoundCue? OriginalFallback(SoundCue cue)=>cue switch
        {
            SoundCue.IntroAwaken=>SoundCue.EventWhisper,
            SoundCue.IntroDoor=>SoundCue.Resonance,
            SoundCue.IntroFlash=>SoundCue.Fateweave,
            SoundCue.IntroStreaks=>null,
            SoundCue.IntroClash=>SoundCue.BossPhase,
            SoundCue.IntroCrown=>SoundCue.HitHeavy,
            SoundCue.IntroShimmer=>SoundCue.EventReveal,
            SoundCue.EnemyDeath=>null,
            SoundCue.CardPlay=>SoundCue.CardPickup,
            SoundCue.CritHit=>null,
            _=>cue
        };
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
            SoundCue.IntroAwaken or SoundCue.IntroDoor or SoundCue.IntroFlash or SoundCue.IntroStreaks or SoundCue.IntroShimmer=>new(.45f,.1f,7,1,0),
            SoundCue.IntroClash or SoundCue.IntroCrown=>new(.55f,.1f,8,1,0),
            SoundCue.EnemyDeath=>new(.5f,.2f,8,1,.02f),
            SoundCue.CardPlay=>new(.17f,.065f,2,3,.035f),
            SoundCue.CritHit=>new(.55f,.1f,8,1,.015f),
            _=>new(.30f,.075f,5,3,.02f)
        };
    }
}
