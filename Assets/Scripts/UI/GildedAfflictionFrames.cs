using System.Collections.Generic;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        // Restrained, code-native engravings; they never enter the rules well.
        private readonly Dictionary<string,Texture2D> afflictionEngravings=new();
        private static Color StatusAccent(string id)=>id switch
        {
            "dazed_mind"=>new Color(.68f,.69f,.87f),"shattered_guard"=>new Color(.57f,.75f,.83f),
            "falter"=>new Color(.70f,.64f,.52f),"heavy_chains"=>new Color(.59f,.63f,.66f),
            "misfortune"=>new Color(.68f,.59f,.71f),"haunting"=>new Color(.53f,.76f,.68f),
            "fractured_will"=>new Color(.70f,.61f,.79f),"arcane_lock"=>new Color(.60f,.66f,.86f),
            "rust"=>new Color(.78f,.49f,.30f),"fatebound"=>new Color(.76f,.67f,.41f),
            "lost_moment"=>new Color(.57f,.73f,.75f),"spirit_scar"=>new Color(.78f,.49f,.57f),
            "twisted_fate"=>new Color(.60f,.73f,.56f),_=>new Color(.65f,.68f,.70f)
        };
        private static Vector2[] StatusEngraving(string id)=>id switch
        {
            // Disconnected thought/eye, broken shield, falling steps, chain links,
            // broken fortune star, ghost, split resolve, seal, corrosion, knots,
            // hourglass, wound and twisted strands, respectively.
            "dazed_mind"=>new[]{new Vector2(0,.5f),new Vector2(.3f,.15f),new Vector2(.55f,.45f),new Vector2(.4f,.75f),new Vector2(.85f,.65f),new Vector2(1,.25f)},
            "shattered_guard"=>new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(.85f,.6f),new Vector2(.5f,1),new Vector2(.2f,.6f),new Vector2(.45f,.45f),new Vector2(.25f,.2f)},
            "falter"=>new[]{new Vector2(0,0),new Vector2(.3f,0),new Vector2(.3f,.35f),new Vector2(.65f,.35f),new Vector2(.65f,.7f),new Vector2(1,1)},
            "heavy_chains"=>new[]{new Vector2(.25f,0),new Vector2(.7f,.15f),new Vector2(.7f,.5f),new Vector2(.25f,.65f),new Vector2(0,.35f),new Vector2(.25f,0),new Vector2(.75f,.35f),new Vector2(1,.65f),new Vector2(.75f,1),new Vector2(.3f,.85f),new Vector2(.3f,.5f)},
            "misfortune"=>new[]{new Vector2(.5f,0),new Vector2(.65f,.4f),new Vector2(1,.5f),new Vector2(.6f,.7f),new Vector2(.4f,.45f),new Vector2(0,.6f),new Vector2(.25f,.3f)},
            "haunting"=>new[]{new Vector2(0,1),new Vector2(.15f,.25f),new Vector2(.5f,0),new Vector2(.85f,.25f),new Vector2(1,1),new Vector2(.65f,.75f),new Vector2(.5f,1),new Vector2(.3f,.75f)},
            "fractured_will"=>new[]{new Vector2(.25f,0),new Vector2(.65f,.3f),new Vector2(.35f,.55f),new Vector2(.75f,1),new Vector2(.8f,.55f),new Vector2(1,.3f)},
            "arcane_lock"=>new[]{new Vector2(.2f,.5f),new Vector2(.2f,.2f),new Vector2(.5f,0),new Vector2(.8f,.2f),new Vector2(.8f,.5f),new Vector2(1,.5f),new Vector2(1,1),new Vector2(0,1),new Vector2(0,.5f),new Vector2(.8f,.5f)},
            "rust"=>new[]{new Vector2(0,.2f),new Vector2(.15f,.7f),new Vector2(.4f,.4f),new Vector2(.55f,1),new Vector2(.7f,.5f),new Vector2(.85f,.7f),new Vector2(1,0)},
            "fatebound"=>new[]{new Vector2(0,.5f),new Vector2(.35f,0),new Vector2(.7f,.5f),new Vector2(.35f,1),new Vector2(0,.5f),new Vector2(.65f,.5f),new Vector2(1,0),new Vector2(1,1),new Vector2(.65f,.5f)},
            "lost_moment"=>new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(.5f,.5f),new Vector2(1,1),new Vector2(0,1),new Vector2(.5f,.5f),new Vector2(0,0)},
            "spirit_scar"=>new[]{new Vector2(.2f,0),new Vector2(.55f,.3f),new Vector2(.3f,.55f),new Vector2(.75f,1),new Vector2(.6f,.6f),new Vector2(.9f,.35f)},
            "twisted_fate"=>new[]{new Vector2(0,0),new Vector2(.8f,.25f),new Vector2(.2f,.75f),new Vector2(1,1),new Vector2(1,0),new Vector2(.2f,.25f),new Vector2(.8f,.75f),new Vector2(0,1)},
            _=>new[]{new Vector2(0,0),new Vector2(.6f,.25f),new Vector2(.3f,.55f),new Vector2(1,1)}
        };
        private void DrawAfflictionFrame(Rect r,CardDef card)
        {
            if(!IsStatusCard(card)&&!IsCurseCard(card))return;
            var curse=IsCurseCard(card);var key=curse?"curse":card.id;
            if(!afflictionEngravings.TryGetValue(key,out var glyph))
            {
                var path=StatusEngraving(key);const int n=96;var pixels=new Color[n*n];
                for(var y=0;y<n;y++)for(var x=0;x<n;x++)
                {
                    var p=new Vector2((x+.5f)/n,1-(y+.5f)/n);var distance=1f;
                    for(var j=1;j<path.Length;j++){var a=path[j-1];var d=path[j]-a;var t=Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.0001f,d.sqrMagnitude));distance=Mathf.Min(distance,(p-a-d*t).magnitude);}
                    pixels[y*n+x]=new Color(1,1,1,Mathf.Clamp01((.035f-distance)*n));
                }
                glyph=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Affliction engraving "+key,hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};glyph.SetPixels(pixels);glyph.Apply(false,true);afflictionEngravings[key]=glyph;
            }
            var tint=curse?new Color(.32f,.25f,.37f,.65f):StatusAccent(card.id);
            // Broken rails give all Status cards the same distressed family.
            foreach(var side in new[]{.026f,.959f})for(var i=0;i<4;i++)
            {
                var y=r.y+r.height*(.22f+i*.175f);
                Fill(new Rect(r.x+r.width*side,y,r.width*.018f,r.height*.019f),Color.Lerp(CardCharacterColor(card),Color.black,.6f));
                DrawCardUiShape(new Rect(r.x+r.width*(side-.006f),y-r.height*.013f,r.width*.028f,r.height*.10f),glyph,tint);
            }
            // Signature sits in the artwork corner, never over the description.
            var plate=new Rect(r.x+r.width*.095f,r.y+r.height*.157f,r.width*.18f,r.width*.18f);
            FillCardSilhouette(plate,new Color(.08f,.09f,.10f,.91f),3);
            DrawCardUiShape(InsetCardRect(plate,r.width*.025f),glyph,tint);
            DrawCardUiShape(new Rect(r.center.x-r.width*.055f,r.y+r.height*.958f,r.width*.11f,r.height*.027f),glyph,tint);
        }
    }
}
