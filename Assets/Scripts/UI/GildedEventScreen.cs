using System.Collections.Generic;
using System.Linq;
using GildedFate.Audio;
using GildedFate.Core;
using GildedFate.Map;
using GildedFate.Saving;
using UnityEngine;

namespace GildedFate.UI
{
    // Events Rework: the new event screen.
    // Options are framed cards: title, one flavor line, then icon chips with costs and risk on the
    // left (red) and rewards on the right (gold). No tooltip repeats the option text.
    public sealed partial class GildedMainMenu
    {
        private enum EventChipIcon { None, Gold, Hp, Heal, Curse, Relic, Shard, Upgrade, Remove, Card, Mystery }
        private enum EventChipTone { Cost, Neutral, Risk, Chance, Reward, Locked }
        private struct EventChip { public string text;public EventChipIcon icon;public EventChipTone tone;public float odds; }
        private string eventStoryKey="";private float eventStoryShownAt;

        // ---------------- story panel ----------------
        private string EventSceneStory(EventDefinition definition)
        {
            var scene=EventSystem.CurrentScene(run,definition);
            var key=(definition?.id??"")+"/"+(scene?.id??"");
            if(key!=eventStoryKey){eventStoryKey=key;eventStoryShownAt=Time.unscaledTime;}
            return scene!=null?scene.prompt:EventStory(definition);
        }
        // The prompt types out quickly; Reduce Motion shows it at once.
        private string TypedEventText(string text)
        {
            if(string.IsNullOrEmpty(text)||profile.reduceMotion)return text;
            var count=Mathf.FloorToInt((Time.unscaledTime-eventStoryShownAt)*90f);
            return count>=text.Length?text:text.Substring(0,Mathf.Max(0,count));
        }
        private void DrawEventStepPips(Rect story)
        {
            var steps=currentEvent?.steps??1;if(steps<=1)return;
            var step=Mathf.Clamp(EventSystem.CurrentStep(run,currentEvent),1,steps);
            var right=story.xMax-34;var y=story.y+36;
            for(var i=0;i<steps;i++)
            {
                var c=new Vector2(right-(steps-1-i)*22,y);var done=i<step;var current=i==step-1;
                var s=current?8f:6f;var col=done?Gold:new Color(.55f,.45f,.3f,.7f);
                var pts=new[]{c+new Vector2(0,-s),c+new Vector2(s,0),c+new Vector2(0,s),c+new Vector2(-s,0)};
                for(var k=0;k<4;k++)DrawLine(pts[k],pts[(k+1)%4],col,current?2.4f:1.4f);
                if(done)Fill(new Rect(c.x-s*.35f,c.y-s*.35f,s*.7f,s*.7f),col);
                if(current&&!profile.reduceFlashing)ShardSoft(c,34,new Color(1f,.8f,.4f,.25f+Mathf.Sin(shimmer*3)*.08f));
            }
            var label=new GUIStyle(ReadableStyle(11,true)){alignment=TextAnchor.MiddleRight,normal={textColor=new Color(.85f,.69f,.40f)}};
            GUI.Label(new Rect(right-(steps-1)*22-170,y-10,150,20),"STEP "+step+" OF "+steps,label);
        }

        // ---------------- option cards ----------------
        private void DrawEventOptionCard(Rect rect,EventChoiceDef choice,int index)
        {
            var availability=EventSystem.Availability(run,choice);var open=availability.available;
            var hot=controllerNavigation?screenControllerIndex==index:rect.Contains(PointerPosition);
            var r=rect;if(hot&&open&&!profile.reduceMotion)r.y-=3;
            if(hot&&open&&!profile.reduceFlashing)ShardSoft(new Rect(r.x-36,r.y-26,r.width+72,r.height+52),new Color(1f,.74f,.32f,.10f));
            Fill(r,open?(hot?new Color(.034f,.029f,.021f,.97f):new Color(.008f,.012f,.02f,.94f)):new Color(.012f,.012f,.014f,.9f));
            var edge=open?(hot?Gold:new Color(.62f,.47f,.24f,.85f)):new Color(.34f,.33f,.31f,.8f);
            Outline(r,edge,hot?2:1);Outline(new Rect(r.x+5,r.y+5,r.width-10,r.height-10),new Color(edge.r,edge.g,edge.b,.26f),1);
            Fill(new Rect(r.x,r.y+12,3,r.height-24),edge);
            foreach(var corner in new[]{new Vector2(r.x,r.y),new Vector2(r.xMax,r.y),new Vector2(r.x,r.yMax),new Vector2(r.xMax,r.yMax)})EventGem(corner,4,edge,true);
            // Number badge.
            var badge=new Vector2(r.x+30,r.y+29);EventGem(badge,15,edge,false);
            GUI.Label(new Rect(badge.x-15,badge.y-12,30,24),(index+1).ToString(),new GUIStyle(titleStyle){fontSize=17,alignment=TextAnchor.MiddleCenter,normal={textColor=open?(hot?Gold:new Color(.85f,.7f,.45f)):Color.gray}});
            // Title and flavor.
            var titleArea=new Rect(r.x+58,r.y+11,r.width-76,32);var title=FittedEventStyle(choice.title,titleArea,titleStyle,22,16);
            title.normal.textColor=open?new Color(.98f,.9f,.7f):new Color(.6f,.59f,.56f);GUI.Label(titleArea,choice.title,title);
            var flavor=!string.IsNullOrEmpty(choice.flavor)?choice.flavor:EventChoiceAction(choice);var compact=r.height<128;
            var chipTop=r.y+48;
            if(!string.IsNullOrEmpty(flavor)&&!compact)
            {
                GUI.Label(new Rect(r.x+58,r.y+42,r.width-76,22),flavor,new GUIStyle(footerStyle){fontSize=14,fontStyle=FontStyle.Italic,alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip,normal={textColor=open?new Color(.72f,.72f,.70f):new Color(.5f,.5f,.48f)}});
                chipTop=r.y+70;
            }
            DrawEventChips(new Rect(r.x+58,chipTop,r.width-76,r.yMax-chipTop-10),choice,availability);
            var enabled=GUI.enabled;GUI.enabled=enabled&&open&&!acquisitionActive;
            if(GUI.Button(rect,"",GUIStyle.none))BeginEventChoice(choice);
            GUI.enabled=enabled;DeniedPress(rect,!open&&!acquisitionActive);
        }
        private void EventGem(Vector2 c,float s,Color color,bool filled)
        {
            var pts=new[]{c+new Vector2(0,-s),c+new Vector2(s,0),c+new Vector2(0,s),c+new Vector2(-s,0)};
            for(var k=0;k<4;k++){if(!filled)DrawLine(pts[k],pts[(k+1)%4],new Color(.02f,.02f,.03f,.9f),4);DrawLine(pts[k],pts[(k+1)%4],color,filled?2.5f:1.6f);}
        }

        // ---------------- chips ----------------
        private List<EventChip> LeftChips(EventChoiceDef choice,EventChoiceAvailability availability)
        {
            var chips=new List<EventChip>();
            if(!availability.available)chips.Add(new EventChip{text=(availability.reason??"Locked").ToUpperInvariant(),icon=EventChipIcon.None,tone=EventChipTone.Locked});
            foreach(var part in SplitEventText(choice.costText))
            {
                var upper=part.ToUpperInvariant();var need=upper.StartsWith("NEED ");
                if(need&&!availability.available)continue; // the lock chip already says it
                chips.Add(new EventChip{text=EventChipPhrase(part,true),icon=EventIconFor(upper),tone=need?EventChipTone.Neutral:EventChipTone.Cost});
            }
            if(choice.chance>0)chips.Add(new EventChip{text=choice.chanceLabel,icon=choice.chanceGood?EventChipIcon.None:EventChipIcon.None,tone=choice.chanceGood?EventChipTone.Chance:EventChipTone.Risk,odds=choice.chance/100f});
            if(chips.Count==0)chips.Add(new EventChip{text="No cost",icon=EventChipIcon.None,tone=EventChipTone.Neutral});
            return chips;
        }
        private List<EventChip> RightChips(EventChoiceDef choice)
        {
            var chips=new List<EventChip>();
            foreach(var part in SplitEventText(choice.rewardText))
            {
                var upper=part.ToUpperInvariant();if(upper=="NOTHING"||upper=="LEAVE"||upper=="NOTHING HAPPENS")continue;
                chips.Add(new EventChip{text=upper=="???"?"Unknown":EventChipPhrase(part,false),icon=EventIconFor(upper),tone=EventChipTone.Reward});
            }
            return chips;
        }
        private static IEnumerable<string> SplitEventText(string text)=>(text??"").Split('·').Select(p=>p.Trim()).Where(p=>p.Length>0);
        private static EventChipIcon EventIconFor(string upper)
        {
            if(upper=="???")return EventChipIcon.Mystery;
            if(upper.Contains("GOLD"))return EventChipIcon.Gold;
            if(upper.Contains("MAX HP")||upper.Contains("HEAL"))return EventChipIcon.Heal;
            if(upper.Contains("HP"))return EventChipIcon.Hp;
            if(upper.Contains("CURSE"))return EventChipIcon.Curse;
            if(upper.Contains("RELIC"))return EventChipIcon.Relic;
            if(upper.Contains("SHARD")||upper.Contains("CHARGE")||upper.Contains("FRACTURED"))return EventChipIcon.Shard;
            if(upper.Contains("UPGRADE"))return EventChipIcon.Upgrade;
            if(upper.Contains("REMOVE"))return EventChipIcon.Remove;
            if(upper.Contains("CARD")||upper.Contains("ATTACK")||upper.Contains("SKILL")||upper.Contains("POWER")||upper.Contains("BINDING"))return EventChipIcon.Card;
            return EventChipIcon.None;
        }
        // "PAY 25 GOLD" -> "25 Gold" on a red chip; "GAIN 45 GOLD" -> "45 Gold" on a gold chip. Icons carry the verb.
        private static string EventChipPhrase(string text,bool cost)
        {
            var result=GameplayTerms.Display(text).Trim().TrimEnd('.').ToLowerInvariant();
            foreach(var verb in new[]{"pay ","lose ","gain "})if(result.StartsWith(verb)&&(result.Contains("gold")||result.EndsWith(" hp")||result.Contains("max hp"))){result=result.Substring(verb.Length);break;}
            foreach(var word in new[]{"HP","Max HP","Gold","Attack","Skill","Power","Binding","Relic","Curse","Fate Shard","Shard","Block","Strength","Fractured"})
                result=System.Text.RegularExpressions.Regex.Replace(result,@"\b"+word+@"\b",word,System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return result.Length==0?text:char.ToUpperInvariant(result[0])+result.Substring(1);
        }
        private void DrawEventChips(Rect area,EventChoiceDef choice,EventChoiceAvailability availability)
        {
            var left=LeftChips(choice,availability);var right=RightChips(choice);
            const float height=30,gap=8;var style=EventChipStyle(15);
            float Width(EventChip chip){var w=style.CalcSize(new GUIContent(chip.text)).x+(chip.icon==EventChipIcon.None?24:48);return Mathf.Min(w,area.width);}
            var leftW=left.Sum(Width)+gap*Mathf.Max(0,left.Count-1);var rightW=right.Sum(Width)+gap*Mathf.Max(0,right.Count-1);
            var oneRow=leftW+rightW+28<=area.width;
            var x=area.x;var y=area.y;
            foreach(var chip in left){var w=Width(chip);if(x+w>area.xMax&&x>area.x){x=area.x;y+=height+6;}DrawEventChip(new Rect(x,y,w,height),chip,availability.available);x+=w+gap;}
            var rowY=oneRow?area.y:y+height+6;
            if(rowY+height>area.yMax+4&&!oneRow)rowY=Mathf.Max(area.y,area.yMax-height);
            var rx=area.xMax;
            for(var i=right.Count-1;i>=0;i--){var w=Width(right[i]);if(rx-w<area.x){rx=area.xMax;rowY+=height+6;}DrawEventChip(new Rect(rx-w,rowY,w,height),right[i],availability.available);rx-=w+gap;}
            if(!oneRow&&right.Count>0&&left.Count>0&&!profile.reducedVfx)
            {
                // A faint thread from cost to reward replaces the old text arrow.
                var from=new Vector2(area.x+Mathf.Min(leftW,area.width*.45f)+6,area.y+height*.5f);var to=new Vector2(area.xMax-Mathf.Min(rightW,area.width*.5f)-6,rowY+height*.5f);
                if(to.x>from.x+20)DrawLine(from,to,new Color(.62f,.47f,.24f,.25f),1);
            }
        }
        private GUIStyle EventChipStyle(int size)=>new(footerStyle){fontSize=size,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip};
        private void DrawEventChip(Rect r,EventChip chip,bool open)
        {
            Color edge,fill,ink;
            switch(chip.tone)
            {
                case EventChipTone.Cost:edge=new Color(.86f,.34f,.28f);fill=new Color(.22f,.05f,.04f,.85f);ink=new Color(1f,.82f,.76f);break;
                case EventChipTone.Risk:edge=new Color(.92f,.3f,.24f);fill=new Color(.2f,.04f,.03f,.9f);ink=new Color(1f,.8f,.72f);break;
                case EventChipTone.Chance:edge=Gold;fill=new Color(.2f,.15f,.05f,.9f);ink=new Color(1f,.92f,.7f);break;
                case EventChipTone.Reward:edge=new Color(.9f,.7f,.32f);fill=new Color(.16f,.12f,.04f,.88f);ink=new Color(1f,.9f,.66f);break;
                case EventChipTone.Locked:edge=new Color(.55f,.5f,.46f);fill=new Color(.08f,.08f,.08f,.9f);ink=new Color(.82f,.78f,.72f);break;
                default:edge=new Color(.5f,.48f,.44f);fill=new Color(.07f,.07f,.08f,.85f);ink=new Color(.85f,.83f,.78f);break;
            }
            if(!open&&chip.tone!=EventChipTone.Locked){edge=Color.Lerp(edge,Color.gray,.6f);ink=Color.Lerp(ink,Color.gray,.5f);fill.a*=.6f;}
            Fill(r,fill);
            if(chip.odds>0)Fill(new Rect(r.x,r.yMax-4,r.width*chip.odds,4),new Color(edge.r,edge.g,edge.b,.85f)); // odds bar
            Outline(r,edge,1);
            var textX=r.x+12;
            if(chip.icon!=EventChipIcon.None){DrawEventIcon(new Rect(r.x+9,r.center.y-10,20,20),chip.icon,open);textX=r.x+36;}
            var style=EventChipStyle(15);style.normal.textColor=ink;
            while(style.fontSize>11&&style.CalcSize(new GUIContent(chip.text)).x>r.xMax-textX-8)style.fontSize--;
            GUI.Label(new Rect(textX,r.y,r.xMax-textX-6,r.height),chip.text,style);
        }
        private void DrawEventIcon(Rect r,EventChipIcon icon,bool open)
        {
            var c=r.center;var a=open?1f:.5f;
            switch(icon)
            {
                case EventChipIcon.Gold:DrawGoldIcon(r);break;
                case EventChipIcon.Hp:
                {
                    var red=new Color(.95f,.28f,.26f,a);var pts=new[]{c+new Vector2(0,8),c+new Vector2(-8,0),c+new Vector2(-7,-5),c+new Vector2(-3,-7),c+new Vector2(0,-4),c+new Vector2(3,-7),c+new Vector2(7,-5),c+new Vector2(8,0)};
                    for(var k=0;k<pts.Length;k++)DrawLine(pts[k],pts[(k+1)%pts.Length],red,2.6f);
                    Fill(new Rect(c.x-4,c.y-3,8,6),red);break;
                }
                case EventChipIcon.Heal:{var green=new Color(.45f,.9f,.5f,a);DrawLine(c+new Vector2(0,-7),c+new Vector2(0,7),green,3.4f);DrawLine(c+new Vector2(-7,0),c+new Vector2(7,0),green,3.4f);break;}
                case EventChipIcon.Curse:{var purple=new Color(.75f,.45f,1f,a);EventGem(c,8,purple,false);DrawLine(c+new Vector2(-3,-3),c+new Vector2(3,3),purple,2);DrawLine(c+new Vector2(3,-3),c+new Vector2(-3,3),purple,2);break;}
                case EventChipIcon.Relic:{var gold=new Color(1f,.8f,.38f,a);EventGem(c,8,gold,false);Fill(new Rect(c.x-2.5f,c.y-2.5f,5,5),gold);break;}
                case EventChipIcon.Shard:{var cyan=new Color(.55f,.88f,1f,a);var pts=new[]{c+new Vector2(0,-9),c+new Vector2(5,0),c+new Vector2(0,9),c+new Vector2(-5,0)};for(var k=0;k<4;k++)DrawLine(pts[k],pts[(k+1)%4],cyan,2.2f);DrawLine(c+new Vector2(0,-9),c+new Vector2(0,9),new Color(1,1,1,.7f*a),1);break;}
                case EventChipIcon.Upgrade:{var green=new Color(.55f,.95f,.55f,a);DrawLine(c+new Vector2(-7,1),c+new Vector2(0,-6),green,2.6f);DrawLine(c+new Vector2(0,-6),c+new Vector2(7,1),green,2.6f);DrawLine(c+new Vector2(-7,7),c+new Vector2(0,0),green,2.6f);DrawLine(c+new Vector2(0,0),c+new Vector2(7,7),green,2.6f);break;}
                case EventChipIcon.Remove:{var ember=new Color(1f,.55f,.35f,a);DrawLine(c+new Vector2(-6,-6),c+new Vector2(6,6),ember,3);DrawLine(c+new Vector2(6,-6),c+new Vector2(-6,6),ember,3);break;}
                case EventChipIcon.Card:{var bone=new Color(.92f,.86f,.72f,a);var card=new Rect(c.x-6,c.y-8,12,16);Outline(card,bone,2);DrawLine(new Vector2(card.x+3,card.y+5),new Vector2(card.xMax-3,card.y+5),bone,1);break;}
                case EventChipIcon.Mystery:GUI.Label(r,"?",new GUIStyle(titleStyle){fontSize=18,alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(1f,.85f,.5f,a)}});break;
            }
        }

        // ---------------- banked loot ----------------
        private void DrawEventBankTray(Rect r)
        {
            if(run.eventBank==null||run.eventBank.Count==0)return;
            var pulse=profile.reduceMotion?0:Mathf.Sin(shimmer*2.2f)*.5f+.5f;
            Fill(r,new Color(.03f,.025f,.012f,.95f));Outline(r,new Color(1f,.78f,.38f,.55f+pulse*.25f),2);
            GUI.Label(new Rect(r.x+16,r.y,150,r.height),"BANKED SO FAR",new GUIStyle(ReadableStyle(12,true)){alignment=TextAnchor.MiddleLeft,normal={textColor=Gold}});
            var x=r.x+170;
            foreach(var entry in run.eventBank)
            {
                var label=EventSystem.BankLabel(entry);var chip=new EventChip{text=label,icon=EventIconFor(label.ToUpperInvariant()),tone=EventChipTone.Reward};
                var w=EventChipStyle(15).CalcSize(new GUIContent(label)).x+48;if(x+w>r.xMax-12)break;
                DrawEventChip(new Rect(x,r.center.y-15,w,30),chip,true);x+=w+8;
            }
        }

        // ---------------- outcome screen ----------------
        private void FinishEventOutcome()
        {
            if(EventSystem.ContinueEvent(run)){screenControllerIndex=0;SaveService.Save(run);screen=ScreenMode.Event;Sfx(SoundCue.EventReveal);return;}
            EventSystem.CompleteResult(run);
            // An event that started a fight turns this room into that fight, with its normal rewards.
            var fight=EventSystem.TakePendingFight(run);
            if(!string.IsNullOrEmpty(fight)&&currentNode!=null){currentNode.kind=fight=="elite"?NodeKind.Elite:NodeKind.Combat;StartNode(currentNode,false);return;}
            Advance();
        }
        private void DrawEventOutcome(float w,float h)
        {
            DrawEventBackdrop(w,h);DrawRunHud(w);
            var pulse=.78f+(profile.reduceMotion?0:Mathf.Sin(shimmer*2.4f)*.16f);var continues=EventSystem.HasNextScene(run);
            var panel=new Rect(w*.2f,h*.19f,w*.6f,h*.58f);
            ShardSoft(new Rect(panel.x-60,panel.y-50,panel.width+120,panel.height+100),new Color(1f,.7f,.3f,.08f));
            Fill(panel,new Color(.005f,.009f,.015f,.95f));Outline(panel,new Color(1f,.72f,.23f,pulse),2);Outline(new Rect(panel.x+6,panel.y+6,panel.width-12,panel.height-12),new Color(1f,.72f,.23f,.25f),1);
            foreach(var corner in new[]{new Vector2(panel.x,panel.y),new Vector2(panel.xMax,panel.y),new Vector2(panel.x,panel.yMax),new Vector2(panel.xMax,panel.yMax)})EventGem(corner,6,Gold,true);
            GUI.Label(new Rect(panel.x+30,panel.y+26,panel.width-60,24),currentEvent?.name??"",new GUIStyle(ReadableStyle(13,true)){alignment=TextAnchor.MiddleCenter,normal={textColor=new Color(.85f,.69f,.40f)}});
            GUI.Label(new Rect(panel.x+30,panel.y+54,panel.width-60,50),continues?"THE PATH CONTINUES":"FATE ALTERED",new GUIStyle(titleStyle){fontSize=32,normal={textColor=new Color(1f,.84f,.45f,pulse)}});
            DrawLine(new Vector2(panel.center.x-140,panel.y+112),new Vector2(panel.center.x+140,panel.y+112),new Color(1f,.75f,.35f,.5f),1);
            var choice=EventSystem.ActiveChoice(run);var story=choice!=null&&(choice.chance>0||!string.IsNullOrEmpty(choice.resultText));
            var body=new Rect(panel.x+60,panel.y+132,panel.width-120,panel.height-240);
            if(story||choice==null)
            {
                var text=run.pendingEventResult??"";var style=new GUIStyle(footerStyle){fontSize=22,wordWrap=true,alignment=TextAnchor.UpperCenter,normal={textColor=new Color(.97f,.94f,.85f)}};
                while(style.fontSize>15&&style.CalcHeight(new GUIContent(text),body.width)>body.height)style.fontSize--;
                GUI.Label(body,TypedEventText(text),style);
            }
            else
            {
                // A plain choice: show what was paid and gained as chips instead of an arrow sentence.
                var left=SplitEventText(choice.costText).Select(p=>new EventChip{text=EventChipPhrase(p,true),icon=EventIconFor(p.ToUpperInvariant()),tone=EventChipTone.Cost}).ToList();
                var right=RightChips(choice);var all=left.Concat(right).ToList();var style=EventChipStyle(17);
                var widths=all.Select(c=>style.CalcSize(new GUIContent(c.text)).x+52).ToList();var total=widths.Sum()+12*Mathf.Max(0,all.Count-1);
                var x=body.center.x-Mathf.Min(total,body.width)*.5f;var y=body.y+20;
                for(var i=0;i<all.Count;i++){if(x+widths[i]>body.xMax){x=body.x;y+=44;}DrawEventChip(new Rect(x,y,widths[i],36),all[i],true);x+=widths[i]+12;}
            }
            var trayRect=new Rect(panel.x+40,panel.yMax-150,panel.width-80,46);DrawEventBankTray(trayRect);
            var next=new Rect(panel.center.x-150,panel.yMax-82,300,48);DrawButtonFrame(next,next.Contains(PointerPosition)||controllerNavigation,false);
            if(GUI.Button(next,continues?"CONTINUE":"RETURN TO THE MAP",buttonStyle))FinishEventOutcome();
            DrawPersistentRunTooltip(w,h);
        }
    }
}
