using System;
using System.IO;
using UnityEngine;
namespace GildedFate.UI
{
    public static class ReaperVideoCatalog
    {
        public sealed class Clip
        {
            public readonly int Index;
            public readonly string Name;
            public readonly float Duration;
            public Clip(int index,string name,float duration){Index=index;Name=name;Duration=duration;}
            public string Path=>System.IO.Path.Combine(Application.streamingAssetsPath,"Animations","Reaper",Index.ToString("00")+"_"+Name+".mp4");
            public string Url=>new Uri(Path).AbsoluteUri;
            // Fixed padded source framing; a consistent ground anchor for all actions.
            public Rect DrawRect(Rect actor){var size=actor.height/.82f;return new Rect(actor.center.x-size*.50f,actor.yMax-size*.922f,size,size);}
        }
        public static readonly Clip[] Clips={
            new Clip(1,"CombatIdle",4),
            new Clip(2,"BasicScytheAttack",3),
            new Clip(3,"AlternateScytheAttack",3),
            new Clip(4,"HeavyAttack",4),
            new Clip(5,"MajorFinisherAttack",4),
            new Clip(6,"DefensiveAnimation",3),
            new Clip(7,"SoulGeneration",3),
            new Clip(8,"SoulConsumption",3),
            new Clip(9,"GraveMarkInteraction",3),
            new Clip(10,"AspectActivation",3),
            new Clip(11,"Buff",3),
            new Clip(12,"Dissipate",3),
            new Clip(13,"LightHit",3),
            new Clip(14,"HeavyHit",3),
            new Clip(15,"LowHPIdle",4),
            new Clip(16,"Defeat",4),
        };
        public static Clip Find(string name)=>Array.Find(Clips,c=>c.Name==name);
        public static bool Available=>File.Exists(Clips[0].Path);
    }
}
