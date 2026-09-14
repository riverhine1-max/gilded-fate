using System.Collections.Generic;
using System.Linq;
using GildedFate.Core;
using GildedFate.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private GUIStyle cachedTooltipHeading,cachedTooltipBody;
        private Font cachedTooltipHeadingFont,cachedTooltipBodyFont;
        private readonly GUIContent tooltipHeadingContent=new(),tooltipBodyContent=new();
        private void EnsureTooltipStyles()
        {
            var headingFont=labelFont?labelFont:bodyFont;
            if(cachedTooltipHeading!=null&&cachedTooltipHeadingFont==headingFont&&cachedTooltipBodyFont==bodyFont)return;
            cachedTooltipHeadingFont=headingFont;cachedTooltipBodyFont=bodyFont;
            cachedTooltipHeading=new GUIStyle(footerStyle){font=headingFont,fontSize=17,fontStyle=FontStyle.Bold,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.98f,.86f,.62f)}};
            cachedTooltipBody=new GUIStyle(footerStyle){font=bodyFont,fontSize=15,richText=true,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=new Color(.94f,.92f,.87f)}};
        }
        private string scrollingTooltipKey;
        private Vector2 scrollingTooltipPosition;
        private Rect lastTooltipBounds;
        private int tooltipScrolledFrame=-1;
        private bool TooltipsEnabled => profile==null||profile.tooltips;
        private static float TooltipScrollValue(float current,float amount,float maximum)=>Mathf.Clamp(current+amount,0,Mathf.Max(0,maximum));
        private static float TooltipStickScrollDelta(float axis,float deltaTime)=>-axis*Mathf.Clamp(deltaTime,0,.05f)*330;
        private string CardGlossaryDetail(CardDef card,CombatCardPreview preview=null)
        {
            var sections=new List<string>();
            foreach(var attachment in CardAttachments(card))
                sections.Add("<b><color=#E9C678>"+attachment.title+"</color></b>\n"+attachment.detail);
            if(preview!=null&&!string.IsNullOrEmpty(preview.breakdown))
                sections.Add("<b><color=#91D6A3>LIVE VALUES</color></b>\n"+preview.breakdown);
            foreach(var keyword in RuleKeywords.Where(k=>HasRuleKeyword(card.text,k)))
                sections.Add("<b><color=#"+keyword.hex+">"+keyword.title+"</color></b>\n"+keyword.detail);
            return string.Join("\n\n",sections);
        }
        private void DrawPolishedTooltip(Rect requested,string title,string detail,Rect? avoid=null)
        {
            if(!TooltipsEnabled){lastTooltipBounds=Rect.zero;return;}
            if(title is "POWER" or "POWERS" or "EXHAUST" or "EXHAUST PILE")title=GameplayTerms.Display(title);
            detail=FormatCardRules(detail);
            var width=Mathf.Min(Mathf.Max(270,requested.width),CombatWidth-32);
            EnsureTooltipStyles();var heading=cachedTooltipHeading;var body=cachedTooltipBody;
            tooltipHeadingContent.text=title;tooltipBodyContent.text=detail;
            var maxHeight=CombatHeight-88;var headingHeight=heading.CalcHeight(tooltipHeadingContent,width-28);
            var bodyHeight=body.CalcHeight(tooltipBodyContent,width-28);
            // Keep the caller's adjacent width: expanding a left-hand tooltip
            // would grow it back over the card that is being inspected.
            var desired=headingHeight+bodyHeight+38;
            var height=Mathf.Min(maxHeight,Mathf.Max(80,desired));
            var r=new Rect(Mathf.Clamp(requested.x,16,CombatWidth-width-16),
                Mathf.Clamp(requested.y,68,CombatHeight-height-16),width,height);
            if(avoid.HasValue&&r.Overlaps(avoid.Value))
            {
                var anchor=avoid.Value;var candidates=new[]{new Rect(anchor.x-width-14,r.y,width,height),new Rect(anchor.xMax+14,r.y,width,height),new Rect(r.x,anchor.y-height-14,width,height),new Rect(r.x,anchor.yMax+14,width,height)};
                foreach(var candidate in candidates)
                    if(candidate.x>=16&&candidate.xMax<=CombatWidth-16&&candidate.y>=68&&candidate.yMax<=CombatHeight-16&&!candidate.Overlaps(anchor)){r=candidate;break;}
            }
            lastTooltipBounds=r;
            Fill(r,new Color(.009f,.013f,.021f,.985f));
            DrawLine(new Vector2(r.x,r.y),new Vector2(r.x,r.yMax),new Color(.83f,.67f,.36f),3);
            DrawLine(new Vector2(r.x+12,r.y+headingHeight+21),new Vector2(r.xMax-12,r.y+headingHeight+21),new Color(.52f,.44f,.31f,.55f),1);
            GUI.Label(new Rect(r.x+14,r.y+11,r.width-28,headingHeight),title,heading);
            var content=new Rect(r.x+14,r.y+headingHeight+29,r.width-28,r.height-headingHeight-38);
            if(desired<=height+.1f){GUI.Label(content,detail,body);return;}
            // Exceptionally long inspection text scrolls at the normal readable
            // font size instead of extending past the screen or becoming tiny.
            var key=title+"\n"+detail;
            if(scrollingTooltipKey!=key){scrollingTooltipKey=key;scrollingTooltipPosition=Vector2.zero;}
            var scrollBodyWidth=content.width-14;bodyHeight=body.CalcHeight(tooltipBodyContent,scrollBodyWidth);
            var maxScroll=Mathf.Max(0,bodyHeight-content.height);
            if(Event.current.type==EventType.ScrollWheel&&(r.Contains(PointerPosition)||combatHudInspectActive||inspectedCard!=null||inspectedRelic!=null))
            {scrollingTooltipPosition.y=TooltipScrollValue(scrollingTooltipPosition.y,Event.current.delta.y*24,maxScroll);Event.current.Use();}
            if((controllerNavigation||combatHudInspectActive)&&Event.current.type==EventType.Repaint&&tooltipScrolledFrame!=Time.frameCount)
            {
                tooltipScrolledFrame=Time.frameCount;
                var axis=Gamepad.current?.rightStick.ReadValue().y??0;
                var keys=Keyboard.current;var page=(keys?.pageDownKey.wasPressedThisFrame==true?1:0)-(keys?.pageUpKey.wasPressedThisFrame==true?1:0);
                scrollingTooltipPosition.y=TooltipScrollValue(scrollingTooltipPosition.y,TooltipStickScrollDelta(axis,Time.unscaledDeltaTime)+page*content.height*.8f,maxScroll);
            }
            scrollingTooltipPosition=GUI.BeginScrollView(content,scrollingTooltipPosition,new Rect(0,0,scrollBodyWidth,bodyHeight),false,false,GUIStyle.none,GUIStyle.none);
            GUI.Label(new Rect(0,0,scrollBodyWidth,bodyHeight),detail,body);
            GUI.EndScrollView();
            var thumbHeight=Mathf.Max(24,content.height*content.height/bodyHeight);
            Fill(new Rect(content.xMax-4,content.y,3,content.height),new Color(.25f,.23f,.20f));
            Fill(new Rect(content.xMax-4,content.y+(content.height-thumbHeight)*(maxScroll<=0?0:scrollingTooltipPosition.y/maxScroll),3,thumbHeight),Gold);
        }
    }
}
