using System;
using System.Collections;
using System.Collections.Generic;
using GildedFate.Audio;
using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private IEnumerator RunAudioChecks()
        {
            // All verification captures leave AudioListener paused: no sound is
            // sent to the user's speakers. These test routing, assets and timing.
            profile.master=1;profile.effects=1;profile.ui=1;UpdateAudioPresentation();
            yield return WaitForCombatQueue();yield return new WaitForSecondsRealtime(.35f);
            var clipIds=new HashSet<AudioClip>();
            foreach(SoundCue cue in Enum.GetValues(typeof(SoundCue)))
            {
                var count=0;
                for(var i=1;i<=SoundCatalog.Variations;i++)
                {
                    var clip=Resources.Load<AudioClip>("Audio/SFX/"+cue+"_"+i);
                    if(clip&&clip.channels==2&&clip.frequency==44100&&clip.length>.08f&&clipIds.Add(clip))
                    {
                        var samples=new float[Mathf.Min(44100,clip.samples*clip.channels)];var audible=false;
                        if(clip.loadState==AudioDataLoadState.Loaded&&clip.GetData(samples,0))foreach(var sample in samples)if(Mathf.Abs(sample)>.002f){audible=true;break;}
                        if(audible)count++;
                    }
                }
                CombatCheck(count==3,"Three loaded licensed-recording stereo variations: "+cue);
            }
            foreach(MusicMood mood in Enum.GetValues(typeof(MusicMood)))
            {
                var clip=Resources.Load<AudioClip>("Audio/Music/"+mood);
                CombatCheck(clip&&clip.channels==2&&clip.length>=170&&clip.loadType==AudioClipLoadType.Streaming,"Streamed full composed track: "+mood);
            }
            CombatCheck(GameAudio.MissingClips==0&&GameAudio.VoiceCount==24,"All sound banks load into a bounded 24-voice stage");
            var gain=new CombatEvent(CombatEventKind.Block,5,true);var absorb=new CombatEvent(CombatEventKind.Block,3,true,null,"BLOCKED"){playerBlock=4};var broken=new CombatEvent(CombatEventKind.Block,4,true,null,"BLOCKED"){playerBlock=0};
            CombatCheck(VitalSound(gain,null)==SoundCue.GainBlock&&VitalSound(absorb,null)==SoundCue.BlockHit&&VitalSound(broken,null)==SoundCue.BlockBreak,"Block gain, absorption and shield break have separate cues");
            CombatCheck(VitalSound(new CombatEvent(CombatEventKind.Damage,5,true),null)==SoundCue.PlayerHurt,"Player damage has a dedicated hurt cue");
            CombatCheck(AttackSound(GameContent.Find("strike"))==SoundCue.AttackSteel&&AttackSound(GameContent.Find("hex"))==SoundCue.AttackArcane&&AttackSound(GameContent.Find("soul_call"))==SoundCue.AttackReaper,"Three characters have distinct attack identities");
            CombatCheck(TriggerSound("TRIGGER:SIGIL EMBER")==SoundCue.Burn&&TriggerSound("TRIGGER:SIGIL RUIN")==SoundCue.HitArcane&&TriggerSound("TRIGGER:RETALIATE")==SoundCue.HitSteel,"Sigil and Retaliate source cues have distinct identities");
            CombatCheck(StatusFeedbackCue(new CombatEvent(CombatEventKind.Status,2,true,null,"STRENGTH"))==SoundCue.Energy&&StatusFeedbackCue(new CombatEvent(CombatEventKind.Status,2,false,null,"WEAK"))==SoundCue.Debuff,"Routine buffs, debuffs and Power activations use separate feedback families");
            CombatCheck(SoundCatalog.Get(SoundCue.Resonance).limit==2,"Repeated resonance is voice-limited");
            var before=GameAudio.PlayedCount;Sfx(SoundCue.GainBlock);var take=GameAudio.LastVariation;
            yield return new WaitForSecondsRealtime(.07f);Sfx(SoundCue.GainBlock);
            CombatCheck(GameAudio.PlayedCount==before+2&&GameAudio.LastVariation!=take,"Repeated Block chooses a different stock-recording variation");
            before=GameAudio.PlayedCount;for(var i=0;i<40;i++)Sfx(SoundCue.UiHover);
            CombatCheck(GameAudio.PlayedCount<=before+1,"Hover chatter is rate-limited");
            profile.effects=0;UpdateAudioPresentation();before=GameAudio.PlayedCount;Sfx(SoundCue.HitHeavy);
            CombatCheck(GameAudio.PlayedCount==before,"Effects slider mutes effects");
            yield return new WaitForSecondsRealtime(.1f);Sfx(SoundCue.UiConfirm);
            CombatCheck(GameAudio.PlayedCount==before+1,"UI remains independent while effects are muted");
            profile.ui=0;profile.effects=1;UpdateAudioPresentation();before=GameAudio.PlayedCount;Sfx(SoundCue.UiConfirm);Sfx(SoundCue.HitReaper);
            CombatCheck(GameAudio.PlayedCount==before+1&&GameAudio.LastCue==SoundCue.HitReaper,"Muting UI does not mute combat");
            profile.master=0;UpdateAudioPresentation();before=GameAudio.PlayedCount;Sfx(SoundCue.Victory);
            CombatCheck(GameAudio.PlayedCount==before,"Master mute covers every effect bus");
            profile.master=1;profile.ui=1;PrepareCombatCheck("strike",5);yield return WaitForCombatQueue();
            combatPauseOpen=true;UpdateAudioPresentation();before=GameAudio.PlayedCount;
            Sfx(SoundCue.HitHeavy,combatSound:true,delay:.12f);yield return new WaitForSecondsRealtime(.23f);
            CombatCheck(GameAudio.PlayedCount==before&&GameAudio.PendingCount>0,"Combat pause holds scheduled impact audio");
            combatPauseOpen=false;UpdateAudioPresentation();yield return new WaitForSecondsRealtime(.24f);
            CombatCheck(GameAudio.PlayedCount==before+1&&GameAudio.LastCue==SoundCue.HitHeavy,"Resume delivers the held impact exactly once");
            Sfx(SoundCue.HitHeavy,combatSound:true,delay:.4f);GameAudio.ClearCombat();before=GameAudio.PlayedCount;yield return new WaitForSecondsRealtime(.45f);
            CombatCheck(GameAudio.PlayedCount==before&&GameAudio.PendingCount==0,"Leaving combat cancels stale delayed sounds");
            var transitions=AdaptiveMusicDirector.TransitionCount;UpdateAudioPresentation();UpdateAudioPresentation();
            CombatCheck(AdaptiveMusicDirector.TransitionCount==transitions,"Repeated UI passes do not restart the music");
            collectionReturnScreen=ScreenMode.Combat;viewingRunDeck=true;screen=ScreenMode.Collection;
            CombatCheck(MoodForScreen()==MusicMood.Combat,"Inspecting the run deck preserves the combat score");
            settingsReturnScreen=ScreenMode.Collection;screen=ScreenMode.Settings;
            CombatCheck(MoodForScreen()==MusicMood.Combat,"Settings opened from the deck preserve the underlying score");
            screen=ScreenMode.Combat;viewingRunDeck=false;yield return new WaitForSecondsRealtime(.18f);
            ResetGoldAudio();run.gold+=25;before=GameAudio.PlayedCount;UpdateAudioPresentation();
            CombatCheck(GameAudio.PlayedCount==before+1&&GameAudio.LastCue==SoundCue.GoldCollect,"Gold gain uses the coin-collection cue exactly once");
            before=GameAudio.PlayedCount;UpdateAudioPresentation();CombatCheck(GameAudio.PlayedCount==before,"An unchanged gold total cannot replay the reward");
            yield return new WaitForSecondsRealtime(.18f);run.gold-=10;before=GameAudio.PlayedCount;UpdateAudioPresentation();
            CombatCheck(GameAudio.PlayedCount==before+1&&GameAudio.LastCue==SoundCue.GoldSpend,"Buying uses the distinct spend cue");
            PrepareCombatCheck("flurry",5);yield return WaitForCombatQueue();
            var hitCount=GameAudio.CueCount(SoundCue.HitSteel);var flurry=combat.hand[2];combatPointer=EnemyDropZone.center;QueueCardPlay(handViews[flurry.instanceId]);yield return WaitForCombatQueue();
            CombatCheck(!combat.hand.Contains(flurry),"Audio audit uses a valid targeted Flurry play");
            CombatCheck(GameAudio.CueCount(SoundCue.HitSteel)==hitCount+3,"Flurry sounds exactly three individual impacts, matching its three HP steps");
            Debug.Log($"[Gilded Fate Audio Audit] {combatInteractionChecks} checks · {combatInteractionFailures} failures · silent isolated routing verification");
        }
    }
}
