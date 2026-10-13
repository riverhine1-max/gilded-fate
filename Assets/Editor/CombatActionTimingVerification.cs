using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GildedFate.Chronicle;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Saving;
using GildedFate.UI;
using UnityEditor;
using UnityEngine;

namespace GildedFate.Editor
{
    // Runs the actual card and enemy coroutines with disk writes suspended before
    // entering Play Mode and across domain reload. No player saves are loaded into
    // the fixture profile, and normal combat rules resolve the test actions.
    [InitializeOnLoad]
    public static class CombatActionTimingVerification
    {
        const string Request="Library/CombatActionTiming.request",Output="Library/CombatActionTimingVerification.json";
        const string Pending="GildedFate.CombatActionTiming.Pending";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static double readyAt,deadline;
        static bool started;
        [Serializable] sealed class Report { public int checks;public List<string> failures=new();public List<string> observations=new(); }
        [Serializable] sealed class Index {public ChronicleDialogueTiming[] lines;}
        static CombatActionTimingVerification()
        {
            if(SessionState.GetBool(Pending,false))ProfileService.Suspended=SaveService.Suspended=true;
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state!=PlayModeStateChange.EnteredEditMode||!SessionState.GetBool(Pending,false))return;
                ProfileService.Suspended=SessionState.GetBool(Pending+".Profile",false);
                SaveService.Suspended=SessionState.GetBool(Pending+".Run",false);
                SessionState.SetBool(Pending,false);readyAt=deadline=0;started=false;
            };
        }
        [MenuItem("Gilded Fate/Verification/Verify Combat Action Timing (Isolated Saves)")]
        static void RequestVerification()=>File.WriteAllText(Request,"Verify timing and one Being response; preserve player saves.");
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(SessionState.GetBool(Pending,false))
            {
                if(!EditorApplication.isPlaying||EditorApplication.isPaused)return;
                if(readyAt==0){readyAt=EditorApplication.timeSinceStartup+3;deadline=readyAt+150;}
                if(EditorApplication.timeSinceStartup>deadline){File.WriteAllText(Output,"{\"checks\":0,\"failures\":[\"Runtime verification timed out\"]}");EditorApplication.ExitPlaymode();return;}
                if(started||EditorApplication.timeSinceStartup<readyAt)return;
                started=true;
                var menu=UnityEngine.Object.FindAnyObjectByType<GildedMainMenu>();
                if(!menu){File.WriteAllText(Output,"{\"checks\":0,\"failures\":[\"Game bootstrap unavailable\"]}");EditorApplication.ExitPlaymode();return;}
                menu.StartCoroutine(Verify(menu));return;
            }
            if(!File.Exists(Request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.GetLastWriteTimeUtc("Assets/Scripts/UI/GildedHeroActionTiming.cs")>File.GetLastWriteTimeUtc(typeof(GildedMainMenu).Assembly.Location))return;
            File.Delete(Request);
            SessionState.SetBool(Pending+".Profile",ProfileService.Suspended);SessionState.SetBool(Pending+".Run",SaveService.Suspended);
            SessionState.SetBool(Pending,true);ProfileService.Suspended=SaveService.Suspended=true;
            EditorApplication.EnterPlaymode();
        }
        static IEnumerator Verify(GildedMainMenu menu)
        {
            var report=new Report();
            void Check(bool pass,string message){report.checks++;if(!pass)report.failures.Add(message);}
            object Get(string name)=>typeof(GildedMainMenu).GetField(name,Private).GetValue(menu);
            void Set(string name,object value)=>typeof(GildedMainMenu).GetField(name,Private).SetValue(menu,value);
            object Call(string name,params object[] args)=>typeof(GildedMainMenu).GetMethod(name,Private).Invoke(menu,args);
            string IntentSignature(int owner)=>string.Join("|",((List<EnemyIntentAction>)Call("EnemyIntents",owner)).Select(a=>a.type+":"+a.ValueText+":"+a.prevented));
            var profile=new PlayerProfile();profile.EnsureMeta();profile.cardAnimationSpeed=1;profile.confirmEndTurn=false;
            Set("profile",profile);Set("captureMode",true);Set("combatTestInput",true);
            foreach(var flag in new[]{"bootIntroActive","runStartActive","chronicleActive","chronicleMilestoneActive"})Set(flag,false);
            var index=JsonUtility.FromJson<Index>(Resources.Load<TextAsset>("Chronicle/Audio/DialogueIndex").text);
            foreach(ChronicleHero hero in Enum.GetValues(typeof(ChronicleHero)))
            foreach(var victory in new[]{false,true})
            for(var variant=0;variant<3;variant++)
            {
                var scene=ChronicleMilestoneScenes.Ending(victory,hero,variant);
                Check(scene.commands.Count(c=>c.style==ChronicleTextStyle.DeeperVoice)==1,"Exactly one Being line: "+hero+"/"+victory+"/"+variant);
                foreach(var command in scene.commands.Where(c=>c.op==ChronicleOp.ShowNarration||c.style==ChronicleTextStyle.DeeperVoice))
                {
                    var speaker=command.style==ChronicleTextStyle.DeeperVoice?"BEING":"NARRATOR";
                    var line=index.lines.SingleOrDefault(l=>l.sceneId==scene.id&&l.commandId==command.id&&l.text==command.text&&l.speaker==speaker);
                    Check(line!=null&&line.Validate(command.text,out _),"Exact measured dialogue binding: "+scene.id+"/"+variant+"/"+command.id);
                    if(line!=null){var audio=Resources.Load<AudioClip>(line.audio);Check(audio&&Math.Abs(audio.length-line.durationSeconds)<.025,"Existing recording duration: "+line.lineId);}
                }
            }
            foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
            foreach(var scenario in new[]{"Attack","Block","Heavy","Finisher","FastBlock"})
            {
                var defense=scenario=="Block"||scenario=="FastBlock";
                profile.fastMode=scenario=="FastBlock";profile.cardAnimationSpeed=profile.fastMode?2:1;
                var pool=GameContent.Cards.Where(c=>c.hero==hero&&(defense?c.effect==EffectKind.Block:c.kind==CardKind.Attack&&c.effect==EffectKind.Damage));
                var card=scenario=="Heavy"?pool.First(c=>c.rarity!=Rarity.Rare&&(hero==HeroId.Vanguard?c.keywords.Contains("Heavy"):c.value>=(hero==HeroId.Hexer?11:12))):
                    scenario=="Finisher"?pool.First(c=>c.rarity==Rarity.Rare):pool.First(c=>c.cost<=2);
                Call("PrepareCombatCheck",card.id,5);
                yield return new WaitForSecondsRealtime(1);
                var combat=(CombatState)Get("combat");var hp=combat.enemy.hp;var block=combat.player.block;
                var view=((IDictionary)Get("handViews"))[combat.hand[0].instanceId];
                Check(view!=null,"Card presentation exists: "+hero+"/"+card.id);
                if(view==null)continue;
                Call("QueueCardPlay",view,0);
                var until=Time.unscaledTime+20;var observed=false;
                while((bool)Get("combatBusy")&&Time.unscaledTime<until)
                {
                    if(!observed&&(combat.enemy.hp<hp||combat.player.block>block))
                    {
                        observed=true;var surface=Get(hero.ToString().ToLowerInvariant()+"Videos");
                        var name=(string)surface.GetType().GetProperty("ActionName").GetValue(surface);
                        var source=(double)surface.GetType().GetProperty("ActionTime").GetValue(surface);
                        var pose=HeroActionTiming.PoseSeconds(hero.ToString(),name);
                        Check(source>=pose-.2,"Effect waits for contact/guard pose: "+hero+"/"+name);
                        report.observations.Add(hero+" "+name+": effect at source "+source.ToString("F3")+"s; pose "+pose.ToString("F2")+"s");
                    }
                    yield return null;
                }
                Check(observed,"Attack/Block resolved: "+hero+"/"+card.id);
                Check(!(bool)Get("combatBusy"),"Recovery releases card input: "+hero+"/"+card.id);
                Check(!(bool)typeof(GildedMainMenu).GetProperty("HeroVideoNeedsRecovery",Private).GetValue(menu),"Action recovery finishes before input unlocks: "+hero+"/"+scenario);
            }
            profile.fastMode=false;profile.cardAnimationSpeed=1;
            // A defeat deliberately holds its last video frame. It must bypass the
            // normal recovery wait, which belongs to attacks and skills.
            Call("StartReaperVideo","Defeat",.6f,100);
            Check(!((IEnumerator)Call("WaitForHeroVideoRecovery")).MoveNext(),"Defeat held frame does not add a ten-second recovery stall");
            foreach(var instant in new[]{false,true})
            {
                profile.instantEnemyTurns=instant;
                Call("PrepareIntentEncounter",new object[]{new[]{"vault_rat","rune_mage","masked_acolyte"},false});
                yield return new WaitForSecondsRealtime(1);
                var combat=(CombatState)Get("combat");var originalTurn=combat.turn;
                string[] executing=null;var stable=true;var lastHitAt=-1f;var releaseAt=-1f;var until=Time.unscaledTime+20;
                Call("QueueEndTurn");
                while((bool)Get("combatBusy")&&Time.unscaledTime<until)
                {
                    if((bool)Get("enemyIntentActionHeld"))
                    {
                        var shown=Enumerable.Range(0,combat.EnemyCount).Select(IntentSignature).ToArray();
                        if(executing==null)executing=shown;
                        else stable&=executing.SequenceEqual(shown);
                        if(combat.phase==CombatPhase.EnemyResolved&&((ICollection)Get("vitalBeats")).Count==0&&lastHitAt<0)lastHitAt=Time.unscaledTime;
                    }
                    if(combat.turn>originalTurn&&releaseAt<0)releaseAt=Time.unscaledTime;
                    yield return null;
                }
                Check(executing!=null&&stable,"All group intents retain the executing action: instant="+instant);
                Check(lastHitAt>=0&&releaseAt-lastHitAt>=.98f,"Next intents wait one real second after final receipt: instant="+instant);
                Check(!(bool)Get("enemyIntentActionHeld")&&!(bool)Get("combatBusy")&&combat.phase==CombatPhase.Player,"Enemy turn releases hold/input: instant="+instant);
                report.observations.Add("Group enemy intent reading hold: "+(releaseAt-lastHitAt).ToString("F3")+"s; instant="+instant);
            }
            Check(ProfileService.Suspended&&SaveService.Suspended,"Real profile and run disk writes remain suspended");
            File.WriteAllText(Output,JsonUtility.ToJson(report,true));
            Debug.Log("[Combat Timing] "+report.checks+" checks, "+report.failures.Count+" failures");
            EditorApplication.ExitPlaymode();
        }
    }
}
