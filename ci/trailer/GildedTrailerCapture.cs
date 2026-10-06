// Gilded Fate trailer capture harness.
// Only copied into the project by the "linux-trailer" CI build (see ci/trailer/prepare.sh).
// Inert unless the player is launched with:  -gfTrailer path/to/shots.json
// It drives the real game code (same methods the mouse/controller use) and renders
// every frame at a locked frame rate, piping raw frames into ffmpeg per shot.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GildedFate.UI
{
    /// <summary>Unscaled-time wait that follows Time.captureDeltaTime (WaitForSecondsRealtime does not).</summary>
    public sealed class TrailerWait : CustomYieldInstruction
    {
        private readonly float end;
        public TrailerWait(float seconds){end=Time.unscaledTime+seconds;}
        public override bool keepWaiting=>Time.unscaledTime<end;
    }

    [Serializable] public sealed class TrailerScript
    {
        public int fps;public int width;public int height;
        public string outputDir;public string ffmpeg;public string encoder;
        public bool stayOpen;
        public TrailerShot[] shots;
    }

    [Serializable] public sealed class TrailerShot
    {
        public string name,type,hero,enemyKind,evt,screenName;
        public int seed,act,floor;
        public string[] relics,deck,hand,draw,enemies,sigils,shards;
        public int[] shardUses;
        public bool setEnergy,setResonance,setPlayerHp,setEnemyHp,setGold;
        public int energy,strength,fortify,block,retaliation,resonance,playerHp,enemyHp,gold,echoArmed,enemyBlock,enemyBurn,enemyMarked;
        public bool bossIntro,skip,keepHint;
        public TrailerStep[] steps;
    }

    [Serializable] public sealed class TrailerStep
    {
        public string op,card,option,hero,text;
        public int target,index,lane;
        public float seconds,x,y,value;
    }

    public sealed class GildedTrailerDirector : MonoBehaviour
    {
        public static TrailerScript Script;
        public static bool Active;
        public static Vector2 Pointer=new Vector2(-9999,-9999);
        private static Process encoderProcess;
        private static Stream encoderInput;
        private static Texture2D grab;
        private static byte[] buffer;
        private static string shotName="";
        private static int shotFrames,totalFrames;
        private static StreamWriter log;
        private static float speed=1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();var i=Array.IndexOf(args,"-gfTrailer");
            if(i<0||i+1>=args.Length)return;
            try{Script=JsonUtility.FromJson<TrailerScript>(File.ReadAllText(args[i+1]));}
            catch(Exception e){Debug.LogError("[Trailer] Could not read script: "+e);Application.Quit(3);return;}
            if(Script==null){Debug.LogError("[Trailer] Empty script");Application.Quit(3);return;}
            if(Script.fps<=0)Script.fps=30;if(Script.width<=0)Script.width=1920;if(Script.height<=0)Script.height=1080;
            if(string.IsNullOrEmpty(Script.outputDir))Script.outputDir="trailer_out";
            if(string.IsNullOrEmpty(Script.ffmpeg))Script.ffmpeg="ffmpeg";
            if(string.IsNullOrEmpty(Script.encoder))Script.encoder="-c:v libx264 -preset medium -crf 14 -pix_fmt yuv420p";
            Script.shots??=Array.Empty<TrailerShot>();
            Directory.CreateDirectory(Script.outputDir);
            log=new StreamWriter(Path.Combine(Script.outputDir,"trailer_log.txt"),false){AutoFlush=true};
            Active=true;
            UnityEngine.Random.InitState(20260928);
            SetSpeed(1);
            var go=new GameObject("Gilded Fate · Trailer Director");DontDestroyOnLoad(go);go.AddComponent<GildedTrailerDirector>();
            Log($"boot fps={Script.fps} size={Script.width}x{Script.height} shots={Script.shots.Length}");
        }

        public static void Log(string line){Debug.Log("[Trailer] "+line);log?.WriteLine($"{DateTime.Now:HH:mm:ss} f{totalFrames} {line}");}
        public static void SetSpeed(float s){speed=Mathf.Clamp(s<=0?1:s,.05f,8f);Time.captureDeltaTime=speed/Script.fps;}
        public static void Sfx(string cue,int take,float gain,float pan,float pitch){if(encoderInput!=null)Log($"SFX shot={shotName} frame={shotFrames} cue={cue} take={take+1} gain={gain:0.000} pan={pan:0.00} pitch={pitch:0.000}");}
        public static void Mark(string text){Log($"MARK shot={shotName} frame={shotFrames} time={shotFrames/(float)Script.fps:0.000}s {text}");}

        private IEnumerator Start()
        {
            GildedMainMenu menu=null;
            while(menu==null){menu=FindAnyObjectByType<GildedMainMenu>();yield return null;}
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            for(var tries=0;tries<240&&(Screen.width!=Script.width||Screen.height!=Script.height);tries++)
            {if(tries%30==0)Screen.SetResolution(Script.width,Script.height,FullScreenMode.Windowed);yield return null;}
            for(var f=0;f<10;f++)yield return null;
            Log($"ready screen={Screen.width}x{Screen.height} captureDelta={Time.captureDeltaTime:0.0000} unscaledDelta={Time.unscaledDeltaTime:0.0000}");
            StartCoroutine(CaptureLoop());
            yield return menu.TrailerRun(Script);
            StopShot();
            Log("DONE");
            if(!Script.stayOpen)Application.Quit(0);
        }

        private static IEnumerator CaptureLoop()
        {
            var eof=new WaitForEndOfFrame();
            while(true){yield return eof;if(encoderInput!=null)WriteFrame();}
        }

        public static void StartShot(string name)
        {
            StopShot();
            shotName=string.IsNullOrEmpty(name)?"shot":name;shotFrames=0;
            var w=Screen.width;var h=Screen.height;
            var path=Path.GetFullPath(Path.Combine(Script.outputDir,shotName+".mp4"));
            var arguments=$"-y -loglevel error -f rawvideo -pix_fmt rgb24 -s {w}x{h} -framerate {Script.fps} -i - -vf vflip {Script.encoder} \"{path}\"";
            try
            {
                var info=new ProcessStartInfo(Script.ffmpeg,arguments){UseShellExecute=false,RedirectStandardInput=true,CreateNoWindow=true};
                encoderProcess=Process.Start(info);encoderInput=encoderProcess.StandardInput.BaseStream;
                Log($"RECORD {shotName} -> {path} ({w}x{h})");
            }
            catch(Exception e){Log("ffmpeg failed to start: "+e.Message);encoderProcess=null;encoderInput=null;}
        }

        public static void StopShot()
        {
            if(encoderInput==null)return;
            try{encoderInput.Flush();encoderInput.Close();}catch(Exception e){Log("close error "+e.Message);}
            try{encoderProcess?.WaitForExit(120000);}catch{}
            Log($"STOP {shotName} frames={shotFrames} seconds={shotFrames/(float)Script.fps:0.00}");
            encoderInput=null;encoderProcess=null;
        }

        private static void WriteFrame()
        {
            var w=Screen.width;var h=Screen.height;
            if(grab==null||grab.width!=w||grab.height!=h){grab=new Texture2D(w,h,TextureFormat.RGB24,false);buffer=new byte[w*h*3];}
            grab.ReadPixels(new Rect(0,0,w,h),0,0,false);grab.Apply(false);
            var data=grab.GetRawTextureData<byte>();
            if(data.Length!=buffer.Length){Log("frame size mismatch "+data.Length);return;}
            data.CopyTo(buffer);
            try{encoderInput.Write(buffer,0,buffer.Length);shotFrames++;totalFrames++;}
            catch(Exception e){Log("write error "+e.Message);StopShot();}
        }
    }

    public sealed partial class GildedMainMenu
    {
        private static T[] Arr<T>(T[] a)=>a??Array.Empty<T>();
        private static HeroId TrailerHero(string name)=>string.IsNullOrEmpty(name)?HeroId.Vanguard:(HeroId)Enum.Parse(typeof(HeroId),name,true);
        private static NodeKind TrailerKind(string name)=>string.IsNullOrEmpty(name)?NodeKind.Combat:(NodeKind)Enum.Parse(typeof(NodeKind),name,true);

        internal IEnumerator TrailerRun(TrailerScript script)
        {
            TrailerPrepareProfile();
            foreach(var shot in Arr(script.shots))
            {
                if(shot==null||shot.skip)continue;
                GildedTrailerDirector.Log("SHOT "+shot.name+" type="+shot.type);
                yield return TrailerSafe(TrailerShotRoutine(shot),shot.name);
                GildedTrailerDirector.StopShot();
                GildedTrailerDirector.SetSpeed(1);
            }
        }

        // Flattens nested coroutines by hand so an exception in one shot is logged and
        // skipped instead of silently stopping the whole render.
        private static IEnumerator TrailerSafe(IEnumerator root,string label)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                var top=stack.Peek();object current;bool moved;
                try{moved=top.MoveNext();current=moved?top.Current:null;}
                catch(Exception e){GildedTrailerDirector.Log("SHOT ERROR "+label+": "+e);yield break;}
                if(!moved){stack.Pop();continue;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}
                yield return current;
            }
        }

        private void TrailerPrepareProfile()
        {
            profile.reduceMotion=false;profile.fastMode=false;profile.cardAnimationSpeed=1f;profile.screenShake=true;profile.damageNumbers=true;
            profile.tooltips=false;profile.reduceFlashing=false;profile.reducedVfx=false;profile.highContrastUi=false;profile.highContrastIntents=false;
            profile.largeCardText=profile.largeIntents=profile.largeEffectIcons=profile.largeDamageNumbers=false;profile.colorblindStatus=false;
            profile.fullscreen=false;profile.vSync=false;profile.antiAliasing=8;profile.textureQuality=0;
            ApplySettings();QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;
            combatTestInput=true;
        }

        private void TrailerReset(TrailerShot s)
        {
            StopAllCoroutines();
            combatBusy=false;combatSequence=null;runStartActive=false;acquisitionActive=false;choicePresented=false;
            inspectedCard=null;inspectedRelic=null;combatPauseOpen=false;runPauseOpen=false;mapPauseOpen=false;pendingMapNode=null;
            bossIntroTime=0;pileOpen=-1;selectedShrineSlot=-1;controllerNavigation=false;combatTestInput=true;
            if(!s.keepHint){inputHint="";hintUntil=float.MaxValue;}
            shimmer=0;transitionAlpha=0;
            TrailerParkPointer();
        }

        private void TrailerParkPointer(){GildedTrailerDirector.Pointer=new Vector2(-9999,-9999);if(combat!=null&&screen==ScreenMode.Combat)HandleCombatPointer(new Vector2(-9999,-9999),false,false,false);}

        private RunCard TrailerRunCard(string spec)
        {
            // "card_id", "card_id+", "card_id+@golden_echo", "card_id@serrated"
            var mod="";var at=spec.IndexOf('@');if(at>=0){mod=spec.Substring(at+1);spec=spec.Substring(0,at);}
            var upgraded=spec.EndsWith("+");var id=upgraded?spec.Substring(0,spec.Length-1):spec;
            if(GameContent.Find(id)==null){GildedTrailerDirector.Log("unknown card "+id);return null;}
            var rc=run.AddCard(id,upgraded);
            if(!string.IsNullOrEmpty(mod))
            {
                if(WorldContent.Bindings.Any(b=>b.id==mod))run.ApplySpecialModification(rc,SpecialModificationKind.Binding,mod);
                else
                {
                    var def=rc.BuildDefinition();
                    switch(mod)
                    {
                        case "gilded_edge":run.ApplySpecialModification(rc,SpecialModificationKind.Fateweave,mod,4);break;
                        case "gilded_guard":run.ApplySpecialModification(rc,SpecialModificationKind.Fateweave,mod,0,5);break;
                        case "perfected_edge":run.ApplySpecialModification(rc,SpecialModificationKind.Fateweave,mod,(int)Math.Ceiling(def.value*.5f));break;
                        case "perfected_guard":run.ApplySpecialModification(rc,SpecialModificationKind.Fateweave,mod,0,(int)Math.Ceiling(def.value*.5f));break;
                        case "first_light":case "unbound_thread":run.ApplySpecialModification(rc,SpecialModificationKind.Fateweave,mod,firstDrawFree:true);break;
                        default:run.ApplySpecialModification(rc,SpecialModificationKind.Fateweave,mod);break;
                    }
                }
            }
            return rc;
        }

        private CardDef TrailerCombatCard(string spec)
        {
            var rc=TrailerRunCard(spec);if(rc==null)return null;
            var card=rc.BuildDefinition();card.instanceId=++combat.nextInstanceId;return card;
        }

        private IEnumerator TrailerShotRoutine(TrailerShot s)
        {
            TrailerReset(s);
            var hero=TrailerHero(s.hero);
            run.NewRun(hero,s.seed==0?20260928:s.seed);
            if(s.act>1)run.act=s.act;
            if(s.setGold)run.gold=s.gold;
            if(Arr(s.deck).Length>0){run.cards.Clear();foreach(var spec in s.deck)TrailerRunCard(spec);}
            foreach(var relic in Arr(s.relics))if(!run.relics.Contains(relic))run.AcquireRelic(relic);
            var shardList=Arr(s.shards);
            for(var i=0;i<shardList.Length;i++){run.AddShard(shardList[i]);var uses=Arr(s.shardUses);if(i<uses.Length&&i<run.shards.Count)run.shards[i].uses=uses[i];}
            var type=string.IsNullOrEmpty(s.type)?"combat":s.type;
            switch(type)
            {
                case "menu":selectingNewRun=true;screen=ScreenMode.Menu;break;
                case "characterSelect":selectingNewRun=true;selectedHero=hero;heroSelectionTime=Time.unscaledTime;screen=ScreenMode.CharacterSelect;break;
                case "fateweave":run.BeginFateweave();rewardRevealTime=1.4f;screen=ScreenMode.Fateweave;break;
                case "map":
                    if(s.floor>0){run.floor=s.floor;foreach(var node in run.nodes){node.available=node.floor==run.floor;node.complete=node.floor<run.floor;}}
                    mapFocusFloor=-1;screen=ScreenMode.Map;mapInputReadyAt=Time.unscaledTime+.22f;break;
                case "collection":collectionRelics=false;collectionScroll=0;screen=ScreenMode.Collection;break;
                case "relics":collectionRelics=true;collectionScroll=0;screen=ScreenMode.Collection;break;
                case "merchant":merchantSold.Clear();run.merchantSold.Clear();run.PrepareMerchantShard();run.stage=RunStage.Merchant;screen=ScreenMode.Merchant;break;
                case "sanctuary":run.stage=RunStage.Sanctuary;screen=ScreenMode.Sanctuary;break;
                case "treasure":run.stage=RunStage.Treasure;screen=ScreenMode.Treasure;break;
                case "event":
                    currentEvent=WorldContent.Events.FirstOrDefault(e=>e.id==s.evt)??WorldContent.Events[0];
                    EventSystem.BeginEvent(run,currentEvent);screen=ScreenMode.Event;break;
                case "screen":screen=(ScreenMode)Enum.Parse(typeof(ScreenMode),s.screenName,true);break;
                default:TrailerBeginCombat(s);break;
            }
            yield return null;
            foreach(var step in Arr(s.steps)){if(step!=null)yield return TrailerStepRoutine(s,step);}
        }

        private void TrailerBeginCombat(TrailerShot s)
        {
            var kind=TrailerKind(s.enemyKind);
            var floor=Mathf.Clamp(s.floor<0?0:s.floor,0,RunModel.FloorCount-1);
            var node=run.nodes.FirstOrDefault(n=>n.floor==floor&&n.lane==RunModel.LaneCount/2)??run.nodes.FirstOrDefault(n=>n.floor==floor)??run.nodes[0];
            node.kind=kind;node.available=true;run.floor=node.floor;
            currentNode=node;run.activeNodeFloor=node.floor;run.activeNodeLane=node.lane;
            run.PrepareEncounter(kind);
            var enemies=Arr(s.enemies);
            if(enemies.Length>1)run.activeEncounterEnemies=new List<string>(enemies);
            else if(enemies.Length==1)run.activeEncounterEnemies=new List<string>();
            currentEnemy=enemies.Length>0?WorldContent.Enemies.First(e=>e.id==enemies[0]):EnemyForNode(node);
            run.stage=RunStage.Combat;run.activeEnemyId=currentEnemy.id;
            BeginCombat(currentEnemy,kind==NodeKind.Boss?2:kind==NodeKind.Elite?1:0,kind==NodeKind.Boss&&s.bossIntro?2.4f:0);
            combat.TakeEvents();
            if(Arr(s.hand).Length>0)
            {
                combat.draw.AddRange(combat.hand);combat.hand.Clear();
                foreach(var spec in s.hand){var c=TrailerCombatCard(spec);if(c!=null)combat.hand.Add(c);}
            }
            var drawSpecs=Arr(s.draw);
            for(var i=drawSpecs.Length-1;i>=0;i--){var c=TrailerCombatCard(drawSpecs[i]);if(c!=null)combat.draw.Add(c);}
            if(Arr(s.sigils).Length>0){combat.sigils.Clear();foreach(var sigil in s.sigils)combat.sigils.Add((SigilKind)Enum.Parse(typeof(SigilKind),sigil,true));}
            if(s.setEnergy)combat.energy=s.energy;
            if(s.setResonance)combat.resonance=s.resonance;
            if(s.setPlayerHp)combat.player.hp=s.playerHp;
            if(s.setEnemyHp){combat.enemy.hp=s.enemyHp;if(combat.enemy.maxHp<s.enemyHp)combat.enemy.maxHp=s.enemyHp;}
            combat.player.strength+=s.strength;combat.player.fortify+=s.fortify;combat.player.block+=s.block;combat.retaliation+=s.retaliation;
            combat.enemy.block+=s.enemyBlock;combat.enemy.burn+=s.enemyBurn;combat.enemy.marked+=s.enemyMarked;
            combat.memory.echoArmed+=s.echoArmed;
            combat.events.Clear();
            foreach(var h in combat.hand)combat.events.Add(new CombatEvent(CombatEventKind.Draw,1,true,h));
            ResetCombatPresentation();
            if(!s.keepHint){inputHint="";hintUntil=float.MaxValue;}
            TrailerParkPointer();
        }

        private IEnumerator TrailerWaitIdle(float timeout)
        {
            var end=Time.unscaledTime+(timeout<=0?20:timeout);
            while(Time.unscaledTime<end&&(combatBusy||(screen==ScreenMode.Combat&&Time.unscaledTime<handReadyAt)||bossIntroTime>0||acquisitionActive||runStartActive||pendingMapNode!=null))yield return null;
            if(Time.unscaledTime>=end)GildedTrailerDirector.Log($"waitIdle timeout busy={combatBusy} choice={choicePresented} acq={acquisitionActive}");
        }

        private IEnumerator TrailerStepRoutine(TrailerShot s,TrailerStep st)
        {
            var op=st.op??"";
            switch(op)
            {
                case "record":GildedTrailerDirector.StartShot(string.IsNullOrEmpty(st.text)?s.name:st.text);break;
                case "stop":GildedTrailerDirector.StopShot();break;
                case "speed":GildedTrailerDirector.SetSpeed(st.value);break;
                case "mark":GildedTrailerDirector.Mark(st.text);break;
                case "wait":yield return new TrailerWait(st.seconds);break;
                case "intro":{BeginBootIntro(true);var end=Time.unscaledTime+(st.seconds>0?st.seconds:15);while(bootIntroActive&&Time.unscaledTime<end)yield return null;break;}
                case "waitIdle":yield return TrailerWaitIdle(st.seconds);break;
                case "play":yield return TrailerPlay(st);break;
                case "endTurn":yield return TrailerWaitIdle(20);QueueEndTurn();yield return null;break;
                case "gild":yield return TrailerWaitIdle(20);if(!TryGild())GildedTrailerDirector.Log($"gild failed: {GildBlockedHint()} gold={run.gold} cost={combat?.GildCost}");yield return null;break;
                case "shard":
                {
                    yield return TrailerWaitIdle(20);
                    if(st.index<0||st.index>=run.shards.Count){GildedTrailerDirector.Log("no shard "+st.index);break;}
                    var saved=run.shards[st.index];selectedShrineSlot=st.index;
                    yield return new TrailerWait(st.seconds>0?st.seconds:.35f);
                    QueueShard(WorldContent.FateShards.First(f=>f.id==saved.id),saved,st.index);selectedShrineSlot=-1;yield return null;break;
                }
                case "choose":yield return TrailerChoose(st);break;
                case "hero":selectedHero=TrailerHero(st.hero);heroSelectionTime=Time.unscaledTime;break;
                case "confirmHero":ConfirmSelectedHero();break;
                case "waitRunStart":
                {
                    var end=Time.unscaledTime+10;while(runStartActive&&Time.unscaledTime<end)yield return null;
                    run.NewRun(runStartHero,s.seed==0?20260928:s.seed);run.BeginFateweave();screen=ScreenMode.Fateweave;break;
                }
                case "fate":
                {
                    while(rewardRevealTime>.05f)yield return null;
                    if(st.index<0||st.index>=run.fateweaveOffers.Count)break;
                    var id=run.fateweaveOffers[st.index];var fate=WorldContent.Fateweaves.First(f=>f.id==id);
                    var cardsBefore=SnapshotCardCopies();var relicsBefore=SnapshotRelics();
                    if(run.SelectFateweave(id))BeginFateweavePull(fate,()=>{screen=run.stage==RunStage.FateweaveCard?ScreenMode.FateweaveCard:ScreenMode.Fateweave;PresentNewRunAcquisitions(cardsBefore,relicsBefore);});
                    break;
                }
                case "cut":
                {
                    // index into the strands left after the pull, or text = a fateweave id.
                    yield return TrailerWaitIdle(20);
                    var remaining=run.FateweaveCutCandidates();
                    var id=!string.IsNullOrEmpty(st.text)?(remaining.Contains(st.text)?st.text:null):st.index>=0&&st.index<remaining.Count?remaining[st.index]:null;
                    if(id==null||!CutFateweaveStrand(id)){GildedTrailerDirector.Log($"cut failed index={st.index} text={st.text} awaiting={run.FateweaveAwaitingCut}");break;}
                    if(st.seconds>0)yield return new TrailerWait(st.seconds);
                    break;
                }
                case "enterAct":
                {
                    // Older scripts never cut: sever the first remaining strand so the act can begin.
                    if(run.FateweaveAwaitingCut){var first=run.FateweaveCutCandidates().FirstOrDefault();if(first!=null&&!CutFateweaveStrand(first,false)){run.CutFateweave(first);observedGold=run.gold;}}
                    if(!run.CompleteFateweave()){GildedTrailerDirector.Log("enterAct: a cut is still owed");break;}
                    mapFocusFloor=-1;screen=ScreenMode.Map;mapInputReadyAt=Time.unscaledTime+.22f;break;
                }
                case "travel":
                {
                    while(Time.unscaledTime<mapInputReadyAt)yield return null;
                    var choices=run.nodes.Where(n=>n.floor==run.floor&&n.available).ToList();
                    var node=choices.FirstOrDefault(n=>n.lane==st.lane)??choices.FirstOrDefault();
                    if(node==null){GildedTrailerDirector.Log("no map node");break;}
                    if(!string.IsNullOrEmpty(st.text))node.kind=TrailerKind(st.text);
                    BeginMapTravel(node);yield return null;break;
                }
                case "mapScroll":{var from=mapScroll;var t0=Time.unscaledTime;var d=Mathf.Max(.01f,st.seconds);while(Time.unscaledTime-t0<d){mapScroll=Mathf.Lerp(from,st.value,Mathf.SmoothStep(0,1,(Time.unscaledTime-t0)/d));yield return null;}mapScroll=st.value;break;}
                case "collectionScroll":{var from=collectionScroll;var t0=Time.unscaledTime;var d=Mathf.Max(.01f,st.seconds);while(Time.unscaledTime-t0<d){collectionScroll=Mathf.Lerp(from,st.value,Mathf.SmoothStep(0,1,(Time.unscaledTime-t0)/d));yield return null;}collectionScroll=st.value;break;}
                case "menuFocus":controllerNavigation=true;menuControllerIndex=st.index;break;
                case "pointer":GildedTrailerDirector.Pointer=new Vector2(st.x,st.y);if(combat!=null&&screen==ScreenMode.Combat)HandleCombatPointer(new Vector2(st.x,st.y),false,false,false);break;
                case "park":TrailerParkPointer();break;
                case "inspectCard":inspectedCard=string.IsNullOrEmpty(st.card)?null:TrailerSpecDefinition(st.card);break;
                case "inspectRelic":inspectedRelic=string.IsNullOrEmpty(st.text)?null:GameContent.Relics.FirstOrDefault(r=>r.id==st.text);break;
                case "rewardCard":
                {
                    var end=Time.unscaledTime+8;while((screen!=ScreenMode.Reward||rewardRevealTime>.05f)&&Time.unscaledTime<end)yield return null;
                    var cards=RewardCards();if(cards==null||cards.Length==0)break;
                    var pick=cards[Mathf.Clamp(st.index,0,cards.Length-1)];
                    if(run.ClaimEncounterCard(pick.id))BeginCardAcquisition(pick,Advance);break;
                }
                case "advance":Advance();break;
                case "bossRelic":{var offers=run.BossRelicOffers();if(offers.Length>0)ClaimBossRelicChoice(offers[0]);break;}
                case "treasure":OpenTreasure();break;
                case "set":TrailerSet(st.text,st.value);break;
                case "screen":screen=(ScreenMode)Enum.Parse(typeof(ScreenMode),st.text,true);break;
                case "checkpoint":
                {
                    var json=JsonUtility.ToJson(combat.CaptureCheckpoint());
                    var back=JsonUtility.FromJson<CombatCheckpoint>(json);
                    var ok=back.TryRestore(out var restored,out var why);
                    var detail=ok?"":"restore failed: "+why;
                    if(ok)
                    {
                        if(restored.EnemyCount!=combat.EnemyCount||restored.wildCombat!=combat.wildCombat)detail+=" count/wild mismatch";
                        var before=combat.PreviewEnemyIntents();var after=restored.PreviewEnemyIntents();
                        for(var i=0;i<combat.EnemyCount;i++)
                        {
                            var a=combat.MindAt(i);var b=restored.MindAt(i);
                            if(combat.EnemyIdAt(i)!=restored.EnemyIdAt(i)||combat.EnemyAt(i).hp!=restored.EnemyAt(i).hp||combat.EnemyAt(i).block!=restored.EnemyAt(i).block)detail+=$" body{i}";
                            if((a==null)!=(b==null)||a!=null&&(a.uid!=b.uid||a.ownerUid!=b.ownerUid||a.step!=b.step||a.step2!=b.step2||a.phase!=b.phase||a.state!=b.state||a.counter!=b.counter||a.planned!=b.planned||a.lastMove!=b.lastMove||a.minion!=b.minion||a.mourning!=b.mourning||a.plannedValue!=b.plannedValue))detail+=$" mind{i}";
                            var sa=string.Join("+",before[i].Select(x=>x.type+":"+x.ValueText));var sb=string.Join("+",after[i].Select(x=>x.type+":"+x.ValueText));
                            if(sa!=sb)detail+=$" intent{i}({sa}|{sb})";
                        }
                        if(st.value>0&&detail==""){combat=restored;RestoreCombatPresentation();}
                    }
                    GildedTrailerDirector.Log((detail==""?"PASS · ":"FAIL · ")+"JsonUtility checkpoint round-trip "+s.name+" bodies="+combat.EnemyCount+" json="+json.Length+(detail==""?"":" ·"+detail));
                    break;
                }
                case "log":GildedTrailerDirector.Log(st.text+$" screen={screen} act={run.act} stage={run.stage} energy={combat?.energy} enemyHp={combat?.enemy?.hp}");break;
                default:GildedTrailerDirector.Log("unknown op "+op);break;
            }
        }

        private CardDef TrailerSpecDefinition(string spec)
        {
            var at=spec.IndexOf('@');if(at>=0)spec=spec.Substring(0,at);
            var up=spec.EndsWith("+");var def=GameContent.Find(up?spec.Substring(0,spec.Length-1):spec);
            return def==null?null:up?GameContent.Upgrade(def):def.Copy();
        }

        // Fills the profile with plausible history so Records screens can be reviewed.
        private void TrailerDemoMeta()
        {
            profile.EnsureMeta();
            var ids=new[]{"ACH_FIRST_ASCENT","ACH_WIN_VANGUARD","ACH_BOSS_HOLLOW_KING","ACH_DEBT_1","ACH_HIT_50","ACH_GILD_1","ACH_ELITES_10","ACH_DAILY_1","ACH_UNLOCK_FIRST"};
            foreach(var id in ids)if(!profile.achievements.Contains(id))profile.achievements.Add(id);
            profile.runHistory.Clear();
            var heroes=new[]{"Vanguard","Hexer","Reaper"};var killers=new[]{"","The Hollow King","Gilded Sentry","Vault Mother","","Executioner"};
            for(var i=0;i<8;i++)
            {
                var win=killers[i%killers.Length]=="";
                profile.runHistory.Add(new GildedFate.Saving.RunRecord{date=System.DateTime.UtcNow.AddDays(-i).ToString("yyyy-MM-dd HH:mm"),hero=heroes[i%3],victory=win,killedBy=killers[i%killers.Length],fateDebt=i%4,act=win?3:1+i%3,floor=win?17:4+i*2,score=win?2400-i*90:600+i*140,seconds=1500+i*230,highestHit=40+i*17,gilds=i%5,marks=win?90:20+i*6,
                    deck=new System.Collections.Generic.List<string>(run.deck.Take(12).Select(id=>GameContent.Find(id.TrimEnd('+'))?.name??id)),relics=new System.Collections.Generic.List<string>(GameContent.Relics.Skip(i).Take(4).Select(r=>r.name))});
            }
            profile.winsByHero[0]=3;profile.winsByHero[1]=1;profile.totalGilds=14;profile.dailyRunsCompleted=2;
        }

        private void TrailerSet(string key,float value)
        {
            var v=Mathf.RoundToInt(value);
            // Meta-progression QA keys (work outside combat).
            if(key.StartsWith("wildHp")&&combat!=null)
            {
                var i=int.Parse(key.Substring(6));if(i<combat.EnemyCount){combat.EnemyAt(i).hp=v;combat.RefreshEnemyState();ConsumeCombatEvents(combat.TakeEvents(),null,0);UpdateBossPhaseVisual();}
                return;
            }
            switch(key)
            {
                case "ashenTheme":run.actThemes=new System.Collections.Generic.List<string>{ActThemes.AshenWilds,ActThemes.Vault,ActThemes.Vault};return;
                case "foundryTheme":run.actThemes=new System.Collections.Generic.List<string>{ActThemes.Vault,ActThemes.CrimsonFoundry,ActThemes.Vault};run.act=2;return;
                case "hollowwoodTheme":run.actThemes=new System.Collections.Generic.List<string>{ActThemes.Vault,ActThemes.Hollowwood,ActThemes.Vault};run.act=2;return;
                case "ruinsTheme":run.actThemes=new System.Collections.Generic.List<string>{ActThemes.GildedRuins,ActThemes.Vault,ActThemes.Vault};return;
                case "cathedralTheme":run.actThemes=new System.Collections.Generic.List<string>{ActThemes.Vault,ActThemes.Vault,ActThemes.BlackCathedral};run.act=3;return;
                case "fracturedTheme":run.actThemes=new System.Collections.Generic.List<string>{ActThemes.Vault,ActThemes.Vault,ActThemes.FracturedRealm};run.act=3;return;
                case "observatoryTheme":run.actThemes=new System.Collections.Generic.List<string>{ActThemes.Vault,ActThemes.ShatteredObservatory,ActThemes.Vault};run.act=2;return;
                case "drownedTheme":run.actThemes=new System.Collections.Generic.List<string>{ActThemes.DrownedQuarter,ActThemes.Vault,ActThemes.Vault};return;
                case "settingsPage":settingsReturnScreen=ScreenMode.Menu;screen=ScreenMode.Settings;settingsOverview=false;settingsPage=v;settingsFocusIndex=0;return;
                case "settingsOverview":settingsReturnScreen=ScreenMode.Menu;screen=ScreenMode.Settings;settingsOverview=true;settingsFocusIndex=v;controllerNavigation=true;return;
                case "settingsFocus":controllerNavigation=true;settingsFocusIndex=v;return;
                case "displayConfirm":CaptureDisplayRevert();displayConfirmOpen=true;displayConfirmUntil=Time.unscaledTime+12;return;
                case "padStyle":profile.padPromptStyle=v;return;
                case "gameSpeed":profile.gameSpeed=v;return;case "confirmEndTurn":profile.confirmEndTurn=v!=0;captureAllowsEndTurnConfirm=v!=0;return;case "instantEnemyTurns":profile.instantEnemyTurns=v!=0;return;
                case "brightness":profile.brightness=value/100f;return;
                case "recordsTab":OpenRecords();recordsTab=v;return;
                case "historySelected":historySelected=v;return;
                case "fateDebtUnlocked":profile.EnsureMeta();for(var i=0;i<3;i++)profile.fateDebtUnlocked[i]=v;return;
                case "fateDebt":profile.EnsureMeta();for(var i=0;i<3;i++)selectedFateDebt[i]=v;return;
                case "marks":profile.EnsureMeta();for(var i=0;i<3;i++)profile.heroMarks[i]=v;profile.totalMarks=v*3;BindMetaUnlocks();return;
                case "demoMeta":TrailerDemoMeta();return;
                case "quitConfirm":quitConfirmOpen=v!=0;return;
                case "whatsNew":whatsNewOpen=v!=0;return;
                case "menuHub":screen=ScreenMode.Menu;OpenMenuHub((MenuHub)v);return;
                case "hubFocus":controllerNavigation=true;hubFocus=v;return;
                case "menuFocusIdx":controllerNavigation=true;menuControllerIndex=v;return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                case "playground":OpenPlayground();return;
                case "pgAdmin":OpenPlayground();pgAdminOpen=true;return;
                case "pgUnlockAll":PgUnlockCards();PgUnlockDebt();PgUnlockAchievements();PgSaveProfile("EVERYTHING UNLOCKED · all cards, Fate Debt X for every hero, all achievements.");GildedTrailerDirector.Log($"unlocked cards {PgUnlockedCardCount()}/{MetaUnlocks.LockedTotal()} debt {profile.fateDebtUnlocked[0]} ach {profile.achievements.Count}");return;
                case "pgReset":PgResetProgress();GildedTrailerDirector.Log($"reset cards {PgUnlockedCardCount()}/{MetaUnlocks.LockedTotal()} ach {profile.achievements.Count}");return;
                case "playgroundFight":OpenPlayground();pgEnemies[0]="gilded_sentry";pgEnemies[1]="gilded_sentry";pgEnemies[2]="";pgEnemies[3]="";StartPlaygroundFight();return;
#endif
                case "fateDebtCombat":if(combat==null)return;run.fateDebt=v;run.fateDebtMask=FateDebt.MaskForLevel(v);ApplyFateDebtToCombat(currentEnemy);GildedTrailerDirector.Log($"debt {v}: enemy hp={combat.enemy.hp}/{combat.enemy.maxHp} str={combat.enemy.strength} gildExtra={combat.gildCostExtra}");return;
            }
            if(combat==null)return;
            switch(key)
            {
                case "gamepad":if(UnityEngine.InputSystem.Gamepad.current==null)UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();menuUsesGamepad=v!=0;controllerNavigation=v!=0;break;
                case "reduceMotion":profile.reduceMotion=v!=0;break;case "reducedVfx":profile.reducedVfx=v!=0;break;
                case "reduceFlashing":profile.reduceFlashing=v!=0;break;case "screenShake":profile.screenShake=v!=0;break;
                case "enemyStrength":combat.enemy.strength=v;break;
                case "energy":combat.energy=v;break;case "strength":combat.player.strength=v;break;case "fortify":combat.player.fortify=v;break;
                case "block":combat.player.block=v;break;case "retaliation":combat.retaliation=v;break;case "resonance":combat.resonance=v;break;
                case "playerHp":combat.player.hp=v;break;case "enemyHp":combat.enemy.hp=v;break;case "echo":combat.memory.echoArmed=v;break;
                case "enemyBurn":combat.enemy.burn=v;break;case "enemyMarked":combat.enemy.marked=v;break;
                default:GildedTrailerDirector.Log("unknown set key "+key);break;
            }
        }

        private IEnumerator TrailerChoose(TrailerStep st)
        {
            var end=Time.unscaledTime+6;while(!choicePresented&&Time.unscaledTime<end)yield return null;
            if(!choicePresented){GildedTrailerDirector.Log("no choice presented");yield break;}
            yield return new TrailerWait(st.seconds>0?st.seconds:.45f);
            if(!string.IsNullOrEmpty(st.option)){var match=combat.ChoiceOptions.FirstOrDefault(o=>o.IndexOf(st.option,StringComparison.OrdinalIgnoreCase)>=0);choiceOptionSelected=match??st.option;}
            else
            {
                var spec=st.card??"";var up=spec.EndsWith("+");var id=up?spec.Substring(0,spec.Length-1):spec;
                var card=combat.ChoiceCards.FirstOrDefault(c=>c.id==id&&(!up||c.upgraded))??combat.ChoiceCards.FirstOrDefault();
                if(card!=null)SubmitCombatChoice(card);
            }
            yield return null;
        }

        private IEnumerator TrailerPlay(TrailerStep st)
        {
            yield return TrailerWaitIdle(20);
            var spec=st.card??"";var up=spec.EndsWith("+");var id=up?spec.Substring(0,spec.Length-1):spec;
            var index=combat.hand.FindIndex(c=>c.id==id&&(!up||c.upgraded));
            if(index<0){GildedTrailerDirector.Log("card not in hand: "+spec+" hand="+string.Join(",",combat.hand.Select(c=>c.id+(c.upgraded?"+":""))));yield break;}
            var card=combat.hand[index];
            if(!combat.CanPlay(card)){GildedTrailerDirector.Log($"cannot play {spec} energy={combat.energy} cost={combat.CostFor(card)}");yield break;}
            var targeted=combat.RequiresEnemyTarget(card);
            if(targeted)combatTargetIndex=Mathf.Clamp(st.target,0,Mathf.Max(0,combat.EnemyCount-1));
            var start=CardPickPoint(index);
            var target=targeted?(GroupCombat?GroupDropZone(combatTargetIndex).center:EnemyDropZone.center):new Vector2(CombatWidth*.5f,CombatHeight*.40f);
            var duration=st.seconds>0?st.seconds:.42f;
            HandleCombatPointer(start,true,true,false);yield return null;
            var lift=new Vector2(start.x,start.y-190);var t0=Time.unscaledTime;
            while(Time.unscaledTime-t0<duration)
            {
                var t=Mathf.SmoothStep(0,1,(Time.unscaledTime-t0)/duration);
                var a=Vector2.Lerp(start,lift,t);var b=Vector2.Lerp(lift,target,t);var p=Vector2.Lerp(a,b,t);
                GildedTrailerDirector.Pointer=p;HandleCombatPointer(p,false,true,false);yield return null;
            }
            HandleCombatPointer(target,false,true,false);yield return null;
            HandleCombatPointer(target,false,false,true);yield return null;
            if(!combatBusy&&combat.hand.Contains(card)&&handViews.TryGetValue(card.instanceId,out var view))
            {GildedTrailerDirector.Log("drag rejected, queueing directly: "+spec);combatPointer=target;QueueCardPlay(view);yield return null;}
            GildedTrailerDirector.Pointer=new Vector2(-9999,-9999);HandleCombatPointer(new Vector2(-9999,-9999),false,false,false);
        }
    }
}
