using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        private sealed class ReadableText
        {
            public GUIStyle style;
            public string[] lines;
            public float lineHeight, widest;
            public float Height=>lines.Length*lineHeight;
        }
        private readonly Dictionary<string,ReadableText> readableTextCache=new();
        private readonly Dictionary<int,Texture2D> readableCardShapes=new();
        private Texture2D readableTitleRibbon,readableArtCut,readableArtRim;
        private static readonly Regex RichTokens=new(@"<[^>]+>|\s+|[^\s<]+",RegexOptions.Compiled);
        private static readonly Regex StripRichTags=new(@"<[^>]+>",RegexOptions.Compiled);

        // Each wrapped word owns balanced tags. A line never starts with a missing
        // color/bold opener, and markup characters never consume layout width.
        private static IEnumerable<string> ReadableWords(string rich)
        {
            var tags=new List<string>();var word=new StringBuilder();
            foreach(Match token in RichTokens.Matches(rich??""))
            {
                var value=token.Value;
                if(value[0]=='<')
                {
                    if(value.StartsWith("</")){if(tags.Count>0)tags.RemoveAt(tags.Count-1);}
                    else tags.Add(value);
                    continue;
                }
                if(char.IsWhiteSpace(value[0]))
                {
                    if(word.Length>0){yield return word.ToString();word.Clear();}
                    foreach(var c in value)if(c=='\n')yield return "\n";
                    continue;
                }
                foreach(var tag in tags)word.Append(tag);
                word.Append(value);
                for(var i=tags.Count-1;i>=0;i--)
                {
                    var name=tags[i].Substring(1).TrimEnd('>');var equals=name.IndexOf('=');
                    if(equals>=0)name=name.Substring(0,equals);
                    word.Append("</").Append(name).Append('>');
                }
            }
            if(word.Length>0)yield return word.ToString();
        }

        private GUIStyle ReadableStyle(int size,bool heading=false)
        {
            // Start clean: inherited skin padding/offsets can clip centered labels.
            return new GUIStyle
            {
                font=heading?(labelFont?labelFont:bodyFont):bodyFont,
                fontSize=size,richText=true,wordWrap=false,
                alignment=TextAnchor.MiddleCenter,clipping=TextClipping.Clip,
                padding=new RectOffset(),margin=new RectOffset(),contentOffset=Vector2.zero,
                fixedWidth=0,fixedHeight=0,
                normal={textColor=new Color(.98f,.95f,.88f)}
            };
        }
        private ReadableText FitReadableText(string rich,Rect area,int preferred,int minimum,bool heading=false)
        {
            if(!heading)rich=GameplayTerms.Display(rich);
            var key=$"{heading}:{area.width:F1}:{area.height:F1}:{preferred}:{minimum}:{rich}";
            if(readableTextCache.TryGetValue(key,out var cached))return cached;
            var words=ReadableWords(rich).ToArray();ReadableText layout=null;
            for(var size=preferred;size>=minimum;size--)
            {
                var style=ReadableStyle(size,heading);var lines=new List<string>();var line="";
                var width=Mathf.Max(1,area.width-4);
                foreach(var word in words)
                {
                    if(word=="\n"){lines.Add(line);line="";continue;}
                    var candidate=line.Length==0?word:line+" "+word;
                    if(line.Length>0&&style.CalcSize(new GUIContent(candidate)).x>width){lines.Add(line);line=word;}
                    else line=candidate;
                }
                if(line.Length>0)lines.Add(line);
                if(lines.Count==0)lines.Add("");
                layout=new ReadableText{style=style,lines=lines.ToArray(),lineHeight=Mathf.Ceil(style.CalcSize(new GUIContent("Ag")).y)+1};
                layout.widest=lines.Max(l=>style.CalcSize(new GUIContent(l)).x);
                // Fractional canvas scaling can round each line up by one pixel.
                // Tighten only spare leading before reducing the readable font.
                var fittedLeading=(area.height-2)/layout.lines.Length;
                if(layout.Height>area.height-2&&fittedLeading>=style.CalcSize(new GUIContent("Ag")).y)
                    layout.lineHeight=fittedLeading;
                if(layout.widest<=width&&layout.Height<=area.height-2)break;
            }
            if(readableTextCache.Count>4096)readableTextCache.Clear();
            readableTextCache[key]=layout;return layout;
        }
        private static void DrawReadableText(Rect area,ReadableText layout)
        {
            var y=area.center.y-layout.Height*.5f;
            foreach(var line in layout.lines)
            {
                GUI.Label(new Rect(area.x+2,y,area.width-4,layout.lineHeight),line,layout.style);
                y+=layout.lineHeight;
            }
        }

        private static Rect CardArtArea(Rect r)=>new(r.x+r.width*.078f,r.y+r.height*.145f,r.width*.844f,r.height*.352f);
        private static Rect CardRuleArea(Rect r)=>new(r.x+r.width*.085f,r.y+r.height*.565f,r.width*.83f,r.height*.365f);
        private static Rect CardNameArea(Rect r)=>new(r.x+r.width*.20f,r.y+r.height*.025f,r.width*.75f,r.height*.108f);
        private ReadableText ReadableCardRules(Rect r,CardDef card,string rules=null)
        {
            var scale=r.width/194f;var large=profile?.largeCardText==true;
            var preferred=Mathf.Clamp(Mathf.RoundToInt((large?16:15)*scale),13,34);
            var minimum=Mathf.Clamp(Mathf.RoundToInt(11*scale),10,25);
            var rich=comparisonBaseRendering==null?FormatCardRules(rules??card.text,card):UpgradeComparison.Highlight(comparisonBaseRendering.text,card.text,part=>FormatCardRules(part,card));
            if(comparisonBaseRendering==null&&screen==ScreenMode.Combat&&combat!=null&&combat.hand.Contains(card)&&card.keywords.Contains("Heavy"))
            {
                var raw=rules??card.text;var at=raw.IndexOf("Heavy:",StringComparison.OrdinalIgnoreCase);
                if(at>=0){var before=raw.Substring(0,at);var clause=System.Text.RegularExpressions.Regex.Replace(raw.Substring(at),"<[^>]+>","");rich=FormatCardRules(before,card)+"<color="+(combat.memory.attacksThisTurn==0?"#F1CD79":"#C1C1C8")+">"+clause+"</color>";}
            }
            return FitReadableText(rich,CardRuleArea(r),preferred,minimum);
        }
        private ReadableText ReadableCardName(Rect r,CardDef card)
        {
            var name=System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(card.name.ToLowerInvariant());
            return FitReadableText(name,CardNameArea(r),Mathf.Clamp(Mathf.RoundToInt(r.width*.078f),13,32),Mathf.Clamp(Mathf.RoundToInt(r.width*.055f),10,22),true);
        }
        private void FillCardSilhouette(Rect r,Color color,float bevel)
        {
            // One masked quad per surface. Separate nine-slice rectangles can
            // expose subpixel seams when the hand rotates a card over the scene.
            const int width=128;
            var height=Mathf.Clamp(Mathf.RoundToInt(width*r.height/r.width/4f)*4,16,384);
            var cut=Mathf.Clamp(Mathf.RoundToInt(bevel/r.width*width),1,Mathf.Min(width,height)/2-1);
            var key=height*256+cut;
            if(!readableCardShapes.TryGetValue(key,out var texture))
            {
                if(readableCardShapes.Count>=256){foreach(var old in readableCardShapes.Values)Destroy(old);readableCardShapes.Clear();}
                texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name="Card UI bevel "+key,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[width*height];
                for(var y=0;y<height;y++)for(var x=0;x<width;x++)
                {
                    var distance=Mathf.Min(Mathf.Min(x+y,width-1-x+y),Mathf.Min(x+height-1-y,width+height-2-x-y))-cut;
                    pixels[y*width+x]=new Color(1,1,1,Mathf.Clamp01(distance*.7071f+.5f));
                }
                texture.SetPixels(pixels);texture.Apply(false,true);readableCardShapes[key]=texture;
            }
            DrawCardUiShape(r,texture,color);
        }
        private void DrawReadableCard(Rect r,CardDef card,int copies,int upgrades)
        {
            // Three independent signals: character on the frame, rarity on the
            // title ribbon, and card type on the small neutral artwork plaque.
            var accent=CardCharacterColor(card);
            if(drawingDisabledHandCard){var grey=accent.grayscale;accent=Color.Lerp(accent,new Color(grey,grey,grey),.78f)*.79f;accent.a=1;}
            var edge=Color.Lerp(accent,Color.white,IsCurseCard(card)?.12f:.32f);
            var corner=Mathf.Clamp(r.width*.062f,7,22);var border=Mathf.Max(2,r.width*.012f);
            FillCardSilhouette(r,Color.Lerp(accent,Color.black,.48f),corner);
            FillCardSilhouette(InsetCardRect(r,border),edge,corner-1);
            FillCardSilhouette(InsetCardRect(r,border*2),accent,corner-2);
            // Broad colored rails with one restrained bevel, not filigree.
            var well=new Rect(r.x+r.width*.058f,r.y+r.height*.116f,r.width*.884f,r.height*.838f);
            FillCardSilhouette(well,Color.Lerp(accent,Color.black,.52f),corner*.48f);
            FillCardSilhouette(InsetCardRect(well,Mathf.Max(1,r.width*.006f)),IsCurseCard(card)?new Color(.21f,.20f,.23f):new Color(.52f,.50f,.53f),corner*.40f);
            FillCardSilhouette(InsetCardRect(well,Mathf.Max(2,r.width*.012f)),CardRulesPanelColor(card),corner*.35f);
            var art=CardArtArea(r);var rules=CardRuleArea(r);
            var artworkColor=GUI.color;if(drawingDisabledHandCard)GUI.color=artworkColor*new Color(.58f,.61f,.66f,1);
            DrawCardArtwork(art,card);GUI.color=artworkColor;
            DrawReadableArtBezel(r,art,card);
            DrawAfflictionFrame(r,card);
            DrawReadableTitleRibbon(r,card);
            var name=ReadableCardName(r,card);
            var oldNameColor=name.style.normal.textColor;name.style.normal.textColor=ReadableCardTitleColor(card);
            DrawReadableText(CardNameArea(r),name);name.style.normal.textColor=oldNameColor;
            var live=screen==ScreenMode.Combat&&combat!=null&&combat.hand.Contains(card)?CardPreview(card):null;
            var normalCost=Mathf.Max(0,card.cost-card.perfectedCostReduction);
            var cost=live?.cost??normalCost;var changed=cost!=normalCost;
            var costColor=live!=null&&!live.playable?new Color(1f,.54f,.48f):changed?(cost<normalCost?new Color(.55f,1f,.65f):new Color(1f,.54f,.48f)):new Color(1f,.98f,.9f);
            if(card.ShowsEnergyCost)
            {
            if(comparisonBaseRendering!=null&&card.cost!=comparisonBaseRendering.cost)costColor=new Color(.51f,.93f,.63f);
            var badge=new Rect(r.x+r.width*.018f,r.y+r.height*.026f,r.width*.17f,r.width*.17f);
            DrawIdentityEnergy(new Rect(badge.x-3,badge.y-3,badge.width+6,badge.height+6),IdentityEnergyIndex(card.origin));
            var costStyle=ReadableStyle(Mathf.Clamp(Mathf.RoundToInt(badge.width*.53f),16,34),true);costStyle.normal.textColor=costColor;GUI.Label(badge,cost.ToString(),costStyle);
            }
            DrawReadableTypeBanner(r,card);
            DrawReadableText(rules,ReadableCardRules(r,card,LiveCardRules(card,live)));
            var marks=(copies>1?$"×{copies} ":"")+(upgrades>0?$"{upgrades}★":card.upgraded?"★":"");
            if(marks.Length>0){var s=ReadableStyle(Mathf.Clamp(Mathf.RoundToInt(r.width*.05f),9,17),true);s.normal.textColor=edge;GUI.Label(new Rect(r.x+r.width*.70f,r.yMax-r.height*.05f,r.width*.23f,r.height*.038f),marks,s);}
        }
        private void DrawReadableTypeBanner(Rect r,CardDef card)
        {
            var banner=new Rect(r.center.x-r.width*.15f,r.y+r.height*.485f,r.width*.30f,r.height*.063f);
            FillCardSilhouette(new Rect(banner.x,banner.y+2,banner.width,banner.height),new Color(.13f,.13f,.15f),3);
            FillCardSilhouette(banner,new Color(.68f,.67f,.69f),3);
            FillCardSilhouette(InsetCardRect(banner,Mathf.Max(1,r.width*.005f)),new Color(.48f,.48f,.51f),2);
            var type=card.rarity==Rarity.Curse?"CURSE":card.rarity==Rarity.Status?"STATUS":CardTypeLabel(card);
            var s=ReadableStyle(Mathf.Clamp(Mathf.RoundToInt(r.width*.057f),10,22),true);s.normal.textColor=new Color(1f,.98f,.92f);GUI.Label(banner,type,s);
        }

        private static Rect InsetCardRect(Rect r,float amount)=>new(r.x+amount,r.y+amount,r.width-amount*2,r.height-amount*2);
        private static bool IsCurseCard(CardDef card)=>card!=null&&(card.origin==CardOrigin.Curse||card.rarity==Rarity.Curse);
        private static bool IsStatusCard(CardDef card)=>card!=null&&(card.origin==CardOrigin.Status||card.kind==CardKind.Status||card.rarity==Rarity.Status);
        private static Color ReadableCardTitleColor(CardDef card)=>IsCurseCard(card)||IsStatusCard(card)||card.rarity==Rarity.Uncommon?new Color(.97f,.98f,.94f):new Color(.11f,.12f,.14f);
        private static Color CardCharacterColor(CardDef card)=>IsCurseCard(card)?new Color(.065f,.059f,.078f):IsStatusCard(card)?Color.Lerp(new Color(.31f,.34f,.35f),StatusAccent(card.id),.17f):card.origin switch
        {
            CardOrigin.Knight=>new Color(.43f,.16f,.19f),
            CardOrigin.Arcane=>new Color(.40f,.28f,.60f),
            CardOrigin.Reaper=>new Color(.16f,.48f,.45f),
            CardOrigin.Wanderer=>new Color(.53f,.46f,.32f),
            CardOrigin.Curse=>new Color(.31f,.24f,.37f),
            _=>new Color(.39f,.41f,.44f)
        };
        private static Color CardTitleRarityColor(CardDef card)=>IsCurseCard(card)?new Color(.10f,.095f,.12f):IsStatusCard(card)?new Color(.24f,.27f,.28f):card.rarity switch
        {
            Rarity.Uncommon=>new Color(.12f,.46f,.32f),
            Rarity.Rare or Rarity.Boss=>new Color(.95f,.73f,.29f),
            // Basic, generated, Curse and Status cards do not imply a rare drop.
            _=>new Color(.98f,.97f,.90f)
        };

        private Texture2D readableGildedRibbon;
        private void DrawReadableTitleRibbon(Rect r,CardDef card)
        {
            if(!readableTitleRibbon)readableTitleRibbon=CreateCardUiPolygon("Card title ribbon",384,96,new[]{new Vector2(0,.28f),new Vector2(.09f,.07f),new Vector2(.50f,0),new Vector2(.91f,.07f),new Vector2(1,.28f),new Vector2(.96f,.98f),new Vector2(.50f,.89f),new Vector2(.04f,.98f)},true);
            var ribbon=new Rect(r.x+r.width*.018f,r.y+r.height*.018f,r.width*.964f,r.height*.124f);
            DrawCardUiShape(new Rect(ribbon.x,ribbon.y+2,ribbon.width,ribbon.height),readableTitleRibbon,new Color(.12f,.12f,.14f));
            DrawCardUiShape(ribbon,readableTitleRibbon,Color.Lerp(CardTitleRarityColor(card),Color.white,IsCurseCard(card)?.12f:.35f));
            var material=readableTitleRibbon;
            if(!IsCurseCard(card)&&!IsStatusCard(card)&&(card.rarity==Rarity.Rare||card.rarity==Rarity.Boss))
            {
                if(!readableGildedRibbon)readableGildedRibbon=CreateCardUiPolygon("Card gilded ribbon",384,96,new[]{new Vector2(0,.28f),new Vector2(.09f,.07f),new Vector2(.50f,0),new Vector2(.91f,.07f),new Vector2(1,.28f),new Vector2(.96f,.98f),new Vector2(.50f,.89f),new Vector2(.04f,.98f)},true);
                material=readableGildedRibbon;
            }
            DrawCardUiShape(InsetCardRect(ribbon,Mathf.Max(1,r.width*.006f)),material,CardTitleRarityColor(card));
        }

        private void DrawReadableArtBezel(Rect r,Rect art,CardDef card)
        {
            // A shallow shield-shaped lower edge gives the image a crafted inset.
            // Opaque UI triangles trim only the image corners; rules remain clear.
            if(!readableArtCut)readableArtCut=CreateCardUiPolygon("Card art corner",256,64,new[]{new Vector2(0,0),new Vector2(1,1),new Vector2(0,1)},false);
            var depth=r.height*.050f;var left=new Rect(art.x,art.yMax-depth,art.width*.5f,depth);
            DrawCardUiShape(left,readableArtCut,CardRulesPanelColor(card));
            DrawCardUiShape(new Rect(art.center.x,left.y,left.width,left.height),readableArtCut,CardRulesPanelColor(card),true);
            if(!readableArtRim)readableArtRim=CreateCardUiPolygon("Card silver art rim",512,96,new[]{new Vector2(0,0),new Vector2(.5f,.79f),new Vector2(1,0),new Vector2(1,.21f),new Vector2(.5f,1),new Vector2(0,.21f)},true);
            var silver=IsCurseCard(card)?new Color(.26f,.25f,.30f):new Color(.62f,.61f,.64f);var dark=new Color(.16f,.16f,.18f);var width=Mathf.Max(2,r.width*.017f);
            // Keep every bezel element in the card's local space. Rotating line
            // matrices inside a clipped collection group displaces the geometry.
            var sideHeight=art.height-depth+1;
            Fill(new Rect(art.x-width*.5f-1,art.y,width+2,sideHeight),dark);
            Fill(new Rect(art.xMax-width*.5f-1,art.y,width+2,sideHeight),dark);
            DrawCardUiShape(new Rect(art.x-1,art.yMax-depth,art.width+2,depth+2),readableArtRim,dark);
            Fill(new Rect(art.x-width*.5f,art.y,width,sideHeight),silver);
            Fill(new Rect(art.xMax-width*.5f,art.y,width,sideHeight),silver);
            DrawCardUiShape(new Rect(art.x,art.yMax-depth,art.width,depth),readableArtRim,silver);
        }

        private static void DrawCardUiShape(Rect r,Texture2D texture,Color tint,bool flip=false)
        {
            var previous=GUI.color;GUI.color=tint;
            GUI.DrawTextureWithTexCoords(r,texture,flip?new Rect(1,0,-1,1):new Rect(0,0,1,1));GUI.color=previous;
        }
        private static Texture2D CreateCardUiPolygon(string name,int width,int height,Vector2[] vertices,bool shaded)
        {
            // Cached, anti-aliased code-native UI geometry, not generated artwork.
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false){name=name,hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[width*height];
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                var coverage=0f;
                for(var sy=0;sy<2;sy++)for(var sx=0;sx<2;sx++)
                {
                    var point=new Vector2((x+(sx+.5f)*.5f)/width,1f-(y+(sy+.5f)*.5f)/height);var inside=false;
                    for(int i=0,j=vertices.Length-1;i<vertices.Length;j=i++)
                    {
                        var a=vertices[i];var b=vertices[j];
                        if((a.y>point.y)!=(b.y>point.y)&&point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                    }
                    if(inside)coverage+=.25f;
                }
                var light=shaded?Mathf.Lerp(.87f,1f,y/(float)(height-1)):1f;
                if(name=="Card title ribbon")
                {
                    var v=y/(float)(height-1);
                    var grain=((x*17+y*71)%31)/30f;
                    light=Mathf.Clamp01(.90f+.075f*Mathf.Sin(v*Mathf.PI*3)+grain*.025f);
                    if(v>.82f)light=1f; // restrained polished lip, not a flat colored label
                }
                if(name=="Card gilded ribbon")
                {
                    var v=y/(float)(height-1);var grain=((x*17+y*71)%31)/30f;
                    light=.77f+.15f*v+Mathf.Exp(-Mathf.Pow((v-.75f)*13,2))*.17f+grain*.02f;
                    if(v>.92f)light=.69f; // forged return beneath the bright polished bevel
                }
                pixels[y*width+x]=new Color(light,light,light,coverage);
            }
            texture.SetPixels(pixels);texture.Apply(false,true);return texture;
        }

        private void AuditCardFrameLanguage()
        {
            var checks=0;var failures=0;
            foreach(var card in GameContent.Cards)
            {
                var copy=card.Copy();var original=CardCharacterColor(card);
                foreach(var rarity in new[]{Rarity.Common,Rarity.Uncommon,Rarity.Rare})
                {
                    copy.rarity=rarity;copy.upgraded=!card.upgraded;checks++;
                    if(CardCharacterColor(copy)!=original)failures++;
                }
            }
            var sample=GameContent.Cards[0].Copy();
            foreach(var pair in new[]{(Rarity.Common,new Color(.98f,.97f,.90f)),(Rarity.Uncommon,new Color(.12f,.46f,.32f)),(Rarity.Rare,new Color(.95f,.73f,.29f))})
            {sample.rarity=pair.Item1;checks++;if(CardTitleRarityColor(sample)!=pair.Item2)failures++;}
            Debug.Log($"[Gilded Fate Card Style] {checks} character/rarity color checks · {failures} failures");
            if(failures>0)throw new InvalidOperationException("Card color language regression.");
        }
    }
}
