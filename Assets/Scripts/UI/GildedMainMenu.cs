using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GildedFate.Audio;
using GildedFate.Combat;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu : MonoBehaviour
    {
        private readonly List<Particle> motes = new();
        private GUIStyle titleStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle footerStyle;
        private Font bodyFont, labelFont, headingFont;
        private float mapScroll;
        private int mapFocusFloor = -1, mapControllerIndex, screenControllerIndex, menuControllerIndex;
        private ScreenMode controllerScreen=ScreenMode.Menu;
        private bool controllerNavigation;
        private MapNode pendingMapNode;
        private float mapTravelStartedAt;
        private HeroId selectedHero=HeroId.Vanguard;
        private float heroSelectionTime;
        private int guiRepaintCount;
        // Cache the root IMGUI pointer in the same 1440x810 canvas used to draw every
        // screen. Event.current.mousePosition is already Game-view local and top-left
        // based; ScreenToGUIPoint would convert it a second time and offset all custom
        // hover/drag hit tests when the Game view is scaled inside the Unity editor.
        private Vector2 guiPointerPosition;
        private bool guiPointerReady;
        private bool captureMode, typographyAudited;
        private int typographyFailures;
        private Texture2D white;
        private int hovered = -1;
        private float shimmer;
        private float impactFlash, gildedFlash;
        private float transitionAlpha;
        private ScreenMode previousScreen = ScreenMode.Menu;
        private int lastHoverAudio = -1;
        private float bossIntroTime, bossPhaseTime, rewardRevealTime, enemyVfxTime, playerVfxTime;
        private float goldCollectTime;
        private int goldCollectAmount,observedGold;
        private Vector2 goldCollectOrigin;
        private int enemyVfxIndex, playerVfxIndex;
        private int lastBossPhase = 1;
        private bool runResultVictory;
        private ScreenMode collectionReturnScreen = ScreenMode.Menu;
        private bool viewingRunDeck;
        private bool collectionRelics;
        private int collectionPage, collectionFilter, collectionSort;
        private float collectionScroll,cardServiceScroll,cardChoiceScroll;
        private CardDef inspectedCard;
        private RelicDef inspectedRelic;
        private bool selectingNewRun = true;
        private bool merchantRemoved, merchantHealed;
        private ScreenMode cardServiceReturnScreen;
        private int settingsPage;
        private bool mapPauseOpen;
        private ScreenMode settingsReturnScreen=ScreenMode.Menu;
        private readonly HashSet<string> merchantSold = new();
        private ScreenMode screen = ScreenMode.Menu;
        private readonly RunModel run = new();
        private CombatState combat;
        private string banner = "";
        private string runHudTooltipTitle,runHudTooltipDetail;
        private Rect runHudTooltipAnchor;
        private CardDef hoveredCardHelp;
        private Rect hoveredCardHelpAnchor;
        private MapNode currentNode;
        private EnemyDef currentEnemy;
        private EventDefinition currentEvent;
        private PlayerProfile profile;
        private Texture2D vaultBackground, heroBackground, reaperCharacter, deathsKeepsake, combatBackground, bossArenaAtlas, mapBackground, mapNodeAtlas, combatIconAtlas, combatReadabilityAtlas, combatVfxAtlas, cardFrameAtlas, bindingFateIconAtlas, hudEmblemAtlas, relicIconAtlas, fateShardIconAtlas, collectionBackground, rewardBackground, logoTexture, relicAtlas, relicAtlasBonus, consumableAtlas, vanguardCardAtlas, hexerCardAtlas, reaperCardAtlasA, reaperCardAtlasB, reaperCardAtlasC, neutralCardAtlas, enemyAtlas, locationAtlas, eventAtlasActI, eventAtlasActII, eventAtlasActIII, eventAtlasGeneral;

        private readonly string[] labels =
        {
            "CONTINUE RUN", "NEW RUN", "COLLECTION",
            "CHARACTERS", "SETTINGS", "CREDITS", "QUIT"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (FindAnyObjectByType<GildedMainMenu>() != null) return;
            var host = new GameObject("Gilded Fate · Main Menu");
            DontDestroyOnLoad(host);
            host.AddComponent<GildedMainMenu>();
        }

        private void Awake()
        {
            QualitySettings.vSyncCount=1;Application.targetFrameRate=120;
            white = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "GF UI Pixel" };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
            for (var i = 0; i < 90; i++) motes.Add(NewParticle(true));
            profile = ProfileService.Load();
            observedGold=run.gold;ResetGoldAudio();
            captureMode=!string.IsNullOrEmpty(CommandValue("-gfCapture"));
            bodyFont=Resources.Load<Font>("Fonts/SourceSans3-Regular");
            labelFont=Resources.Load<Font>("Fonts/SourceSans3-Semibold");
            headingFont=Resources.Load<Font>("Fonts/SourceSerif4Display-Semibold");
            if(!string.IsNullOrEmpty(CommandValue("-gfCapture"))){profile.fullscreen=false;profile.vSync=false;AudioListener.pause=true;var width=int.TryParse(CommandValue("-screen-width"),out var captureWidth)?captureWidth:1440;var height=int.TryParse(CommandValue("-screen-height"),out var captureHeight)?captureHeight:810;Screen.SetResolution(width,height,FullScreenMode.Windowed);}
            ApplySettings();
            vaultBackground=Resources.Load<Texture2D>("Art/GildedVault_Menu");
            heroBackground=Resources.Load<Texture2D>("Art/Heroes_Select");
            reaperCharacter=Resources.Load<Texture2D>("Art/ReaperCharacter");deathsKeepsake=Resources.Load<Texture2D>("Art/DeathsKeepsake");
            combatBackground=Resources.Load<Texture2D>("Art/CombatArena_LowerVault");
            bossArenaAtlas=Resources.Load<Texture2D>("Art/BossArenaAtlas_3");
            relicAtlas=Resources.Load<Texture2D>("Art/RelicAtlas_24"); relicAtlasBonus=Resources.Load<Texture2D>("Art/RelicAtlas_Bonus_12"); consumableAtlas=Resources.Load<Texture2D>("Art/ConsumableAtlas_8");
            vanguardCardAtlas=Resources.Load<Texture2D>("Art/VanguardCardAtlas_25"); hexerCardAtlas=Resources.Load<Texture2D>("Art/HexerCardAtlas_25");
            reaperCardAtlasA=Resources.Load<Texture2D>("Art/ReaperCardAtlas_A25");reaperCardAtlasB=Resources.Load<Texture2D>("Art/ReaperCardAtlas_B25");reaperCardAtlasC=Resources.Load<Texture2D>("Art/ReaperCardAtlas_C4");
            neutralCardAtlas=Resources.Load<Texture2D>("Art/NeutralCurseCardAtlas_12");
            enemyAtlas=Resources.Load<Texture2D>("Art/EnemyAtlas_17");
            locationAtlas=Resources.Load<Texture2D>("Art/LocationAtlas_4");
            eventAtlasActI=Resources.Load<Texture2D>("Art/EventAtlas_ActI_15");eventAtlasActII=Resources.Load<Texture2D>("Art/EventAtlas_ActII_15");eventAtlasActIII=Resources.Load<Texture2D>("Art/EventAtlas_ActIII_15");eventAtlasGeneral=Resources.Load<Texture2D>("Art/EventAtlas_General_5");
            mapBackground=Resources.Load<Texture2D>("Art/RunMap_LowerVault");
            mapNodeAtlas=Resources.Load<Texture2D>("Art/MapNodeAtlas_7");
            combatIconAtlas=Resources.Load<Texture2D>("Art/CombatIconAtlas_10");
            combatReadabilityAtlas=Resources.Load<Texture2D>("Art/CombatReadabilityAtlas_64");
            cardFrameAtlas=Resources.Load<Texture2D>("Art/CardFrameAtlas_4_v2");
            bindingFateIconAtlas=Resources.Load<Texture2D>("Art/BindingFateIconAtlas_16");
            combatVfxAtlas=Resources.Load<Texture2D>("Art/CombatVfxAtlas_8");
            hudEmblemAtlas=Resources.Load<Texture2D>("Art/HudEmblemAtlas_8");relicIconAtlas=Resources.Load<Texture2D>("Art/RelicIconAtlas_36");fateShardIconAtlas=Resources.Load<Texture2D>("Art/FateShardIconAtlas_30");
            collectionBackground=Resources.Load<Texture2D>("Art/CollectionArchive");rewardBackground=Resources.Load<Texture2D>("Art/RewardReliquary");
            logoTexture=Resources.Load<Texture2D>("Art/GildedFate_Logo");
            GameAudio.Initialize();GameAudio.SetVolumes(profile.master,profile.music,profile.effects,profile.ui);ResetGoldAudio();
            // The illustrated boss presentation is the visual language for every
            // encounter. Keep the authored 3D assets dormant so combat always uses
            // the large portrait composition and never mixes rendering styles.
        }

        private void Start()
        {
            PrepareRunStartMaterial();
            var capture=CommandValue("-gfCapture");if(string.IsNullOrEmpty(capture))return;
            if(capture=="audio-checks"){combatTestInput=true;PrepareCombatCheck("strike",5);}
            else if(capture=="credits")screen=ScreenMode.Credits;
            else if(ConfigureRunStartCapture(capture)){}
            else if(ConfigureHexerVideoCapture(capture)){}
            else if(ConfigureVanguardVideoCapture(capture)){}
            else if(ConfigureReaperVideoCapture(capture)){}
            else if(capture=="xbox-input"){ConfigureShardCapture("group-four");}
            else if(ConfigureStatusResonanceCapture(capture)){}
            else if(ConfigureScreenPassCapture(capture)){}
            else if(ConfigureIdentityCapture(capture)){}
            else if(ConfigureRuleIntegrityCapture(capture)){}
            else if(ConfigureQuickPlaytestCapture(capture)){}
            else if(ConfigureMasterPolishCapture(capture)){}
            else if(ConfigureReaperFixCapture(capture)){}
            else if(ConfigureRelicCapture(capture)){}
            else if(ConfigureMajorCapture(capture)){}
            else if(ConfigureExpansionCapture(capture)){}
            else if(ConfigureMapShopCapture(capture)){}
            else if(ConfigureShardCapture(capture)){}
            else if(ConfigureBindingCapture(capture)){}
            else if(ConfigureRunFlowCapture(capture)){}
            else if(ConfigureCombatCapture(capture)){}
            else if(capture=="combat"){run.NewRun(HeroId.Vanguard,20260902);run.AddShard("firstblood");currentNode=new MapNode{floor=5,lane=1,kind=NodeKind.Elite};currentEnemy=WorldContent.Enemies.First(e=>e.id=="mirror_witch");BeginCombat(currentEnemy,1);combat.player.block=11;combat.resonance=4;run.AcquireRelic("worn_whetstone");}
            else if(capture=="combat-hexer"){run.NewRun(HeroId.Hexer,20260902);run.AddShard("emberglass");currentNode=new MapNode{floor=4,lane=1,kind=NodeKind.Combat};currentEnemy=WorldContent.Enemies.First(e=>e.id=="gilded_sentry");BeginCombat(currentEnemy,0);combat.player.block=7;combat.resonance=3;}
            else if(capture=="combat-reaper"){run.NewRun(HeroId.Reaper,20260902);currentNode=new MapNode{floor=4,lane=1,kind=NodeKind.Combat};currentEnemy=WorldContent.Enemies.First(e=>e.id=="gilded_sentry");BeginCombat(currentEnemy,0);combat.PlayFree(GameContent.Find("soul_call"));ResetCombatPresentation();}
            else if(capture=="boss"){run.NewRun(HeroId.Hexer,20260902);currentNode=new MapNode{floor=RunModel.FloorCount-1,lane=RunModel.LaneCount/2,kind=NodeKind.Boss};currentEnemy=WorldContent.Enemies.First(e=>e.id=="last_dealer");BeginCombat(currentEnemy,2);bossIntroTime=0;combat.enemy.hp=55;combat.RefreshEnemyState();lastBossPhase=3;}
            else if(capture=="map"||capture=="map-summit"){run.NewRun(HeroId.Vanguard,20260902);if(capture=="map-summit"){run.floor=RunModel.FloorCount-1;foreach(var node in run.nodes){node.available=node.floor==run.floor;node.complete=node.floor<run.floor;}}screen=ScreenMode.Map;}
            else if(capture=="reward"){run.NewRun(HeroId.Hexer,20260902);currentNode=new MapNode{floor=4,lane=1,kind=NodeKind.Combat};rewardRevealTime=0;screen=ScreenMode.Reward;}
            else if(capture=="fateweave"){run.NewRun(HeroId.Hexer,20260902);run.act=2;run.BeginFateweave();rewardRevealTime=0;screen=ScreenMode.Fateweave;}
            else if(capture=="binding"){run.NewRun(HeroId.Vanguard,20260902);run.act=3;run.BeginBindingChoice();screen=ScreenMode.BindingSelect;}
            else if(capture=="shard-fractured"){run.NewRun(HeroId.Hexer,20260902);run.AddShard("emberglass");run.shards[0].uses=2;currentNode=new MapNode{floor=4,lane=1,kind=NodeKind.Combat};currentEnemy=WorldContent.Enemies.First(e=>e.id=="gilded_sentry");BeginCombat(currentEnemy,0);combat.player.block=7;combat.resonance=3;}
            else if(capture!="menu")
            {
                run.NewRun(HeroId.Vanguard,20260902);currentNode=run.nodes[0];
                if(capture=="characters"||capture=="characters-hexer"||capture=="characters-reaper"){selectedHero=capture=="characters-hexer"?HeroId.Hexer:capture=="characters-reaper"?HeroId.Reaper:HeroId.Vanguard;heroSelectionTime=Time.unscaledTime;screen=ScreenMode.CharacterSelect;}
                else if(capture=="collection"||capture=="inspection"||capture=="inspection-upgraded"||capture=="card-readability"||capture=="card-style"){screen=ScreenMode.Collection;if(capture=="inspection")inspectedCard=GameContent.Find("battle_rush");if(capture=="inspection-upgraded")inspectedCard=GameContent.Upgrade(GameContent.Find("battle_rush"));}
                else if(capture=="relics"||capture=="relic-inspection"){screen=ScreenMode.Collection;collectionRelics=true;if(capture=="relic-inspection")inspectedRelic=GameContent.Relics.OrderByDescending(r=>r.text.Length).First();}
                else if(capture=="merchant"){run.gold=225;screen=ScreenMode.Merchant;}
                else if(capture=="settings")screen=ScreenMode.Settings;
                else if(capture=="sanctuary")screen=ScreenMode.Sanctuary;
                else if(capture=="event"){currentEvent=WorldContent.Events[0];EventSystem.BeginEvent(run,currentEvent);screen=ScreenMode.Event;}
            }
            var path=CommandValue("-gfCapturePath");if(!string.IsNullOrEmpty(path))StartCoroutine(CaptureFrame(path));
        }

        private static string CommandValue(string key){var args=System.Environment.GetCommandLineArgs();for(var i=0;i<args.Length-1;i++)if(args[i]==key)return args[i+1];return string.Empty;}
        private IEnumerator CaptureFrame(string path)
        {
            var timeout=Time.realtimeSinceStartup+20f;
            while(!UnityEngine.Rendering.SplashScreen.isFinished&&Time.realtimeSinceStartup<timeout)yield return null;
            if(CommandValue("-gfCapture")=="combat-input")yield return RunCombatInteractionChecks();
            if(CommandValue("-gfCapture")=="hexer-video-input")yield return RunHexerVideoChecks();
            if(CommandValue("-gfCapture")=="vanguard-video-input")yield return RunVanguardVideoChecks();
            if(CommandValue("-gfCapture")=="reaper-video-input")yield return RunReaperVideoChecks();
            if(CommandValue("-gfCapture")=="reaper-video-attack")yield return PrepareReaperVideoCapture();
            if(CommandValue("-gfCapture")=="vanguard-video-attack")yield return PrepareVanguardVideoCapture();
            if(CommandValue("-gfCapture")=="hexer-video-attack")yield return PrepareHexerVideoCapture();
            if(CommandValue("-gfCapture")=="run-start-input")yield return RunStartTransitionChecks();
            if(CommandValue("-gfCapture")=="expansion-input")yield return RunExpansionInteractionChecks();
            if(CommandValue("-gfCapture")=="major-input")yield return RunMajorInteractionChecks();
            if(CommandValue("-gfCapture")=="major-new-input")yield return RunRemainingInteractionChecks();
            if(CommandValue("-gfCapture")=="relic-new-input")yield return RunRelicInteractionChecks();
            if(CommandValue("-gfCapture")=="polish-input")yield return RunMasterPolishChecks();
            if(CommandValue("-gfCapture")=="polish-menu-input")yield return RunMenuPolishChecks();
            if(CommandValue("-gfCapture")=="polish-complete-input")yield return RunCompletionChecks();
            if(CommandValue("-gfCapture")=="quick-input")yield return RunQuickPlaytestChecks();
            if(CommandValue("-gfCapture")=="integrity-input")yield return RunRuleIntegrityChecks();
            if(CommandValue("-gfCapture")=="xbox-input")yield return RunXboxInteractionChecks();
            if(CommandValue("-gfCapture")=="status-input")yield return RunStatusResonanceChecks();
            if(CommandValue("-gfCapture")=="screens-input")yield return RunScreenPassChecks();
            if(CommandValue("-gfCapture")=="identity-input")yield return RunIdentityChecks();
            if(CommandValue("-gfCapture")=="polish-final-input")yield return RunFinalPolishChecks();
            if(CommandValue("-gfCapture")=="polish-powers")yield return RunPowerPolishChecks();
            if(CommandValue("-gfCapture")=="polish-receipts")yield return RunPresentationReceiptChecks();
            if(CommandValue("-gfCapture") is "polish-attachments" or "polish-rewards")yield return PrepareReceiptGallery(CommandValue("-gfCapture"));
            if(CommandValue("-gfCapture")=="polish-intents-input")yield return RunEnemyIntentChecks();
            if(CommandValue("-gfCapture")=="polish-focus"){yield return new WaitForSecondsRealtime(.3f);OpenCombatHudFocus(7);yield return new WaitForSecondsRealtime(.2f);}
            if(CommandValue("-gfCapture")=="polish-tooltip"){yield return new WaitForSecondsRealtime(.3f);OpenCombatHudFocus(7);yield return new WaitForSecondsRealtime(.2f);}
            if(CommandValue("-gfCapture")=="reaper-fix-input")yield return RunReaperFixChecks();
            if(CommandValue("-gfCapture")=="group-input")yield return RunGroupInteractionChecks();
            if(CommandValue("-gfCapture")=="map-shop-input")yield return RunMapShopInteractionChecks();
            if(CommandValue("-gfCapture")=="audio-checks")yield return RunAudioChecks();
            if(CommandValue("-gfCapture")=="combat-save")yield return RunPersistenceChecks();
            if(CommandValue("-gfCapture")=="map-input")yield return RunMapFlowChecks();
            yield return new WaitForSecondsRealtime(1f);
            for(var i=0;i<18;i++)yield return new WaitForEndOfFrame();
            if(CommandValue("-gfCapture")=="identity-travel")
            {
                profile.reduceMotion=false;BeginMapTravel(run.nodes.First(n=>n.available&&n.floor==run.floor));mapTravelStartedAt=Time.unscaledTime-.32f;
                yield return new WaitForEndOfFrame();
            }
            yield return new WaitForEndOfFrame();
            var image=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,Screen.width,Screen.height),0,0,false);image.Apply(false,false);
            var visibleSamples=0;var totalSamples=0;
            for(var y=0;y<image.height;y+=24)for(var x=0;x<image.width;x+=24){var pixel=image.GetPixel(x,y);totalSamples++;if(pixel.maxColorComponent>.035f)visibleSamples++;}
            var blank=visibleSamples<totalSamples*.01f;
            File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);
            var failed=blank||!bodyFont||!labelFont||!headingFont||!typographyAudited||typographyFailures>0||combatInteractionFailures>0||persistenceFailures>0||runFlowFailures>0;
            Debug.Log($"[Gilded Fate Capture] {Screen.width}x{Screen.height} · GUI repaints {guiRepaintCount} · visible samples {visibleSamples}/{totalSamples} · fonts {(bodyFont&&labelFont&&headingFont?"loaded":"missing")} · typography failures {typographyFailures} · {(failed?"FAIL":"PASS")}");
            for(var i=0;i<8;i++)yield return new WaitForEndOfFrame();Application.Quit(failed?2:0);
        }
        private static Vector2 CanvasPointFromPhysical(Vector2 point,float screenHeight,float scale)=>new Vector2(point.x/scale,(screenHeight-point.y)/scale);
        private Vector2 PointerPosition
        {
            get
            {
                if(guiPointerReady)return guiPointerPosition;
                var mouse=UnityEngine.InputSystem.Mouse.current;
                return mouse!=null?CanvasPointFromPhysical(mouse.position.ReadValue(),Screen.height,CombatScale):Vector2.zero;
            }
        }

        private Particle NewParticle(bool randomY = false)
        {
            return new Particle
            {
                position = new Vector2(Random.value, randomY ? Random.value : 1.05f),
                speed = Random.Range(.012f, .045f),
                size = Random.Range(1f, 3.5f),
                phase = Random.value * 8f
            };
        }

        private void Update()
        {
            UpdateHexerVideos();UpdateVanguardVideos();UpdateReaperVideos();
            if(runStartActive){if(!runStartCaptureFrozen)AdvanceRunStart(Time.unscaledDeltaTime);return;}
            UpdatePointerNavigationMode();
            if(routeInspectionOpen){UpdateRouteInspectionInput();return;}
            UpdateAudioPresentation();
            UpdateControllerNavigation();
            UpdateMapPointerInput();
            UpdateMapTravel();
            UpdateCombatPresentation();
            UpdateCombat3D();
            shimmer += Time.unscaledDeltaTime;
            if(run.gold!=observedGold){if(run.gold>observedGold&&goldCollectTime<=0){goldCollectAmount=run.gold-observedGold;goldCollectOrigin=new Vector2(CombatWidth*.64f,CombatHeight*.48f);goldCollectTime=profile.reduceMotion?.32f:1.08f;}observedGold=run.gold;}
            impactFlash=Mathf.Max(0,impactFlash-Time.unscaledDeltaTime*4f);gildedFlash=Mathf.Max(0,gildedFlash-Time.unscaledDeltaTime*2.2f);
            if(screen!=previousScreen){previousScreen=screen;transitionAlpha=1f;}transitionAlpha=Mathf.Max(0,transitionAlpha-Time.unscaledDeltaTime*(profile!=null&&profile.fastMode?6f:2.8f));
            enemyVfxTime=Mathf.Max(0,enemyVfxTime-Time.unscaledDeltaTime*2.1f);playerVfxTime=Mathf.Max(0,playerVfxTime-Time.unscaledDeltaTime*2.1f);bossIntroTime=Mathf.Max(0,bossIntroTime-Time.unscaledDeltaTime);bossPhaseTime=Mathf.Max(0,bossPhaseTime-Time.unscaledDeltaTime);rewardRevealTime=Mathf.Max(0,rewardRevealTime-Time.unscaledDeltaTime*(profile!=null&&profile.fastMode?2f:1f));goldCollectTime=Mathf.Max(0,goldCollectTime-Time.unscaledDeltaTime);
            if(run.deck.Count>0&&!IsRunInspectionPaused&&screen!=ScreenMode.Menu&&screen!=ScreenMode.Settings&&screen!=ScreenMode.Statistics&&screen!=ScreenMode.Credits&&screen!=ScreenMode.RunResult)run.elapsedSeconds+=Time.unscaledDeltaTime;
            for (var i = 0; i < motes.Count; i++)
            {
                var p = motes[i];
                p.position.y -= p.speed * Time.unscaledDeltaTime;
                p.position.x += Mathf.Sin(shimmer * .35f + p.phase) * .000025f;
                if (p.position.y < -.05f) p = NewParticle();
                motes[i] = p;
            }
        }

        private void UpdatePointerNavigationMode()
        {
            if(captureMode)return;var pointer=Mouse.current;if(pointer==null)return;
            if(pointer.delta.ReadValue().sqrMagnitude>4||pointer.leftButton.wasPressedThisFrame||pointer.rightButton.wasPressedThisFrame||pointer.scroll.ReadValue().sqrMagnitude>0)UseCombatPointerMode();
        }

        private void UpdateControllerNavigation()
        {
            menuInputConsumed=false;if(captureMode)return;
            var input=ReadMenuNavigation();
            combatNavigationInput=input;
            if(RouteMenuNavigation(input)||screen==ScreenMode.Combat)return;
            HandleLegacyMenuNavigation(input);
        }
        private void HandleLegacyMenuNavigation(MenuNavigation input)
        {
            if(runStartActive)return;
            var previous=input.x<0||input.y<0;var next=input.x>0||input.y>0;
            var accept=input.accept;var back=input.back;var inspectPressed=input.inspect;
            if(inspectedRelic!=null){if(back||accept)inspectedRelic=null;return;}
            if(inspectedCard!=null){PrepareCardInspection(inspectedCard);if(back)inspectedCard=inspectionSource=null;else if(previous||next||accept)SelectInspectionVersion(!inspectionShowUpgrade);return;}
            if(!previous&&!next&&!accept&&!back&&!inspectPressed)return;
            controllerNavigation=true;if(controllerScreen!=screen){controllerScreen=screen;screenControllerIndex=0;}
            var delta=next?1:previous?-1:0;
            if(PerfectedScreenOpen){var options=run.PerfectedEligible();if(options.Length>0){screenControllerIndex=(screenControllerIndex+delta+options.Length)%options.Length;if(accept)ChoosePerfectedCard(options[screenControllerIndex]);else if(inspectPressed)inspectedCard=options[screenControllerIndex].BuildDefinition();}if(back){SaveService.Save(run);screen=ScreenMode.Menu;}return;}
            if(screen==ScreenMode.Menu)
            {
                if(delta!=0){do menuControllerIndex=(menuControllerIndex+delta+labels.Length)%labels.Length;while(menuControllerIndex==0&&!SaveService.HasRun);Sfx(SoundCue.UiHover);}
                if(accept&&(menuControllerIndex!=0||SaveService.HasRun))Activate(menuControllerIndex);return;
            }
            if(screen==ScreenMode.CharacterSelect)
            {
                if(delta!=0){selectedHero=(HeroId)(((int)selectedHero+(delta>0?1:2))%3);heroSelectionTime=Time.unscaledTime;Sfx(SoundCue.UiHover);}
                if(accept)ConfirmSelectedHero();else if(back)screen=ScreenMode.Menu;return;
            }
            if(screen==ScreenMode.Map)
            {
                if(mapPauseOpen){if(back)mapPauseOpen=false;return;}
                var available=run.nodes.Where(n=>n.floor==run.floor&&n.available).OrderBy(n=>n.lane).ToArray();if(available.Length==0)return;
                if(delta!=0){mapControllerIndex=(mapControllerIndex+delta+available.Length)%available.Length;Sfx(SoundCue.UiHover);}
                if(accept)BeginMapTravel(available[Mathf.Clamp(mapControllerIndex,0,available.Length-1)]);
                else if(back)mapPauseOpen=true;return;
            }
            if(screen==ScreenMode.Merchant)
            {
                if(MerchantBusy)return;run.PrepareMerchantStock();if(delta!=0)MoveScreenController(delta,13);
                if(inspectPressed&&screenControllerIndex<7){InspectUpgrade(GameContent.Find(run.merchantCardIds[screenControllerIndex]));return;}
                if(back){Advance();return;}if(!accept)return;
                if(screenControllerIndex<7)BuyShopCard(screenControllerIndex);else if(screenControllerIndex<9)BuyShopRelic(screenControllerIndex-7);else if(screenControllerIndex==9)BuyShopShard();else if(screenControllerIndex==10)OpenMerchantRemoval();else if(screenControllerIndex==11)BuyShopHeal();else if(screenControllerIndex==12)Advance();return;
            }
            if(screen==ScreenMode.Reward)
            {
                if(!run.combatGoldClaimed&&run.pendingCombatGold>0){if(accept)ClaimCombatGold(new Vector2(CombatWidth*.5f,CombatHeight*.48f));return;}
                if(run.encounterRewards?.relicClaimed==false&&!string.IsNullOrEmpty(run.encounterRewards.relicId)){if(accept)ClaimRolledRelic();return;}
                if(run.encounterRewards?.shardClaimed==false&&!string.IsNullOrEmpty(run.encounterRewards.shardId))return;
                var cards=RewardCards();var count=cards.Length+1;if(delta!=0)MoveScreenController(delta,count);
                screenControllerIndex=Mathf.Clamp(screenControllerIndex,0,count-1);
                if(inspectPressed&&screenControllerIndex<cards.Length){InspectUpgrade(cards[screenControllerIndex]);return;}
                if(accept&&rewardRevealTime<=.05f){if(screenControllerIndex<cards.Length){var chosen=cards[screenControllerIndex];if(run.ClaimEncounterCard(chosen.id)){SaveService.Save(run);BeginCardAcquisition(chosen,Advance);}}else Advance();}return;
            }
            if(screen==ScreenMode.Sanctuary)
            {
                if(delta!=0)MoveScreenController(delta,3);if(!accept)return;
                if(screenControllerIndex==0)RestAtShrine();
                else if(screenControllerIndex==1){collectionPage=0;cardServiceScroll=0;cardServiceReturnScreen=ScreenMode.Sanctuary;run.stage=RunStage.CardUpgrade;SaveService.Save(run);screen=ScreenMode.CardUpgrade;}
                else{collectionPage=0;cardChoiceScroll=0;run.BeginBindingChoice();SaveService.Save(run);screen=ScreenMode.BindingSelect;}return;
            }
            if(screen==ScreenMode.Event)
            {var count=currentEvent?.choices?.Length??0;if(delta!=0)MoveScreenController(delta,count);if(accept&&count>0)BeginEventChoice(currentEvent.choices[Mathf.Clamp(screenControllerIndex,0,count-1)]);return;}
            if(screen==ScreenMode.EventSelection)
            {HandleControllerEventSelection(delta,accept,back);return;}
            if(screen==ScreenMode.EventResult)
            {if(accept){EventSystem.CompleteResult(run);Advance();}return;}
            if(screen==ScreenMode.Treasure)
            {if(accept)OpenTreasure();return;}
            if(screen==ScreenMode.RelicReward)
            {if(currentNode?.kind==NodeKind.Boss){var offers=run.BossRelicOffers();if(delta!=0&&offers.Length>0)MoveScreenController(delta,offers.Length);if(accept&&offers.Length>0)ClaimBossRelicChoice(offers[Mathf.Clamp(screenControllerIndex,0,offers.Length-1)]);return;}if(accept&&currentNode?.kind==NodeKind.Treasure&&!ShardDiscoveryOpen)GrantRelic();return;}
            if(screen==ScreenMode.BindingSelect)
            {
                if(back){CancelBindingScreen();return;}
                var offers=run.bindingOffers.Select(id=>WorldContent.Bindings.FirstOrDefault(b=>b.id==id)).Where(b=>b!=null).ToArray();if(offers.Length==0)return;if(delta!=0)MoveScreenController(delta,offers.Length);
                if(accept){var binding=offers[screenControllerIndex];if(run.cards.Any(c=>c.specialModificationKind==SpecialModificationKind.None&&BindingMatches(binding,c.BuildDefinition()))&&run.SelectBinding(binding.id)){collectionPage=0;cardChoiceScroll=0;SaveService.Save(run);screen=ScreenMode.BindingCard;}}return;
            }
            if(screen is ScreenMode.BindingCard or ScreenMode.FateweaveCard)
            {HandleControllerPhysicalCardChoice(delta,accept,back);return;}
            if(screen==ScreenMode.Fateweave)
            {
                if(!string.IsNullOrEmpty(run.pendingFateweaveId)&&run.pendingChoicesNeeded==0){if(accept){run.CompleteFateweave();SaveService.Save(run);mapFocusFloor=-1;screen=ScreenMode.Map;}return;}
                var offers=run.fateweaveOffers.Select(id=>WorldContent.Fateweaves.FirstOrDefault(f=>f.id==id)).Where(f=>f!=null).ToArray();if(offers.Length==0)return;if(delta!=0)MoveScreenController(delta,offers.Length);
                if(accept){var fate=offers[screenControllerIndex];var cardsBefore=SnapshotCardCopies();var relicsBefore=SnapshotRelics();if(run.SelectFateweave(fate.id)){SaveService.Save(run);BeginFateweavePull(fate,()=>{screen=run.stage==RunStage.FateweaveCard?ScreenMode.FateweaveCard:ScreenMode.Fateweave;PresentNewRunAcquisitions(cardsBefore,relicsBefore);});}}return;
            }
            if(screen==ScreenMode.CardUpgrade||screen==ScreenMode.CardRemove)
            {HandleControllerDeckService(delta,accept,back,screen==ScreenMode.CardUpgrade);return;}
            if(screen==ScreenMode.Merchant)
            {if(back)Advance();return;}
            if(screen==ScreenMode.RunResult)
            {if(delta!=0)MoveScreenController(delta,2);if(accept){if(screenControllerIndex==0){selectingNewRun=true;selectedHero=HeroId.Vanguard;heroSelectionTime=Time.unscaledTime;screen=ScreenMode.CharacterSelect;}else screen=ScreenMode.Menu;}return;}
            if(screen==ScreenMode.Settings&&back){ProfileService.Save(profile);screen=settingsReturnScreen;return;}
            if(back)screen=ScreenMode.Menu;
        }

        private void MoveScreenController(int delta,int count){if(count>0){screenControllerIndex=(screenControllerIndex+delta+count)%count;Sfx(SoundCue.UiHover);}}
        private CardDef[] RewardCards()=>run.EncounterCardOffers(currentNode?.kind??NodeKind.Combat);
        private RelicDef[] RelicRewardPool(bool boss){var pool=GameContent.Relics.Where(r=>boss?r.rarity==Rarity.Boss:r.rarity is not (Rarity.Boss or Rarity.Special)&&!run.relics.Contains(r.id)).ToArray();return pool.Length>0?pool:GameContent.Relics.Where(r=>boss?r.rarity==Rarity.Boss:r.rarity is not (Rarity.Boss or Rarity.Special)).ToArray();}
        private void HandleControllerPhysicalCardChoice(int delta,bool accept,bool back)
        {
            if(back&&screen==ScreenMode.BindingCard){CancelBindingScreen();return;}
            if(screen==ScreenMode.FateweaveCard&&run.pendingCardOfferIds.Count>0)
            {
                var offers=run.pendingCardOfferIds.Select(GameContent.Find).Where(c=>c!=null).ToArray();if(delta!=0)MoveScreenController(delta,offers.Length);if(accept&&offers.Length>0){var shown=run.pendingFateweaveId=="entangled_fates"?GameContent.Upgrade(offers[screenControllerIndex]):offers[screenControllerIndex];if(run.ChooseFateweaveReward(offers[screenControllerIndex].id)){SaveService.Save(run);BeginCardAcquisition(shown,()=>screen=ScreenMode.Fateweave);}}return;
            }
            var choices=(screen==ScreenMode.BindingCard?run.cards:run.FateweaveEligibleCards()).ToArray();if(choices.Length==0)return;if(delta!=0)MoveScreenController(delta,choices.Length);screenControllerIndex=Mathf.Clamp(screenControllerIndex,0,choices.Length-1);
            if(!accept)return;var card=choices[screenControllerIndex];if(screen==ScreenMode.BindingCard){if(run.ApplyPendingBinding(card)){var shown=card.BuildDefinition();banner=shown.name+" IS NOW BOUND";SaveService.Save(run);BeginModificationAcquisition(shown,Advance);}}
            else if(run.ChooseFateweaveCard(card)){SaveService.Save(run);var shown=card.BuildDefinition();if(run.pendingFateweaveId=="severed_burden"){Sfx(SoundCue.PlayerHurt);if(run.pendingChoicesNeeded==0)screen=ScreenMode.Fateweave;}else BeginModificationAcquisition(shown,()=>{if(run.pendingChoicesNeeded==0)screen=ScreenMode.Fateweave;});}
        }
        private void HandleControllerEventSelection(int delta,bool accept,bool back)
        {
            if(back){if(EventSystem.CancelSelection(run)){collectionPage=0;SaveService.Save(run);screen=ScreenMode.Event;}return;}
            if(run.eventSelectionKind==EventSelectionKind.Card)
            {
                var cards=EventSystem.PendingEligibleCards(run).ToArray();if(cards.Length==0)return;if(delta!=0)MoveScreenController(delta,cards.Length);screenControllerIndex=Mathf.Clamp(screenControllerIndex,0,cards.Length-1);
                if(accept)ChooseEventCardWithAcquisition(cards[screenControllerIndex]);return;
            }
            if(run.eventSelectionKind==EventSelectionKind.ShardReplacement)
            {
                var decided=run.pendingEventShardDecisions.Select(d=>d.Split(new[]{"=>"},System.StringSplitOptions.None)[0]).ToHashSet();var newShard=run.pendingEventOfferIds.FirstOrDefault(id=>!decided.Contains(id));if(string.IsNullOrEmpty(newShard))return;var count=run.shards.Count+1;if(delta!=0)MoveScreenController(delta,count);
                if(accept){var replacement=screenControllerIndex<run.shards.Count?run.shards[screenControllerIndex].id:"discard";ChooseEventShardReplacementWithAcquisition(newShard,replacement);}return;
            }
            string[] offers;
            if(run.eventSelectionKind==EventSelectionKind.OwnedShard)offers=EventSystem.PendingEligibleShards(run).Select(s=>s.id).ToArray();
            else if(run.eventSelectionKind==EventSelectionKind.Relic)offers=EventSystem.PendingEligibleRelics(run).Select(r=>r.id).ToArray();
            else offers=run.pendingEventOfferIds.ToArray();
            if(offers.Length==0)return;if(delta!=0)MoveScreenController(delta,offers.Length);screenControllerIndex=Mathf.Clamp(screenControllerIndex,0,offers.Length-1);
            if(accept)ChooseEventOfferWithAcquisition(offers[screenControllerIndex]);
        }
        private void HandleControllerDeckService(int delta,bool accept,bool back,bool upgrading)
        {
            if(back){if(upgrading){run.stage=cardServiceReturnScreen==ScreenMode.Event?RunStage.Event:RunStage.Sanctuary;SaveService.Save(run);screen=cardServiceReturnScreen;}else{SaveMerchantState();screen=ScreenMode.Merchant;}return;}
            run.EnsureCardInstances();var entries=run.cards.Where(saved=>saved.BuildDefinition()!=null&&(!upgrading||saved.BuildDefinition().rarity is not (Rarity.Curse or Rarity.Status))).ToArray();if(entries.Length==0)return;
            if(delta!=0)MoveScreenController(delta,entries.Length);screenControllerIndex=Mathf.Clamp(screenControllerIndex,0,entries.Length-1);
            if(!accept)return;var target=entries[screenControllerIndex];if(upgrading){if(target.upgraded)return;var shown=GameContent.Upgrade(target.BuildDefinition());if(run.UpgradeCard(target)){banner=shown.name+" NOW BEARS THE GOLDEN RUNE";Advance();}}
            else CompleteMerchantRemoval(target);
        }

        private void BuildStyles()
        {
            if (titleStyle != null) return;
            var font = bodyFont ? bodyFont : GUI.skin.font;
            titleStyle = new GUIStyle(GUI.skin.label) { font = headingFont ? headingFont : font, fontSize = 46, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
            subtitleStyle = new GUIStyle(titleStyle) { font = font, fontSize = 16, fontStyle = FontStyle.Normal };
            // Frames are drawn by us. A Unity button background would hide the artwork.
            buttonStyle = new GUIStyle(GUI.skin.label) { font = labelFont ? labelFont : font, fontSize = 18, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(12,12,0,0) };
            foreach(var state in new[]{buttonStyle.normal,buttonStyle.hover,buttonStyle.active,buttonStyle.focused,buttonStyle.onNormal,buttonStyle.onHover,buttonStyle.onActive,buttonStyle.onFocused}){state.background=null;state.textColor=new Color(.97f,.94f,.85f);}
            buttonStyle.hover.textColor=Color.white;
            footerStyle = new GUIStyle(GUI.skin.label) { font=font,fontSize = 13, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(.8f,.77f,.7f) } };
        }

        private void OnGUI()
        {
            // Read this before applying GUI.matrix. Unity resets the matrix for each
            // OnGUI pass, so this is the unscaled, Game-view-local pointer.
            var rootEventPointer=Event.current.mousePosition;
            if(Event.current.type==EventType.Repaint)guiRepaintCount++;
            if(bodyFont)GUI.skin.font=bodyFont;
            BuildStyles();
            hoveredCardHelp=null;
            if(acquisitionActive&&(Event.current.type==EventType.MouseDown||Event.current.type==EventType.MouseUp||Event.current.type==EventType.KeyDown))Event.current.Use();
            if(captureMode&&!typographyAudited&&Event.current.type==EventType.Repaint)AuditCardTypography();
            // Author every screen against one coherent canvas, then fit that canvas to the
            // current Game view. This keeps the large cards and menu together at any aspect.
            var uiScale = Mathf.Max(.35f, Mathf.Min(Screen.width / 1440f, Screen.height / 810f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            // Cache once at the root canvas. Never re-convert inside clipped groups.
            guiPointerPosition=rootEventPointer/uiScale;
            if(captureMode&&CommandValue("-gfCapture")=="merchant-hover")guiPointerPosition=ShopCardRect(0).center;
            guiPointerReady=true;
            var w = Screen.width / uiScale;
            var h = Screen.height / uiScale;
            DrawBackdrop(w, h);
            if(runStartActive)
            {
                if(Event.current.isMouse||Event.current.isKey||Event.current.type==EventType.ScrollWheel)Event.current.Use();
                var enabled=GUI.enabled;GUI.enabled=false;DrawCharacterSelect(w,h);GUI.enabled=enabled;
                DrawRunStartTransition(w,h);return;
            }
            if(Event.current.type==EventType.Repaint)combatHudTargets.Clear();
            if(runPauseOpen&&screen!=ScreenMode.Settings){DrawRunHud(w);DrawUnifiedPauseMenu(w,h,false);return;}
            if(routeInspectionOpen){DrawRouteInspection(w,h);return;}
            if(PerfectedScreenOpen&&inspectedCard==null){DrawPerfectedSelection(w,h);return;}
            if(inspectedCard!=null){DrawCardInspection(w,h,inspectedCard);DrawScreenCardKeywordHelp(w,h);return;}
            if(severedCard!=null){DrawLocationBackdrop(w,h,0);DrawRunDock(w);DrawSeveredThread(w,h);return;}
            if(ShardDiscoveryOpen||acquisitionActive)GUI.enabled=false;

            if (screen == ScreenMode.CharacterSelect) { DrawCharacterSelect(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Map) { DrawMap(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Combat) { DrawCombat(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Reward) { DrawReward(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Collection) { DrawCollection(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Merchant) { DrawMerchant(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Sanctuary) { DrawSanctuary(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Event) { DrawEvent(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.EventSelection) { DrawEventSelection(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.EventResult) { DrawEventResult(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Treasure) { DrawTreasure(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.RelicReward) { DrawRelicReward(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Statistics) { DrawStatistics(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Settings) { DrawSettings(w, h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.CardUpgrade) { DrawDeckService(w,h,true); DrawTransition(w,h); return; }
            if (screen == ScreenMode.CardRemove) { DrawDeckService(w,h,false); DrawTransition(w,h); return; }
            if (screen == ScreenMode.BindingSelect) { DrawBindingSelect(w,h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.BindingCard) { DrawBindingCards(w,h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Fateweave) { DrawFateweave(w,h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.FateweaveCard) { DrawFateweaveCards(w,h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.Credits) { DrawCredits(w,h); DrawTransition(w,h); return; }
            if (screen == ScreenMode.RunResult) { DrawRunResult(w,h); DrawTransition(w,h); return; }

            var contentW = Mathf.Min(720f, w * .66f);
            var left = (w - contentW) * .5f;
            var titleY = Mathf.Max(55f, h * .095f);
            var pulse = .82f + Mathf.Sin(shimmer * 1.35f) * .12f;
            titleStyle.normal.textColor = new Color(1f, .72f + pulse * .12f, .28f, 1f);
            if(logoTexture){var old=GUI.color;GUI.color=new Color(1f,1f,1f,.9f+pulse*.08f);GUI.DrawTexture(new Rect(left-130,titleY-70,contentW+260,190),logoTexture,ScaleMode.ScaleToFit,true);GUI.color=old;}else ShadowLabel(new Rect(left, titleY, contentW, 82), "GILDED FATE", titleStyle);
            subtitleStyle.normal.textColor = new Color(.88f,.84f,.73f);
            GUI.Label(new Rect(left, titleY + 88, contentW, 30), "ENTER THE VAULT", subtitleStyle);

            var menuY = titleY + 154f;
            hovered = -1;
            for (var i = 0; i < labels.Length; i++)
            {
                var rect = new Rect(left + 82, menuY + i * 56, contentW - 164, 47);
                var over = rect.Contains(PointerPosition)||(controllerNavigation&&menuControllerIndex==i);
                if (over) hovered = i;
                DrawButtonFrame(rect, over, i == 0&&!SaveService.HasRun);
                GUI.enabled=i!=0||SaveService.HasRun;
                if (GUI.Button(rect, labels[i], buttonStyle)) Activate(i);
                GUI.enabled=true;
            }
            if(hovered>=0&&hovered!=lastHoverAudio)Sfx(SoundCue.UiHover);lastHoverAudio=hovered;

            if(!string.IsNullOrEmpty(banner)){var note=new GUIStyle(footerStyle){fontSize=12,normal={textColor=new Color(.95f,.67f,.24f)}};GUI.Label(new Rect(w*.2f,h-62,w*.6f,24),banner,note);}
            GUI.Label(new Rect(0, h - 34, w, 20), "THE GILDED VAULT AWAITS  ·  VERSION 0.1", footerStyle);
            DrawTransition(w,h);
        }

        private void DrawBackdrop(float w, float h)
        {
            Fill(new Rect(0, 0, w, h), new Color(.015f, .019f, .03f));
            if(vaultBackground){var old=GUI.color;GUI.color=new Color(.78f,.82f,.9f,1f);GUI.DrawTexture(new Rect(0,0,w,h),vaultBackground,ScaleMode.ScaleAndCrop);GUI.color=old;Fill(new Rect(0,0,w,h),new Color(.005f,.008f,.016f,.34f));}
            for (var i = 0; i < 8; i++)
            {
                var t = i / 7f;
                if(!vaultBackground)Fill(new Rect(0, h * t, w, h / 7f + 1), Color.Lerp(new Color(.025f, .045f, .075f), new Color(.012f, .009f, .018f), t));
            }

            var cx = w * .5f;
            var doorW = Mathf.Min(w * .48f, 620f);
            var door = new Rect(cx - doorW * .5f, h * .12f, doorW, h * .88f);
            if(!vaultBackground){Fill(door, new Color(.035f, .038f, .048f, .9f));Outline(door, new Color(.42f, .3f, .12f, .65f), 2);Outline(new Rect(door.x + 18, door.y + 18, door.width - 36, door.height - 18), new Color(.15f, .12f, .07f, .8f), 3);Fill(new Rect(cx - 1, door.y + 24, 2, door.height), new Color(.48f, .3f, .08f, .38f));}

            foreach (var p in motes)
            {
                var alpha = .2f + (.5f * (Mathf.Sin(shimmer + p.phase) * .5f + .5f));
                Fill(new Rect(p.position.x * w, p.position.y * h, p.size, p.size), new Color(1f, .66f, .18f, alpha));
            }
            Fill(new Rect(0, 0, w * .2f, h), new Color(0, 0, 0, .28f));
            Fill(new Rect(w * .8f, 0, w * .2f, h), new Color(0, 0, 0, .28f));
        }

        private void DrawButtonFrame(Rect rect, bool hover, bool disabled)
        {
            if(hover&&!disabled)Fill(new Rect(rect.x-5,rect.y-5,rect.width+10,rect.height+10),new Color(1f,.55f,.12f,.1f));
            Fill(rect, hover&&!disabled ? new Color(.22f, .135f, .035f, .92f) : new Color(.018f, .024f, .034f, .94f));
            Outline(rect, hover&&!disabled ? new Color(1f, .7f, .24f, .96f) : new Color(.42f, .32f, .16f, .76f), hover&&!disabled ? 3 : 2);Outline(new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10),new Color(.21f,.17f,.1f,.78f),1);
            Fill(new Rect(rect.x, rect.y, hover&&!disabled ? 8 : 4, rect.height), hover&&!disabled ? new Color(1f, .69f, .18f) : new Color(.45f, .32f, .13f));Fill(new Rect(rect.x+16,rect.y,rect.width-32,2),new Color(.85f,.57f,.18f,hover?.82f:.34f));
            var corner=7f;Fill(new Rect(rect.x-2,rect.y-2,corner,2),new Color(.95f,.68f,.24f));Fill(new Rect(rect.x-2,rect.y-2,2,corner),new Color(.95f,.68f,.24f));Fill(new Rect(rect.xMax-corner+2,rect.y-2,corner,2),new Color(.95f,.68f,.24f));Fill(new Rect(rect.xMax,rect.y-2,2,corner),new Color(.95f,.68f,.24f));
            if (disabled) Fill(new Rect(rect.xMax - 76, rect.center.y, 50, 1), new Color(.35f, .28f, .16f));
        }

        private void Activate(int index)
        {
            Sfx(SoundCue.UiConfirm);
            if (index == 0 && SaveService.HasRun) { var loaded=SaveService.Load();if(loaded==null){banner=SaveService.LastError;return;} CopyRun(loaded);banner=SaveService.RecoveryNotice;if(run.floor>=run.ActFloorCount){SaveService.Clear();banner="THAT FATE HAS ALREADY BEEN SEALED";screen=ScreenMode.Menu;}else RestoreRunStage(); }
            else if (index == 1) {selectingNewRun=true;selectedHero=HeroId.Vanguard;heroSelectionTime=Time.unscaledTime;screen=ScreenMode.CharacterSelect;}
            else if (index == 2) {viewingRunDeck=false;collectionRelics=false;collectionReturnScreen=ScreenMode.Menu;collectionPage=collectionFilter=collectionSort=screenControllerIndex=0;collectionScroll=0;inspectedCard=null;inspectedRelic=null;screen=ScreenMode.Collection;}
            else if (index == 3) {selectingNewRun=false;selectedHero=HeroId.Vanguard;heroSelectionTime=Time.unscaledTime;screen=ScreenMode.CharacterSelect;}
            else if (index == 4) {settingsReturnScreen=ScreenMode.Menu;settingsOverview=true;settingsFocusIndex=0;screen=ScreenMode.Settings;}
            else if (index == 5) screen=ScreenMode.Credits;
            else if (index == 6) Application.Quit();
            else banner = labels[index] + " · COMING IN THE NEXT VAULT UPDATE";
        }

        private void DrawCharacterSelect(float w,float h)
        {
            // The shared selection painting is used only inside the chosen hero's
            // stage. The vault backdrop stays neutral so the unselected hero never
            // remains visible behind the confirmation panel.
            Fill(new Rect(0,0,w,h),new Color(.004f,.007f,.014f,.34f));
            var accent=HeroAccent(selectedHero);
            Heading(w,selectingNewRun?"CHOOSE YOUR FATE":"CHARACTER ARCHIVE",selectingNewRun?"SELECT A HERO · THEN CONFIRM THE ASCENT":"ONE HERO · ONE COMPLETE DECKBUILDING PATH");
            var stage=new Rect(64,116,790,h-196);Fill(stage,new Color(.008f,.012f,.021f,.62f));Outline(stage,new Color(accent.r,accent.g,accent.b,.7f),2);
            DrawSelectedHeroArtwork(new Rect(stage.x+4,stage.y+4,stage.width-8,stage.height-8),selectedHero);
            Fill(new Rect(stage.x,stage.yMax-132,stage.width,132),new Color(.006f,.009f,.016f,.88f));
            var heroName=selectedHero==HeroId.Vanguard?"THE VANGUARD":selectedHero==HeroId.Hexer?"THE HEXER":"THE REAPER";
            var heroRole=selectedHero==HeroId.Vanguard?"STEEL · BLOCK · RETRIBUTION":selectedHero==HeroId.Hexer?"MARKS · BURN · RITUAL":"SOULS · DISSIPATE · SCYTHE";
            GUI.Label(new Rect(stage.x+28,stage.yMax-119,stage.width-56,43),heroName,new GUIStyle(titleStyle){fontSize=34,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.99f,.92f,.75f)}});
            GUI.Label(new Rect(stage.x+30,stage.yMax-72,stage.width-60,30),heroRole,new GUIStyle(subtitleStyle){fontSize=14,alignment=TextAnchor.MiddleLeft,normal={textColor=accent}});

            var panel=new Rect(w-500,150,430,h-250);Fill(panel,new Color(.009f,.014f,.024f,.96f));Outline(panel,accent,2);Fill(new Rect(panel.x,panel.y,panel.width,4),accent);
            var hp=selectedHero==HeroId.Vanguard?"80 MAX HP":selectedHero==HeroId.Hexer?"68 MAX HP":"72 MAX HP";
            var lore=selectedHero==HeroId.Vanguard?"A broken crown. An unbroken oath.\n\nBuild an iron defense, turn Block into pressure, and answer every blow with royal steel.":selectedHero==HeroId.Hexer?"A porcelain mask hiding a forbidden name.\n\nWeave Marks, Burn, and ritual Powers into a precise chain of lethal hexes.":"A keeper of the names the Vault tried to bury.\n\nHarvest temporary Souls, cycle the Exhaust pile, and finish the fallen with a spectral scythe.";
            GUI.Label(new Rect(panel.x+30,panel.y+28,panel.width-60,42),heroName,new GUIStyle(titleStyle){fontSize=29,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.98f,.91f,.75f)}});
            GUI.Label(new Rect(panel.x+31,panel.y+76,panel.width-62,28),hp,new GUIStyle(subtitleStyle){fontSize=15,alignment=TextAnchor.MiddleLeft,normal={textColor=accent}});
            var body=new GUIStyle(GUI.skin.label){font=bodyFont?bodyFont:GUI.skin.font,alignment=TextAnchor.UpperLeft,fontSize=18,wordWrap=true,normal={textColor=new Color(.94f,.92f,.86f)}};
            GUI.Label(new Rect(panel.x+31,panel.y+123,panel.width-62,145),lore,body);
            var deckTitle=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.84f,.76f,.58f)}};
            GUI.Label(new Rect(panel.x+31,panel.y+285,panel.width-62,25),"STARTING DECK",deckTitle);
            GUI.Label(new Rect(panel.x+31,panel.y+315,panel.width-62,62),selectedHero==HeroId.Vanguard?"4 Strike · 4 Defend\nBattle Cry · Brace":selectedHero==HeroId.Hexer?"4 Hex Strike · 4 Ward\nInvocation · First Ritual":"4 Scythe Strike · 4 Death's Veil\nSoul Call · Reaping Blow",new GUIStyle(body){fontSize=16});
            var confirm=new Rect(panel.x+28,panel.yMax-68,panel.width-56,46);DrawButtonFrame(confirm,confirm.Contains(PointerPosition),false);
            if(GUI.Button(confirm,selectingNewRun?"CONFIRM HERO · BEGIN ASCENT":"OPEN HERO CARD ARCHIVE",buttonStyle))ConfirmSelectedHero();

            var selectorY=h-104;var selectorTitle=new GUIStyle(footerStyle){fontSize=12,normal={textColor=new Color(.9f,.84f,.7f)}};GUI.Label(new Rect(w*.5f-145,selectorY-25,290,20),"CHOOSE YOUR HERO",selectorTitle);
            DrawHeroSelector(new Rect(w*.5f-145,selectorY,82,70),HeroId.Vanguard);
            DrawHeroSelector(new Rect(w*.5f-41,selectorY,82,70),HeroId.Hexer);
            DrawHeroSelector(new Rect(w*.5f+63,selectorY,82,70),HeroId.Reaper);
            BackButton(w,h);
        }

        private void DrawSelectedHeroArtwork(Rect r,HeroId hero)
        {
            var old=GUI.color;var reveal=runStartActive?runStartPortraitReveal:Mathf.Clamp01((Time.unscaledTime-heroSelectionTime)*5f);GUI.color=new Color(1,1,1,.55f+reveal*.45f);
            DrawFloatingHero(r,hero);GUI.color=old;
        }

        private void DrawHeroSelector(Rect r,HeroId hero)
        {
            var selected=selectedHero==hero;var over=r.Contains(PointerPosition);var accent=HeroAccent(hero);
            Fill(new Rect(r.x-5,r.y-5,r.width+10,r.height+10),selected?new Color(accent.r,accent.g,accent.b,.22f):new Color(0,0,0,.5f));
            DrawSelectedHeroArtwork(r,hero);Outline(r,selected?accent:new Color(.5f,.48f,.44f),selected?4:over?3:1);
            if(GUI.Button(r,"",GUIStyle.none)&&!selected){selectedHero=hero;heroSelectionTime=Time.unscaledTime;Sfx(SoundCue.UiConfirm);}
        }

        private void ConfirmSelectedHero()
        {
            if(runStartActive)return;
            Sfx(SoundCue.Resonance);
            if(selectingNewRun)BeginRunStartTransition();
            else{viewingRunDeck=false;collectionRelics=false;collectionReturnScreen=ScreenMode.Menu;collectionFilter=selectedHero==HeroId.Vanguard?1:selectedHero==HeroId.Hexer?2:3;collectionPage=0;collectionScroll=0;screen=ScreenMode.Collection;}
        }

        private void DrawMap(float w,float h)=>DrawFateMap(w,h);

        private int mapWheelFrame=-1;
        private float MapContentHeight=>170+(run.ActFloorCount-1)*118f;
        private static Rect MapViewport(float w,float h)=>new(126,76,Mathf.Max(650,w-405),h-88);
        private static Rect MapScrollZone(float w,float h)=>new(0,66,w-275,h-66);
        private Vector2 MapPosition(MapNode node,float width)
        {
            var margin=Mathf.Clamp(width*.075f,72,110);var span=Mathf.Max(420,width-margin*2);var laneX=margin+node.lane*span/Mathf.Max(1,RunModel.LaneCount-1);return new Vector2(laneX+node.xOffset*Mathf.Clamp(width*.055f,48,76),82+(run.ActFloorCount-1-node.floor)*118f);
        }
        private void FocusMapToCurrentFloor(float w,float h)
        {
            var viewport=MapViewport(w,h);var viewportHeight=viewport.height;var maxScroll=Mathf.Max(0,MapContentHeight-viewportHeight);
            if(mapFocusFloor==run.floor)return;
            mapFocusFloor=run.floor;var node=CurrentMapLocation??run.nodes.Where(n=>n.floor==run.floor).OrderBy(n=>Mathf.Abs(n.lane-RunModel.LaneCount/2f)).FirstOrDefault();mapScroll=Mathf.Clamp((node==null?MapContentHeight:MapPosition(node,viewport.width).y)-viewportHeight+150,0,maxScroll);
        }
        private bool TryActivateMapNodeAt(Vector2 pointer)
        {
            if(screen!=ScreenMode.Map||pendingMapNode!=null||run.nodes==null||run.nodes.Count==0)return false;
            var w=CombatWidth;var h=CombatHeight;if(mapPauseOpen)return false;FocusMapToCurrentFloor(w,h);
            var viewport=MapViewport(w,h);if(!viewport.Contains(pointer))return false;
            var local=pointer-viewport.position+new Vector2(0,mapScroll);
            foreach(var node in run.nodes.Where(n=>n.floor==run.floor&&n.available))
            {
                var world=MapPosition(node,viewport.width);var p=world-new Vector2(0,mapScroll);var hit=node.kind==NodeKind.Boss?108f:92f;
                if(p.y<-hit||p.y>viewport.height+hit)continue;
                if(!new Rect(world.x-hit*.5f,world.y-hit*.5f,hit,hit).Contains(local))continue;
                BeginMapTravel(node);return true;
            }
            return false;
        }
        private void UpdateMapPointerInput()
        {
            if(screen!=ScreenMode.Map||profile==null||pendingMapNode!=null||mapPauseOpen||inspectedCard!=null||ShardDiscoveryOpen||acquisitionActive)return;
            FocusMapToCurrentFloor(CombatWidth,CombatHeight);
            var mouse=UnityEngine.InputSystem.Mouse.current;if(mouse==null)return;
            var physical=mouse.position.ReadValue();var pointer=guiPointerReady?guiPointerPosition:CanvasPointFromPhysical(physical,Screen.height,CombatScale);
            var viewport=MapViewport(CombatWidth,CombatHeight);
            var wheel=mouse.scroll.ReadValue().y;
            if(MapScrollZone(CombatWidth,CombatHeight).Contains(pointer)&&Mathf.Abs(wheel)>.01f)
            {
                var maxScroll=Mathf.Max(0,MapContentHeight-viewport.height);
                mapScroll=Mathf.Clamp(mapScroll-wheel*.28f,0,maxScroll);mapFocusFloor=run.floor;mapWheelFrame=Time.frameCount;
            }
            if(mouse.leftButton.wasReleasedThisFrame)TryActivateMapNodeAt(pointer);
        }
        private void DrawMapLegend(float w,float h)
        {
            var pointer=PointerPosition;var legend=new Rect(w-262,294,240,376);Fill(legend,new Color(.006f,.012f,.021f,.90f));Outline(legend,new Color(.52f,.43f,.28f,.72f),1);
            GUI.Label(new Rect(legend.x+16,legend.y+15,legend.width-32,28),"VAULT ROOMS",new GUIStyle(titleStyle){fontSize=20,normal={textColor=new Color(.96f,.89f,.7f)}});
            var discovered=run.nodes.Select(n=>n.kind).ToHashSet();
            NodeKind? hoveredKind=null;Rect hoveredRect=default;
            for(var i=0;i<7;i++)
            {
                var kind=(NodeKind)i;var known=discovered.Contains(kind);var row=new Rect(legend.x+11,legend.y+49+i*44,legend.width-22,40);var over=known&&row.Contains(pointer);var shown=over?new Rect(row.x-3,row.y-2,row.width+6,row.height+4):row;
                if(over)Fill(shown,new Color(.18f,.12f,.035f,.72f));
                if(known)DrawMapNodeIcon(i,new Rect(shown.x+7,shown.y+2,36,36));
                else{Fill(new Rect(shown.x+11,shown.y+7,28,28),new Color(.018f,.023f,.032f,.92f));GUI.Label(new Rect(shown.x+11,shown.y+7,28,28),"?",new GUIStyle(titleStyle){fontSize=17,normal={textColor=new Color(.48f,.48f,.47f)}});}
                GUI.Label(new Rect(shown.x+53,shown.y,shown.width-59,shown.height),known?kind.ToString().ToUpperInvariant():"UNREAD FATE",new GUIStyle(footerStyle){fontSize=12,alignment=TextAnchor.MiddleLeft,normal={textColor=known?new Color(.94f,.91f,.81f):new Color(.48f,.48f,.46f)}});
                if(over){hoveredKind=kind;hoveredRect=shown;}
            }
            if(hoveredKind.HasValue)DrawTooltip(new Rect(legend.x-286,hoveredRect.y-8,270,94),hoveredKind.Value.ToString().ToUpperInvariant(),NodeDescription(hoveredKind.Value));
        }
        private void DrawPersistentRunControls(float w,Vector2 pointer)
        {
            DrawRouteMapControl(w,pointer);
            var deck=RunDeckControlRect(w);var gear=new Rect(w-67,7,46,44);var icon=new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=25,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.94f,.86f,.67f)}};
            RegisterCombatHudTarget("nav:map",0,RouteMapButton(w),"ACT MAP","Inspect your route. A: open; no rooms can be entered from the preview.",2);
            RegisterCombatHudTarget("nav:deck",0,deck,"FULL DECK","Inspect your permanent deck. A: open; return without advancing the run.",1);
            RegisterCombatHudTarget("nav:settings",0,gear,"PAUSE & SETTINGS","A: pause and open settings.",3);
            var deckActive=screen==ScreenMode.Collection&&viewingRunDeck;var gearActive=screen==ScreenMode.Settings||runPauseOpen||screen==ScreenMode.Map&&mapPauseOpen||screen==ScreenMode.Combat&&combatPauseOpen;
            var deckBlocked=routeInspectionOpen||acquisitionActive||runPauseOpen||screen==ScreenMode.Combat&&(combatBusy||choicePresented||dragView!=null)||screen==ScreenMode.Map&&mapPauseOpen;
            if(deck.Contains(pointer)||deckActive)Fill(new Rect(deck.x-3,deck.y-3,deck.width+6,deck.height+6),new Color(1f,.68f,.2f,deckActive?.19f:.12f));
            if(gear.Contains(pointer)||gearActive)Fill(new Rect(gear.x-3,gear.y-3,gear.width+6,gear.height+6),new Color(1f,.68f,.2f,gearActive?.19f:.12f));
            if(hudEmblemAtlas){DrawAtlasIcon(hudEmblemAtlas,3,4,2,deck);DrawAtlasIcon(hudEmblemAtlas,4,4,2,gear);}else{GUI.Label(deck,"▤",icon);GUI.Label(gear,"⚙",icon);}
            GUI.Label(new Rect(deck.x+27,deck.y+26,23,20),run.cards.Count.ToString(),new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=10,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}});
            if(deck.Contains(pointer))SetRunHudTooltip(deck,"FULL DECK · "+run.cards.Count,"Inspect every physical card in acquisition order, including upgrades and permanent enchantments.");
            if(gear.Contains(pointer))SetRunHudTooltip(gear,screen is ScreenMode.Map or ScreenMode.Combat?"PAUSE & SETTINGS":"SETTINGS","Pause this screen or change graphics, audio, gameplay, and accessibility options.");
            GUI.enabled=!deckBlocked&&!deckActive;if(GUI.Button(deck,"",GUIStyle.none))OpenRunDeck();GUI.enabled=true;
            GUI.enabled=!routeInspectionOpen&&!acquisitionActive&&screen!=ScreenMode.Settings;if(GUI.Button(gear,"",GUIStyle.none))ToggleRunSettings();GUI.enabled=true;
        }
        private void OpenRunDeck(){if(screen==ScreenMode.Collection&&viewingRunDeck)return;collectionReturnScreen=screen;viewingRunDeck=true;collectionRelics=false;collectionPage=collectionFilter=collectionSort=screenControllerIndex=0;collectionScroll=0;inspectedCard=null;inspectedRelic=null;screen=ScreenMode.Collection;}
        private void ToggleRunSettings(){if(screen==ScreenMode.Map){mapPauseOpen=!mapPauseOpen;return;}if(screen==ScreenMode.Combat){combatPauseOpen=!combatPauseOpen;return;}if(screen==ScreenMode.Settings)return;if(ShowsPersistentRunHud){runPauseOpen=!runPauseOpen;pauseMenuIndex=0;return;}settingsReturnScreen=screen;settingsPage=settingsFocusIndex=0;screen=ScreenMode.Settings;}
        private void DrawMapPauseMenu(float w,float h)=>DrawUnifiedPauseMenu(w,h,false);

        private void BeginMapTravel(MapNode node)
        {
            if(node==null||pendingMapNode!=null||node.floor!=run.floor||!node.available)return;
            pendingMapNode=node;mapTravelStartedAt=Time.unscaledTime;Sfx(SoundCue.MapStep);gildedFlash=1f;
        }
        private void UpdateMapTravel()
        {
            if(pendingMapNode==null)return;
            if(screen!=ScreenMode.Map){pendingMapNode=null;return;}
            var duration=profile!=null&&profile.reduceMotion ? .08f : .72f;
            if(Time.unscaledTime-mapTravelStartedAt<duration)return;
            var destination=pendingMapNode;pendingMapNode=null;StartNode(destination);
        }
        private void DrawMapTravelTransition(float w,float h,Rect viewport)
        {
            if(pendingMapNode==null)return;var duration=profile.reduceMotion?.08f:.72f;var t=Mathf.Clamp01((Time.unscaledTime-mapTravelStartedAt)/duration);
            var destination=viewport.position+MapPosition(pendingMapNode,viewport.width)-new Vector2(0,mapScroll);
            var previous=run.nodes.FirstOrDefault(n=>n.complete&&n.floor==pendingMapNode.floor-1&&(n.nextMask&(1<<pendingMapNode.lane))!=0);
            var start=previous==null?new Vector2(destination.x,viewport.yMax):viewport.position+MapPosition(previous,viewport.width)-new Vector2(0,mapScroll);
            var progress=Mathf.SmoothStep(0,1,Mathf.Min(1,t/.8f));var end=FateThreadPoint(start,destination,progress);
            DrawFateThread(start,destination,viewport,true,true,true,progress);
            if(viewport.Contains(end))Fill(new Rect(end.x-4,end.y-4,8,8),new Color(1,1,.82f));
            if(t>.72f)Fill(new Rect(0,58,w,h-58),new Color(.025f,.015f,.002f,(t-.72f)*2.4f));
        }
        private void DrawBossMapPortrait(Rect r,MapNode node)
        {
            DrawFloatingEnemy(r,EnemyForNode(node));
        }
        private void DrawMapLink(Vector2 lower,Vector2 upper,Rect clip,Color color,float width)
        {
            clip=new Rect(clip.x+3,clip.y+3,clip.width-6,clip.height-6);
            if(!ClipMapLine(ref lower,ref upper,clip))return;
            DrawLine(lower,upper,color,width);
        }
        private static bool ClipMapLine(ref Vector2 a,ref Vector2 b,Rect rect)
        {
            var start=a;var delta=b-a;var near=0f;var far=1f;
            if(!ClipMapEdge(-delta.x,start.x-rect.xMin,ref near,ref far)||!ClipMapEdge(delta.x,rect.xMax-start.x,ref near,ref far)||!ClipMapEdge(-delta.y,start.y-rect.yMin,ref near,ref far)||!ClipMapEdge(delta.y,rect.yMax-start.y,ref near,ref far))return false;
            a=start+delta*near;b=start+delta*far;return true;
        }
        private static bool ClipMapEdge(float direction,float distance,ref float near,ref float far)
        {
            if(Mathf.Abs(direction)<.0001f)return distance>=0;
            var ratio=distance/direction;
            if(direction<0){if(ratio>far)return false;if(ratio>near)near=ratio;}
            else{if(ratio<near)return false;if(ratio<far)far=ratio;}
            return true;
        }
        private static string NodeDescription(NodeKind kind)=>kind switch
        {
            NodeKind.Combat=>"COMBAT\nDefeat a vault guardian. Earn gold and choose a new card.",
            NodeKind.Elite=>"ELITE\nA dangerous opponent with unique rules. Win a relic, but arrive prepared.",
            NodeKind.Event=>"EVENT\nMeet the vault's inhabitants. Your choices can change the run.",
            NodeKind.Merchant=>"MERCHANT\nSpend gold on cards, relics, supplies, or removing a card.",
            NodeKind.Sanctuary=>"SANCTUARY\nRest to recover health, or upgrade one card in your deck.",
            NodeKind.Treasure=>"TREASURE\nClaim a relic to strengthen the rest of your ascent.",
            _=>"BOSS\nThe final keeper awaits. Defeat all of its phases to escape the vault."
        };
        private void DrawMapNodeIcon(int index,Rect r){DrawIdentityRoom(index,r);}
        private static string RiskLabel(NodeKind kind)=>kind==NodeKind.Elite?" · HIGH RISK":kind==NodeKind.Boss?" · FINAL":kind==NodeKind.Treasure?" · REWARD":"";
        private static string RomanAct(int act)=>act==1?"I":act==2?"II":"III";
        private void DrawLine(Vector2 a,Vector2 b,Color color,float width)
        {
            // Compose the line in authored-canvas space after the existing UI scale.
            // RotateAroundPivot treats a scaled pivot as screen space and visibly offsets
            // arrows/connectors on small and ultrawide Game views.
            var matrix=GUI.matrix;var angle=Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg;var length=Vector2.Distance(a,b);
            GUI.matrix=matrix*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,angle),Vector3.one);
            Fill(new Rect(0,-width*.5f,length,width),color);GUI.matrix=matrix;
        }

        private void StartNode(MapNode n)
        {
            currentNode=n;run.activeNodeFloor=n.floor;run.activeNodeLane=n.lane;
            if(n.kind==NodeKind.Combat||n.kind==NodeKind.Elite||n.kind==NodeKind.Boss)
            {
                run.PrepareEncounter(n.kind);currentEnemy=EnemyForNode(n);
                run.stage=RunStage.Combat;run.activeEnemyId=currentEnemy.id;BeginCombat(currentEnemy,n.kind==NodeKind.Boss?2:n.kind==NodeKind.Elite?1:0,n.kind==NodeKind.Boss&&!profile.reduceMotion?2.4f:0);
            }
            else if(n.kind==NodeKind.Merchant){merchantSold.Clear();run.merchantSold.Clear();merchantRemoved=merchantHealed=run.merchantRemoved=run.merchantHealed=false;run.stage=RunStage.Merchant;run.PrepareMerchantShard();screen=ScreenMode.Merchant;SaveService.Save(run);}
            else if(n.kind==NodeKind.Sanctuary){run.stage=RunStage.Sanctuary;screen=ScreenMode.Sanctuary;SaveService.Save(run);}
            else if(n.kind==NodeKind.Event){currentEvent=EventSystem.SelectEvent(run);EventSystem.BeginEvent(run,currentEvent);screen=ScreenMode.Event;PlayEventCue();SaveService.Save(run);}
            else{run.stage=RunStage.Treasure;screen=ScreenMode.Treasure;SaveService.Save(run);}
        }

        private EnemyDef EnemyForNode(MapNode node)
        {
            if(node.kind==NodeKind.Combat){var chosen=EncounterContent.Choose(run.act,node.floor+1,run.normalCombatsCompleted,run.seed^run.act*104729^node.floor*397^node.lane*7919^27011);return WorldContent.Enemies.First(e=>e.id==chosen.enemies[0]);}
            var candidates=WorldContent.Enemies.Where(e=>node.kind==NodeKind.Boss?e.boss:e.elite).ToArray();
            var hash=unchecked((uint)run.seed*2654435761u^(uint)(node.floor*397+node.lane*71));
            return candidates[hash%(uint)candidates.Length];
        }

        private void BeginCombat(EnemyDef enemy,int difficulty,float intro=0){currentEnemy=enemy;combat=new CombatState();var shard=run.shards.FirstOrDefault(s=>s.active);var actScale=Mathf.Max(0,run.act-1);var enemyHp=enemy.hp;var enemyDamage=enemy.baseDamage;combat.Begin(run.hero,BuildCombatDeck(),enemyHp,difficulty+actScale,run.hp,run.maxHp,run.relics,enemy.id,enemyDamage,run.seed^(run.act*104729)^(run.floor*397),run.immortalThreadUsed,shard?.id??"",shard?.activeFractured??false,run.activeEncounterEnemies?.ToArray(),run.TemporaryCombatCards().Count());var eventBlock=run.TemporaryEventValue("start_block");if(eventBlock>0)combat.player.block+=eventBlock;lastBossPhase=1;bossIntroTime=intro;screen=ScreenMode.Combat;ResetCombatPresentation();SaveCombatCheckpoint();}

        private IEnumerable<CardDef> BuildCombatDeck()
        {
            run.EnsureCardInstances();foreach(var saved in run.cards){var card=saved.BuildDefinition();if(card!=null){if(run.HasTemporaryFreeDraw(saved.persistentId))card.firstDrawFree=true;yield return card;}}foreach(var status in run.TemporaryCombatCards())yield return status;
        }


        private void DrawCard(Rect r,CardDef c,int copies=1,int upgrades=0)
        {
            DrawReadableCard(r,c,copies,upgrades);DrawCardAttachments(r,c);
        }
        private static int CardFrameIndex(CardDef card)=>card.origin==CardOrigin.Wanderer?0:(card.rarity is Rarity.Curse or Rarity.Status)?3:((card.rarity is Rarity.Rare or Rarity.Boss or Rarity.Special)||card.upgraded)?2:card.rarity==Rarity.Uncommon?1:0;
        private static Color CardBaseColor(CardDef card)=>(card.rarity is Rarity.Curse or Rarity.Status)?new Color(.018f,.011f,.022f,1):(card.rarity is Rarity.Rare or Rarity.Boss or Rarity.Special)?new Color(.025f,.020f,.014f,1):new Color(.026f,.031f,.034f,1);
        private static Color CardRulesPanelColor(CardDef card)=>IsCurseCard(card)?new Color(.045f,.044f,.053f):new Color(.235f,.23f,.25f);
        private static bool CardUsesDarkRules(CardDef card)=>true;
        private static Color CardRulesTextColor(CardDef card)=>CardUsesDarkRules(card)?new Color(.97f,.94f,.87f):new Color(.075f,.067f,.052f);
        private static string CardKindGlyph(CardDef card)=>card.rarity==Rarity.Curse?"☠":card.kind==CardKind.Attack?"⚔":card.kind==CardKind.Power?"✦":card.kind==CardKind.Skill?"◆":"◇";
        private void DrawCardTypeBanner(Rect r,CardDef card)
        {
            var color=card.rarity==Rarity.Curse?new Color(.62f,.19f,.76f):card.rarity==Rarity.Status?new Color(.40f,.43f,.48f):card.kind==CardKind.Attack?new Color(.68f,.20f,.13f):card.kind==CardKind.Power?new Color(.55f,.35f,.72f):new Color(.13f,.43f,.62f);var edge=Color.Lerp(color,Color.white,.42f);var banner=new Rect(r.x+r.width*.09f,r.y+r.height*.505f,r.width*.82f,Mathf.Max(19,r.height*.064f));
            Fill(new Rect(banner.x-5,banner.y+3,banner.width+10,banner.height-6),new Color(.005f,.008f,.013f,.94f));Fill(banner,new Color(color.r*.38f,color.g*.38f,color.b*.38f,.97f));Outline(banner,new Color(edge.r,edge.g,edge.b,.90f),1);Fill(new Rect(banner.x,banner.y,banner.width,2),new Color(edge.r,edge.g,edge.b,.72f));
            var type=card.rarity==Rarity.Curse?"CURSE":card.rarity==Rarity.Status?"STATUS":CardTypeLabel(card);var iconIndex=card.rarity==Rarity.Curse?53:card.rarity==Rarity.Status?61:card.kind==CardKind.Attack?57:card.kind==CardKind.Power?59:58;var iconSize=banner.height+7;DrawAtlasIcon(combatReadabilityAtlas,iconIndex,8,8,new Rect(banner.x+5,banner.center.y-iconSize*.5f,iconSize,iconSize));GUI.Label(new Rect(banner.x+iconSize+8,banner.y,banner.width-iconSize-18,banner.height),type,new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=Mathf.Clamp(Mathf.RoundToInt(r.width*.055f),11,17),fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.98f,.96f,.90f)}});
        }
        private string LiveCardRules(CardDef card,CombatCardPreview preview)
        {
            if(card==null)return "";var text=PermanentRelicCardRules(card);if(preview==null)return text;
            if((card.kind==CardKind.Attack||card.id is "eye_for_an_eye" or "soul")&&card.id is not ("break_the_line" or "arcane_detonation"))
            {
                var dark=CardUsesDarkRules(card);var direction=preview.DamageImproved?"↗ ":preview.DamageReduced?"↘ ":"";var color=preview.DamageImproved?(dark?"64E884":"167A41"):preview.DamageReduced?(dark?"FF7568":"B72E28"):(dark?"F7F2E6":"34281B");
                var amount=preview.DamageExpression;var live=$"Deal <b><color=#{color}>{direction}{amount}</color></b> damage.";
                text=new Regex(@"Deal [^.]+\.",RegexOptions.IgnoreCase).Replace(text,match=>match.Value.IndexOf("ALL enemies",System.StringComparison.OrdinalIgnoreCase)>=0?live.TrimEnd('.')+" to ALL enemies.":live,1);if(card.id=="eye_for_an_eye")text+="\nBased on this enemy’s attempted damage before Block.";
            }
            if(!preview.awaitingChoice&&(card.effect==EffectKind.Block||preview.authoredBlock>0&&card.kind!=CardKind.Power))
            {
                var dark=CardUsesDarkRules(card);var direction=preview.BlockImproved?"↗ ":preview.BlockReduced?"↘ ":"";var color=preview.BlockImproved?(dark?"64E884":"167A41"):preview.BlockReduced?(dark?"FF7568":"B72E28"):(dark?"7CD7FF":"006784");
                var number=$"<b><color=#{color}>{direction}{preview.cardBlock}</color></b>";
                if(card.id=="soul_guard")return $"Gain {number} Block.\nFrom {combat.hand.Count(c=>c.id=="soul")} Souls in hand.";
                // A resolved total must not be described as another per-unit amount.
                // Keep the scaling explanation and any other damage/condition clauses.
                var scaled=new Regex(@"\bGain \d+ Block (?:per|for each|for every) ([^.]+)\.",RegexOptions.IgnoreCase);
                if(scaled.IsMatch(text))text=scaled.Replace(text,m=>$"Gain {number} Block total.\nScaled by {m.Groups[1].Value}.",1);
                else text=new Regex(@"\b(Gain )\d+( Block)\b",RegexOptions.IgnoreCase).Replace(text,m=>m.Groups[1].Value+number+m.Groups[2].Value,1);
            }
            if(!string.IsNullOrEmpty(preview.conditionLabel))
            {
                var dark=CardUsesDarkRules(card);var state=preview.conditionActive?$"<color=#{(dark?"64E884":"167A41")}><b>ACTIVE ✓</b></color>":$"<color=#{(dark?"938E83":"5E594F")}>INACTIVE ◇</color>";text+=$"\n{state} · {preview.conditionLabel}";
            }
            return text;
        }
        private void DrawCardRarityOrnaments(Rect r,CardDef card,Color accent)
        {
            if((card.rarity is Rarity.Rare or Rarity.Boss or Rarity.Special)&&!profile.reduceMotion){var t=Mathf.Repeat(shimmer*.14f+(card.instanceId&7)*.11f,1);var gleamX=r.x+12+t*(r.width-32);Fill(new Rect(gleamX,r.y+3,12,2),new Color(1f,.92f,.65f,.72f));Fill(new Rect(r.xMax-5,r.y+18+t*(r.height-42),2,12),new Color(1f,.83f,.36f,.45f));}
            if(card.rarity==Rarity.Curse&&!profile.reduceMotion){var pulse=.35f+Mathf.Sin(shimmer*1.7f+(card.instanceId&3))*.12f;Outline(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),new Color(.68f,.20f,.82f,pulse),2);}
            if(card.upgraded){var crown=new Rect(r.center.x-18,r.y-9,36,20);Fill(crown,new Color(.025f,.018f,.008f,.95f));Outline(crown,new Color(1f,.82f,.25f),1);GUI.Label(crown,"★",new GUIStyle(titleStyle){fontSize=13,normal={textColor=new Color(1f,.88f,.42f)}});}
        }
        private void DrawCardMaterialLanguage(Rect r,CardDef card,Color accent)
        {
            if(card.origin==CardOrigin.Wanderer)
            {
                var ivory=new Color(.92f,.82f,.63f,.78f);Outline(new Rect(r.x+3,r.y+3,r.width-6,r.height-6),ivory,2);var wave=profile.reduceMotion?0:Mathf.Sin(shimmer*1.4f+(card.instanceId&7))*3;DrawLine(new Vector2(r.x+11,r.y+r.height*.56f+wave),new Vector2(r.x+11,r.yMax-20),new Color(1f,.74f,.34f,.72f),2);DrawLine(new Vector2(r.xMax-11,r.y+22),new Vector2(r.xMax-11,r.y+r.height*.44f-wave),new Color(.78f,.69f,.56f,.56f),1);
            }
            if(card.rarity==Rarity.Basic)
            {
                for(var i=0;i<3;i++){var y=r.y+26+i*17+(card.instanceId&3);DrawLine(new Vector2(r.x+17+i*8,y),new Vector2(r.x+43+i*9,y-9),new Color(.70f,.68f,.61f,.22f),1);}
            }
            if(card.rarity==Rarity.Status)
            {
                DrawLine(new Vector2(r.x+8,r.y+r.height*.31f),new Vector2(r.x+25,r.y+r.height*.38f),new Color(.78f,.80f,.82f,.48f),2);DrawLine(new Vector2(r.x+25,r.y+r.height*.38f),new Vector2(r.x+13,r.y+r.height*.44f),new Color(.78f,.80f,.82f,.42f),1);DrawLine(new Vector2(r.xMax-9,r.y+r.height*.69f),new Vector2(r.xMax-27,r.y+r.height*.76f),new Color(.78f,.80f,.82f,.40f),2);
            }
            if(card.rarity==Rarity.Curse&&!profile.reduceMotion){var t=Mathf.Repeat(shimmer*.18f+(card.instanceId&5)*.13f,1);Fill(new Rect(r.x+4,r.y+18+t*(r.height-42),2,10),new Color(.72f,.22f,.92f,.58f));}
        }
        private void DrawCardEnergySeal(Rect r,int value,Color accent,Color valueColor,bool changed,bool improved)
        {
            DrawEnergySealCore(r,Color.Lerp(accent,new Color(1f,.78f,.29f),.36f),changed?1f:.62f);
            GUI.Label(r,value.ToString(),new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=Mathf.Clamp(Mathf.RoundToInt(r.width*.48f),22,36),normal={textColor=valueColor}});
            if(changed)
            {
                var tag=new Rect(r.xMax-r.width*.27f,r.yMax-r.height*.25f,r.width*.31f,r.height*.29f);Fill(tag,new Color(.006f,.009f,.014f,.98f));Outline(tag,valueColor,1);
                GUI.Label(tag,improved?"↓":"↑",new GUIStyle(titleStyle){fontSize=Mathf.Clamp(Mathf.RoundToInt(r.width*.22f),10,16),normal={textColor=valueColor}});
            }
        }
        private void DrawEnergySealCore(Rect r,Color color,float pulse)
        {
            var size=Mathf.Min(r.width,r.height);var center=r.center;var outer=size*.39f;var inner=size*.27f;
            // Never rotate GUI.matrix here: scroll groups use local coordinates and
            // the old rotation produced the loose diamonds seen between collection cards.
            var dark=new Rect(center.x-inner,center.y-inner,inner*2,inner*2);Fill(dark,new Color(.018f,.024f,.034f,.98f));
            var edge=new Color(color.r,color.g,color.b,.72f+.24f*pulse);var gleam=new Color(1f,.91f,.65f,.58f+.2f*pulse);var thick=Mathf.Max(2,size*.04f);var thin=Mathf.Max(1,size*.019f);
            Vector2 top=new(center.x,center.y-outer),right=new(center.x+outer,center.y),bottom=new(center.x,center.y+outer),left=new(center.x-outer,center.y);
            DrawLine(top,right,edge,thick);DrawLine(right,bottom,edge,thick);DrawLine(bottom,left,edge,thick);DrawLine(left,top,edge,thick);
            Vector2 it=new(center.x,center.y-inner),ir=new(center.x+inner,center.y),ib=new(center.x,center.y+inner),il=new(center.x-inner,center.y);
            DrawLine(it,ir,gleam,thin);DrawLine(ir,ib,gleam,thin);DrawLine(ib,il,gleam,thin);DrawLine(il,it,gleam,thin);
        }
        private static int StatusCardIcon(string id)=>id switch{"dazed_mind"=>41,"shattered_guard"=>42,"falter"=>43,"heavy_chains"=>44,"misfortune"=>45,"haunting"=>46,"fractured_will"=>47,"arcane_lock"=>48,"rust"=>49,"fatebound"=>50,"lost_moment"=>51,"spirit_scar"=>52,"twisted_fate"=>53,_=>-1};
        private void AuditCardTypography()
        {
            typographyAudited=true;var checks=0;
            AuditCardFrameLanguage();
            foreach(var card in GameContent.Cards)foreach(var upgraded in new[]{false,true})
            {
                if(upgraded&&card.rarity is Rarity.Curse or Rarity.Status or Rarity.Special)continue;
                var shown=upgraded?GameContent.Upgrade(card):card;
                foreach(var size in new[]{new Vector2(176,260),new Vector2(194,264),new Vector2(190,281),new Vector2(220,310),new Vector2(420,600)})
                {
                    var rect=new Rect(0,0,size.x,size.y);
                    var variants=new[]{shown.text,shown.text+"\n<color=#64E884><b>ACTIVE ✓</b></color> · Bonus condition met"};
                    foreach(var text in variants)
                    {
                        var body=ReadableCardRules(rect,shown,text);var area=CardRuleArea(rect);checks++;
                        if(body.Height>area.height-2+.5f||body.widest>area.width-4+.5f)
                        {typographyFailures++;Debug.LogError($"[Gilded Fate Typography] CLIPPED RULES {shown.id} {size} {body.widest:F1}/{area.width:F1}w {body.Height:F1}/{area.height:F1}h");}
                        var painted=string.Join(" ",body.lines.Select(line=>StripRichTags.Replace(line,"")));
                        var expected=StripRichTags.Replace(FormatCardRules(text,shown),"");
                        if(Regex.Replace(painted,@"\s+"," ").Trim()!=Regex.Replace(expected,@"\s+"," ").Trim())
                        {typographyFailures++;Debug.LogError("[Gilded Fate Typography] LOST WORDS "+shown.id);}
                    }
                    var name=ReadableCardName(rect,shown);var titleArea=CardNameArea(rect);checks++;
                    if(name.Height>titleArea.height-2+.5f||name.widest>titleArea.width-4+.5f)
                    {typographyFailures++;Debug.LogError($"[Gilded Fate Typography] CLIPPED NAME {shown.id} {size}");}
                }
            }
            Debug.Log($"[Gilded Fate Typography] {checks} base/upgrade/live/layout checks · {typographyFailures} clipped or missing text cases");
        }
        private void DrawCombatVfx(Vector2 center,float size,int index,float life){if(!combatVfxAtlas||life<=0)return;var reduced=profile!=null&&profile.reducedVfx;var progress=1f-Mathf.Clamp01(life/.48f);var pulse=Mathf.Sin(progress*Mathf.PI);var actual=size*(reduced?.72f:1f)*Mathf.Lerp(.62f,1.08f,progress);var old=GUI.color;GUI.color=new Color(1f,1f,1f,Mathf.Clamp01(life*3f)*(.55f+pulse*(reduced?.2f:.45f)));DrawAtlasIcon(combatVfxAtlas,index,4,2,new Rect(center.x-actual*.5f,center.y-actual*.5f,actual,actual));GUI.color=old;}
        private static int VfxFor(EffectKind effect)=>effect==EffectKind.Damage?0:effect==EffectKind.Block?1:effect==EffectKind.Burn?2:effect==EffectKind.Mark||effect==EffectKind.Vulnerable||effect==EffectKind.Weak?3:effect==EffectKind.Resonance||effect==EffectKind.Strength||effect==EffectKind.Power?4:effect==EffectKind.Heal?5:effect==EffectKind.Draw?4:0;
        private void DrawMechanicPanel(Rect r){Fill(r,new Color(.012f,.016f,.024f,.94f));var accent=currentEnemy!=null&&currentEnemy.boss?new Color(1f,.7f,.57f):currentEnemy!=null&&currentEnemy.elite?new Color(1f,.85f,.59f):new Color(.68f,.83f,.94f);Outline(r,accent,1);var head=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=17,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=accent}};var body=new GUIStyle(footerStyle){fontSize=16,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.94f,.92f,.86f)}};GUI.Label(new Rect(r.x+18,r.y+16,r.width-36,42),combat.mechanicTitle,head);GUI.Label(new Rect(r.x+18,r.y+60,r.width-36,r.height-70),combat.mechanicText+"\n"+combat.IntentDetail,body);}
        private void DrawBossPhaseMarkers(Rect r){for(var i=1;i<=2;i++){var x=r.x+r.width*i/3f;Fill(new Rect(x-1,r.y-2,2,r.height+4),new Color(1f,.7f,.22f,.9f));}}
        private void DrawStatusChips(Rect r,FighterState fighter,bool playerSide){var parts=new List<string>();if(!playerSide&&fighter.block>0)parts.Add("BLOCK "+fighter.block);if(fighter.strength>0)parts.Add("STR "+fighter.strength);if(fighter.burn>0)parts.Add("BURN "+fighter.burn);if(fighter.marked>0)parts.Add("MARK "+fighter.marked);if(fighter.weak>0)parts.Add("WEAK "+fighter.weak);if(fighter.vulnerable>0)parts.Add("VULN "+fighter.vulnerable);if(parts.Count==0)return;var style=new GUIStyle(footerStyle){fontSize=14,alignment=playerSide?TextAnchor.MiddleLeft:TextAnchor.MiddleCenter,normal={textColor=new Color(.98f,.9f,.72f)}};GUI.Label(r,string.Join("   ",parts),style);}
        private void DrawTooltip(Rect r,string title,string text,Rect? avoid=null)=>DrawPolishedTooltip(r,title,text,avoid);
        private void DrawEnemySigil(Rect r)
        {
            DrawFloatingEnemy(r,currentEnemy);
        }
        private void DrawBossIntro(float w,float h){Fill(new Rect(0,0,w,h),new Color(0,0,0,.38f));var progress=1f-bossIntroTime/2.4f;var scale=Mathf.Lerp(1.2f,1f,Mathf.SmoothStep(0,1,progress));var portrait=new Rect(w*.5f-180*scale,h*.25f-30*scale,360*scale,320*scale);DrawFloatingEnemy(portrait,currentEnemy);var name=new GUIStyle(titleStyle){fontSize=42,normal={textColor=new Color(1f,.58f,.22f)}};ShadowLabel(new Rect(w*.15f,h*.64f,w*.7f,58),currentEnemy.name,name);var lore=new GUIStyle(subtitleStyle){fontSize=14,wordWrap=true};lore.normal.textColor=new Color(.9f,.78f,.62f);GUI.Label(new Rect(w*.25f,h*.72f,w*.5f,55),currentEnemy.description,lore);var warning=new GUIStyle(footerStyle){fontSize=11,normal={textColor=new Color(1f,.35f,.22f)}};GUI.Label(new Rect(w*.3f,h*.81f,w*.4f,22),"THE FINAL SEAL BREAKS",warning);}
        private void UpdateBossPhaseVisual(){if(currentEnemy==null||!currentEnemy.boss||combat.enemy.hp<=0)return;combat.RefreshEnemyState();var phase=combat.bossPhase;if(phase>lastBossPhase){lastBossPhase=phase;bossPhaseTime=profile.reduceMotion ? .35f : 1.2f;gildedFlash=1f;Sfx(SoundCue.BossPhase);}}
        private void DrawBossPhaseTransition(float w,float h){var alpha=Mathf.Clamp01(bossPhaseTime*1.4f);Fill(new Rect(0,0,w,h),new Color(.15f,0,.01f,alpha*.46f));var style=new GUIStyle(titleStyle){fontSize=52,normal={textColor=new Color(1f,.52f,.18f,alpha)}};ShadowLabel(new Rect(w*.2f,h*.39f,w*.6f,72),"PHASE "+lastBossPhase,style);var sub=new GUIStyle(subtitleStyle){fontSize=13};sub.normal.textColor=new Color(1f,.76f,.4f,alpha);GUI.Label(new Rect(w*.25f,h*.48f,w*.5f,28),"THE VAULT REWRITES ITS COMMAND",sub);}

        private void CheckCombat()
        {
            if(!combat.IsOver)return;
            run.immortalThreadUsed=combat.ImmortalThreadUsed;
            var receipt=run.runId+":"+run.act+":"+currentNode.floor+":"+currentNode.lane;
            profile.completedEncounterReceipts??=new List<string>();
            var newResult=!profile.completedEncounterReceipts.Contains(receipt);
            if(newResult)
            {
                profile.completedEncounterReceipts.Add(receipt);
                if(profile.completedEncounterReceipts.Count>2048)profile.completedEncounterReceipts.RemoveRange(0,1024);
                profile.cardsPlayed+=combat.cardsPlayed;profile.highestDamage=Mathf.Max(profile.highestDamage,combat.highestDamage);profile.mostBlock=Mathf.Max(profile.mostBlock,combat.highestBlock);
            }
            run.SyncPerfectedGrowth(combat);run.ClearCombatCheckpoint();run.FinishActiveShard();run.ConsumeTemporaryCombatStatuses();run.ConsumeTemporaryEventEffects();
            if(!combat.AnyEnemyAlive&&combat.player.hp>0)
            {
                run.hp=combat.player.hp;run.gold=Mathf.Max(0,run.gold-combat.goldLost);
                var gain=currentNode.kind==NodeKind.Boss?100:currentNode.kind==NodeKind.Elite?34:18;if(currentEnemy?.id=="collector")gain+=combat.stolenGold+10;if(run.relics.Contains("lucky_coin"))gain=Mathf.CeilToInt(gain*1.15f);run.pendingCombatGold=gain;run.combatGoldClaimed=false;
                run.RollEncounterRewards(currentNode.kind);
                if(newResult){profile.enemiesDefeated+=combat.EnemyCount;if(currentNode.kind==NodeKind.Combat)run.normalCombatsCompleted++;if(currentNode.kind==NodeKind.Elite)profile.elitesDefeated++;if(currentNode.kind==NodeKind.Boss)profile.bossesDefeated++;}
                run.AddEncounterCardChoices(combat.bonusCardRewards);rewardRevealTime=profile.reduceMotion?0:1.15f;
                run.stage=RunStage.CardReward;screen=ScreenMode.Reward;
                ProfileService.Save(profile);SaveService.Save(run);
            }
            else
            {
                run.hp=0;if(newResult)profile.losses++;profile.RegisterCompletedRun(run.runId);runResultVictory=false;
                ProfileService.Save(profile);SaveService.Clear();screen=ScreenMode.RunResult;
            }
        }
        private void DrawReward(float w,float h)
        {
            if(DrawPendingCombatGold(w,h)||DrawRolledCombatExtras(w,h))return;
            if(run.encounterRewards.cardClaimed&&!acquisitionActive){Advance();return;}
            DrawFullBackdrop(rewardBackground,w,h,.26f);DrawImportantRewardAtmosphere(w,h);DrawRunDock(w);
            Heading(w,CombatRewardTitle,"CHOOSE ONE CARD · OR SKIP");var options=RewardCards();
            var rewardGap=28f;var rewardW=Mathf.Min(220f,(w-180-rewardGap*(options.Length-1))/options.Length);var rewardH=Mathf.Min(rewardW*1.41f,h*.37f);var rewardStart=Mathf.Max(124,(w-(rewardW*options.Length+rewardGap*(options.Length-1)))*.5f);for(int i=0;i<options.Length;i++){var reveal=RewardReveal(i)*RewardChoiceOpacity;var r=new Rect(rewardStart+i*(rewardW+rewardGap),h*.29f+(1f-reveal)*85f,rewardW,rewardH);var old=GUI.color;GUI.color=new Color(1f,1f,1f,reveal);DrawCard(r,options[i]);RegisterCardKeywordHelp(r,options[i]);if(controllerNavigation&&screenControllerIndex==i){Outline(new Rect(r.x-5,r.y-5,r.width+10,r.height+10),Gold,4);hoveredCardHelp=options[i];hoveredCardHelpAnchor=r;}if(acquisitionActive)Fill(r,new Color(.004f,.008f,.012f,1-RewardChoiceOpacity));GUI.color=old;if(!acquisitionActive&&reveal>=.99f&&GUI.Button(r,"",GUIStyle.none)){var chosen=options[i];if(run.ClaimEncounterCard(chosen.id)){SaveService.Save(run);BeginCardAcquisition(chosen,Advance);}}}
            var skip=new Rect(w*.5f-95,h*.72f,190,50);var skipHot=ScreenChoiceHot(skip,options.Length);DrawButtonFrame(skip,skipHot,rewardRevealTime>.05f);if(!acquisitionActive&&rewardRevealTime<=.05f&&GUI.Button(skip,"SKIP",buttonStyle))Advance();
        }
        private bool DrawPendingCombatGold(float w,float h)
        {
            if(run.combatGoldClaimed||run.pendingCombatGold<=0)return false;
            DrawFullBackdrop(rewardBackground,w,h,.28f);DrawImportantRewardAtmosphere(w,h);DrawRunDock(w);Heading(w,CombatRewardTitle,"CLAIM WHAT THE VAULT SURRENDERED");
            var r=new Rect(w*.5f-245,h*.30f,490,218);var hot=r.Contains(PointerPosition)||controllerNavigation;if(hot)Fill(new Rect(r.x-8,r.y-8,r.width+16,r.height+16),new Color(1f,.62f,.12f,.12f));Fill(r,new Color(.008f,.014f,.023f,.97f));Outline(r,hot?new Color(1f,.82f,.38f):new Color(.74f,.57f,.26f),hot?3:2);
            DrawGoldIcon(new Rect(r.x+36,r.y+43,102,102));GUI.Label(new Rect(r.x+153,r.y+38,r.width-183,52),run.pendingCombatGold+" GOLD",new GUIStyle(titleStyle){fontSize=32,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.84f,.42f)}});GUI.Label(new Rect(r.x+154,r.y+92,r.width-186,54),"The defeated guardian's gilding loosens into spendable coin.",new GUIStyle(footerStyle){fontSize=16,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.91f,.88f,.79f)}});GUI.Label(new Rect(r.x+153,r.yMax-52,r.width-182,28),"TAKE THE SPOILS",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=13,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=hot?Color.white:Gold}});
            if(GUI.Button(r,"",GUIStyle.none))ClaimCombatGold(r.center);return true;
        }
        private void ClaimCombatGold(Vector2 origin)
        {
            if(run.combatGoldClaimed||run.pendingCombatGold<=0)return;goldCollectAmount=run.pendingCombatGold;goldCollectOrigin=origin;goldCollectTime=profile.reduceMotion?.32f:1.08f;run.gold+=run.pendingCombatGold;observedGold=run.gold;profile.goldCollected+=run.pendingCombatGold;run.pendingCombatGold=0;run.combatGoldClaimed=true;rewardRevealTime=profile.reduceMotion?0:1.15f;ProfileService.Save(profile);SaveService.Save(run);
        }
        private static Rect RunGoldIconRect=>new Rect(195,13,30,30);
        private void DrawGoldCollectFlight()
        {
            if(goldCollectTime<=0||goldCollectAmount<=0)return;var duration=profile.reduceMotion?.32f:1.08f;var t=1-Mathf.Clamp01(goldCollectTime/duration);var target=RunGoldIconRect.center;for(var i=0;i<7;i++){var p=Mathf.Clamp01(t-i*.055f);var at=Vector2.Lerp(goldCollectOrigin,target,Mathf.SmoothStep(0,1,p));at.y-=Mathf.Sin(p*Mathf.PI)*(70+i*5);var size=Mathf.Lerp(36,20,p);DrawGoldIcon(new Rect(at.x-size*.5f,at.y-size*.5f,size,size));}GUI.Label(new Rect(goldCollectOrigin.x+34,goldCollectOrigin.y-22,160,34),"+"+goldCollectAmount,new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=20,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.86f,.44f,1-t*.65f)}});
        }
        private float RewardReveal(int index){if(profile.reduceMotion)return 1f;var elapsed=1.15f-rewardRevealTime;return Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-Mathf.Min(index,2)*.16f)/.58f));}

        private void DrawCollection(float w,float h)
        {
            var wasEnabled=GUI.enabled;if(inspectedCard!=null||inspectedRelic!=null)GUI.enabled=false;
            DrawFullBackdrop(collectionBackground,w,h,.78f);
            var deckMode=viewingRunDeck;
            if(!deckMode)DrawCollectionRail(h);
            if(collectionRelics)DrawRelicCollection(w,h);else DrawCardCollection(w,h,deckMode);
            BackButton(w,h);
            if(deckMode)DrawRunHud(w);
            DrawMenuNavigationHint(w,h,deckMode?"Arrows  Browse    Enter / I  Inspect    Q / E  Filter    Esc  Return":"Arrows  Browse    Enter / I  Inspect    Q / E  Filter    C  Cards / Relics    S  Sort    Esc  Return",deckMode?"D-pad / Stick  Browse    A / X  Inspect    LB / RB  Filter    B  Return":"D-pad / Stick  Browse    A / X  Inspect    LB / RB  Filter    Y  Cards / Relics    R-stick click  Sort    B  Return");
            GUI.enabled=wasEnabled;
        }

        private void DrawCollectionRail(float h)
        {
            var panel=new Rect(20,135,142,h-230);Fill(panel,new Color(.006f,.011f,.018f,.94f));Outline(panel,new Color(.47f,.39f,.25f,.82f),1);
            GUI.Label(new Rect(panel.x+10,panel.y+17,panel.width-20,22),"ARCHIVE",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=11,fontStyle=FontStyle.Bold,normal={textColor=new Color(.83f,.69f,.39f)}});
            var names=new[]{"CARDS","RELICS"};
            for(var i=0;i<2;i++)
            {
                var r=new Rect(panel.x+10,panel.y+54+i*58,panel.width-20,46);var active=collectionRelics==(i==1);var hot=r.Contains(PointerPosition);
                Fill(r,active?new Color(.19f,.13f,.045f,.98f):hot?new Color(.075f,.061f,.038f,.98f):new Color(.012f,.018f,.028f,.96f));Outline(r,active?Gold:new Color(.39f,.35f,.27f),active?2:1);
                GUI.Label(r,names[i],new GUIStyle(buttonStyle){fontSize=13,normal={textColor=active?new Color(1f,.91f,.65f):new Color(.82f,.81f,.76f)}});
                if(GUI.Button(r,"",GUIStyle.none)&&!active){Sfx(SoundCue.UiHover);collectionRelics=i==1;collectionFilter=collectionSort=screenControllerIndex=0;collectionScroll=0;inspectedCard=null;inspectedRelic=null;}
            }
            GUI.Label(new Rect(panel.x+13,panel.y+188,panel.width-26,80),collectionRelics?"Every recovered relic in one continuous vault ledger.":"Browse the complete card archive. Use the filters and sort controls above.",new GUIStyle(footerStyle){fontSize=11,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.65f,.64f,.59f)}});
        }

        private CardDef[] CollectionCardEntries(bool deckMode)
        {
            run.EnsureCardInstances();
            IEnumerable<CardDef> cards=deckMode?run.cards.Select(c=>c.BuildDefinition()).Where(c=>c!=null):GameContent.Cards;
            if(deckMode)
            {
                if(collectionFilter==1)cards=cards.Where(c=>c.kind==CardKind.Attack);
                else if(collectionFilter==2)cards=cards.Where(c=>c.kind==CardKind.Skill);
                else if(collectionFilter==3)cards=cards.Where(c=>c.kind==CardKind.Power||c.rarity is Rarity.Curse or Rarity.Status);
            }
            else
            {
                cards=cards.Where(c=>CardArchive.Matches(c,collectionFilter));
            }
            if(!deckMode)cards=collectionSort==0?cards.OrderBy(c=>c.cost).ThenBy(c=>c.name):collectionSort==1?cards.OrderBy(c=>c.rarity).ThenBy(c=>c.cost).ThenBy(c=>c.name):cards.OrderBy(c=>c.name);
            if(captureMode&&CommandValue("-gfCapture")=="card-readability")cards=cards.OrderByDescending(c=>c.text.Length);
            if(captureMode&&CommandValue("-gfCapture")=="expansion-vanguard")cards=GameContent.Cards.Where(c=>GameContent.MartialOccultCardIds.Contains(c.id)&&c.hero==HeroId.Vanguard);
            if(captureMode&&CommandValue("-gfCapture")=="expansion-hexer")cards=GameContent.Cards.Where(c=>GameContent.MartialOccultCardIds.Contains(c.id)&&c.hero==HeroId.Hexer);
            if(captureMode&&CommandValue("-gfCapture")=="card-style")cards=new[]{CardOrigin.Knight,CardOrigin.Arcane,CardOrigin.Reaper,CardOrigin.Wanderer}.SelectMany(origin=>new[]{Rarity.Common,Rarity.Uncommon,Rarity.Rare}.Select(rarity=>GameContent.Cards.First(c=>c.origin==origin&&c.rarity==rarity)));
            if(bindingCaptureCards!=null)cards=bindingCaptureCards;
            return cards.ToArray();
        }

        private void DrawCardCollection(float w,float h,bool deckMode)
        {
            var entries=CollectionCardEntries(deckMode);
            Heading(w,deckMode?"CURRENT DECK":"THE COLLECTION",deckMode?$"{run.cards.Count} PHYSICAL CARDS  ·  ACQUISITION ORDER":$"CARDS  ·  {GameContent.Cards.Length} DISCOVERED");
            var modal=inspectedCard!=null;
            var filterNames=deckMode?new[]{"ALL","ATTACK","SKILL","ASPECT"}:CardArchive.Tabs;
            var controlsTop=deckMode?174f:136f;
            DrawTabs(w*.5f-filterNames.Length*53f,controlsTop,106,filterNames,collectionFilter,SelectCollectionFilter);
            if(deckMode)GUI.Label(new Rect(w*.5f-180,controlsTop+40,360,28),"SORT  ·  FIRST ADDED",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=11,normal={textColor=new Color(.88f,.72f,.42f)}});
            else{var sortNames=new[]{"COST","RARITY","A–Z"};DrawTabs(w*.5f-sortNames.Length*52f,controlsTop+40,104,sortNames,collectionSort,i=>{collectionSort=i;collectionScroll=0;screenControllerIndex=0;});}
            const int columns=6;const float gap=12f;var contentLeft=deckMode?36f:180f;var viewport=new Rect(contentLeft,controlsTop+86,w-contentLeft-30,h-controlsTop-170);var cardW=Mathf.Min(190f,(viewport.width-36-gap*(columns-1))/columns);var cardH=cardW*1.48f;var totalW=columns*cardW+(columns-1)*gap;var startX=(viewport.width-totalW)*.5f;var rows=Mathf.CeilToInt(entries.Length/(float)columns);var contentHeight=Mathf.Max(viewport.height,rows*(cardH+18)+12);if(controllerNavigation)KeepGridSelectionVisible(ref collectionScroll,screenControllerIndex,columns,cardH+18,viewport.height,contentHeight);UpdateScrollArea(viewport,ref collectionScroll,contentHeight);
            GUI.BeginGroup(viewport);
            for(int index=0;index<entries.Length;index++)
            {
                var card=entries[index];var row=index/columns;var col=index%columns;var baseRect=new Rect(startX+col*(cardW+gap),row*(cardH+18)+7-collectionScroll,cardW,cardH);if(baseRect.yMax<0||baseRect.y>viewport.height)continue;var globalHit=new Rect(viewport.x+baseRect.x,viewport.y+baseRect.y,baseRect.width,baseRect.height);var draw=baseRect;if(CardHelpContains(globalHit,card,PointerPosition)&&!modal){draw.y-=6;draw.height+=6;}DrawMiniCard(draw,card,1,card.upgraded?1:0);if(controllerNavigation&&screenControllerIndex==index&&!modal){Outline(new Rect(draw.x-3,draw.y-3,draw.width+6,draw.height+6),Gold,2);hoveredCardHelp=card;hoveredCardHelpAnchor=globalHit;}if(!modal)RegisterCardKeywordHelp(new Rect(viewport.x+draw.x,viewport.y+draw.y,draw.width,draw.height),card);if(!modal&&GUI.Button(baseRect,"",GUIStyle.none)){Sfx(SoundCue.UiConfirm);inspectedCard=card;}
            }
            GUI.EndGroup();DrawScrollRail(viewport,collectionScroll,contentHeight,entries.Length+" CARDS  ·  SCROLL");
            // Inspections are drawn once by the shared overlay on every card screen.
        }

        private void SelectCollectionFilter(int index)
        {collectionFilter=index;collectionScroll=0;screenControllerIndex=0;}

        // Sort only the gallery view; catalog indices remain the identity of atlas artwork.
        // OrderBy is stable, so existing ordering within each rarity is preserved.
        private static readonly int[] CollectionRelicIndices = Enumerable.Range(0,GameContent.Relics.Length)
            .OrderBy(index=>GameContent.Relics[index].rarity).ToArray();

        private void DrawRelicCollection(float w,float h)
        {
            Heading(w,"THE COLLECTION",$"RELICS  ·  {GameContent.Relics.Length} RECOVERED TREASURES");
            GUI.Label(new Rect(w*.5f-220,134,440,26),"ALL RELICS  ·  ORDERED BY RARITY",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=11,normal={textColor=new Color(.88f,.72f,.42f)}});
            var modal=inspectedRelic!=null;const int columns=6;const float gap=13f;var viewport=new Rect(180,174,w-210,h-258);var tileW=Mathf.Min(184f,(viewport.width-36-gap*(columns-1))/columns);var tileH=tileW*1.17f;var totalW=columns*tileW+(columns-1)*gap;var startX=(viewport.width-totalW)*.5f;var rows=Mathf.CeilToInt(GameContent.Relics.Length/(float)columns);var contentHeight=Mathf.Max(viewport.height,rows*(tileH+14)+10);if(controllerNavigation)KeepGridSelectionVisible(ref collectionScroll,screenControllerIndex,columns,tileH+14,viewport.height,contentHeight);UpdateScrollArea(viewport,ref collectionScroll,contentHeight);
            GUI.BeginGroup(viewport);
            for(int index=0;index<GameContent.Relics.Length;index++)
            {
                var catalogIndex=CollectionRelicIndices[index];var relic=GameContent.Relics[catalogIndex];var row=index/columns;var col=index%columns;var baseRect=new Rect(startX+col*(tileW+gap),row*(tileH+14)+5-collectionScroll,tileW,tileH);if(baseRect.yMax<0||baseRect.y>viewport.height)continue;var globalHit=new Rect(viewport.x+baseRect.x,viewport.y+baseRect.y,baseRect.width,baseRect.height);var draw=baseRect;if(!controllerNavigation&&globalHit.Contains(PointerPosition)&&!modal){draw.y-=5;draw.height+=5;}DrawRelicTile(draw,relic,catalogIndex);if(controllerNavigation&&screenControllerIndex==index&&!modal)Outline(new Rect(draw.x-3,draw.y-3,draw.width+6,draw.height+6),Gold,2);if(!modal&&GUI.Button(baseRect,"",GUIStyle.none)){Sfx(SoundCue.UiConfirm);inspectedRelic=relic;}
            }
            GUI.EndGroup();DrawScrollRail(viewport,collectionScroll,contentHeight,GameContent.Relics.Length+" RELICS  ·  SCROLL");
            // Relic inspection is drawn above the disabled collection by the shared overlay.
        }

        private void UpdateScrollArea(Rect viewport,ref float scroll,float contentHeight)
        {
            var max=Mathf.Max(0,contentHeight-viewport.height);scroll=Mathf.Clamp(scroll,0,max);
            if(Event.current.type==EventType.ScrollWheel&&viewport.Contains(PointerPosition)){scroll=Mathf.Clamp(scroll+Event.current.delta.y*46f,0,max);Event.current.Use();}
        }

        private void DrawScrollRail(Rect viewport,float scroll,float contentHeight,string caption)
        {
            GUI.Label(new Rect(viewport.x,viewport.yMax+5,viewport.width,18),caption,new GUIStyle(footerStyle){fontSize=9,normal={textColor=new Color(.69f,.65f,.56f)}});if(contentHeight<=viewport.height+1)return;
            var rail=new Rect(viewport.xMax+8,viewport.y+4,4,viewport.height-8);Fill(rail,new Color(.12f,.11f,.09f,.82f));var visible=Mathf.Clamp01(viewport.height/contentHeight);var thumbH=Mathf.Max(42,rail.height*visible);var max=Mathf.Max(1,contentHeight-viewport.height);var thumb=new Rect(rail.x-2,rail.y+(rail.height-thumbH)*(scroll/max),rail.width+4,thumbH);Fill(thumb,new Color(.84f,.61f,.24f,.92f));
        }

        private static void KeepGridSelectionVisible(ref float scroll,int index,int columns,float rowStep,float viewportHeight,float contentHeight)
        {
            if(index<0)return;var top=(index/columns)*rowStep;var bottom=top+rowStep;if(top<scroll)scroll=top;else if(bottom>scroll+viewportHeight)scroll=bottom-viewportHeight;scroll=Mathf.Clamp(scroll,0,Mathf.Max(0,contentHeight-viewportHeight));
        }

        private void DrawMiniCard(Rect r,CardDef card,int copies,int upgrades=0)
        {
            DrawCard(r,card,copies,upgrades);
        }

        private void DrawRelicTile(Rect r,RelicDef relic,int index,bool showDescription=false)
        {
            var accent=RarityAccent(relic.rarity);Fill(r,new Color(.03f,.034f,.047f,.98f));Outline(r,accent,relic.rarity is Rarity.Rare or Rarity.Boss?3:1);var icon=Mathf.Min(r.width-16,r.height*(showDescription?.44f:.58f));var art=new Rect(r.center.x-icon*.5f,r.y+8,icon,icon);DrawRelicArt(art,index);
            var name=new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=14,wordWrap=true,normal={textColor=new Color(.97f,.93f,.81f)}};GUI.Label(new Rect(r.x+6,art.yMax+4,r.width-12,32),relic.name,name);
            if(showDescription){var body=new GUIStyle(footerStyle){fontSize=15,wordWrap=true,normal={textColor=new Color(.94f,.91f,.84f)}};GUI.Label(new Rect(r.x+15,art.yMax+39,r.width-30,r.yMax-art.yMax-47),relic.text,body);}
            else{var rarity=new GUIStyle(footerStyle){fontSize=11,normal={textColor=Color.Lerp(accent,Color.white,.5f)}};GUI.Label(new Rect(r.x+4,r.yMax-20,r.width-8,16),relic.rarity.ToString().ToUpperInvariant(),rarity);}
        }

        private void DrawCardInspection(float w,float h,CardDef card)
        {
            DrawInspectableCardModal(w,h,card);
        }

        private void DrawRelicInspection(float w,float h,RelicDef relic)
        {
            Fill(new Rect(0,0,w,h),new Color(0,0,0,.76f));var index=System.Array.IndexOf(GameContent.Relics,relic);var accent=RarityAccent(relic.rarity);
            var name=new GUIStyle(titleStyle){fontSize=24,wordWrap=true,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.82f,.42f)}};
            var body=new GUIStyle(subtitleStyle){font=bodyFont,fontSize=18,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.94f,.91f,.84f)}};
            const float width=520;var nameHeight=Mathf.Max(36,name.CalcHeight(new GUIContent(relic.name),width-64));var bodyHeight=body.CalcHeight(new GUIContent(relic.text),width-64)+8;
            var artSize=Mathf.Clamp(h-nameHeight-bodyHeight-260,100,240);var height=32+artSize+18+nameHeight+12+bodyHeight+85;
            var r=new Rect((w-width)*.5f,(h-height)*.5f,width,height);Fill(r,new Color(.025f,.028f,.04f,.995f));Outline(r,accent,3);var art=new Rect(r.center.x-artSize*.5f,r.y+24,artSize,artSize);DrawRelicArt(art,index);
            GUI.Label(new Rect(r.x+32,art.yMax+18,r.width-64,nameHeight),relic.name,name);GUI.Label(new Rect(r.x+32,art.yMax+30+nameHeight,r.width-64,bodyHeight),relic.text,body);var rarity=new GUIStyle(subtitleStyle){fontSize=11};rarity.normal.textColor=accent;GUI.Label(new Rect(r.x+30,r.yMax-82,r.width-60,22),relic.rarity.ToString().ToUpperInvariant()+" RELIC",rarity);
            var close=new Rect(r.x+105,r.yMax-55,r.width-210,35);DrawButtonFrame(close,close.Contains(PointerPosition),false);if(GUI.Button(close,"RETURN TO GALLERY",buttonStyle)){Sfx(SoundCue.UiHover);inspectedRelic=null;}
        }

        private void DrawRelicArt(Rect r,int index)
        {
            if(index>=36&&index<GameContent.Relics.Length){var relative=index-36;var atlas=LoadAuthoredArt("Art/Relics/Expansion/major_relics_"+(relative/18+1));if(atlas)DrawAtlasIcon(atlas,relative%18,6,3,r);return;}
            if(index>=0&&index<GameContent.Relics.Length&&GameContent.Relics[index].id=="deaths_keepsake"&&deathsKeepsake)GUI.DrawTexture(r,deathsKeepsake,ScaleMode.ScaleToFit,true);
            else if(relicIconAtlas)DrawAtlasIcon(relicIconAtlas,Mathf.Clamp(index,0,35),6,6,r);
            else if(index<24)DrawAtlasIcon(relicAtlas,Mathf.Max(0,index),6,4,r);
            else DrawAtlasIcon(relicAtlasBonus,Mathf.Clamp(index-24,0,11),4,3,r);
        }

        private void DrawPageControls(float w,float h,int pageCount)
        {
            if(pageCount<=1)return;var y=h-76f;var previous=new Rect(w*.5f-170,y,105,42);var next=new Rect(w*.5f+65,y,105,42);DrawButtonFrame(previous,previous.Contains(PointerPosition),collectionPage==0);DrawButtonFrame(next,next.Contains(PointerPosition),collectionPage>=pageCount-1);if(GUI.Button(previous,"PREV",buttonStyle)&&collectionPage>0)collectionPage--;if(GUI.Button(next,"NEXT",buttonStyle)&&collectionPage<pageCount-1)collectionPage++;var pageStyle=new GUIStyle(footerStyle){fontSize=13,normal={textColor=new Color(.9f,.73f,.4f)}};GUI.Label(new Rect(w*.5f-60,y,120,42),$"{collectionPage+1} / {pageCount}",pageStyle);
        }

        private void DrawTabs(float x,float y,float width,string[] names,int selected,System.Action<int> choose)
        {
            for(int i=0;i<names.Length;i++){var r=new Rect(x+i*width,y,width-8,34);var active=i==selected;Fill(r,active?new Color(.2f,.16f,.08f,.96f):new Color(.018f,.024f,.034f,.94f));Outline(r,active?new Color(.96f,.84f,.57f):new Color(.53f,.49f,.38f),active?2:1);var style=new GUIStyle(footerStyle){fontSize=14,font=active&&labelFont?labelFont:bodyFont,normal={textColor=active?new Color(1f,.96f,.82f):new Color(.87f,.86f,.8f)}};GUI.Label(r,names[i],style);if(GUI.Button(r,"",GUIStyle.none)&&i!=selected){Sfx(SoundCue.UiHover);choose(i);}}
        }

        private static readonly int[] KnightAttackArt={0,2,4,8,10,15,17,18,22,24};
        private static readonly int[] KnightDefenseArt={1,3,5,6,7,11,12,16,21,23};
        private static readonly int[] KnightPowerArt={9,13,14,20,23};
        private static readonly int[] KnightSkillArt={7,9,11,13,14,20,21};
        private static readonly int[] ArcaneAttackArt={0,8,15,20};
        private static readonly int[] ArcaneDefenseArt={1,5,16,23};
        private static readonly int[] ArcaneRitualArt={2,7,10,12,17,21,22};
        private static readonly int[] ArcaneBurnArt={6,13,18,24};
        private static readonly int[] ArcaneCurseArt={4,9,14,19};
        private static readonly int[] ArcaneResourceArt={3,7,11,21};
        private void DrawCardArtwork(Rect r,CardDef card)
        {
            DrawAuthoredCardArt(r,card);
        }
        private static int KnightArtworkIndex(CardDef card,int hash)
        {
            var authored=card.id switch{"strike"=>0,"defend"=>1,"battle_cry"=>20,"stand_firm"=>3,_=>-1};if(authored>=0)return authored;
            if(card.kind==CardKind.Attack)return SelectCardArt(hash,KnightAttackArt);
            if(card.effect is EffectKind.Block or EffectKind.Fortify or EffectKind.Retaliate)return SelectCardArt(hash,KnightDefenseArt);
            if(card.kind==CardKind.Power||card.effect==EffectKind.Strength)return SelectCardArt(hash,KnightPowerArt);
            return SelectCardArt(hash,KnightSkillArt);
        }
        private static int ArcaneArtworkIndex(CardDef card,int hash)
        {
            var authored=card.id switch{"hex_strike"=>0,"ward"=>5,"arcane_thread"=>3,"invocation"=>7,"first_ritual"=>12,_=>-1};if(authored>=0)return authored;
            if(card.kind==CardKind.Attack)return SelectCardArt(hash,ArcaneAttackArt);
            if(card.effect==EffectKind.Block)return SelectCardArt(hash,ArcaneDefenseArt);
            if(card.effect==EffectKind.Burn)return SelectCardArt(hash,ArcaneBurnArt);
            if(card.effect is EffectKind.Mark or EffectKind.Vulnerable or EffectKind.Weak)return SelectCardArt(hash,ArcaneCurseArt);
            if(card.effect is EffectKind.Draw or EffectKind.Energy or EffectKind.Resonance)return SelectCardArt(hash,ArcaneResourceArt);
            return SelectCardArt(hash,ArcaneRitualArt);
        }
        private static int SelectCardArt(int hash,int[] pool)=>pool[hash%pool.Length];
        private static readonly string[] ReaperArtworkIds={"soul","scythe_strike","deaths_veil","soul_call","reaping_blow","grave_cut","soul_slash","spirit_cleave","deaths_touch","reaping_sweep","grave_guard","soul_guard","dark_veil_reaper","call_beyond","soul_offering","death_knell","grim_focus","grave_search","scythe_cycle","grim_flurry","soul_piercer_reaper","dark_insight","hollow_cut","soul_feast","spirit_scythe","deaths_embrace","grave_pact","soul_rend","death_march","hollow_scythe","soul_exchange","gravekeeper","deaths_door","soulstorm","beyond_the_veil","empty_grave","soul_carver","graves_edge","call_from_beyond","soul_echo","reapers_momentum","army_of_the_dead","soul_reaper","endless_harvest","devour_the_dead","death_incarnate","final_procession","grim_ascension","claim_the_fallen","soul_conversion","soulbound_tome","eternal_souls","reapers_calling"};
        private static int ReaperArtworkIndex(CardDef card){var index=System.Array.IndexOf(ReaperArtworkIds,card.id);return index<0?0:index;}
        private void DrawReaperCardArtwork(Rect r,int index)
        {
            if(index<25)DrawReaperAtlasCell(reaperCardAtlasA,index,5,5,.957f,r);else if(index<50)DrawReaperAtlasCell(reaperCardAtlasB,index-25,5,5,.968f,r);else DrawReaperAtlasCell(reaperCardAtlasC,index-50,2,2,1f,r);
        }
        private static void DrawReaperAtlasCell(Texture2D atlas,int index,int columns,int rows,float contentHeight,Rect destination)
        {
            if(!atlas||index<0)return;var col=index%columns;var row=index/columns;var cellHeight=contentHeight/rows;var uv=new Rect(col/(float)columns,1f-(row+1)*cellHeight,1f/columns,cellHeight);GUI.DrawTextureWithTexCoords(destination,atlas,uv,true);
        }
        private void DrawHeroPortrait(Rect r,HeroId hero)
        {
            DrawFloatingHero(r,hero);
        }
        private static Color HeroAccent(HeroId hero)=>hero==HeroId.Vanguard?new Color(.91f,.66f,.31f):hero==HeroId.Hexer?new Color(.67f,.43f,.94f):new Color(.22f,.83f,.76f);
        private static Color CardAccent(CardDef card)=>card.origin==CardOrigin.Wanderer?new Color(.82f,.72f,.52f):card.upgraded?new Color(1f,.82f,.24f):card.rarity==Rarity.Rare?new Color(1f,.68f,.16f):card.rarity==Rarity.Curse?new Color(.65f,.14f,.18f):card.rarity==Rarity.Status?new Color(.46f,.50f,.56f):card.origin==CardOrigin.Arcane?new Color(.55f,.27f,.8f):card.origin==CardOrigin.Reaper?new Color(.18f,.68f,.64f):card.origin==CardOrigin.Knight?new Color(.67f,.36f,.18f):new Color(.48f,.58f,.62f);
        private static string CardTypeLabel(CardDef card)=>card.kind==CardKind.Power?"ASPECT":card.kind.ToString().ToUpperInvariant();
        private static Color RarityAccent(Rarity rarity)=>rarity is Rarity.Boss or Rarity.Special?new Color(1f,.76f,.25f):rarity==Rarity.Rare?new Color(.85f,.42f,.85f):rarity==Rarity.Uncommon?new Color(.28f,.7f,.82f):rarity==Rarity.Curse?new Color(.7f,.16f,.18f):rarity==Rarity.Status?new Color(.48f,.53f,.6f):new Color(.62f,.56f,.42f);

        private void DrawMerchant(float w,float h)=>DrawPhysicalMerchant(w,h);
        private void SaveMerchantState(RunStage stage=RunStage.Merchant){run.merchantSold=merchantSold.ToList();run.merchantRemoved=merchantRemoved;run.merchantHealed=merchantHealed;run.stage=stage;SaveService.Save(run);}

        private void DrawSanctuary(float w,float h)
        {
            DrawLocationBackdrop(w,h,1);DrawRunDock(w);
            Heading(w,"SANCTUARY","A QUIET FLAME BURNS WITHOUT FUEL"); Choice(new Rect(w*.125f,h*.34f,w*.23f,220),"REST","Heal 30% maximum HP.\nRecover up to "+Mathf.Min(run.maxHp-run.hp,Mathf.RoundToInt(run.maxHp*.3f))+" HP now.",RestAtShrine,0); Choice(new Rect(w*.385f,h*.34f,w*.23f,220),"UPGRADE","Choose one physical card copy and improve its authored upgrade.\nYou can cancel before choosing a card.",()=>{collectionPage=0;cardServiceScroll=0;cardServiceReturnScreen=ScreenMode.Sanctuary;run.stage=RunStage.CardUpgrade;SaveService.Save(run);screen=ScreenMode.CardUpgrade;},1); Choice(new Rect(w*.645f,h*.34f,w*.23f,220),"BIND A CARD","Choose one Act-appropriate Binding, then engrave it onto one eligible unmodified card copy.\nYou can cancel before engraving.",()=>{collectionPage=0;cardChoiceScroll=0;run.BeginBindingChoice();SaveService.Save(run);screen=ScreenMode.BindingSelect;},2);
        }

        private void DrawDeckService(float w,float h,bool upgrading)
        {
            DrawRunHud(w);
            run.EnsureCardInstances();var entries=run.cards.Where(saved=>saved.BuildDefinition()!=null&&(!upgrading||saved.BuildDefinition().rarity is not (Rarity.Curse or Rarity.Status))).ToArray();
            Heading(w,upgrading?"INSCRIBE A GOLDEN RUNE":"THE BROKER'S SHEARS",(upgrading?"CHOOSE ONE PHYSICAL CARD TO UPGRADE":"CHOOSE ONE PHYSICAL CARD TO REMOVE · "+run.MerchantRemovalCost+" GOLD")+"  ·  FIRST ADDED FIRST");
            const int columns=6;const float gap=12f;var viewport=new Rect(34,146,w-68,h-226);var cardW=Mathf.Min(188f,(viewport.width-34-gap*(columns-1))/columns);var cardH=cardW*1.48f;var rowStep=cardH+17;var totalW=columns*cardW+(columns-1)*gap;var startX=(viewport.width-totalW)*.5f;var rows=Mathf.CeilToInt(entries.Length/(float)columns);var contentHeight=Mathf.Max(viewport.height,rows*rowStep+10);if(controllerNavigation)KeepGridSelectionVisible(ref cardServiceScroll,screenControllerIndex,columns,rowStep,viewport.height,contentHeight);UpdateScrollArea(viewport,ref cardServiceScroll,contentHeight);RunCard selected=null;
            GUI.BeginGroup(viewport);
            for(var index=0;index<entries.Length;index++)
            {
                var saved=entries[index];var current=saved.BuildDefinition();var available=!upgrading||!saved.upgraded;var shown=upgrading&&available?GameContent.Upgrade(current):current;var row=index/columns;var col=index%columns;var r=new Rect(startX+col*(cardW+gap),row*rowStep+5-cardServiceScroll,cardW,cardH);if(r.yMax<0||r.y>viewport.height)continue;DrawCard(r,shown);RegisterCardKeywordHelp(new Rect(viewport.x+r.x,viewport.y+r.y,r.width,r.height),shown,controllerNavigation&&screenControllerIndex==index);if(controllerNavigation&&screenControllerIndex==index)Outline(new Rect(r.x-4,r.y-4,r.width+8,r.height+8),Gold,4);if(!available){Fill(r,new Color(0,0,0,.61f));GUI.Label(new Rect(r.x,r.center.y-15,r.width,30),"ALREADY UPGRADED",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=11,fontStyle=FontStyle.Bold,normal={textColor=new Color(.72f,.7f,.65f)}});}GUI.enabled=available;if(GUI.Button(r,"",GUIStyle.none))selected=saved;GUI.enabled=true;
            }
            GUI.EndGroup();DrawScrollRail(viewport,cardServiceScroll,contentHeight,entries.Length+" PHYSICAL CARDS  ·  SCROLL");
            if(selected!=null){Sfx(upgrading?SoundCue.Upgrade:SoundCue.RemoveCard);if(upgrading){var shown=GameContent.Upgrade(selected.BuildDefinition());if(run.UpgradeCard(selected)){banner=shown.name+" NOW BEARS THE GOLDEN RUNE";Advance();}}else CompleteMerchantRemoval(selected);}
            var cancel=new Rect(34,h-62,160,36);DrawButtonFrame(cancel,cancel.Contains(PointerPosition),false);if(GUI.Button(cancel,"CANCEL",buttonStyle)){if(upgrading){run.stage=cardServiceReturnScreen==ScreenMode.Event?RunStage.Event:RunStage.Sanctuary;SaveService.Save(run);screen=cardServiceReturnScreen;}else{SaveMerchantState();screen=ScreenMode.Merchant;}}
        }

        private void DrawEvent(float w,float h)
        {
            if(currentEvent==null){currentEvent=EventSystem.SelectEvent(run);EventSystem.BeginEvent(run,currentEvent);}
            DrawEventBackdrop(w,h);DrawRunHud(w);
            var scene=new Rect(26,116,w*.515f,h-142);DrawEventArtwork(scene);Outline(scene,new Color(.72f,.54f,.25f,.82f),2);
            var storyHeight=Mathf.Clamp(scene.height*.37f,205,270);var story=new Rect(scene.x,scene.yMax-storyHeight,scene.width,storyHeight);
            for(var band=0;band<12;band++){var t=band/11f;Fill(new Rect(story.x,story.y-band*10,story.width,story.height/12f+11),new Color(.002f,.004f,.008f,.18f+t*.065f));}
            Fill(story,new Color(.003f,.006f,.011f,.73f));Fill(new Rect(story.x+24,story.y+18,4,story.height-40),new Color(1f,.69f,.24f,.72f));
            var eventTitle=new GUIStyle(titleStyle){fontSize=31,alignment=TextAnchor.UpperLeft,wordWrap=true,normal={textColor=new Color(1f,.91f,.68f)}};GUI.Label(new Rect(story.x+48,story.y+20,story.width-78,76),currentEvent.name,eventTitle);
            var storyText=EventStory(currentEvent);var promptRect=new Rect(story.x+49,story.y+94,story.width-84,story.height-126);var promptStyle=new GUIStyle(footerStyle){fontSize=17,alignment=TextAnchor.UpperLeft,wordWrap=true,normal={textColor=new Color(.97f,.94f,.87f)}};while(promptStyle.fontSize>12&&promptStyle.CalcHeight(new GUIContent(storyText),promptRect.width)>promptRect.height)promptStyle.fontSize--;GUI.Label(promptRect,storyText,promptStyle);
            GUI.Label(new Rect(story.x+49,story.yMax-28,story.width-84,18),currentEvent.ambientCue.ToUpperInvariant()+"  ·  A VAULT ENCOUNTER",new GUIStyle(footerStyle){fontSize=9,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.85f,.69f,.40f)}});

            var choices=currentEvent.choices??System.Array.Empty<EventChoiceDef>();var rightX=scene.xMax-18;var rightW=w-rightX-28;var gap=12f;var availableHeight=h-164;var choiceH=Mathf.Min(190f,(availableHeight-gap*Mathf.Max(0,choices.Length-1))/Mathf.Max(1,choices.Length));var startY=116+(availableHeight-(choiceH*choices.Length+gap*Mathf.Max(0,choices.Length-1)))*.5f;
            for(var i=0;i<choices.Length;i++)DrawClearEventChoice(new Rect(rightX,startY+i*(choiceH+gap),rightW,choiceH),choices[i],i);
            DrawPersistentRunTooltip(w,h);
        }

        private void DrawEventChoice(Rect rect,EventChoiceDef choice,int index)
        {
            var availability=EventSystem.Availability(run,choice);
            var focused=controllerNavigation&&screenControllerIndex==index;
            var hot=rect.Contains(PointerPosition)||focused;var draw=rect;
            if(hot&&!profile.reduceMotion)draw.x+=5;
            Fill(draw,new Color(.006f,.011f,.018f,hot?.97f:.89f));
            Fill(new Rect(draw.x,draw.y,hot?4:2,draw.height),availability.available?(hot?Gold:new Color(.56f,.43f,.25f)):new Color(.39f,.36f,.33f));
            var textColor=availability.available?new Color(.97f,.94f,.85f):new Color(.68f,.67f,.63f);
            var parts=EventChoiceLayout(draw);
            GUI.Label(new Rect(draw.x+15,draw.y+17,34,28),(index+1).ToString("00"),new GUIStyle(titleStyle){fontSize=18,normal={textColor=hot?Gold:new Color(.63f,.51f,.31f)}});
            var title=FittedEventStyle(choice.title,parts[0],titleStyle,22,17);title.normal.textColor=textColor;
            GUI.Label(parts[0],choice.title,title);
            var cost=availability.available?choice.costText:availability.reason;
            var costText=string.IsNullOrWhiteSpace(cost)?"No cost":cost;
            var costStyle=FittedEventStyle(costText,parts[1],footerStyle,14,13);costStyle.fontStyle=FontStyle.Bold;
            costStyle.normal.textColor=string.IsNullOrWhiteSpace(cost)?new Color(.68f,.79f,.70f):new Color(1f,.64f,.48f);
            GUI.Label(parts[1],costText,costStyle);
            if(parts[2].height>0)
            {
                var actionStyle=FittedEventStyle(EventChoiceAction(choice),parts[2],footerStyle,14,13);actionStyle.normal.textColor=new Color(.76f,.79f,.78f);
                GUI.Label(parts[2],EventChoiceAction(choice),actionStyle);
            }
            var outcome=parts[3];DrawLine(new Vector2(outcome.x,outcome.y-7),new Vector2(outcome.xMax,outcome.y-7),new Color(.57f,.46f,.27f,.40f),1);
            if((choice.rewardText??"").ToUpperInvariant().Contains("GOLD")){DrawGoldIcon(new Rect(outcome.x,outcome.y+2,24,24));outcome.x+=32;outcome.width-=32;}
            var rewardStyle=FittedEventStyle(choice.rewardText,outcome,footerStyle,17,14);rewardStyle.normal.textColor=availability.available?new Color(1f,.88f,.57f):textColor;
            GUI.Label(outcome,choice.rewardText,rewardStyle);
            GUI.enabled=availability.available&&!acquisitionActive;
            if(GUI.Button(rect,"",GUIStyle.none))BeginEventChoice(choice);
            GUI.enabled=true;
        }

        // One layout drives drawing and the exhaustive event-text fit checks.
        private static Rect[] EventChoiceLayout(Rect rect)
        {
            var x=rect.x+58;var width=rect.width-78;var compact=rect.height<176;
            var title=new Rect(x,rect.y+13,width,32);
            var cost=new Rect(x,rect.y+49,width,26);
            var action=new Rect(x,rect.y+79,width,compact?0:33);
            var rewardY=rect.y+(compact?87:125);
            return new[]{title,cost,action,new Rect(x,rewardY,width,Mathf.Max(28,rect.yMax-rewardY-14))};
        }
        private static GUIStyle FittedEventStyle(string text,Rect rect,GUIStyle template,int size,int minimum)
        {
            var style=new GUIStyle(template){fontSize=size,wordWrap=true,alignment=TextAnchor.UpperLeft};
            while(style.fontSize>minimum&&style.CalcHeight(new GUIContent(text??""),rect.width)>rect.height)style.fontSize--;
            return style;
        }

        private static string EventStory(EventDefinition e)
        {
            if(e==null)return "The Vault waits without breath.";var detail=e.ambientCue switch
            {
                "forge"=>"Heat rolls across the chamber in slow waves. Every hammer mark in the anvil looks recent, yet no smith answers your call.",
                "thread"=>"The strand tightens when you approach, humming with a future that has not happened. Touching it will make that future yours.",
                "clock"=>"Each backward tick steals a heartbeat from the room. Dust rises from the floor and settles on shelves it left moments ago.",
                "merchant"=>"Their smile arrives a moment too late. The wares are genuine, but the price feels heavier than the numbers suggest.",
                "grave"=>"The soil is warm and the surrounding footprints end at the edge. Something below remembers the weight of a name.",
                "shard"=>"Fractured light crawls over the walls as the pieces answer one another. The closer you stand, the more clearly they whisper your name.",
                "mirror"=>"The glass refuses to copy your movements. Its reflection watches your deck instead, already judging which fate it would trade.",
                "chest"=>"Gold glints between iron teeth. The lid opens a finger's width, waiting to learn whether hunger or caution rules you.",
                "door"=>"Old mechanisms wake behind the stone. Every seal promises a different answer and warns of a different regret.",
                "library"=>"Pages turn without wind. Somewhere among them, a version of you has already made this choice and underlined the cost.",
                "chains"=>"Nothing moves, yet links scrape together high above. The chamber measures every burden you carried here.",
                "water"=>"The surface reflects a brighter Vault than the one around you. A single drop climbs against gravity and waits at the rim.",
                "road"=>"The routes rearrange whenever you look away. Only the path beneath your boots remains honest.",
                _=>"The room holds the silence of a held breath. Whatever waits here has been expecting someone willing to choose."
            };return e.prompt+"\n\n"+detail;
        }
        private static string EventChoiceAction(EventChoiceDef choice)
        {
            var title=(choice?.title??"ACT").ToUpperInvariant();
            if(title.Contains("BUY")||title.Contains("PAY"))return "You count the price into an open, waiting hand.";
            if(title.Contains("SEARCH"))return "You kneel, sift through the remains, and take what the Vault overlooked.";
            if(title.Contains("TAKE")||title.Contains("CLAIM")||title.Contains("COLLECT"))return "You reach into the danger and close your hand around the reward.";
            if(title.Contains("PULL"))return "You brace yourself and pull until the golden thread answers.";
            if(title.Contains("OFFER")||title.Contains("GIVE")||title.Contains("SACRIFICE"))return "You place the price forward and wait for the chamber to accept it.";
            if(title.Contains("OPEN"))return "You break the seal and accept whatever wakes on the other side.";
            if(title.Contains("DRINK"))return "You lift the vessel and drink before doubt can stop you.";
            if(title.Contains("LEAVE")||title.Contains("REFUSE"))return "You keep your hands clear and walk away before the offer changes.";
            return "You commit to the choice and let the Vault rewrite what follows.";
        }

        private void BeginEventChoice(EventChoiceDef choice)
        {var cardsBefore=SnapshotCardCopies();var relicsBefore=SnapshotRelics();var shardsBefore=SnapshotShards();if(EventSystem.BeginChoice(run,choice)){collectionPage=screenControllerIndex=0;cardChoiceScroll=0;Sfx(SoundCue.UiConfirm);AfterEventInteraction(cardsBefore,relicsBefore,shardsBefore);}}

        private void AfterEventInteraction(HashSet<string> cardsBefore=null,HashSet<string> relicsBefore=null,HashSet<string> shardsBefore=null)
        {
            SaveService.Save(run);screen=run.stage==RunStage.EventSelection?ScreenMode.EventSelection:run.stage==RunStage.EventResult?ScreenMode.EventResult:ScreenMode.Event;if(screen==ScreenMode.EventResult){gildedFlash=1f;Sfx(SoundCue.Resonance);}if(cardsBefore!=null||relicsBefore!=null||shardsBefore!=null)PresentNewRunAcquisitions(cardsBefore,relicsBefore,shardsBefore);
        }
        private bool ChooseEventCardWithAcquisition(RunCard card)
        {
            var cardsBefore=SnapshotCardCopies();var relicsBefore=SnapshotRelics();var shardsBefore=SnapshotShards();if(!EventSystem.ChooseCard(run,card))return false;AfterEventInteraction(cardsBefore,relicsBefore,shardsBefore);return true;
        }
        private bool ChooseEventOfferWithAcquisition(string offerId)
        {
            var cardsBefore=SnapshotCardCopies();var relicsBefore=SnapshotRelics();var shardsBefore=SnapshotShards();if(!EventSystem.ChooseOffer(run,offerId))return false;AfterEventInteraction(cardsBefore,relicsBefore,shardsBefore);return true;
        }
        private bool ChooseEventShardReplacementWithAcquisition(string newShardId,string replacementId)
        {
            var cardsBefore=SnapshotCardCopies();var relicsBefore=SnapshotRelics();var shardsBefore=SnapshotShards();if(!EventSystem.ChooseShardReplacement(run,newShardId,replacementId))return false;AfterEventInteraction(cardsBefore,relicsBefore,shardsBefore);return true;
        }

        private void DrawEventSelection(float w,float h)
        {
            if(EventShardPresentation)Fill(new Rect(0,0,w,h),new Color(.004f,.008f,.018f));else DrawEventBackdrop(w,h);DrawRunHud(w);var choice=EventContent.FindChoice(run.activeEventId,run.pendingEventChoiceId);var title=run.eventSelectionKind switch{EventSelectionKind.Card=>"CHOOSE A PHYSICAL CARD",EventSelectionKind.RewardCard=>"CHOOSE ONE REWARD",EventSelectionKind.Binding=>"CHOOSE A BINDING",EventSelectionKind.FocusedEffect=>"FOCUS ONE EFFECT",EventSelectionKind.ShardReward=>"CHOOSE A FATE SHARD",EventSelectionKind.OwnedShard=>"CHOOSE AN OWNED SHARD",EventSelectionKind.ShardReplacement=>"FATE SHARD CASE FULL",EventSelectionKind.Relic=>"CHOOSE A RELIC TO SURRENDER",_=>"COMPLETE THE CHOICE"};
            GUI.Label(new Rect(w*.16f,66,w*.68f,48),title,new GUIStyle(titleStyle){fontSize=31,normal={textColor=new Color(.98f,.91f,.72f)}});Fill(new Rect(w*.5f-210,115,420,1),new Color(.76f,.61f,.32f,.7f));GUI.Label(new Rect(w*.16f,119,w*.68f,25),(choice?.title??currentEvent?.name??"EVENT")+"  ·  "+Mathf.Max(1,run.pendingEventChoicesNeeded)+" SELECTION"+(run.pendingEventChoicesNeeded==1?"":"S")+" REMAIN",new GUIStyle(subtitleStyle){fontSize=14,normal={textColor=new Color(.9f,.86f,.75f)}});
            if(run.eventSelectionKind==EventSelectionKind.Card)DrawEventCardSelection(w,h);
            else if(run.eventSelectionKind==EventSelectionKind.RewardCard)DrawEventCardRewards(w,h);
            else if(run.eventSelectionKind is EventSelectionKind.Binding or EventSelectionKind.FocusedEffect)DrawEventBindingOffers(w,h);
            else if(run.eventSelectionKind is EventSelectionKind.ShardReward or EventSelectionKind.OwnedShard)DrawEventShardOffers(w,h);
            else if(run.eventSelectionKind==EventSelectionKind.ShardReplacement)DrawEventShardReplacement(w,h);
            else if(run.eventSelectionKind==EventSelectionKind.Relic)DrawEventRelicOffers(w,h);
            DrawEventCancel(w,h);DrawPersistentRunTooltip(w,h);
        }

        private void DrawEventCardSelection(float w,float h)
        {
            var eligibleCards=EventSystem.PendingEligibleCards(run).ToArray();var eligible=eligibleCards.Select(c=>c.persistentId).ToHashSet();var controllerCard=controllerNavigation&&eligibleCards.Length>0?eligibleCards[Mathf.Clamp(screenControllerIndex,0,eligibleCards.Length-1)]:null;var all=run.cards.ToArray();const int columns=6;const float gap=12f;var viewport=new Rect(34,151,w-68,h-230);var cardW=Mathf.Min(184f,(viewport.width-34-gap*(columns-1))/columns);var cardH=cardW*1.48f;var rowStep=cardH+17;var totalW=columns*cardW+(columns-1)*gap;var start=(viewport.width-totalW)*.5f;var rows=Mathf.CeilToInt(all.Length/(float)columns);var contentHeight=Mathf.Max(viewport.height,rows*rowStep+8);if(controllerCard!=null)KeepGridSelectionVisible(ref cardChoiceScroll,System.Array.IndexOf(all,controllerCard),columns,rowStep,viewport.height,contentHeight);UpdateScrollArea(viewport,ref cardChoiceScroll,contentHeight);RunCard selected=null;
            GUI.BeginGroup(viewport);
            for(var index=0;index<all.Length;index++){var saved=all[index];var shown=saved.BuildDefinition();var row=index/columns;var col=index%columns;var r=new Rect(start+col*(cardW+gap),row*rowStep+4-cardChoiceScroll,cardW,cardH);if(r.yMax<0||r.y>viewport.height)continue;DrawCard(r,shown);RegisterCardKeywordHelp(new Rect(viewport.x+r.x,viewport.y+r.y,r.width,r.height),shown,controllerCard==saved);var available=eligible.Contains(saved.persistentId);if(!available)Fill(r,new Color(0,0,0,.69f));else Outline(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),controllerCard==saved?Gold:new Color(1f,.78f,.28f,.72f),controllerCard==saved?5:2);GUI.enabled=available&&!acquisitionActive;if(GUI.Button(r,"",GUIStyle.none))selected=saved;GUI.enabled=true;}
            GUI.EndGroup();DrawScrollRail(viewport,cardChoiceScroll,contentHeight,all.Length+" PHYSICAL CARDS  ·  FIRST ADDED FIRST");if(selected!=null)ChooseEventCardWithAcquisition(selected);
        }

        private void DrawEventCardRewards(float w,float h)
        {
            var choice=EventContent.FindChoice(run.activeEventId,run.pendingEventChoiceId);var effect=choice?.effects.FirstOrDefault(e=>e.kind==EventEffectKind.RewardCards);var offers=run.pendingEventOfferIds.Select(GameContent.Find).Where(c=>c!=null).ToArray();var cardW=220f;var cardH=330f;var gap=34f;var start=(w-(offers.Length*cardW+Mathf.Max(0,offers.Length-1)*gap))*.5f;
            for(var i=0;i<offers.Length;i++){var shown=effect?.upgraded==true?GameContent.Upgrade(offers[i]):offers[i];var r=new Rect(start+i*(cardW+gap),h*.27f,cardW,cardH);DrawCard(r,shown);RegisterCardKeywordHelp(r,shown,controllerNavigation&&screenControllerIndex==i);if(controllerNavigation&&screenControllerIndex==i)Outline(new Rect(r.x-6,r.y-6,r.width+12,r.height+12),Gold,4);if(!acquisitionActive&&GUI.Button(r,"",GUIStyle.none))ChooseEventOfferWithAcquisition(offers[i].id);}
        }

        private void DrawEventBindingOffers(float w,float h)
        {
            var offers=run.pendingEventOfferIds;var width=Mathf.Min(265f,(w-180)/Mathf.Max(1,offers.Count));var gap=28f;var start=(w-(width*offers.Count+gap*Mathf.Max(0,offers.Count-1)))*.5f;
            for(var i=0;i<offers.Count;i++){var id=offers[i];var binding=WorldContent.Bindings.FirstOrDefault(b=>b.id==id);var r=new Rect(start+i*(width+gap),h*.28f,width,340);Fill(r,new Color(.008f,.012f,.02f,.98f));Outline(r,controllerNavigation&&screenControllerIndex==i?Gold:new Color(.78f,.57f,.2f),controllerNavigation&&screenControllerIndex==i?4:2);GUI.Label(new Rect(r.x+20,r.y+28,r.width-40,92),BindingGlyph(id)+"\n"+(binding?.name??id.ToUpperInvariant()),new GUIStyle(titleStyle){fontSize=23,normal={textColor=Gold}});GUI.Label(new Rect(r.x+22,r.y+136,r.width-44,130),binding?.text??("Apply +1 stack to "+id+"."),new GUIStyle(footerStyle){fontSize=16,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.94f,.91f,.82f)}});GUI.Label(new Rect(r.x+20,r.yMax-52,r.width-40,28),"ENGRAVE GOLDEN THREAD",footerStyle);if(GUI.Button(r,"",GUIStyle.none))ChooseEventOfferWithAcquisition(id);}
        }

        private void DrawEventShardOffers(float w,float h)
        {
            var ids=run.eventSelectionKind==EventSelectionKind.OwnedShard?EventSystem.PendingEligibleShards(run).Select(s=>s.id).ToList():run.pendingEventOfferIds;var width=270f;var gap=32f;var start=(w-(ids.Count*width+gap*Mathf.Max(0,ids.Count-1)))*.5f;
            for(var i=0;i<ids.Count;i++){var shard=WorldContent.FateShards.FirstOrDefault(s=>s.id==ids[i]);if(shard==null)continue;var r=new Rect(start+i*(width+gap),h*.3f,width,280);Fill(r,new Color(.008f,.014f,.022f,.98f));Outline(r,controllerNavigation&&screenControllerIndex==i?Gold:new Color(.51f,.67f,.82f),controllerNavigation&&screenControllerIndex==i?4:2);DrawFateShardArt(new Rect(r.center.x-65,r.y+22,130,130),shard);GUI.Label(new Rect(r.x+16,r.y+160,r.width-32,34),shard.name,new GUIStyle(titleStyle){fontSize=21,normal={textColor=new Color(.96f,.83f,.49f)}});GUI.Label(new Rect(r.x+20,r.y+202,r.width-40,64),shard.stableText,new GUIStyle(footerStyle){fontSize=13,wordWrap=true,alignment=TextAnchor.UpperCenter});if(GUI.Button(r,"",GUIStyle.none))ChooseEventOfferWithAcquisition(shard.id);}
        }

        private void DrawEventShardReplacement(float w,float h)
        {
            var decided=run.pendingEventShardDecisions.Select(d=>d.Split(new[]{"=>"},System.StringSplitOptions.None)[0]).ToHashSet();var newId=run.pendingEventOfferIds.FirstOrDefault(id=>!decided.Contains(id));var incoming=WorldContent.FateShards.FirstOrDefault(s=>s.id==newId);GUI.Label(new Rect(w*.16f,132,w*.68f,68),"INCOMING: "+(incoming?.name??"FATE SHARD")+"\nChoose one owned Shard to replace, or discard the incoming Shard.",new GUIStyle(subtitleStyle){fontSize=18,wordWrap=true,normal={textColor=new Color(.94f,.9f,.78f)}});var options=run.shards.Count+1;var width=245f;var gap=24f;var start=(w-(options*width+gap*(options-1)))*.5f;
            for(var i=0;i<options;i++){var discard=i==run.shards.Count;var shard=discard?null:WorldContent.FateShards.FirstOrDefault(s=>s.id==run.shards[i].id);var r=new Rect(start+i*(width+gap),h*.35f,width,265);Fill(r,new Color(.008f,.013f,.021f,.98f));Outline(r,controllerNavigation&&screenControllerIndex==i?Gold:discard?new Color(.58f,.34f,.3f):new Color(.58f,.55f,.42f),controllerNavigation&&screenControllerIndex==i?4:2);GUI.Label(new Rect(r.x+18,r.y+35,r.width-36,76),discard?"×\nDISCARD NEW":shard.name,new GUIStyle(titleStyle){fontSize=22,wordWrap=true,normal={textColor=discard?new Color(.9f,.5f,.45f):new Color(1f,.86f,.5f)}});GUI.Label(new Rect(r.x+22,r.y+130,r.width-44,90),discard?"Keep every owned Shard. The incoming Shard is lost.":shard.stableText,new GUIStyle(footerStyle){fontSize=14,wordWrap=true,alignment=TextAnchor.UpperCenter});if(GUI.Button(r,"",GUIStyle.none))ChooseEventShardReplacementWithAcquisition(newId,discard?"discard":run.shards[i].id);}
        }

        private void DrawEventRelicOffers(float w,float h)
        {
            var relics=EventSystem.PendingEligibleRelics(run).ToArray();var width=250f;var gap=30f;var start=(w-(relics.Length*width+gap*Mathf.Max(0,relics.Length-1)))*.5f;
            for(var i=0;i<relics.Length;i++){var relic=relics[i];var r=new Rect(start+i*(width+gap),h*.31f,width,285);DrawRelicTile(r,relic,System.Array.IndexOf(GameContent.Relics,relic),false);if(controllerNavigation&&screenControllerIndex==i)Outline(new Rect(r.x-5,r.y-5,r.width+10,r.height+10),Gold,4);if(GUI.Button(r,"",GUIStyle.none))ChooseEventOfferWithAcquisition(relic.id);}
        }

        private void DrawEventResult(float w,float h)
        {
            DrawEventBackdrop(w,h);DrawRunHud(w);var pulse=.78f+(profile.reduceMotion?0:Mathf.Sin(shimmer*2.4f)*.16f);var panel=new Rect(w*.2f,h*.21f,w*.6f,h*.54f);Fill(panel,new Color(.005f,.009f,.015f,.94f));Outline(panel,new Color(1f,.72f,.23f,pulse),3);GUI.Label(new Rect(panel.x+30,panel.y+38,panel.width-60,80),"✦\nFATE ALTERED",new GUIStyle(titleStyle){fontSize=32,normal={textColor=new Color(1f,.82f,.42f,pulse)}});GUI.Label(new Rect(panel.x+55,panel.y+150,panel.width-110,145),run.pendingEventResult,new GUIStyle(footerStyle){fontSize=22,wordWrap=true,normal={textColor=new Color(.97f,.94f,.85f)}});var next=new Rect(panel.center.x-150,panel.yMax-82,300,48);DrawButtonFrame(next,next.Contains(PointerPosition),false);if(GUI.Button(next,"RETURN TO THE MAP",buttonStyle)){EventSystem.CompleteResult(run);Advance();}DrawPersistentRunTooltip(w,h);
        }

        private void DrawEventCancel(float w,float h){var r=new Rect(34,h-67,170,38);DrawButtonFrame(r,r.Contains(PointerPosition),false);if(GUI.Button(r,"CANCEL",buttonStyle)&&EventSystem.CancelSelection(run)){collectionPage=0;SaveService.Save(run);screen=ScreenMode.Event;}}
        private void DrawEventBackdrop(float w,float h){if(mapBackground)GUI.DrawTexture(new Rect(0,0,w,h),mapBackground,ScaleMode.ScaleAndCrop);else DrawLocationBackdrop(w,h,(currentEvent?.artIndex??0)%4);Fill(new Rect(0,0,w,h),new Color(.002f,.005f,.01f,.66f));var seed=(currentEvent?.artIndex??0)+1;for(var i=0;i<9;i++){var x=Mathf.Repeat(seed*127+i*173,w);var sway=profile.reduceMotion?0:Mathf.Sin(shimmer*.5f+i)*24;DrawLine(new Vector2(x+sway,-20),new Vector2(x-sway+70,h+20),new Color(1f,.65f,.16f,.025f+(i%3)*.012f),1+(i%2));}}
        private void DrawEventArtwork(Rect r){var index=currentEvent?.artIndex??0;var atlas=index<15?eventAtlasActI:index<30?eventAtlasActII:index<45?eventAtlasActIII:eventAtlasGeneral;var local=index<15?index:index<30?index-15:index<45?index-30:index-45;if(atlas)DrawAtlasIcon(atlas,local,5,index<45?3:1,r);else DrawAtlasIcon(locationAtlas,index%4,4,1,r);Fill(new Rect(r.x,r.y,r.width,24),new Color(0,0,0,.24f));}
        private void DrawPersistentRunBar(float w,bool combatMode)
        {
            runHudTooltipTitle=runHudTooltipDetail=null;
            var pointer=PointerPosition;var hp=combatMode&&combat!=null?displayedPlayerHp:run.hp;var maxHp=combatMode&&combat!=null?combat.player.maxHp:run.maxHp;
            Fill(new Rect(0,0,w,58),profile.highContrastUi?new Color(.002f,.004f,.007f,.99f):new Color(.003f,.006f,.011f,.95f));
            Fill(new Rect(0,57,w,2),new Color(.78f,.60f,.28f,.72f));

            var health=new Rect(18,8,162,40);
            if(hudEmblemAtlas)DrawAtlasIcon(hudEmblemAtlas,1,4,2,new Rect(health.x+5,health.y+2,38,38));else GUI.Label(new Rect(health.x+8,health.y,34,health.height),"♥",new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=25,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.32f,.31f)}});
            GUI.Label(new Rect(health.x+43,health.y,111,health.height),$"{hp}/{maxHp}",new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=19,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.91f,.86f)}});
            if(health.Contains(pointer))SetRunHudTooltip(health,"HEALTH",$"{hp} of {maxHp} health remains."+(combatMode&&combat.player.block>0?$"\n{combat.player.block} Block is protecting it this turn.":""));

            var gold=new Rect(190,8,104,40);DrawGoldIcon(RunGoldIconRect);
            GUI.Label(new Rect(gold.x+39,gold.y,gold.width-42,gold.height),run.gold.ToString(),new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=18,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(1f,.85f,.52f)}});
            if(gold.Contains(pointer))SetRunHudTooltip(gold,"GOLD",$"{run.gold} Gold. Spend it with merchants and at certain Vault events.");

            var bossIndex=Mathf.Clamp(13+run.act,14,16);var boss=WorldContent.Enemies[bossIndex];var bossRect=new Rect(313,6,46,46);
            DrawFloatingEnemy(bossRect,boss);
            if(bossRect.Contains(pointer))SetRunHudTooltip(bossRect,$"ACT {run.act} BOSS · {boss.name}",boss.description);

            var floorRect=new Rect(376,7,116,43);DrawFloorHudIcon(new Rect(floorRect.x+4,floorRect.y+5,39,32));
            GUI.Label(new Rect(floorRect.x+49,floorRect.y+1,floorRect.width-50,21),"FLOOR",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=8,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.72f,.71f,.67f)}});
            GUI.Label(new Rect(floorRect.x+49,floorRect.y+17,floorRect.width-50,25),(run.floor+1).ToString(),new GUIStyle(titleStyle){font=labelFont?labelFont:bodyFont,fontSize=18,alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.98f,.88f,.65f)}});
            if(floorRect.Contains(pointer))SetRunHudTooltip(floorRect,$"ACT {run.act} · FLOOR {run.floor+1}","Your current room in this ascent. Follow a connected golden path to climb toward the pictured boss.");
            DrawPersistentRunControls(w,pointer);

            var shown=VisibleRunRelics(w);
            for(var i=0;i<shown;i++)
            {
                var id=run.relics[i];var relic=GameContent.Relics.FirstOrDefault(x=>x.id==id);var atlasIndex=System.Array.FindIndex(GameContent.Relics,x=>x.id==id);var r=RunRelicSlotRect(i);
                RegisterCombatHudTarget("relic:"+id,9,r,relic?.name??id,(relic?.text??"")+(combat!=null?"\n\n"+combat.RelicProgress(id):""));
                var grow=screen==ScreenMode.Combat?RelicPulseGrowth(id,Time.unscaledTime):0;var relicDraw=new Rect(r.x-grow,r.y-grow,r.width+grow*2,r.height+grow*2);DrawRelicArt(relicDraw,Mathf.Max(0,atlasIndex));DrawRelicCounter(r,id);
                if(r.Contains(pointer)){Outline(new Rect(r.x-2,r.y-2,r.width+4,r.height+4),Gold,2);SetRunHudTooltip(r,relic?.name??id.Replace('_',' ').ToUpperInvariant(),(relic?.text??"A relic carried for the rest of this run.")+(screen==ScreenMode.Combat&&combat!=null?"\n\n"+combat.RelicProgress(id):""));}
            }
            if(run.relics.Count>shown)
            {
                var overflow=RunRelicSlotRect(shown);
                GUI.Label(overflow,"+"+(run.relics.Count-shown),new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=14,alignment=TextAnchor.MiddleCenter,normal={textColor=Gold}});
                var detail=string.Join("\n\n",run.relics.Skip(shown).Select(id=>GameContent.Relics.FirstOrDefault(r=>r.id==id)).Where(r=>r!=null).Select(r=>r.name+"\n"+r.text));
                RegisterCombatHudTarget("relic:overflow",9,overflow,"MORE RELICS",detail);
                if(overflow.Contains(pointer))SetRunHudTooltip(overflow,"MORE RELICS",detail);
            }
        }
        private void DrawFloorHudIcon(Rect r)
        {
            Fill(new Rect(r.x+2,r.yMax-8,12,7),new Color(.66f,.49f,.25f));Fill(new Rect(r.x+13,r.yMax-15,12,14),new Color(.77f,.58f,.29f));Fill(new Rect(r.x+24,r.yMax-23,12,22),new Color(.92f,.71f,.36f));
            DrawLine(new Vector2(r.x+2,r.yMax),new Vector2(r.xMax-1,r.yMax),new Color(1f,.82f,.46f,.8f),2);DrawLine(new Vector2(r.xMax-4,r.y+1),new Vector2(r.xMax-4,r.y+12),new Color(1f,.82f,.46f,.82f),2);
        }
        private void DrawGoldIcon(Rect r)
        {
            if(hudEmblemAtlas){DrawAtlasIcon(hudEmblemAtlas,0,4,2,r);return;}
            // A freestanding three-coin silhouette: no square tile or ornamental box.
            var edge=new Color(1f,.78f,.23f,.98f);var light=new Color(1f,.88f,.43f,.98f);var dark=new Color(.48f,.25f,.035f,.98f);var h=Mathf.Max(3,r.height*.18f);
            for(var i=0;i<3;i++){var x=r.x+r.width*(.08f+i*.18f);var y=r.yMax-h*(1.25f+i*.72f);var w=r.width*.62f;Fill(new Rect(x,y,w,h),dark);Fill(new Rect(x+1,y,w-2,Mathf.Max(1,h*.42f)),light);DrawLine(new Vector2(x,y),new Vector2(x+w,y),edge,1.5f);DrawLine(new Vector2(x,y+h),new Vector2(x+w,y+h),new Color(.82f,.47f,.09f),1);}
        }
        private void DrawFateShardArt(Rect r,FateShardDef shard)
        {
            var index=Mathf.Max(0,System.Array.IndexOf(WorldContent.FateShards,shard));if(fateShardIconAtlas)DrawAtlasIcon(fateShardIconAtlas,index,6,5,r);else DrawAtlasIcon(consumableAtlas,index%8,4,2,r);
        }
        private void SetRunHudTooltip(Rect anchor,string title,string detail){runHudTooltipAnchor=anchor;runHudTooltipTitle=title;runHudTooltipDetail=detail;}
        private void DrawPersistentRunTooltip(float w,float h)
        {
            if(string.IsNullOrEmpty(runHudTooltipTitle))return;
            const float width=330f;var measure=new GUIStyle(footerStyle){fontSize=16,wordWrap=true};
            var height=Mathf.Max(124,measure.CalcHeight(new GUIContent(runHudTooltipTitle+"\n"+runHudTooltipDetail),width-24)+20);
            var x=runHudTooltipAnchor.x;var y=runHudTooltipAnchor.yMax+9;
            if(x+width>w-16)x=w-width-16;if(x<16)x=16;
            if(y+height>h-14)y=Mathf.Max(68,runHudTooltipAnchor.y-height-9);
            var rect=new Rect(x,y,width,height);
            if(ShardDiscoveryOpen&&run.shards.Count==RunModel.ShardCapacity)rect=ShardReplacementTooltipRect(runHudTooltipAnchor,w,h,height);
            DrawTooltip(rect,runHudTooltipTitle,runHudTooltipDetail,runHudTooltipAnchor);
        }
        private void DrawRunHud(float w){DrawPersistentRunBar(w,screen==ScreenMode.Combat);DrawHealthServiceFeedback();}
        private void DrawRunDock(float w){DrawPersistentRunBar(w,false);DrawHealthServiceFeedback();}
        private static int BindingIconIndex(string id)=>id switch{"serrated"=>0,"reinforced"=>1,"weighted"=>2,"quickened"=>3,"lingering"=>4,"gilded"=>5,"focused"=>6,"chained"=>7,"recurring"=>8,"fateful"=>9,"perfected"=>10,_=>15};
        private static int FateweaveIconIndex(string id)=>id switch
        {
            "foreign_memory" or "favorable_hand" or "entangled_fates" or "stolen_destiny"=>11,
            "wanderers_thread"=>12,
            "gilded_cache" or "burdened_fortune" or "heavy_crown" or "fortunes_burden"=>13,
            "severed_burden"=>14,
            "gilded_edge"=>0,"gilded_guard"=>1,"first_light"=>3,"deepened_power"=>6,"perfected_edge" or "perfected_guard"=>10,"golden_echo"=>8,
            _=>15
        };
        private static int ModificationIconIndex(CardDef card)=>card.specialModificationKind==SpecialModificationKind.Binding?BindingIconIndex(card.specialModification):FateweaveIconIndex(card.specialModification);
        private static void CardModificationInfo(CardDef card,out string title,out string detail)
        {
            if(card==null||!card.IsModified){title="";detail="";return;}
            if(card.specialModificationKind==SpecialModificationKind.Binding){var binding=WorldContent.Bindings.FirstOrDefault(b=>b.id==card.specialModification);title=binding?.name??card.specialModification.Replace('_',' ').ToUpperInvariant();detail=binding?.text??"This physical card copy carries a permanent Binding.";return;}
            var fate=WorldContent.Fateweaves.FirstOrDefault(f=>f.id==card.specialModification);title=fate?.name??card.specialModification.Replace('_',' ').ToUpperInvariant();detail=fate?.text??"This physical card copy was permanently rewritten by the Fateweave.";
        }
        private static string FateweaveTag(string id)=>id switch
        {
            "gilded_cache" or "burdened_fortune" or "heavy_crown" or "fortunes_burden"=>"RELIC BOON",
            "severed_burden"=>"DECK SACRIFICE",
            "foreign_memory" or "wanderers_thread" or "favorable_hand" or "entangled_fates" or "stolen_destiny"=>"CARD REVELATION",
            _=>"PERMANENT REWRITE"
        };
        private static string BindingGlyph(string id)=>id switch{"serrated"=>"╱╱","reinforced"=>"⬡","weighted"=>"⇊","quickened"=>"⌛","lingering"=>"∞","gilded"=>"♛","focused"=>"◎","chained"=>"⚭","recurring"=>"↻","fateful"=>"✦✦✦","perfected"=>"◇",_=>"✦"};

        private void DrawTreasure(float w,float h){DrawLocationBackdrop(w,h,2);DrawRunDock(w);Heading(w,"TREASURE VAULT",run.beggarFavor?"SIX GOLDEN HANDPRINTS SHIMMER ON THE LOCK":"GOLDEN LIGHT ESCAPES FROM THE SEAMS");Choice(new Rect(w*.35f,h*.34f,w*.3f,260),"OPEN THE CHEST","Gain gold, a relic, and sometimes a Fate Shard.",OpenTreasure,0);}
        private void OpenTreasure()
        {
            if(acquisitionActive)return;var gain=run.beggarFavor?105:45;run.gold+=gain;observedGold=run.gold;profile.goldCollected+=gain;run.beggarFavor=false;goldCollectAmount=gain;goldCollectOrigin=new Vector2(CombatWidth*.5f,CombatHeight*.48f);goldCollectTime=profile.reduceMotion?.32f:1.08f;
            var shardId=run.RollTreasureShard();if(!string.IsNullOrEmpty(shardId))run.OfferShard(shardId);
            run.stage=RunStage.RelicReward;screen=ScreenMode.RelicReward;ProfileService.Save(profile);SaveService.Save(run);
        }

        private void DrawRelicReward(float w,float h)
        {
            if(currentNode?.kind==NodeKind.Boss&&run.encounterRewards?.cardClaimed==true){DrawBossRelicReward(w,h);return;}
            // Migrate old unfinished elite/boss reward screens into the card reward flow.
            if(currentNode?.kind!=NodeKind.Treasure){run.RollEncounterRewards(currentNode?.kind??NodeKind.Combat);run.stage=RunStage.CardReward;screen=ScreenMode.Reward;SaveService.Save(run);DrawReward(w,h);return;}
            DrawFullBackdrop(rewardBackground,w,h,.24f);DrawRunDock(w);Heading(w,"TREASURE RECOVERED","A RELIC FROM THE SEALED VAULT");
            if(!ShardDiscoveryOpen)Choice(new Rect(w*.35f,h*.34f,w*.3f,200),run.treasureRelicClaimedReceipt==run.RoomReceipt?"CONTINUE":"CLAIM RELIC",run.treasureRelicClaimedReceipt==run.RoomReceipt?"Relic collected. Return to your ascent.":"Take the relic and return to your ascent.",GrantRelic);
        }
        private void EnterFateweave(){if(run.act>=3){CompleteRun();return;}run.BeginNextAct();run.BeginFateweave();rewardRevealTime=profile.reduceMotion?0:1.4f;SaveService.Save(run);screen=ScreenMode.Fateweave;Sfx(SoundCue.Fateweave);}

        private void DrawBindingSelect(float w,float h)
        {
            DrawFullBackdrop(rewardBackground,w,h,.48f);DrawRunHud(w);Heading(w,"CHOOSE A BINDING",$"ACT {run.act} · ONE SPECIAL MODIFICATION PER PHYSICAL CARD COPY");var offers=run.bindingOffers.Select(id=>WorldContent.Bindings.FirstOrDefault(b=>b.id==id)).Where(b=>b!=null).ToArray();
            for(var i=0;i<offers.Length;i++)
            {
                var binding=offers[i];var baseRect=new Rect(w*.5f-410+i*285,h*.27f,255,352);var hot=ScreenChoiceHot(baseRect,i);var r=baseRect;r.y+=hot&&!profile.reduceMotion?-10:0;var eligible=run.cards.Count(c=>c.specialModificationKind==SpecialModificationKind.None&&BindingMatches(binding,c.BuildDefinition()));
                Fill(new Rect(r.x-8,r.y+9,r.width+16,r.height+10),new Color(0,0,0,.42f));Fill(r,new Color(.009f,.015f,.025f,.98f));Fill(new Rect(r.x,r.y,r.width,98),new Color(.04f,.12f,.17f,.44f));Outline(r,hot?new Color(.39f,.9f,1f):eligible>0?new Color(.72f,.61f,.34f):new Color(.3f,.3f,.32f),hot?4:eligible>0?2:1);
                var icon=new Rect(r.center.x-48,r.y+17,96,96);if(eligible<=0)GUI.color=new Color(.38f,.38f,.4f,1);DrawAtlasIcon(bindingFateIconAtlas,BindingIconIndex(binding.id),4,4,icon);GUI.color=Color.white;
                GUI.Label(new Rect(r.x+18,r.y+116,r.width-36,34),binding.name,new GUIStyle(titleStyle){fontSize=22,normal={textColor=eligible>0?new Color(.84f,.94f,1f):new Color(.5f,.5f,.52f)}});
                GUI.Label(new Rect(r.x+22,r.y+158,r.width-44,92),binding.text,new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.92f,.89f,.82f)}});
                Fill(new Rect(r.x+20,r.yMax-78,r.width-40,1),new Color(.35f,.72f,.82f,.5f));GUI.Label(new Rect(r.x+18,r.yMax-69,r.width-36,22),eligible+" ELIGIBLE CARDS",new GUIStyle(footerStyle){fontSize=11,normal={textColor=eligible>0?new Color(.53f,.87f,.98f):new Color(.5f,.5f,.52f)}});GUI.Label(new Rect(r.x+18,r.yMax-42,r.width-36,25),"CHOOSE THIS BINDING",new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=12,normal={textColor=hot?Color.white:Gold}});
                GUI.enabled=eligible>0&&!acquisitionActive;if(GUI.Button(baseRect,"",GUIStyle.none)&&run.SelectBinding(binding.id)){collectionPage=0;cardChoiceScroll=0;SaveService.Save(run);screen=ScreenMode.BindingCard;}GUI.enabled=true;
            }
            DrawBindingBack(h);
        }
        private void DrawBindingCards(float w,float h)
        {
            DrawRunHud(w);var binding=WorldContent.Bindings.FirstOrDefault(b=>b.id==run.pendingBindingId);Heading(w,"ENGRAVE "+(binding?.name??"BINDING"),"CHOOSE AN ELIGIBLE CARD · DIMMED CARDS CANNOT RECEIVE THIS BINDING");DrawPhysicalCardChoices(w,h,run.cards.ToArray(),card=>{if(acquisitionActive)return;if(run.ApplyPendingBinding(card)){var shown=card.BuildDefinition();banner=shown.name+" IS NOW BOUND";SaveService.Save(run);BeginModificationAcquisition(shown,()=>Advance());}});
            var panel=new Rect(w-288,190,254,390);Fill(panel,new Color(.01f,.02f,.03f,.95f));
            if(binding!=null){DrawAtlasIcon(bindingFateIconAtlas,BindingIconIndex(binding.id),4,4,new Rect(panel.center.x-36,panel.y+18,72,72));GUI.Label(new Rect(panel.x+18,panel.y+108,panel.width-36,260),binding.name+"\n\n"+binding.text+"\n\nOne permanent modification per physical card. Nothing is applied until you confirm an eligible card.",new GUIStyle(footerStyle){fontSize=16,wordWrap=true,alignment=TextAnchor.UpperLeft});}
            DrawBindingBack(h);
        }
        private static bool BindingMatches(BindingDef binding,CardDef card)
            =>EventSystem.BindingEligible(binding,card);

        private void DrawFateweave(float w,float h)
        {
            DrawFateweaveVoid(w,h);
            if(!FateweavePullActive&&!string.IsNullOrEmpty(run.pendingFateweaveId)&&run.pendingChoicesNeeded==0){Heading(w,"FATE ALTERED",run.pendingFateweaveId.Replace('_',' ').ToUpperInvariant());var pulse=.84f+Mathf.Sin(shimmer*2)*.16f;var chosen=WorldContent.Fateweaves.FirstOrDefault(f=>f.id==run.pendingFateweaveId);DrawAtlasIcon(bindingFateIconAtlas,FateweaveIconIndex(run.pendingFateweaveId),4,4,new Rect(w*.5f-82,h*.27f,164,164));GUI.Label(new Rect(w*.25f,h*.50f,w*.5f,72),chosen?.text??"The chosen strand has entered your fate.",new GUIStyle(footerStyle){fontSize=17,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(1f,.85f,.55f,pulse)}});var next=new Rect(w*.5f-170,h*.72f,340,58);DrawButtonFrame(next,next.Contains(PointerPosition)&&!acquisitionActive,acquisitionActive);GUI.enabled=!acquisitionActive;if(GUI.Button(next,"ENTER ACT "+RomanAct(run.act),buttonStyle)){run.CompleteFateweave();SaveService.Save(run);mapFocusFloor=-1;screen=ScreenMode.Map;}GUI.enabled=true;return;}
            Heading(w,"THE FATEWEAVE",$"BEFORE ACT {RomanAct(run.act)} · PULL ONE OF THREE DESCENDING STRANDS");var offers=run.fateweaveOffers.Select(id=>WorldContent.Fateweaves.FirstOrDefault(f=>f.id==id)).Where(f=>f!=null).ToArray();
            const float width=272,height=370,gap=42;var start=(w-(offers.Length*width+Mathf.Max(0,offers.Length-1)*gap))*.5f;
            for(var i=0;i<offers.Length;i++)
            {
                var opacity=FateweaveChoiceOpacity;if(opacity<=0)continue;var previousColor=GUI.color;GUI.color=new Color(1,1,1,opacity);var fate=offers[i];var baseRect=new Rect(start+i*(width+gap),h*.255f,width,height);var hot=!acquisitionActive&&(ScreenChoiceHot(baseRect,i));var r=baseRect;r.y+=profile.reduceMotion?0:hot?-12:(1-opacity)*20;var x=r.center.x;var strandColor=new Color(hot?1f:.76f,hot?.78f:.52f,hot?.28f:.17f,(hot?1f:.58f)*opacity);var previous=new Vector2(x,92);
                for(var segment=1;segment<=8;segment++){var t=segment/8f;var next=new Vector2(Mathf.Lerp(x,r.center.x,t)+Mathf.Sin(segment*.9f+i*1.6f+shimmer*(profile.reduceMotion?0:hot?.65f:.12f))*(hot?8:2),Mathf.Lerp(92,r.y+31,t));DrawLine(previous,next,strandColor,hot?4:2);previous=next;}
                Fill(new Rect(r.x-9,r.y+10,r.width+18,r.height+14),new Color(0,0,0,.48f*opacity));Fill(r,new Color(.006f,.010f,.019f,.97f*opacity));Fill(new Rect(r.x,r.y,r.width,126),new Color(.20f,.10f,.27f,.22f*opacity));Outline(r,hot?Gold:new Color(.48f,.39f,.24f,opacity),hot?4:2);Outline(new Rect(r.x+7,r.y+7,r.width-14,r.height-14),new Color(.42f,.29f,.55f,.36f*opacity),1);
                var icon=new Rect(r.center.x-52,r.y+22,104,104);DrawAtlasIcon(bindingFateIconAtlas,FateweaveIconIndex(fate.id),4,4,icon);
                GUI.Label(new Rect(r.x+18,r.y+134,r.width-36,18),FateweaveTag(fate.id),new GUIStyle(footerStyle){font=labelFont?labelFont:bodyFont,fontSize=10,normal={textColor=new Color(.72f,.55f,.94f)}});
                GUI.Label(new Rect(r.x+16,r.y+157,r.width-32,34),fate.name,new GUIStyle(titleStyle){fontSize=21,normal={textColor=hot?new Color(1f,.92f,.63f):new Color(.88f,.73f,.42f)}});
                GUI.Label(new Rect(r.x+25,r.y+202,r.width-50,93),fate.text,new GUIStyle(footerStyle){fontSize=15,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.94f,.91f,.83f)}});
                var button=new Rect(r.x+25,r.yMax-54,r.width-50,36);Fill(button,new Color(.04f,.025f,.015f,.96f*opacity));Outline(button,hot?Gold:new Color(.58f,.44f,.21f,opacity),hot?2:1);GUI.Label(button,"PULL THIS STRAND",new GUIStyle(buttonStyle){fontSize=12,normal={textColor=hot?Color.white:Gold}});
                GUI.color=previousColor;
                if(!acquisitionActive&&GUI.Button(baseRect,"",GUIStyle.none)){var cardsBefore=SnapshotCardCopies();var relicsBefore=SnapshotRelics();if(run.SelectFateweave(fate.id)){SaveService.Save(run);BeginFateweavePull(fate,()=>{screen=run.stage==RunStage.FateweaveCard?ScreenMode.FateweaveCard:ScreenMode.Fateweave;PresentNewRunAcquisitions(cardsBefore,relicsBefore);});}}
            }
        }
        private void DrawFateweaveCards(float w,float h)
        {
            Fill(new Rect(0,0,w,h),new Color(.004f,.006f,.012f,1));DrawRunHud(w);Heading(w,"THE STRAND ENTERS YOUR DECK",run.pendingChoicesNeeded+" SELECTION"+(run.pendingChoicesNeeded==1?"":"S")+" REMAIN");
            if(run.pendingCardOfferIds.Count>0){var cards=run.pendingCardOfferIds.Select(GameContent.Find).Where(c=>c!=null).ToArray();var start=(w-(cards.Length*220+(cards.Length-1)*34))*.5f;for(var i=0;i<cards.Length;i++){var r=new Rect(start+i*254,h*.27f,220,330);var shown=run.pendingFateweaveId=="entangled_fates"?GameContent.Upgrade(cards[i]):cards[i];DrawCard(r,shown);RegisterCardKeywordHelp(r,shown,controllerNavigation&&screenControllerIndex==i);if(controllerNavigation&&screenControllerIndex==i)Outline(new Rect(r.x-5,r.y-5,r.width+10,r.height+10),Gold,4);if(!acquisitionActive&&GUI.Button(r,"",GUIStyle.none)&&run.ChooseFateweaveReward(cards[i].id)){SaveService.Save(run);BeginCardAcquisition(shown,()=>screen=ScreenMode.Fateweave);}}return;}
            DrawPhysicalCardChoices(w,h,run.FateweaveEligibleCards().ToArray(),card=>{if(acquisitionActive)return;var fateId=run.pendingFateweaveId;if(run.ChooseFateweaveCard(card)){SaveService.Save(run);if(fateId=="severed_burden"){Sfx(SoundCue.PlayerHurt);if(run.pendingChoicesNeeded==0)screen=ScreenMode.Fateweave;return;}var shown=card.BuildDefinition();BeginModificationAcquisition(shown,()=>{if(run.pendingChoicesNeeded==0)screen=ScreenMode.Fateweave;});}});
        }
        private void DrawPhysicalCardChoices(float w,float h,RunCard[] choices,System.Action<RunCard> choose)
        {
            var bindingMode=screen==ScreenMode.BindingCard;var eligible=bindingMode?run.BindingEligibleCards().ToArray():choices;
            const int columns=6;const float gap=12f;var viewport=new Rect(34,PerfectedScreenOpen?182:bindingMode?172:150,w-(bindingMode?342:68),h-(PerfectedScreenOpen?320:bindingMode?242:220));var cardW=Mathf.Min(186f,(viewport.width-34-gap*(columns-1))/columns);var cardH=cardW*1.48f;var rowStep=cardH+17;var totalW=columns*cardW+(columns-1)*gap;var start=(viewport.width-totalW)*.5f;var rows=Mathf.CeilToInt(choices.Length/(float)columns);var contentHeight=Mathf.Max(viewport.height,rows*rowStep+8);if(controllerNavigation)KeepGridSelectionVisible(ref cardChoiceScroll,screenControllerIndex,columns,rowStep,viewport.height,contentHeight);UpdateScrollArea(viewport,ref cardChoiceScroll,contentHeight);RunCard selected=null;
            GUI.BeginGroup(viewport);
            for(var index=0;index<choices.Length;index++)
            {
                var saved=choices[index];var shown=saved.BuildDefinition();var r=new Rect(start+index%columns*(cardW+gap),index/columns*rowStep+4-cardChoiceScroll,cardW,cardH);if(r.yMax<0||r.y>viewport.height)continue;
                var allowed=eligible.Contains(saved);DrawCard(r,shown);if(!allowed)Fill(r,new Color(.008f,.012f,.02f,.55f));
                var root=new Rect(viewport.x+r.x,viewport.y+r.y,r.width,r.height);var hot=controllerNavigation?screenControllerIndex==index:root.Contains(PointerPosition);
                if(bindingMode&&hot){RegisterInspectionInput(root,shown);SetRunHudTooltip(root,shown.name,BindingCardReason(saved));}else RegisterCardKeywordHelp(root,shown,controllerNavigation&&screenControllerIndex==index);
                if(hot)Outline(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),allowed?Gold:Color.gray,2);
                if(GUI.Button(r,"",GUIStyle.none)&&allowed)selected=saved;
            }
            GUI.EndGroup();DrawScrollRail(viewport,cardChoiceScroll,contentHeight,(bindingMode?eligible.Length+" ELIGIBLE / ":"")+choices.Length+" PHYSICAL CARDS · FIRST ADDED FIRST");if(selected!=null)choose(selected);if(choices.Length==0)GUI.Label(new Rect(w*.25f,h*.4f,w*.5f,80),"No eligible unmodified card copies remain for this strand.",new GUIStyle(subtitleStyle){fontSize=20,wordWrap=true});
        }
        private void GrantRelic()
        {
            if(acquisitionActive||ShardDiscoveryOpen)return;
            if(run.treasureRelicClaimedReceipt==run.RoomReceipt){Advance();return;}
            var available=GameContent.Relics.Where(r=>r.rarity is not (Rarity.Boss or Rarity.Special)&&!run.relics.Contains(r.id)).ToArray();
            if(available.Length==0){run.treasureRelicClaimedReceipt=run.RoomReceipt;SaveService.Save(run);Advance();return;}
            var relic=available[(run.floor*7)%available.Length];run.treasureRelicClaimedReceipt=run.RoomReceipt;
            if(run.AcquireRelic(relic.id))profile.relicsCollected++;
            ProfileService.Save(profile);SaveService.Save(run);BeginRelicAcquisition(relic,Advance);
        }
        private void CompleteRun(){if(profile.RegisterCompletedRun(run.runId)){profile.wins++;if(profile.fastestVictory<=0||run.elapsedSeconds<profile.fastestVictory)profile.fastestVictory=run.elapsedSeconds;}runResultVictory=true;ProfileService.Save(profile);SaveService.Clear();screen=ScreenMode.RunResult;}

        private void DrawRunResult(float w,float h)
        {
            DrawFullBackdrop(runResultVictory?rewardBackground:combatBackground,w,h,runResultVictory ? .2f : .55f);var title=runResultVictory?"THE LOWER VAULT FALLS":"FATE SHATTERED";var subtitle=runResultVictory?"THE CROWN ANSWERS TO YOU":"THE VAULT REMEMBERS THIS ATTEMPT";Heading(w,title,subtitle);var time=System.TimeSpan.FromSeconds(run.elapsedSeconds).ToString(@"mm\:ss");var result=$"{run.hero.ToString().ToUpperInvariant()}\n\nACT REACHED   {run.act} / 3\nFLOOR REACHED   {Mathf.Min(run.ActFloorCount,run.floor+1)} / {run.ActFloorCount}\nFINAL GOLD   {run.gold}\nDECK SIZE   {run.deck.Count}\nRELICS   {run.relics.Count}\nFATEWEAVES   {run.fateweaveSelections.Count}\nRUN TIME   {time}";var style=new GUIStyle(titleStyle){fontSize=18,fontStyle=FontStyle.Normal,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.9f,.84f,.7f)}};GUI.Label(new Rect(w*.33f,h*.25f,w*.34f,h*.4f),result,style);var retry=new Rect(w*.5f-220,h*.72f,200,44);var menu=new Rect(w*.5f+20,h*.72f,200,44);DrawButtonFrame(retry,retry.Contains(PointerPosition)||controllerNavigation&&screenControllerIndex==0,false);DrawButtonFrame(menu,menu.Contains(PointerPosition)||controllerNavigation&&screenControllerIndex==1,false);if(GUI.Button(retry,"BEGIN NEW RUN",buttonStyle)){selectingNewRun=true;selectedHero=HeroId.Vanguard;heroSelectionTime=Time.unscaledTime;screen=ScreenMode.CharacterSelect;}if(GUI.Button(menu,"MAIN MENU",buttonStyle))screen=ScreenMode.Menu;
        }

        private void DrawCredits(float w,float h)
        {
            Heading(w,"CREDITS","GILDED FATE · THE LOWER VAULT");
            var style=new GUIStyle(titleStyle){fontSize=15,fontStyle=FontStyle.Normal,alignment=TextAnchor.UpperCenter,wordWrap=true,normal={textColor=new Color(.82f,.78f,.68f)}};
            var development="GAME DESIGN · SYSTEMS · VISUAL DIRECTION\nGILDED FATE DEVELOPMENT\n\nORIGINAL WORLDBUILDING\nThe Gilded Vault · Vanguard · Hexer · Reaper\n\nMADE WITH UNITY\n\nSOUND EFFECTS — CC0\nKenney · kenney.nl\nrubberduck · artisticdude · OpenGameArt.org\n\nRecordings edited, layered, faded and level-matched.\nThank you for entering the Vault.";
            var music="MUSIC BY SCOTT BUCKLEY\nReleased under CC-BY 4.0\nwww.scottbuckley.com.au\ncreativecommons.org/licenses/by/4.0\n\nMemories Of Stone · Passage Of Time\nLegionnaire (2022 Remaster) · Song Of The Forge\nAmberlight · Echoes Of Home\nMoonlight · Born Of The Sky\n\nPlayback levels and endpoint fades adapted for the game.\nFull sources and notices: AUDIO_CREDITS.txt\n\nVideos: include these music credits in your description.";
            GUI.Label(new Rect(w*.12f,h*.23f,w*.35f,h*.49f),development,style);
            GUI.Label(new Rect(w*.53f,h*.23f,w*.35f,h*.49f),music,style);
            var stats=new Rect(w*.5f-105,h*.79f,210,40);DrawButtonFrame(stats,stats.Contains(PointerPosition),false);
            if(GUI.Button(stats,"VIEW STATISTICS",buttonStyle))screen=ScreenMode.Statistics;BackButton(w,h);
        }

        private void DrawStatistics(float w,float h){Heading(w,"STATISTICS","THE VAULT KEEPS EVERY ACCOUNT");var fastest=profile.fastestVictory<=0?"—":System.TimeSpan.FromSeconds(profile.fastestVictory).ToString(@"mm\:ss");var lines=$"RUNS PLAYED   {profile.runsPlayed}\nWINS   {profile.wins}\nLOSSES   {profile.losses}\nHIGHEST DAMAGE   {profile.highestDamage}\nMOST BLOCK   {profile.mostBlock}\nCARDS PLAYED   {profile.cardsPlayed}\nENEMIES DEFEATED   {profile.enemiesDefeated}\nELITES DEFEATED   {profile.elitesDefeated}\nBOSSES DEFEATED   {profile.bossesDefeated}\nGOLD COLLECTED   {profile.goldCollected}\nRELICS COLLECTED   {profile.relicsCollected}\nFASTEST VICTORY   {fastest}";var s=new GUIStyle(titleStyle){fontSize=17,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.82f,.77f,.66f)}};GUI.Label(new Rect(w*.25f,h*.2f,w*.5f,h*.68f),lines,s);BackButton(w,h);}
        private void ApplySettings(){if(profile==null)return;Application.targetFrameRate=profile.fpsLimit;QualitySettings.vSyncCount=profile.vSync?1:0;QualitySettings.globalTextureMipmapLimit=Mathf.Clamp(profile.textureQuality,0,2);QualitySettings.antiAliasing=profile.antiAliasing;if(Screen.fullScreen!=profile.fullscreen)Screen.fullScreenMode=profile.fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;}
        private void Choice(Rect r,string name,string text,System.Action action,int controllerIndex=-1){var over=ScreenChoiceHot(r,controllerIndex);if(over)Fill(new Rect(r.x-7,r.y-7,r.width+14,r.height+14),new Color(1f,.56f,.12f,.11f));Fill(r,over?new Color(.085f,.06f,.025f,.98f):new Color(.018f,.026f,.04f,.98f));Outline(r,over?new Color(1f,.84f,.55f):new Color(.73f,.61f,.39f),over?3:2);Outline(new Rect(r.x+7,r.y+7,r.width-14,r.height-14),new Color(.26f,.2f,.11f,.8f),1);Fill(new Rect(r.x+18,r.y+7,r.width-36,2),new Color(.9f,.78f,.53f,.5f));var n=new GUIStyle(titleStyle){fontSize=24,wordWrap=true,normal={textColor=new Color(.98f,.93f,.78f)}};GUI.Label(new Rect(r.x+16,r.y+18,r.width-32,58),name,n);var b=new GUIStyle(footerStyle){fontSize=17,wordWrap=true,normal={textColor=new Color(.94f,.91f,.84f)}};GUI.Label(new Rect(r.x+22,r.y+82,r.width-44,r.height-98),text,b);if(GUI.Button(r,"",GUIStyle.none)){Sfx(SoundCue.UiConfirm);action();}}

        private void Advance(){if(ShardDiscoveryOpen)return;if(run.pendingBonusCardRewards>0){run.pendingBonusCardRewards--;run.NextBonusCardReward();rewardRevealTime=profile.reduceMotion?0:1.15f;run.stage=RunStage.CardReward;SaveService.Save(run);screen=ScreenMode.Reward;return;}if(currentNode?.kind==NodeKind.Boss){run.RollEncounterRewards(NodeKind.Boss);run.encounterRewards.cardClaimed=true;if(!run.encounterRewards.bossRelicClaimed&&run.BossRelicOffers().Length>0){run.stage=RunStage.RelicReward;screen=ScreenMode.RelicReward;screenControllerIndex=0;SaveService.Save(run);return;}if(run.perfectedSelectionPending)return;EnterFateweave();return;}if(currentNode!=null)run.AdvanceFrom(currentNode);SaveService.Save(run);mapFocusFloor=-1;screen=run.floor>=run.ActFloorCount?ScreenMode.Menu:ScreenMode.Map;}
        private void HealthBar(Rect r,int hp,int max,string label){Fill(new Rect(r.x-3,r.y-3,r.width+6,r.height+6),new Color(0,0,0,.7f));Fill(r,new Color(.075f,.018f,.025f));var fill=new Rect(r.x+2,r.y+2,(r.width-4)*Mathf.Clamp01(hp/(float)max),r.height-4);Fill(fill,new Color(.68f,.08f,.105f));Fill(new Rect(fill.x,fill.y,fill.width,Mathf.Max(2,fill.height*.18f)),new Color(1f,.38f,.28f,.58f));Outline(r,new Color(.86f,.62f,.24f),2);var s=new GUIStyle(footerStyle){fontSize=Mathf.Clamp(Mathf.RoundToInt(r.height*.45f),12,15),fontStyle=FontStyle.Bold,normal={textColor=Color.white}};GUI.Label(r,$"{label}   {Mathf.Max(0,hp)}/{max}",s);}
        private static bool IsRunScreen(ScreenMode mode)=>mode is ScreenMode.Map or ScreenMode.Combat or ScreenMode.Reward or ScreenMode.Merchant or ScreenMode.Sanctuary or ScreenMode.Event or ScreenMode.EventSelection or ScreenMode.EventResult or ScreenMode.Treasure or ScreenMode.RelicReward or ScreenMode.CardUpgrade or ScreenMode.CardRemove or ScreenMode.BindingSelect or ScreenMode.BindingCard or ScreenMode.Fateweave or ScreenMode.FateweaveCard;
        private bool ShowsPersistentRunHud=>!string.IsNullOrEmpty(run.runId)&&!run.closed&&(IsRunScreen(screen)||screen==ScreenMode.Collection&&viewingRunDeck||screen==ScreenMode.Settings&&(IsRunScreen(settingsReturnScreen)||settingsReturnScreen==ScreenMode.Collection&&viewingRunDeck));
        private void Heading(float w,string title,string sub){var top=ShowsPersistentRunHud?68f:30f;titleStyle.fontSize=38;titleStyle.normal.textColor=new Color(.98f,.92f,.76f);ShadowLabel(new Rect(w*.16f,top,w*.68f,59),title,titleStyle);var lineW=Mathf.Min(440f,w*.38f);Fill(new Rect(w*.5f-lineW*.5f,top+61,lineW,1),new Color(.76f,.65f,.43f,.65f));Fill(new Rect(w*.5f-3,top+58,6,7),new Color(.97f,.88f,.64f,.85f));subtitleStyle.fontSize=17;subtitleStyle.normal.textColor=new Color(.91f,.87f,.77f);GUI.Label(new Rect(w*.16f,top+67,w*.68f,28),sub,subtitleStyle);}
        private void BackButton(float w,float h){var r=new Rect(34,h-76,148,46);DrawButtonFrame(r,r.Contains(PointerPosition),false);if(GUI.Button(r,"BACK",buttonStyle)){Sfx(SoundCue.UiBack);if(screen==ScreenMode.Settings){ProfileService.Save(profile);screen=settingsReturnScreen;return;}if(screen==ScreenMode.Collection){ReturnFromCollection();return;}screen=ScreenMode.Menu;}}
        private void CopyRun(RunModel source)
        {
            if(source==null)return;run.perfectedSelectionPending=source.perfectedSelectionPending;run.perfectedSelectionStep=source.perfectedSelectionStep;run.CopyRewardState(source);run.seed=source.seed;run.runId=source.runId;run.saveFormat=source.saveFormat;run.closed=source.closed;run.hasCombatCheckpoint=source.hasCombatCheckpoint;run.combatCheckpoint=source.combatCheckpoint;run.immortalThreadUsed=source.immortalThreadUsed;mapFocusFloor=-1;
            run.hero=source.hero;run.act=source.act;run.floor=source.floor;run.gold=source.gold;run.hp=source.hp;run.maxHp=source.maxHp;run.nextCardSerial=source.nextCardSerial;run.pendingCombatGold=source.pendingCombatGold;run.pendingBonusCardRewards=source.pendingBonusCardRewards;run.combatGoldClaimed=source.combatGoldClaimed||source.pendingCombatGold<=0;run.elapsedSeconds=source.elapsedSeconds;run.beggarFavor=source.beggarFavor;run.stage=source.stage;run.activeNodeFloor=source.activeNodeFloor;run.activeNodeLane=source.activeNodeLane;run.activeEnemyId=source.activeEnemyId??"";run.activeEventId=source.activeEventId??"";run.merchantRemoved=source.merchantRemoved;run.merchantHealed=source.merchantHealed;
            run.cards=source.cards??new List<RunCard>();run.shards=source.shards??new List<FateShardState>();run.fateweaveSelections=source.fateweaveSelections??new List<string>();run.temporaryMultiCombatStatuses=source.temporaryMultiCombatStatuses??new List<string>();run.temporaryEventEffects=source.temporaryEventEffects??new List<TemporaryEventEffect>();run.seenEventIds=source.seenEventIds??new List<string>();run.pendingEventOfferIds=source.pendingEventOfferIds??new List<string>();run.pendingEventSelectionIds=source.pendingEventSelectionIds??new List<string>();run.pendingEventShardDecisions=source.pendingEventShardDecisions??new List<string>();run.eventSelectionKind=source.eventSelectionKind;run.pendingEventChoiceId=source.pendingEventChoiceId??"";run.pendingEventBindingId=source.pendingEventBindingId??"";run.pendingEventResult=source.pendingEventResult??"";run.pendingEventChoicesNeeded=source.pendingEventChoicesNeeded;run.fateweaveOffers=source.fateweaveOffers??new List<string>();run.bindingOffers=source.bindingOffers??new List<string>();run.pendingCardOfferIds=source.pendingCardOfferIds??new List<string>();run.pendingSelectedCardIds=source.pendingSelectedCardIds??new List<string>();run.pendingFateweaveId=source.pendingFateweaveId??"";run.pendingBindingId=source.pendingBindingId??"";run.pendingChoicesNeeded=source.pendingChoicesNeeded;run.deck=source.deck??new List<string>();run.upgradedCards=source.upgradedCards??new List<string>();run.relics=source.relics??new List<string>();run.consumables=source.consumables??new List<string>();run.merchantSold=source.merchantSold??new List<string>();run.nodes=source.nodes??new List<MapNode>();run.EnsureEventState();run.EnsureCardInstances();RepairRunMap();
            observedGold=run.gold;ResetGoldAudio();
        }
        private void RepairRunMap()
        {
            if(run.HasValidMap())return;
            var oldLane=run.activeNodeLane;var replacement=new RunModel();replacement.NewRun(run.hero,run.seed==0?20260903:run.seed);run.nodes=replacement.nodes;
            foreach(var node in run.nodes){node.complete=false;node.available=node.floor==0;}
            for(var floorIndex=0;floorIndex<Mathf.Clamp(run.floor,0,run.ActFloorCount);floorIndex++)
            {
                var reachable=run.nodes.Where(n=>n.floor==floorIndex&&n.available).OrderBy(n=>Mathf.Abs(n.lane-RunModel.LaneCount/2f)).ToArray();if(reachable.Length==0)break;
                var chosen=reachable[0];chosen.complete=true;
                foreach(var next in run.nodes.Where(n=>n.floor==floorIndex+1))next.available=(chosen.nextMask&(1<<next.lane))!=0;
            }
            if(run.stage!=RunStage.Map&&run.activeNodeFloor==run.floor)
            {
                var mapped=Mathf.Clamp(oldLane*2,0,RunModel.LaneCount-1);var active=run.nodes.Where(n=>n.floor==run.floor&&n.available).OrderBy(n=>Mathf.Abs(n.lane-mapped)).FirstOrDefault();
                if(active!=null){run.activeNodeFloor=active.floor;run.activeNodeLane=active.lane;}
            }
        }
        private void RestoreRunStage()
        {
            currentNode=run.nodes.FirstOrDefault(n=>n.floor==run.activeNodeFloor&&n.lane==run.activeNodeLane);if(run.stage==RunStage.Map||currentNode==null){run.stage=RunStage.Map;screen=ScreenMode.Map;return;}
            if(run.stage==RunStage.Combat){var enemy=WorldContent.Enemies.FirstOrDefault(e=>e.id==run.activeEnemyId);if(enemy==null){run.stage=RunStage.Map;screen=ScreenMode.Map;}else if(run.hasCombatCheckpoint&&run.combatCheckpoint!=null&&run.combatCheckpoint.TryRestore(out var restored,out _)){currentEnemy=enemy;combat=restored;screen=ScreenMode.Combat;RestoreCombatPresentation();}else BeginCombat(enemy,enemy.boss?2:enemy.elite?1:0,enemy.boss&&!profile.reduceMotion?1.4f:0);return;}
            if(run.stage==RunStage.Merchant||run.stage==RunStage.CardRemove){merchantSold.Clear();foreach(var id in run.merchantSold)merchantSold.Add(id);merchantRemoved=run.merchantRemoved;merchantHealed=run.merchantHealed;screen=run.stage==RunStage.CardRemove?ScreenMode.CardRemove:ScreenMode.Merchant;return;}
            if(run.stage==RunStage.Sanctuary){screen=ScreenMode.Sanctuary;return;}if(run.stage==RunStage.CardUpgrade){cardServiceReturnScreen=currentNode.kind==NodeKind.Event?ScreenMode.Event:ScreenMode.Sanctuary;screen=ScreenMode.CardUpgrade;return;}if(run.stage==RunStage.BindingSelect){screen=ScreenMode.BindingSelect;return;}if(run.stage==RunStage.BindingCard){screen=ScreenMode.BindingCard;return;}if(run.stage==RunStage.Fateweave){screen=ScreenMode.Fateweave;return;}if(run.stage==RunStage.FateweaveCard){screen=ScreenMode.FateweaveCard;return;}
            if(run.stage is RunStage.Event or RunStage.EventSelection or RunStage.EventResult){currentEvent=WorldContent.Events.FirstOrDefault(e=>e.id==run.activeEventId)??EventSystem.SelectEvent(run);screen=run.stage==RunStage.EventSelection?ScreenMode.EventSelection:run.stage==RunStage.EventResult?ScreenMode.EventResult:ScreenMode.Event;return;}if(run.stage==RunStage.Treasure){screen=ScreenMode.Treasure;return;}if(run.stage==RunStage.CardReward){rewardRevealTime=profile.reduceMotion?0:1.15f;screen=ScreenMode.Reward;return;}if(run.stage==RunStage.RelicReward){rewardRevealTime=profile.reduceMotion?0:1.15f;screen=ScreenMode.RelicReward;return;}screen=ScreenMode.Map;
        }

        private void ShadowLabel(Rect rect, string text, GUIStyle style)
        {
            var color = style.normal.textColor;
            style.normal.textColor = new Color(0, 0, 0, .75f);
            GUI.Label(new Rect(rect.x + 3, rect.y + 4, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = previous;
        }

        private void Outline(Rect r, Color color, int width)
        {
            Fill(new Rect(r.x, r.y, r.width, width), color);
            Fill(new Rect(r.x, r.yMax - width, r.width, width), color);
            Fill(new Rect(r.x, r.y, width, r.height), color);
            Fill(new Rect(r.xMax - width, r.y, width, r.height), color);
        }
        private static void DrawAtlasIcon(Texture2D atlas,int index,int columns,int rows,Rect destination){if(!atlas||index<0)return;var col=index%columns;var row=index/columns;var uv=new Rect(col/(float)columns,1f-(row+1)/(float)rows,1f/columns,1f/rows);GUI.DrawTextureWithTexCoords(destination,atlas,uv,true);}
        private void DrawFullBackdrop(Texture2D texture,float w,float h,float veil){if(texture)GUI.DrawTexture(new Rect(0,0,w,h),texture,ScaleMode.ScaleAndCrop);Fill(new Rect(0,0,w,h),new Color(.004f,.007f,.014f,veil));}
        private void DrawLocationBackdrop(float w,float h,int index){DrawAtlasIcon(locationAtlas,index,2,2,new Rect(0,0,w,h));Fill(new Rect(0,0,w,h),new Color(.005f,.008f,.015f,.24f));}
private void DrawTransition(float w,float h){if(ShowsPersistentRunHud){if(ShowsNormalShardShrine)DrawShardShrine(w,h);if(ShowsNormalShardShrine||acquisitionActive)DrawShardFlights();}if(ShardDiscoveryOpen){GUI.enabled=!acquisitionActive;DrawShardDiscovery(w,h);}for(var i=0;i<6;i++){var a=.075f*(1f-i/6f);var edge=12f+i*16f;Fill(new Rect(i*16,0,16,h),new Color(0,0,0,a));Fill(new Rect(w-edge,0,16,h),new Color(0,0,0,a));Fill(new Rect(0,i*12,w,12),new Color(0,0,0,a*.7f));Fill(new Rect(0,h-(i+1)*12,w,12),new Color(0,0,0,a));}Outline(new Rect(1,1,w-2,h-2),new Color(.62f,.41f,.14f,.22f),2);if(transitionAlpha>0)Fill(new Rect(0,0,w,h),new Color(.005f,.008f,.015f,transitionAlpha));if(ShowsPersistentRunHud)DrawPersistentRunTooltip(w,h);DrawScreenCardKeywordHelp(w,h);DrawAcquisitionPresentation(w,h);DrawGoldCollectFlight();DrawSeveredThread(w,h);DrawCombatHudFocus(w,h);DrawMenuHudNavigation(w,h);DrawScreenNavigationHint(w,h);if(inspectedCard!=null)DrawCardInspection(w,h,inspectedCard);if(inspectedRelic!=null&&!ShardDiscoveryOpen&&!acquisitionActive)DrawRelicInspection(w,h,inspectedRelic);DrawFinalPolishProbe(w,h);}
        private MusicMood MoodForScreen(){var page=AudioContextScreen();if(page==ScreenMode.Combat)return currentEnemy!=null&&currentEnemy.boss?MusicMood.Boss:MusicMood.Combat;if(page==ScreenMode.Map)return MusicMood.Map;if(page==ScreenMode.Sanctuary||page is ScreenMode.BindingSelect or ScreenMode.BindingCard)return MusicMood.Sanctuary;if(page==ScreenMode.Merchant)return MusicMood.Merchant;if(page is ScreenMode.Event or ScreenMode.EventSelection or ScreenMode.EventResult||page==ScreenMode.Treasure)return MusicMood.Event;if(page is ScreenMode.Reward or ScreenMode.RelicReward or ScreenMode.Fateweave or ScreenMode.FateweaveCard||page==ScreenMode.RunResult&&runResultVictory)return MusicMood.Victory;return MusicMood.Menu;}


        private struct Particle { public Vector2 position; public float speed; public float size; public float phase; }
        private enum ScreenMode { Menu, CharacterSelect, Map, Combat, Reward, Collection, Merchant, Sanctuary, Event, EventSelection, EventResult, Treasure, RelicReward, Statistics, Settings, CardUpgrade, CardRemove, BindingSelect, BindingCard, Fateweave, FateweaveCard, Credits, RunResult }
    }
}
