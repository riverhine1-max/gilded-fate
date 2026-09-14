using GildedFate.Audio;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private string audioRunId;
        private int audioGold;
        private ScreenMode audioScreen;
        private bool audioReady,wasCombatAudio;
        private void Sfx(SoundCue cue,float pan=0,float intensity=1,bool combatSound=false,float delay=0)
            =>GameAudio.Play(cue,intensity,pan,combatSound,delay);
        private void ResetGoldAudio(){audioRunId=run.runId;audioGold=run.gold;}
        private ScreenMode AudioContextScreen()
        {
            var page=screen;if(page==ScreenMode.Settings)page=settingsReturnScreen;
            if(page==ScreenMode.Collection&&viewingRunDeck)page=collectionReturnScreen;
            if(page==ScreenMode.CardRemove)page=ScreenMode.Merchant;
            if(page==ScreenMode.CardUpgrade)page=cardServiceReturnScreen;
            return page;
        }
        private void UpdateAudioPresentation()
        {
            if(profile==null)return;
            GameAudio.SetVolumes(profile.master,profile.music,profile.effects,profile.ui);
            var context=AudioContextScreen()==ScreenMode.Combat;
            if(wasCombatAudio&&!context)GameAudio.ClearCombat();wasCombatAudio=context;
            GameAudio.SetCombatPaused(context&&(combatPauseOpen||screen!=ScreenMode.Combat));
            AdaptiveMusicDirector.SetMood(MoodForScreen(),profile.music);
            AdaptiveMusicDirector.SetPaused(combatPauseOpen||screen==ScreenMode.Settings);
            if(!audioReady||audioRunId!=run.runId)ResetGoldAudio();
            else if(audioGold!=run.gold){Sfx(run.gold>audioGold?SoundCue.GoldCollect:SoundCue.GoldSpend,pan:-.25f);audioGold=run.gold;}
            if(audioReady&&screen!=audioScreen)
            {
                if((screen is ScreenMode.Reward or ScreenMode.RelicReward)&&BeginRewardPresentation())Sfx(SoundCue.RewardReveal,intensity:currentNode?.kind==NodeKind.Boss?1f:currentNode?.kind==NodeKind.Elite?.9f:.8f);
                if(screen==ScreenMode.Combat&&audioScreen!=ScreenMode.Collection&&audioScreen!=ScreenMode.Settings&&currentEnemy?.boss==true)Sfx(SoundCue.BossIntro);
            }
            audioReady=true;audioScreen=screen;
        }
        private static SoundCue AttackSound(CardDef card)=>card?.origin==CardOrigin.Reaper?SoundCue.AttackReaper:card?.origin==CardOrigin.Arcane?SoundCue.AttackArcane:SoundCue.AttackSteel;
        private static SoundCue VitalSound(CombatEvent fact,CardDef source)
        {
            if(fact.kind==CombatEventKind.Heal)return SoundCue.Heal;
            if(fact.kind==CombatEventKind.Block)
            {
                if(fact.label!="BLOCKED")return SoundCue.GainBlock;
                return (fact.playerSide?fact.playerBlock:fact.enemyBlock)<=0?SoundCue.BlockBreak:SoundCue.BlockHit;
            }
            if(fact.playerSide)return SoundCue.PlayerHurt;
            if(fact.label?.Contains("BURN")==true)return SoundCue.Burn;
            var card=fact.card??source;
            return card?.origin==CardOrigin.Reaper?SoundCue.HitReaper:card?.origin==CardOrigin.Arcane?SoundCue.HitArcane:fact.amount>=16?SoundCue.HitHeavy:SoundCue.HitSteel;
        }
        private void PlayVitalSound(CombatEvent fact,CardDef card)
        {
            if(fact.amount<=0)return;
            Sfx(VitalSound(fact,card),fact.playerSide?-.24f:.24f,Mathf.Lerp(.8f,1.05f,Mathf.Clamp01(fact.amount/24f)),true);
        }
        private void PlayStatusSound(CombatEvent fact,float delay)
        {
            if(fact.card?.kind==CardKind.Power||fact.label=="POWER")Sfx(SoundCue.Power,pan:-.15f,combatSound:true,delay:delay);
            else if(fact.sigilSlot>=0&&fact.label?.EndsWith(" ACTIVATE")==true)return; // Source already sounded.
            else if(fact.label?.StartsWith("TRIGGER:")==true)
                Sfx(TriggerSound(fact.label),pan:fact.playerSide?-.15f:.15f,intensity:.5f,combatSound:true,delay:delay);
            else Sfx(StatusFeedbackCue(fact),fact.playerSide?-.2f:.2f,.45f,true,delay);
        }
        private static SoundCue StatusFeedbackCue(CombatEvent fact)=>fact.label=="BURN"?SoundCue.Burn:fact.playerSide&&fact.amount>0?SoundCue.Energy:SoundCue.Debuff;
        private static SoundCue TriggerSound(string label)=>label switch
        {
            "TRIGGER:SIGIL EMBER"=>SoundCue.Burn,
            "TRIGGER:SIGIL HEX" or "TRIGGER:SIGIL WITHER"=>SoundCue.Debuff,
            "TRIGGER:SIGIL GRAVE"=>SoundCue.CardExhaust,
            "TRIGGER:SIGIL RUIN"=>SoundCue.HitArcane,
            "TRIGGER:SIGIL ECHO" or "TRIGGER:SIGIL MIRROR"=>SoundCue.Resonance,
            "TRIGGER:RETALIATE"=>SoundCue.HitSteel,
            _=>SoundCue.Power
        };
        private void PlayEventCue()
        {
            if(currentEvent==null)return;
            Sfx(currentEvent.ambientCue switch{"clock"=>SoundCue.EventClock,"shard"=>SoundCue.RewardShard,"whisper"=>SoundCue.EventWhisper,"thread" or "chains" or "ink"=>SoundCue.EventThread,_=>SoundCue.EventReveal});
        }
    }
}
