using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GildedFate.Core;
using GildedFate.Map;
using UnityEngine;

namespace GildedFate.UI
{
    // Events Rework: the event stage.
    // Layout: the event's art as a dark atmospheric backdrop; a gilded scene panel on the left
    // (art, title, prompt); a "Choose Your Fate" panel on the right whose option cards each read as
    // one plain sentence ("Spend 25 Gold. Gain a random common relic."). Risky options add an omen
    // bar, mystery options a veil and a sealed-fate emblem, locked options a lock with the reason.
    public sealed partial class GildedMainMenu
    {
        private const float EventTop=112,EventMargin=34;

        private void DrawEventScreen(float w,float h)
        {
            DrawEventAtmosphere(w,h);DrawRunHud(w);
            var now=Time.unscaledTime;
            // ---------- scene panel ----------
            var scene=new Rect(EventMargin,EventTop,Mathf.Round(w*.43f),h-EventTop-EventMargin);
            Fill(new Rect(scene.x+8,scene.y+10,scene.width,scene.height),new Color(0,0,0,.45f));
            Fill(scene,new Color(.012f,.013f,.02f,.94f));
            var storyText=EventSceneStory(currentEvent);
            var promptNeed=new GUIStyle(footerStyle){fontSize=18,wordWrap=true}.CalcHeight(new GUIContent(storyText),scene.width-60);
            var artHeight=Mathf.Clamp(scene.height-(28+64+promptNeed+64),scene.height*.46f,scene.height*.70f); // a short prompt hands its space to the art
            var art=new Rect(scene.x+14,scene.y+14,scene.width-28,Mathf.Round(artHeight));
            DrawEventArtwork(art);
            for(var band=0;band<10;band++){var k=band/9f;Fill(new Rect(art.x,art.yMax-art.height*.32f+band*art.height*.032f,art.width,art.height*.033f+1),new Color(.012f,.013f,.02f,k*k*.92f));}
            DrawGildedFrame(scene,1f);
            var titleY=art.yMax+14;var steps=currentEvent?.steps??1;
            var titleRect=new Rect(scene.x+30,titleY,scene.width-60-(steps>1?150:0),44);
            var titleStyle2=SingleLineStyle(currentEvent.name,titleRect.width,titleStyle,31,18);titleStyle2.alignment=TextAnchor.MiddleLeft;titleStyle2.normal.textColor=new Color(1f,.9f,.66f);
            GUI.Label(new Rect(titleRect.x+2,titleRect.y+2,titleRect.width,titleRect.height),currentEvent.name,new GUIStyle(titleStyle2){normal={textColor=new Color(0,0,0,.6f)}});
            GUI.Label(titleRect,currentEvent.name,titleStyle2);
            if(steps>1)DrawEventStepPipsAt(new Vector2(scene.xMax-34,titleRect.center.y),steps);
            var divY=titleRect.yMax+6;DrawOrnamentRule(new Vector2(scene.x+30,divY),scene.width-60,new Color(1f,.78f,.4f,.55f));
            var promptRect=new Rect(scene.x+30,divY+16,scene.width-60,scene.yMax-(divY+16)-44);
            var promptStyle=new GUIStyle(footerStyle){fontSize=18,alignment=TextAnchor.UpperLeft,wordWrap=true,normal={textColor=new Color(.96f,.93f,.86f)}};
            while(promptStyle.fontSize>13&&promptStyle.CalcHeight(new GUIContent(storyText),promptRect.width)>promptRect.height)promptStyle.fontSize--;
            GUI.Label(promptRect,TypedEventText(storyText),promptStyle);
            GUI.Label(new Rect(scene.x+30,scene.yMax-34,scene.width-60,18),(currentEvent.ambientCue??"vault").ToUpperInvariant()+"  ·  A VAULT ENCOUNTER",new GUIStyle(ReadableStyle(11,true)){alignment=TextAnchor.MiddleLeft,normal={textColor=new Color(.78f,.62f,.36f,.85f)}});
            StoryEndEventPolish(scene);

            // ---------- choose-your-fate panel ----------
            var col=new Rect(scene.xMax+26,EventTop,w-scene.xMax-26-EventMargin,scene.height);
            Fill(new Rect(col.x+8,col.y+10,col.width,col.height),new Color(0,0,0,.4f));
            Fill(col,new Color(.008f,.01f,.016f,.80f));
            for(var band=0;band<8;band++)Fill(new Rect(col.x,col.y+band*24,col.width,24),new Color(1f,.75f,.35f,.028f*(1-band/8f)));
            DrawGildedFrame(col,.85f);
            DrawPanelHeader(new Rect(col.x,col.y+12,col.width,26),"CHOOSE YOUR FATE");
            var choices=EventSystem.CurrentChoices(run,currentEvent).ToArray();
            var banked=run.eventBank!=null&&run.eventBank.Count>0;
            var inner=new Rect(col.x+20,col.y+54,col.width-40,col.height-54-20-(banked?62:0));
            const float gap=14;var n=Mathf.Max(1,choices.Length);
            var heights=choices.Select(c=>EventCardHeight(c,inner.width)).ToArray();var total=heights.Sum()+gap*(n-1);
            var squeeze=total>inner.height?(inner.height-gap*(n-1))/Mathf.Max(1,heights.Sum()):1f;total=heights.Sum()*squeeze+gap*(n-1);
            var cardY=inner.y+Mathf.Max(0,(inner.height-total)*.5f);
            for(var i=0;i<choices.Length;i++){var ch=heights[i]*squeeze;DrawEventChoiceCard(new Rect(inner.x,cardY,inner.width,ch),choices[i],i,now);cardY+=ch+gap;}
            if(banked)DrawEventBankTray(new Rect(col.x+20,col.yMax-20-50,col.width-40,50));
            DrawPersistentRunTooltip(w,h);
        }

        // ---------- atmosphere ----------
        private void DrawEventAtmosphere(float w,float h)
        {
            DrawEventBackdrop(w,h);
            // The event's own art, huge and dim, sets the room's color.
            var old=GUI.color;GUI.color=new Color(1,1,1,.28f);
            var drift=profile.reduceMotion?0:Mathf.Sin(Time.unscaledTime*.05f)*12;
            DrawEventArtwork(new Rect(-w*.06f+drift,-h*.08f,w*1.12f,h*1.16f));GUI.color=old;
            Fill(new Rect(0,0,w,h),new Color(.004f,.005f,.012f,.62f));
            ShardVignette(w,h,new Color(0,0,0,.85f));
            var now=Time.unscaledTime;var motion=!profile.reduceMotion;
            // Light falls from the upper left.
            if(!profile.reducedVfx)for(var i=0;i<6;i++)
            {
                var x=w*(.06f+i*.11f)+(motion?Mathf.Sin(now*.15f+i)*18:0);
                DrawLine(new Vector2(x,-40),new Vector2(x+h*.55f,h+40),new Color(1f,.82f,.5f,.018f+(i%2)*.012f),26+i*6);
            }
            // Embers and dust drift up through the room.
            if(motion&&!profile.reducedVfx)for(var i=0;i<26;i++)
            {
                var k=Mathf.Repeat(now*(.025f+CardVfxHash(i,3)*.03f)+CardVfxHash(i,5),1f);
                var p=new Vector2(w*CardVfxHash(i,7)+Mathf.Sin(now*.4f+i)*20,h*(1.05f-k*1.1f));
                var col=i%4==0?new Color(1f,.6f,.25f):new Color(1f,.88f,.65f);
                ShardSoft(p,3+CardVfxHash(i,9)*5,new Color(col.r,col.g,col.b,.35f*Mathf.Sin(k*Mathf.PI)));
            }
            // Low fog along the floor.
            for(var i=0;i<5;i++)ShardSoft(new Vector2(w*(.1f+i*.2f)+(motion?Mathf.Sin(now*.08f+i)*40:0),h+30),h*.7f,new Color(.35f,.25f,.45f,.07f));
        }

        // ---------- frame pieces ----------
        private void DrawGildedFrame(Rect r,float strength)
        {
            var gold=new Color(.86f,.66f,.32f,.85f*strength);var faint=new Color(gold.r,gold.g,gold.b,.22f*strength);
            Outline(r,gold,1);Outline(new Rect(r.x+5,r.y+5,r.width-10,r.height-10),faint,1);
            const float arm=30;
            foreach(var (corner,sx,sy) in new[]{(new Vector2(r.x,r.y),1f,1f),(new Vector2(r.xMax,r.y),-1f,1f),(new Vector2(r.x,r.yMax),1f,-1f),(new Vector2(r.xMax,r.yMax),-1f,-1f)})
            {
                Fill(new Rect(sx>0?corner.x-1:corner.x-arm+1,sy>0?corner.y-1:corner.y-2,arm,3),gold);
                Fill(new Rect(sx>0?corner.x-1:corner.x-2,sy>0?corner.y-1:corner.y-arm+1,3,arm),gold);
                DrawLine(corner+new Vector2(sx*9,sy*9),corner+new Vector2(sx*18,sy*18),gold,1.4f);
                EventGem(corner,4.5f,new Color(1f,.84f,.5f,strength),true);
            }
            var crest=new Vector2(r.center.x,r.y);
            if(CardVfxRepaint&&!profile.reduceFlashing)ShardSoft(crest,60,new Color(1f,.75f,.35f,.18f*strength));
            EventGem(crest,9,new Color(1f,.84f,.5f,strength),false);EventGem(crest,4,new Color(1f,.9f,.6f,strength),true);
            DrawLine(crest+new Vector2(-60,0),crest+new Vector2(-14,0),gold,1.6f);DrawLine(crest+new Vector2(14,0),crest+new Vector2(60,0),gold,1.6f);
        }
        private void DrawOrnamentRule(Vector2 from,float width,Color color)
        {
            var mid=from+new Vector2(Mathf.Min(140,width*.35f),0);
            DrawLine(from,mid-new Vector2(10,0),color,1.4f);EventGem(mid,5,color,false);Fill(new Rect(mid.x-1.5f,mid.y-1.5f,3,3),color);
            DrawLine(mid+new Vector2(10,0),from+new Vector2(width,0),new Color(color.r,color.g,color.b,color.a*.35f),1);
        }
        private void DrawPanelHeader(Rect r,string text)
        {
            var style=new GUIStyle(ReadableStyle(13,true)){alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.84f,.5f)}};
            var half=style.CalcSize(new GUIContent(text)).x*.5f+18;var c=r.center;var gold=new Color(.86f,.66f,.32f,.7f);
            GUI.Label(r,text,style);
            DrawLine(new Vector2(r.x+34,c.y),new Vector2(c.x-half,c.y),gold,1.2f);DrawLine(new Vector2(c.x+half,c.y),new Vector2(r.xMax-34,c.y),gold,1.2f);
            EventGem(new Vector2(c.x-half+4,c.y),3.5f,gold,true);EventGem(new Vector2(c.x+half-4,c.y),3.5f,gold,true);
        }
        private void DrawEventStepPipsAt(Vector2 right,int steps)
        {
            var step=Mathf.Clamp(EventSystem.CurrentStep(run,currentEvent),1,steps);
            for(var i=0;i<steps;i++)
            {
                var c=new Vector2(right.x-(steps-1-i)*20,right.y);var done=i<step;var current=i==step-1;
                var s=current?7.5f:5.5f;var colr=done?Gold:new Color(.55f,.45f,.3f,.7f);
                EventGem(c,s,colr,false);if(done)Fill(new Rect(c.x-s*.35f,c.y-s*.35f,s*.7f,s*.7f),colr);
                if(current&&CardVfxRepaint&&!profile.reduceFlashing)ShardSoft(c,30,new Color(1f,.8f,.4f,.22f+Mathf.Sin(shimmer*3)*.08f));
            }
            GUI.Label(new Rect(right.x-(steps-1)*20-128,right.y+10,140,16),"STEP "+step+" OF "+steps,new GUIStyle(ReadableStyle(10,true)){alignment=TextAnchor.MiddleRight,normal={textColor=new Color(.85f,.69f,.40f)}});
        }

        // ---------- option card ----------
        // A card is as tall as its content: title, flavor, the sentence, and the odds or lock row.
        private float EventCardHeight(EventChoiceDef choice,float width)
        {
            var tw=width-66-22-136;var sentence=StripRich(EventSentence(choice));
            var sentenceHeight=new GUIStyle(footerStyle){fontSize=18,wordWrap=true}.CalcHeight(new GUIContent(sentence),tw);
            var hasFlavor=!string.IsNullOrEmpty(choice.flavor)||!string.IsNullOrEmpty(EventChoiceAction(choice));
            var bottomRow=choice.chance>0||!EventSystem.Availability(run,choice).available;
            return Mathf.Clamp(44+(hasFlavor?24:0)+sentenceHeight+(bottomRow?40:18),96,176);
        }
        private void DrawEventChoiceCard(Rect rect,EventChoiceDef choice,int index,float now)
        {
            var availability=EventSystem.Availability(run,choice);var open=availability.available;
            var hot=controllerNavigation?screenControllerIndex==index:rect.Contains(PointerPosition);var mystery=IsMysteryChoice(choice);
            var appear=profile.reduceMotion?1f:ShardEase(Mathf.Clamp01((now-eventStoryShownAt-.12f-index*.07f)/.38f));
            var r=rect;r.x+=(1-appear)*56;if(hot&&open&&!profile.reduceMotion)r.y-=3;
            Fill(new Rect(r.x+4,r.y+6,r.width,r.height),new Color(0,0,0,.35f*appear));
            if(hot&&open&&CardVfxRepaint&&!profile.reduceFlashing)ShardSoft(new Rect(r.x-40,r.y-30,r.width+80,r.height+60),new Color(1f,.74f,.32f,.12f));
            Fill(r,open?(hot?new Color(.05f,.04f,.028f,.97f):new Color(.017f,.019f,.03f,.95f)):new Color(.015f,.015f,.017f,.92f));
            for(var b=0;b<6;b++)Fill(new Rect(r.x,r.y+r.height*b/6f,r.width,r.height/6f+1),new Color(1f,.8f,.45f,(hot?.04f:.018f)*(1-b/6f)));
            if(mystery)DrawMysteryVeil(r,hot,open,index*97+(choice.id??"").Length*13);
            var edge=open?(hot?Gold:mystery?new Color(.74f,.58f,.86f,.85f):new Color(.62f,.47f,.24f,.85f)):new Color(.34f,.33f,.31f,.8f);
            Outline(r,edge,hot?2:1);Outline(new Rect(r.x+4,r.y+4,r.width-8,r.height-8),new Color(edge.r,edge.g,edge.b,.2f),1);
            Fill(new Rect(r.x,r.y+12,3,r.height-24),edge);
            foreach(var corner in new[]{new Vector2(r.x,r.y),new Vector2(r.xMax,r.y),new Vector2(r.x,r.yMax),new Vector2(r.xMax,r.yMax)})EventGem(corner,3.5f,edge,true);
            if(hot&&open&&CardVfxRepaint&&!profile.reduceMotion){var sx=r.x+20+Mathf.Repeat(now*.55f,1f)*(r.width-40);ShardSoft(new Rect(sx-80,r.y-5,160,10),new Color(1f,.9f,.62f,.85f));} // light running along the top edge
            // Medallion.
            var m=new Vector2(r.x+34,r.y+31);
            if(CardVfxRepaint)ShardSoft(m,hot?72:46,new Color(1f,.76f,.36f,hot?.22f:.12f));
            EventGem(m,17,edge,false);EventGem(m,11,new Color(edge.r,edge.g,edge.b,.35f),false);
            GUI.Label(new Rect(m.x-17,m.y-13,34,26),(index+1).ToString(),new GUIStyle(titleStyle){fontSize=18,alignment=TextAnchor.MiddleCenter,normal={textColor=open?(hot?new Color(1f,.95f,.78f):new Color(.92f,.78f,.5f)):Color.gray}});
            // Text column.
            const float reserve=136f;var tx=r.x+66;var tw=r.width-66-22-reserve; // the right side always holds an emblem
            var titleArea=new Rect(tx,r.y+12,tw,30);var title=SingleLineStyle(choice.title,titleArea.width,titleStyle,21,14);title.alignment=TextAnchor.MiddleLeft;
            title.normal.textColor=open?new Color(.99f,.91f,.7f):new Color(.6f,.59f,.56f);GUI.Label(titleArea,choice.title,title);
            var y=r.y+44;var compact=r.height<92;
            var flavor=!string.IsNullOrEmpty(choice.flavor)?choice.flavor:EventChoiceAction(choice);
            if(!string.IsNullOrEmpty(flavor)&&!compact)
            {
                GUI.Label(new Rect(tx,y,tw,20),flavor,new GUIStyle(footerStyle){fontSize=14,fontStyle=FontStyle.Italic,alignment=TextAnchor.MiddleLeft,clipping=TextClipping.Clip,normal={textColor=open?new Color(.72f,.70f,.68f):new Color(.48f,.48f,.46f)}});
                y+=24;
            }
            var bottom=choice.chance>0||!open?40f:12f;
            var sentenceRect=new Rect(tx,y,tw,Mathf.Max(22,r.yMax-bottom-y));
            var sentence=EventSentence(choice);var sentenceStyle=FittedEventStyle(StripRich(sentence),sentenceRect,footerStyle,18,13);
            sentenceStyle.richText=true;sentenceStyle.normal.textColor=open?new Color(.97f,.94f,.87f):new Color(.62f,.61f,.58f);
            GUI.Label(sentenceRect,open?sentence:StripRich(sentence),sentenceStyle);
            if(!open)DrawLockedReason(new Rect(tx,r.yMax-34,tw,24),availability.reason);
            else if(choice.chance>0)DrawOmenBar(new Rect(tx,r.yMax-36,Mathf.Min(320,tw),26),choice.chance/100f,choice.chanceGood,choice.chanceLabel,true);
            var emblemCenter=new Vector2(r.xMax-78,r.center.y-6);var emblemSize=Mathf.Clamp(r.height*.2f,20,28);
            if(mystery)DrawFateEmblem(emblemCenter,emblemSize,hot,open,choice.chance>0);
            else DrawRewardEmblem(emblemCenter,emblemSize,choice,hot,open);
            var enabled=GUI.enabled;GUI.enabled=enabled&&open&&!acquisitionActive;
            if(GUI.Button(rect,"",GUIStyle.none))BeginEventChoice(choice);
            GUI.enabled=enabled;DeniedPress(rect,!open&&!acquisitionActive);
        }
        private void DrawLockedReason(Rect r,string reason)
        {
            var c=new Vector2(r.x+9,r.center.y+2);var red=new Color(1f,.55f,.45f);
            Outline(new Rect(c.x-6,c.y-3,12,9),red,2);
            for(var i=0;i<=6;i++){var a=Mathf.PI*(1+i/6f);var p0=c+new Vector2(Mathf.Cos(a)*4,-3+Mathf.Sin(a)*5);var a1=Mathf.PI*(1+(i+1)/6f);if(i<6)DrawLine(p0,c+new Vector2(Mathf.Cos(a1)*4,-3+Mathf.Sin(a1)*5),red,1.8f);}
            GUI.Label(new Rect(r.x+24,r.y,r.width-24,r.height),reason??"Locked",new GUIStyle(footerStyle){fontSize=14,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,normal={textColor=red}});
        }

        // ---------- one plain sentence per option ----------
        // "PAY 25 GOLD" + "GAIN A RANDOM COMMON RELIC" -> "Spend 25 Gold. Gain a random common relic."
        private string EventSentence(EventChoiceDef choice)
        {
            var costs=new List<string>();var rewards=new List<string>();var prefix="";
            foreach(var part in SplitEventText(ActScaledText(choice.costText)))
            {
                var p=CostClause(part);if(p==null)continue;
                if(p.StartsWith("for the next",System.StringComparison.Ordinal)){prefix=p;continue;}
                costs.Add(p);
            }
            foreach(var part in SplitEventText(ActScaledText(choice.rewardText))){var p=RewardClause(part);if(p!=null)rewards.Add(p);}
            if(rewards.Count>0&&prefix.Length>0)rewards[0]=prefix+", "+rewards[0];
            costs=costs.OrderBy(c=>c.StartsWith("choose",System.StringComparison.Ordinal)?1:0).ToList(); // "Take a curse and choose a card", not the reverse
            var sentences=new List<string>();
            if(costs.Count>0)sentences.Add(JoinClauses(costs));
            if(rewards.Count>0)sentences.Add(JoinClauses(rewards));
            if(sentences.Count==0)sentences.Add(IsMysteryChoice(choice)?"open it and see":"nothing happens");
            var text=string.Join(" ",sentences.Select(s=>Capitalize(s)+"."));
            text=Regex.Replace(text,@"\b([Aa]) (?=[aeiouAEIOU])","$1n ");      // a uncommon -> an uncommon
            text=Regex.Replace(text,@"\bthe ([a-z]+(?: [a-z]+)?) curse",m=>"the "+string.Join(" ",m.Groups[1].Value.Split(' ').Select(NameCase))+" curse"); // curse names
            text=Regex.Replace(text,@"\bwith ([a-z]+)(?= or|\.)",m=>"with "+NameCase(m.Groups[1].Value));          // binding names
            text=Regex.Replace(text,@"\bor ([a-z]+)\.",m=>"or "+NameCase(m.Groups[1].Value)+".");
            return HighlightTerms(text);
        }
        private string ActScaledText(string text)
        {
            if(string.IsNullOrEmpty(text))return text;
            return Regex.Replace(text,@"(\d+)\s*/\s*(\d+)\s*/\s*(\d+)\s*GOLD BY ACT",m=>m.Groups[Mathf.Clamp(run.act,1,3)].Value+" GOLD",RegexOptions.IgnoreCase);
        }
        private static string CostClause(string part)
        {
            var s=GameplayTerms.Display(part).Trim().TrimEnd('.').ToLowerInvariant();
            if(s.Length==0||s=="nothing")return null;
            s=s.Replace(" eligible","").Replace("weighted ","");
            s=Regex.Replace(s,@"^(pay|lose) (\d+) gold$","spend $2 Gold");
            s=Regex.Replace(s,@"^need (\d+) gold$","bid $1 Gold");
            s=Regex.Replace(s,@"^add curse: (.+)$","take the $1 curse");
            s=Regex.Replace(s,@"^add (1|a) (random )?curse$","take a random curse");
            s=Regex.Replace(s,@"^add (\d+) (random )?curses$","take $1 random curses");
            s=Regex.Replace(s,@"^remove exactly (\d+) cards$","remove $1 cards");
            s=Regex.Replace(s,@"^(lose|give up) 1 (eligible )?common relic$","give up a common relic");
            s=Regex.Replace(s,@"^remove 1 owned shard$","give up a Fate Shard");
            s=Regex.Replace(s,@"^choose exactly (\d+) cards$","choose $1 cards");
            s=Regex.Replace(s,@"^choose (1|an|a) ","choose a "); // the a/an pass fixes vowels afterwards
            s=Regex.Replace(s,@"^next (\d+) combats$","for the next $1 combats");
            s=Regex.Replace(s,@"^start an elite fight$","fight an elite");
            return TermCase(s);
        }
        private static string RewardClause(string part)
        {
            var s=GameplayTerms.Display(part).Trim().TrimEnd('.').ToLowerInvariant();
            if(s.Length==0||s is "nothing" or "nothing happens" or "leave" or "nothing more")return null;
            if(s=="???")return "the outcome is hidden";
            s=s.Replace(" eligible","").Replace("weighted ","");
            s=Regex.Replace(s,@"^add curse: (.+)$","take the $1 curse");
            s=Regex.Replace(s,@"^(.+) if you win$","if you win, gain a $1");
            s=Regex.Replace(s,@"^replace with ","replace it with ");
            s=Regex.Replace(s,@"^first draw costs 0 that turn$","it costs 0 the first time you draw it");
            s=Regex.Replace(s,@"act iii","Act III");
            s=Regex.Replace(s,@"^(\d+) gold \(banked\)$","bank $1 Gold");
            s=Regex.Replace(s,@"^(.+) \(banked\)$","bank a $1");
            s=Regex.Replace(s,@"^everything banked$","take everything you've banked");
            s=Regex.Replace(s,@"^win (\d+)$","win $1 Gold");
            s=Regex.Replace(s,@"^reveal (\d+) (.+)$","see $1 $2");
            s=Regex.Replace(s,@"^choose 1$","keep 1");
            s=Regex.Replace(s,@"^small wanderer chance$","sometimes a Wanderer card");
            s=Regex.Replace(s,@"^apply binding: (.+)$","bind it with $1");
            s=Regex.Replace(s,@"^apply (\w+) or (\w+)$","bind it with $1 or $2");
            s=Regex.Replace(s,@"^apply a random compatible act-unlocked binding$","bind it with a random Binding");
            s=Regex.Replace(s,@"^create a separate copy with a random compatible binding$","copy it with a random Binding");
            s=Regex.Replace(s,@"^transform into same-rarity character card$","transform it into a character card of the same rarity");
            s=Regex.Replace(s,@"^reduce its activation count by 1$","restore 1 use");
            s=Regex.Replace(s,@"^move it to final fractured state$","make it Fractured");
            s=Regex.Replace(s,@"^a (weighted )?fatewheel outcome$","spin for a random fate");
            s=Regex.Replace(s,@"^next move: any path$","your next move can take any path");
            s=Regex.Replace(s,@"^starts every fight with (\d+) charge$","it starts every fight with $1 charge");
            s=Regex.Replace(s,@"^fully restored$","fully restore it");
            s=Regex.Replace(s,@"^primed$","prime it");
            s=Regex.Replace(s,@"^next mirror$","walk on to the next mirror");
            s=Regex.Replace(s,@"^elite rewards$","earn elite rewards");
            s=Regex.Replace(s,@"^(\d+) (foreign|other|wanderer)","see $1 $2");
            if(Regex.IsMatch(s,@"^(rare|uncommon|common) "))s=(s.StartsWith("uncommon")?"gain an ":"gain a ")+s;
            if(Regex.IsMatch(s,@"^\d+ gold$"))s="gain "+s;
            return TermCase(s);
        }
        private static string TermCase(string s)
        {
            foreach(var word in new[]{"Max HP","HP","Gold","Fate Shards","Fate Shard","Attacks","Attack","Skills","Skill","Powers","Power","Aspects","Aspect","Block","Strength","Energy","Bindings","Binding","Wanderer","Fractured","Stable","Dissipate"})
                s=Regex.Replace(s,@"\b"+word+@"\b",word,RegexOptions.IgnoreCase);
            return s;
        }
        private static string JoinClauses(List<string> clauses)
        {
            if(clauses.Count==1)return clauses[0];
            // "gain a card and gain 35 Gold" -> "gain a card and 35 Gold"
            for(var i=1;i<clauses.Count;i++)
            {
                var verb=clauses[0].Split(' ')[0];
                if(clauses[i].StartsWith(verb+" ",System.StringComparison.Ordinal)&&verb is "gain" or "see" or "lose" or "take" or "spend")clauses[i]=clauses[i].Substring(verb.Length+1);
            }
            return string.Join(", ",clauses.Take(clauses.Count-1))+" and "+clauses[clauses.Count-1];
        }
        private static string NameCase(string s)=>s is "a" or "an" or "it" or "the" or "random" or "everything" ?s:Capitalize(s);
        private static string Capitalize(string s)=>string.IsNullOrEmpty(s)?s:char.ToUpperInvariant(s[0])+s.Substring(1);
        // Numbers that matter take their resource's color.
        private static string HighlightTerms(string s)
        {
            s=Regex.Replace(s,@"\b(\d+) Gold\b","<color=#FFD27A><b>$1 Gold</b></color>");
            s=Regex.Replace(s,@"\b(\d+) Max HP\b","<color=#9BE89B><b>$1 Max HP</b></color>");
            s=Regex.Replace(s,@"\b(\d+) HP\b(?!</b>)","<color=#FF9080><b>$1 HP</b></color>");
            s=Regex.Replace(s,@"\b(relics?)\b","<color=#F4C978>$1</color>");
            s=Regex.Replace(s,@"\b(curses?)\b","<color=#C9A2FF>$1</color>");
            s=Regex.Replace(s,@"\b(Fate Shards?)\b","<color=#9FE3FF>$1</color>");
            return s;
        }
        private static GUIStyle SingleLineStyle(string text,float width,GUIStyle template,int size,int minimum)
        {
            var style=new GUIStyle(template){fontSize=size,wordWrap=false,clipping=TextClipping.Clip};
            while(style.fontSize>minimum&&style.CalcSize(new GUIContent(text??"")).x>width)style.fontSize--;
            return style;
        }
        private static string StripRich(string s)=>Regex.Replace(s??"","<.*?>","");
    }
}
