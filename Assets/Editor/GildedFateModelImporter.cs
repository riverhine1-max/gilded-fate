using UnityEditor;

namespace GildedFate.Editor
{
    // Each source FBX contains one authored motion timeline. Import it as named
    // Legacy clips so the runtime can cross-fade without generated controllers.
    public sealed class GildedFateModelImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            var vanguard=assetPath=="Assets/Resources/Models/Vanguard.fbx";
            var hexer=assetPath=="Assets/Resources/Models/Hexer.fbx";
            var gildedSentry=assetPath=="Assets/Resources/Models/Enemies/GildedSentry.fbx";
            var mirrorWitch=assetPath=="Assets/Resources/Models/Enemies/MirrorWitch.fbx";
            if(!vanguard&&!hexer&&!gildedSentry&&!mirrorWitch)return;
            var importer=(ModelImporter)assetImporter;
            importer.importAnimation=true;
            importer.animationType=ModelImporterAnimationType.Legacy;
            importer.animationCompression=ModelImporterAnimationCompression.Optimal;
            var take=vanguard?"Vanguard_Authored_Motion":hexer?"Hexer_Authored_Motion":gildedSentry?"Gilded_Sentry_Authored_Motion":"Mirror_Witch_Authored_Motion";
            importer.clipAnimations=vanguard?new[]
            {
                Clip(take,"Idle",0,90,true),Clip(take,"Attack",100,132,false),Clip(take,"Guard",145,180,false),
                Clip(take,"Cast",195,235,false),Clip(take,"Hit",250,273,false),Clip(take,"Buff",285,325,false),
                Clip(take,"Death",340,390,false),Clip(take,"Victory",405,460,false)
            }:hexer?new[]
            {
                Clip(take,"Idle",0,90,true),Clip(take,"Attack",100,134,false),Clip(take,"Guard",145,182,false),
                Clip(take,"Cast",195,238,false),Clip(take,"Hit",250,274,false),Clip(take,"Buff",286,329,false),
                Clip(take,"Death",340,394,false),Clip(take,"Victory",406,464,false)
            }:gildedSentry?new[]
            {
                Clip(take,"Idle",0,90,true),Clip(take,"Attack",100,150,false),Clip(take,"Defend",165,198,false),
                Clip(take,"Hit",210,236,false),Clip(take,"Buff",250,286,false),Clip(take,"Debuff",300,334,false),
                Clip(take,"Special",345,388,false),Clip(take,"Death",400,455,false)
            }:new[]
            {
                Clip(take,"Idle",0,90,true),Clip(take,"Attack",100,146,false),Clip(take,"Defend",160,196,false),
                Clip(take,"Hit",208,234,false),Clip(take,"Buff",246,282,false),Clip(take,"Debuff",294,334,false),
                Clip(take,"Special",346,392,false),Clip(take,"Death",404,458,false)
            };
        }

        private static ModelImporterClipAnimation Clip(string take,string name,float first,float last,bool loop)
        {
            return new ModelImporterClipAnimation
            {name=name,takeName=take,firstFrame=first,lastFrame=last,loopTime=loop,loopPose=loop};
        }
    }
}
