using System;
using System.IO;
using System.Linq;
using GildedFate.ChronicleBook;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GildedFate.Editor
{
    [InitializeOnLoad]
    public static class ChronicleFinishedBookAssets
    {
        const string Root="Assets/Resources/Chronicle";
        const string PrefabPath=Root+"/ChronicleBook.prefab";
        const string ConfigPath=Root+"/ChronicleBookRigConfig.asset";
        static bool building;
        static ChronicleFinishedBookAssets() => EditorApplication.delayCall += Ensure;

        public static void Ensure()
        {
            if (building || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Ensure;
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) && AssetDatabase.LoadAssetAtPath<ChronicleBookRigConfig>(ConfigPath)) return;
            if (!ChronicleBookBinaryRestore.EnsureModel()) return;
            building=true;
            try { Build(); } finally { building=false; }
        }

        [MenuItem("Gilded Fate/Chronicle/Rebuild Finished Book Assets")]
        public static void Rebuild()
        {
            if (building || EditorApplication.isPlayingOrWillChangePlaymode) return;
            AssetDatabase.DeleteAsset(PrefabPath);
            Ensure();
        }

        static Material Material(string name,string set,Shader shader)
        {
            var path=Root+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material=new Material(shader) { name=name }; AssetDatabase.CreateAsset(material,path); }
            var prefix=Root+"/Textures/"+set+"/CHR_"+set+"_";
            foreach(var pair in new[]{new[]{"_MainTex","BaseColor"},new[]{"_BumpMap","Normal"},new[]{"_Mask","UnityMask"}})
            {
                var texturePath=prefix+pair[1]+".png";
                if(set=="Parchment" && pair[1]=="BaseColor")texturePath=Root+"/Textures/Parchment/CHR_Weathered_BaseColor.png";
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if(!texture && File.Exists(texturePath))
                {
                    AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                    texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                }
                if(!texture)throw new BuildFailedException("Missing Chronicle texture: "+texturePath+" (asset type: "+AssetDatabase.GetMainAssetTypeAtPath(texturePath)+")");
                material.SetTexture(pair[0],texture);
            }
            material.SetFloat("_PaperRelief",set=="Parchment"?.65f:0f);
            if(set=="Parchment")material.SetFloat("_NormalStrength",.22f);
            if(set=="Emerald")
            {
                material.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(prefix+"Emission.png"));
                material.SetColor("_EmissionColor",new Color(.06f,.12f,.09f));
            }
            EditorUtility.SetDirty(material);return material;
        }

        static Transform Find(Transform root,string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        static void Anchor(GameObject root,string name,string surface,string bone)
        {
            var renderer=Find(root.transform,surface).GetComponent<SkinnedMeshRenderer>();
            var mesh=new Mesh();renderer.BakeMesh(mesh);
            var bounds=mesh.bounds;
            var go=new GameObject(name);
            go.transform.position=renderer.transform.TransformPoint(bounds.center)+Vector3.up*.0007f;
            go.transform.rotation=Quaternion.identity;
            go.transform.SetParent(Find(root.transform,bone),true);
            UnityEngine.Object.DestroyImmediate(mesh);
        }

        static void Build()
        {
            AssetDatabase.ImportAsset(ChronicleFinishedBookImporter.ModelPath,ImportAssetOptions.ForceSynchronousImport);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ChronicleFinishedBookImporter.ModelPath);
            if(!model)throw new BuildFailedException("Chronicle FBX could not be imported.");
            var clips=AssetDatabase.LoadAllAssetsAtPath(ChronicleFinishedBookImporter.ModelPath).OfType<AnimationClip>()
                .Where(c=>!c.name.StartsWith("__preview__",StringComparison.Ordinal) && c.name.IndexOf("CHR_",StringComparison.Ordinal)>=0)
                .ToDictionary(c=>c.name.Substring(c.name.LastIndexOf("CHR_",StringComparison.Ordinal)));
            var required=new[]{"CHR_Open","CHR_Close","CHR_PageTurn_RL","CHR_PageTurn_LR","CHR_PageTurn_Cinematic_RL","CHR_Idle_Open","CHR_Reaction_Subtle","CHR_Pose_Open","CHR_Pose_Closed"};
            foreach(var key in required)if(!clips.ContainsKey(key))throw new BuildFailedException("Chronicle FBX is missing clip "+key+". Imported clips: "+string.Join(", ",AssetDatabase.LoadAllAssetsAtPath(ChronicleFinishedBookImporter.ModelPath).OfType<AnimationClip>().Select(c=>c.name)));
            Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"/ChronicleSurface.shader");
            if(!shader || ShaderUtil.ShaderHasError(shader))throw new BuildFailedException("Chronicle surface shader could not compile.");
            var leather=Material("Leather","Leather",shader);var gold=Material("Gold","Gold",shader);
            var paper=Material("Parchment_Front","Parchment",shader);var back=Material("Parchment_Back","Parchment",shader);
            var edges=Material("GildedEdges","GildedEdges",shader);var emerald=Material("Emerald","Emerald",shader);var ribbon=Material("Bookmark","Bookmark",shader);
            var controllerPath=Root+"/ChronicleBook.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var sm=controller.layers[0].stateMachine;
            foreach(var old in sm.states)sm.RemoveState(old.state);
            var states=required.ToDictionary(key=>key,key=>{var state=sm.AddState(key);state.motion=clips[key];state.writeDefaultValues=true;return state;});
            sm.defaultState=states["CHR_Pose_Closed"];
            foreach(var pair in new[]{new[]{"CHR_Open","CHR_Idle_Open"},new[]{"CHR_Close","CHR_Pose_Closed"},new[]{"CHR_Reaction_Subtle","CHR_Idle_Open"}})
            {
                var transition=states[pair[0]].AddTransition(states[pair[1]]);
                transition.hasExitTime=true;transition.exitTime=1;transition.hasFixedDuration=true;transition.duration=0;
            }
            var host=new GameObject("ChronicleBook");
            try
            {
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,host.transform);
                clips["CHR_Pose_Open"].SampleAnimation(instance,0);
                var surface=Find(instance.transform,"PageSurface_Right").GetComponent<SkinnedMeshRenderer>();
                var baked=new Mesh();surface.BakeMesh(baked);
                var up=surface.transform.TransformDirection(baked.normals[0]);
                // Keep the bookmark at the reader's bottom and the closed spine on the left.
                host.transform.rotation=Quaternion.AngleAxis(180f,Vector3.up)*Quaternion.FromToRotation(up,Vector3.up)*host.transform.rotation;
                UnityEngine.Object.DestroyImmediate(baked);
                foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var name=renderer.name;
                    Material mat=name.StartsWith("Cover_") || name.StartsWith("Spine_Hinge") || name=="Spine_Shell" ? leather
                        : name.StartsWith("PageStack_") ? edges : name=="Bookmark_Ribbon" ? ribbon
                        : name.Contains("Enamel") || name=="Emblem_GemInsets" ? emerald : gold;
                    if(name.StartsWith("PageSurface_") || name=="TurningPage_01")renderer.sharedMaterials=new[]{paper,back};
                    else renderer.sharedMaterials=new[]{mat};
                    renderer.updateWhenOffscreen=true;
                }
                Anchor(instance,"PageAnchor_Left","PageSurface_Left","Cover_Front");
                Anchor(instance,"PageAnchor_Right","PageSurface_Right","Cover_Back");
                var animator=instance.GetComponentInChildren<Animator>();if(!animator)animator=instance.AddComponent<Animator>();
                animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var config=AssetDatabase.LoadAssetAtPath<ChronicleBookRigConfig>(ConfigPath);
                if(!config){config=ScriptableObject.CreateInstance<ChronicleBookRigConfig>();AssetDatabase.CreateAsset(config,ConfigPath);}
                config.openState="CHR_Open";config.closeState="CHR_Close";config.turnForwardState="CHR_PageTurn_RL";config.turnBackState="CHR_PageTurn_LR";
                config.idleState="CHR_Idle_Open";config.correctionState="CHR_Reaction_Subtle";config.majorMagicState="CHR_Reaction_Subtle";
                config.openClip=clips["CHR_Open"];config.closeClip=clips["CHR_Close"];config.turnForwardClip=clips["CHR_PageTurn_RL"];config.turnBackClip=clips["CHR_PageTurn_LR"];
                config.correctionClip=clips["CHR_Reaction_Subtle"];config.majorMagicClip=clips["CHR_Reaction_Subtle"];
                config.crossFadeSeconds=0;config.pageSize=new Vector2(.240f,.340f);config.cameraDistance=1.12f;config.cameraTarget=new Vector3(0,.04f,0);
                EditorUtility.SetDirty(config);
                clips["CHR_Pose_Closed"].SampleAnimation(instance,0);
                Find(instance.transform,"TurningPage_01").GetComponent<Renderer>().enabled=false;
                PrefabUtility.SaveAsPrefabAsset(host,PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Chronicle] Finished Blender book, seven texture materials, nine clips and page anchors are ready.");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
    }

    public sealed class ChronicleBookBuildPreparation : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) => ChronicleFinishedBookAssets.Ensure();
    }
}
