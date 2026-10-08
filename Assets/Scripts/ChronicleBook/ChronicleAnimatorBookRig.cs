using GildedFate.Chronicle;
using UnityEngine;

namespace GildedFate.ChronicleBook
{
    /// <summary>
    /// Drives an imported, animated Chronicle model. The pose, timing and overlay visibility come from the same
    /// <see cref="ChronicleBookAnimator"/> the temporary book uses, so story playback behaves identically; this class only
    /// also fires the model's Animator states at the right moments. A state name that does not exist is reported once, not thrown.
    /// </summary>
    public sealed class ChronicleAnimatorBookRig : MonoBehaviour, IChronicleBookRig, IChroniclePageSurface
    {
        ChronicleBookRigConfig config; Animator animator; ChronicleBookAnimator logic;
        Transform leftAnchor, rightAnchor;
        readonly System.Collections.Generic.HashSet<string> reported = new System.Collections.Generic.HashSet<string>();

        public ChronicleBookAnimator Logic => logic;
        public ChronicleBookRigConfig Config => config;
        public ChronicleBookFrame Frame => logic.Frame;
        public float DurationOf(ChronicleBookAction action) => logic.DurationOf(action);

        /// <summary>Loads the finished book if one has been added. Returns null when there is no model yet, so the temporary book is used.</summary>
        public static ChronicleAnimatorBookRig TryCreate(Transform parent, int layer)
        {
            var prefab = Resources.Load<GameObject>("Chronicle/ChronicleBook");
            if (!prefab) return null;
            var config = Resources.Load<ChronicleBookRigConfig>("Chronicle/ChronicleBookRigConfig");
            if (!config) { config = ScriptableObject.CreateInstance<ChronicleBookRigConfig>(); Debug.LogWarning("[Chronicle] No ChronicleBookRigConfig found; using default state names."); }
            var instance = Instantiate(prefab, parent, false); instance.name = "Chronicle · finished book";
            SetLayer(instance.transform, layer);
            var rig = instance.AddComponent<ChronicleAnimatorBookRig>();
            rig.Setup(config);
            return rig;
        }

        static void SetLayer(Transform t, int layer) { t.gameObject.layer = layer; for (var i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer); }

        static Transform FindDeep(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++) { var found = FindDeep(root.GetChild(i), name); if (found) return found; }
            return null;
        }

        void Setup(ChronicleBookRigConfig cfg)
        {
            config = cfg; logic = new ChronicleBookAnimator(cfg.Timings());
            animator = GetComponentInChildren<Animator>();
            if (animator) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            else Debug.LogWarning("[Chronicle] The book prefab has no Animator; it will not move.");
            leftAnchor = FindDeep(transform, cfg.leftPageAnchorName); rightAnchor = FindDeep(transform, cfg.rightPageAnchorName);
            if (!leftAnchor || !rightAnchor) Debug.LogWarning("[Chronicle] Page anchors '" + cfg.leftPageAnchorName + "' / '" + cfg.rightPageAnchorName + "' were not found; page text cannot be placed.");
        }

        public void Tick(float dt) { logic.Update(dt); }

        public void Perform(ChronicleBookAction action, ChronicleNarratorState narrator)
        {
            logic.Perform(action, narrator);
            if (!animator || config == null) return;
            switch (action)
            {
                case ChronicleBookAction.Open: Play(config.openState); break;
                case ChronicleBookAction.Close: Play(config.closeState); break;
                case ChronicleBookAction.TurnPageForward: SetSpread(); Play(config.turnForwardState); break;
                case ChronicleBookAction.TurnPageBack: SetSpread(); Play(config.turnBackState); break;
                case ChronicleBookAction.Idle: Play(config.idleState); break;
                case ChronicleBookAction.ReactToCorrection: Play(config.correctionState); break;
                case ChronicleBookAction.MajorMagic: Play(config.majorMagicState); break;
            }
        }

        public void SetPoseImmediate(ChronicleBookPose pose, int spreadIndex)
        {
            logic.SetPoseImmediate(pose, spreadIndex);
            if (!animator || config == null) return;
            SetSpread();
            // Jump to the last frame of the matching state: open at its end is the open pose, close at its end is the closed pose.
            var state = pose == ChronicleBookPose.Open ? config.openState : config.closeState;
            var hash = Animator.StringToHash(state);
            if (animator.HasState(config.animatorLayer, hash)) { animator.Play(hash, config.animatorLayer, 1f); animator.Update(0f); }
            else Report(state);
        }

        void SetSpread() { if (!string.IsNullOrEmpty(config.spreadParameter)) animator.SetFloat(config.spreadParameter, logic.Frame.spreadIndex); }

        void Play(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            var hash = Animator.StringToHash(state);
            if (!animator.HasState(config.animatorLayer, hash)) { Report(state); return; }
            animator.CrossFadeInFixedTime(hash, config.crossFadeSeconds, config.animatorLayer);
        }

        void Report(string state) { if (reported.Add(state)) Debug.LogWarning("[Chronicle] The book's Animator has no state named '" + state + "'. Map it in the ChronicleBookRigConfig."); }

        public bool TryGetPageCorners(ChroniclePageSide side, Vector3[] corners)
        {
            var anchor = side == ChroniclePageSide.Left ? leftAnchor : rightAnchor;
            if (!anchor || corners == null || corners.Length < 4) return false;
            var hx = config.pageSize.x * .5f; var hz = config.pageSize.y * .5f;
            corners[0] = anchor.TransformPoint(new Vector3(-hx, 0, -hz)); corners[1] = anchor.TransformPoint(new Vector3(-hx, 0, hz));
            corners[2] = anchor.TransformPoint(new Vector3(hx, 0, hz)); corners[3] = anchor.TransformPoint(new Vector3(hx, 0, -hz));
            return true;
        }
    }
}
