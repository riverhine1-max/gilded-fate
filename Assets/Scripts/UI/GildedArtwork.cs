using System.Collections.Generic;
using GildedFate.Core;
using UnityEngine;

namespace GildedFate.UI
{
    public sealed partial class GildedMainMenu
    {
        // Measured painted seams, not assumed uniform spacing. Generated sheets
        // can vary a few pixels per row; these bounds keep adjacent art out of cards.
        private static readonly float[] VanguardXTop={0,168,334,500,667,887};
        private static readonly float[] VanguardXBottom={0,177,354,532,710,887};
        private static readonly float[] VanguardY={0,182,355,523,695,888,1067,1244,1421,1598,1774};
        private static readonly float[] HexerX={0,177,354,532,710,887};
        private static readonly float[] HexerY={0,177,356,534,712,888,1064,1242,1420,1598,1774};
        private static readonly float[] ReaperX={0,162,324,486,647,810,971};
        private static readonly float[] ReaperY={0,161,323,486,646,808,971,1133,1295,1456,1619};
        private static readonly float[] WandererX={0,166,334,490,650,806,971};
        private static readonly float[] WandererY={0,180,351,521,690,856,1017,1178,1333,1481,1619};

        private static Rect AuthoredCellBounds(GildedArtCatalog.CardTile tile,Texture2D texture)
        {
            var row=tile.index/tile.sheet.columns;var column=tile.index%tile.sheet.columns;
            if(tile.sheet.resource.StartsWith("Art/Cards/Expansion/"))return new Rect(column*(float)texture.width/tile.sheet.columns,row*(float)texture.height/tile.sheet.rows,(float)texture.width/tile.sheet.columns,(float)texture.height/tile.sheet.rows);
            float[] xs,ys;
            switch(tile.sheet.resource)
            {
                case "Art/Cards/Vanguard_5x10":xs=row<5?VanguardXTop:VanguardXBottom;ys=VanguardY;break;
                case "Art/Cards/Hexer_5x10":xs=HexerX;ys=HexerY;break;
                case "Art/Cards/Reaper_6x10":xs=ReaperX;ys=ReaperY;break;
                default:xs=WandererX;ys=WandererY;break;
            }
            var sx=texture.width/xs[xs.Length-1];var sy=texture.height/ys[ys.Length-1];
            return new Rect(xs[column]*sx,ys[row]*sy,(xs[column+1]-xs[column])*sx,(ys[row+1]-ys[row])*sy);
        }
        private readonly Dictionary<string,Texture2D> authoredArt=new();
        private Texture2D LoadAuthoredArt(string resource)
        {
            if(authoredArt.TryGetValue(resource,out var texture))return texture;
            texture=Resources.Load<Texture2D>(resource);
            authoredArt.Add(resource,texture);
            if(!texture)Debug.LogWarning("[Gilded Fate Art] Missing authored illustration: "+resource);
            return texture;
        }

        private void DrawAuthoredCardArt(Rect destination,CardDef card)
        {
            if(card==null||!GildedArtCatalog.TryGetCard(card.id,out var tile))return;
            var texture=LoadAuthoredArt(tile.sheet.resource);if(!texture)return;
            var cell=AuthoredCellBounds(tile,texture);
            var cellWidth=cell.width;
            var cellHeight=cell.height;
            // A small inset avoids sampling neighboring paintings or grid seams.
            var inset=Mathf.Max(2f,Mathf.Min(cellWidth,cellHeight)*.018f);
            var width=cellWidth-inset*2;var height=cellHeight-inset*2;
            var x=cell.x+inset;
            var y=cell.y+inset;
            var aspect=destination.width/Mathf.Max(1,destination.height);
            if(width/height>aspect){var cropped=height*aspect;x+=(width-cropped)*.5f;width=cropped;}
            else{var cropped=width/aspect;y+=(height-cropped)*.5f;height=cropped;}
            GUI.DrawTextureWithTexCoords(destination,texture,new Rect(x/texture.width,1-(y+height)/texture.height,width/texture.width,height/texture.height),true);
        }

        private void DrawFloatingHero(Rect destination,HeroId hero)
        {
            var texture=LoadAuthoredArt(GildedArtCatalog.HeroResource(hero));
            if(texture)DrawFloatingTexture(destination,texture);
        }
        private void DrawFloatingEnemy(Rect destination,EnemyDef enemy)
        {
            if(enemy==null)return;
            var texture=LoadAuthoredArt(GildedArtCatalog.EnemyResource(enemy.id));
            if(texture)DrawFloatingTexture(destination,texture);
        }
        private static void DrawFloatingTexture(Rect destination,Texture2D texture)
        {
            var scale=Mathf.Min(destination.width/texture.width,destination.height/texture.height);
            var width=texture.width*scale;var height=texture.height*scale;
            // Feet stay anchored to the health-bar baseline for tall and wide actors.
            var fitted=new Rect(destination.center.x-width*.5f,destination.yMax-height,width,height);
            GUI.DrawTexture(fitted,texture,ScaleMode.StretchToFill,true);
        }
    }
}
