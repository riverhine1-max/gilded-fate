using GildedFate.Combat;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        // The clasp straddles only the left frame rail, below the cost and clear
        // of the art, title and rules. Its size follows the card in every view.
        private static Rect CardBindingClaspRect(Rect card)
        {
            var size=card.width*.15f;
            return new Rect(card.x-size*.52f,card.y+card.height*.265f-size*.5f,size,size);
        }

        private bool CardHelpContains(Rect card,CardDef definition,Vector2 point)
            =>card.Contains(point)||AttachmentContains(card,definition,point);

        private bool HandCardContains(Vector2 point,CardDef card,float x,float y,float angle,float scale=1)
        {
            // Keep the existing card-body hit test exactly as it was. Only a
            // modified copy gains the tiny extra clickable area of its clasp.
            if(HandLayout.Contains(point.x,point.y,x,y,angle,scale))return true;
            if(card==null||scale<=0)return false;
            var local=HandCardLocalPoint(point,x,y,angle,scale);
            return AttachmentContains(new Rect(-HandLayout.CardWidth*.5f,-HandLayout.CardHeight*.5f,HandLayout.CardWidth,HandLayout.CardHeight),card,local);
        }

        private static Vector2 HandCardLocalPoint(Vector2 point,float x,float y,float angle,float scale)
        {
            var radians=-angle*Mathf.Deg2Rad;var delta=(point-new Vector2(x,y))/scale;
            return new Vector2(delta.x*Mathf.Cos(radians)-delta.y*Mathf.Sin(radians),delta.x*Mathf.Sin(radians)+delta.y*Mathf.Cos(radians));
        }

        private void DrawCardBindingClasp(Rect card,CardDef definition)
        {
            if(definition?.IsModified!=true)return;
            var r=CardBindingClaspRect(card);var size=r.width;
            var fate=definition.specialModificationKind==SpecialModificationKind.Fateweave;
            var light=fate?new Color(1f,.77f,.33f):new Color(.38f,.88f,1f);
            var metal=new Color(.79f,.62f,.32f);var dark=new Color(.10f,.075f,.035f);
            // Two little gilt stitches fasten the jewel to the rail. These are
            // local-space quads, so scrolling and card rotation cannot detach them.
            foreach(var offset in new[]{-.27f,.27f})
            {
                var stitch=new Rect(r.center.x, r.center.y+size*offset-size*.055f,size*.61f,size*.11f);
                FillCardSilhouette(stitch,dark,size*.025f);
                FillCardSilhouette(InsetCardRect(stitch,size*.025f),metal,size*.02f);
            }
            FillCardSilhouette(r,dark,size*.25f);
            FillCardSilhouette(InsetCardRect(r,size*.035f),metal,size*.23f);
            FillCardSilhouette(InsetCardRect(r,size*.075f),light,size*.20f);
            FillCardSilhouette(InsetCardRect(r,size*.11f),fate?new Color(.13f,.073f,.02f):new Color(.018f,.065f,.09f),size*.18f);
            DrawAtlasIcon(bindingFateIconAtlas,ModificationIconIndex(definition),4,4,InsetCardRect(r,size*.075f));
            // A restrained gem at the clasp tip distinguishes bound / fatewoven
            // cards even when their effect uses the same sword or shield symbol.
            var gem=new Rect(r.center.x-size*.055f,r.yMax-size*.10f,size*.11f,size*.11f);
            FillCardSilhouette(gem,light,size*.045f);
        }
    }
}
