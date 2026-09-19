using System;
using System.IO;
using UnityEngine;
namespace GildedFate.UI
{
    public static class VanguardVideoCatalog
    {
        public sealed class Clip
        {
            public readonly int Index;
            public readonly string Name;
            public readonly float Duration;
            public Clip(int index,string name,float duration){Index=index;Name=name;Duration=duration;}
            public string Path=>System.IO.Path.Combine(Application.streamingAssetsPath,"Animations","Vanguard",Index.ToString("00")+"_"+Name+".mp4");
            public string Url=>new Uri(Path).AbsoluteUri;
            // Fixed reference-frame geometry: padded canvas, no per-frame auto-crop.
            public Rect DrawRect(Rect actor){var size=actor.height/.70f;return new Rect(actor.center.x-size*.50f,actor.yMax-size*.872f,size,size);}
        }
        public static readonly Clip[] Clips={
            new Clip(1,"CombatIdle",4),
            new Clip(2,"BasicAttack",3),
            new Clip(3,"AlternateAttack",3),
            new Clip(4,"HeavyAttack",4),
            new Clip(5,"MajorFinisherAttack",4),
            new Clip(6,"Block",3),
            new Clip(7,"BlockImpact",3),
            new Clip(8,"MajorDefense",4),
            new Clip(9,"LightHit",3),
            new Clip(10,"HeavyHitStagger",3),
            new Clip(11,"AspectActivation",3),
            new Clip(12,"BuffStrength",3),
            new Clip(13,"Fortify",3),
            new Clip(14,"RetaliateReady",3),
            new Clip(15,"RetaliateAttack",3),
            new Clip(16,"LowHPIdle",4),
            new Clip(17,"Defeat",4),
        };
        public static Clip Find(string name)=>Array.Find(Clips,c=>c.Name==name);
        public static bool Available=>File.Exists(Clips[0].Path);
    }
}
