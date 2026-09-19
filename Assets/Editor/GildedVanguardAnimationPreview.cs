using UnityEditor;
using UnityEngine;
using GildedFate.UI;

namespace GildedFate.Editor
{
    public sealed class GildedVanguardAnimationPreview : EditorWindow
    {
        private VanguardEditorVideoSurface surface;
        private int selected;
        private bool paused,loop=true,raw;
        private float speed=1;
        private Vector2 scroll;
        [MenuItem("Gilded Fate/Animation Preview/Vanguard")]
        public static void Open()=>GetWindow<GildedVanguardAnimationPreview>("Vanguard Animations");
        private void OnEnable(){minSize=new Vector2(730,530);EditorApplication.update+=Advance;}
        private void OnDisable(){EditorApplication.update-=Advance;surface?.Dispose();surface=null;}
        private void Advance()
        {
            if(surface==null)return;
            surface.Tick(paused);Repaint();
        }
        private void Play()
        {
            // Restart must recover a failed idle decoder as well as the selected clip.
            if(surface!=null&&!string.IsNullOrEmpty(surface.Error)){surface.Dispose();surface=null;}
            surface??=new VanguardEditorVideoSurface();paused=false;
            surface.Play(VanguardVideoCatalog.Clips[selected].Name,VanguardVideoCatalog.Clips[selected].Duration/speed,loop);
        }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("VANGUARD · ANIMATION PREVIEW",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Local video assets · no run or save changes",EditorStyles.miniLabel);
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUILayout.VerticalScope(GUILayout.Width(175)))
                {
                    scroll=EditorGUILayout.BeginScrollView(scroll);
                    for(var i=0;i<VanguardVideoCatalog.Clips.Length;i++)
                    {
                        var old=GUI.backgroundColor;if(i==selected)GUI.backgroundColor=new Color(.85f,.67f,.30f);
                        if(GUILayout.Button(ObjectNames.NicifyVariableName(VanguardVideoCatalog.Clips[i].Name),GUILayout.Height(25))){selected=i;Play();}
                        GUI.backgroundColor=old;
                    }
                    EditorGUILayout.EndScrollView();
                }
                using(new EditorGUILayout.VerticalScope())
                {
                    var area=GUILayoutUtility.GetRect(400,400,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
                    EditorGUI.DrawRect(area,new Color(.065f,.07f,.085f));
                    var ground=area.yMax-38;EditorGUI.DrawRect(new Rect(area.x,ground,area.width,1),new Color(.28f,.25f,.18f));
                    GUI.BeginGroup(area);
                    var actor=new Rect(area.width*.35f,area.height*.18f,area.width*.3f,area.height*.72f);
                    if(surface==null||!surface.Draw(actor,raw))GUI.Label(new Rect(24,24,area.width-48,60),surface?.Error!=""&&surface!=null?surface.Error:"Select a clip to preview.",EditorStyles.wordWrappedLabel);
                    GUI.EndGroup();
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        if(GUILayout.Button("Play / Restart"))Play();
                        if(GUILayout.Button(paused?"Resume":"Pause"))paused=!paused;
                        loop=GUILayout.Toggle(loop,"Loop");raw=GUILayout.Toggle(raw,"Show packed source");
                    }
                    speed=EditorGUILayout.Slider("Playback speed",speed,.25f,4f);
                    EditorGUILayout.LabelField("Select Play to apply speed / loop changes. Combat uses faster action timing.",EditorStyles.wordWrappedMiniLabel);
                    if(!string.IsNullOrEmpty(surface?.Error))EditorGUILayout.HelpBox(surface.Error,MessageType.Warning);
                }
            }
        }
    }
}
