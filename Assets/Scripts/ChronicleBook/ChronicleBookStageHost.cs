using GildedFate.Chronicle;
using UnityEngine;

namespace GildedFate.ChronicleBook
{
    /// <summary>
    /// Owns everything needed to see the book: an isolated layer, a camera rendered by hand into a RenderTexture (the same
    /// pattern the combat stage uses), and whichever rig is available. The finished model is used if it has been added to
    /// Resources/Chronicle, otherwise the temporary book. Page overlays follow the animated paper surfaces and fit
    /// inside their projection, including the reading camera's perspective.
    /// </summary>
    public sealed class ChronicleBookStageHost
    {
        public const int Layer = 29;   // the combat stage uses 30
        public IChronicleBookRig Rig { get; private set; }
        public IChroniclePageSurface Surface { get; private set; }
        public RenderTexture Texture { get; private set; }
        public Camera Camera { get; private set; }
        public bool UsingFinishedModel { get; private set; }

        GameObject root; ChronicleProceduralBook procedural; ChronicleAnimatorBookRig finished;
        readonly Vector3[] corners = new Vector3[4];
        readonly Vector3[] otherCorners = new Vector3[4];
        readonly Vector2[] projected = new Vector2[4];
        float baseDistance = 11.5f; Vector3 target = new Vector3(0f, .2f, 0f);

        /// <summary>Returns null if no rig could be built (for example the shader is missing); callers then skip the Chronicle gracefully.</summary>
        public static ChronicleBookStageHost Create(int width, int height)
        {
            var host = new ChronicleBookStageHost();
            // Far from anything else in the scene, and on its own layer, so no other camera ever sees the book.
            host.root = new GameObject("Gilded Fate · Chronicle book stage");
            host.root.transform.position = new Vector3(0f, 4000f, 0f);
            if (Application.isPlaying) Object.DontDestroyOnLoad(host.root);

            host.finished = ChronicleAnimatorBookRig.TryCreate(host.root.transform, Layer);
            if (host.finished)
            {
                host.Rig = host.finished; host.Surface = host.finished; host.UsingFinishedModel = true;
                host.baseDistance = host.finished.Config.cameraDistance; host.target = host.finished.Config.cameraTarget;
            }
            else
            {
                host.procedural = ChronicleProceduralBook.Create(host.root.transform, Layer);
                if (!host.procedural) { DestroyStageObject(host.root); return null; }
                host.Rig = host.procedural; host.Surface = host.procedural;
            }

            host.Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Gilded Fate · Chronicle book", antiAliasing = 4, useMipMap = false, autoGenerateMips = false };
            host.Texture.Create();
            var cameraHost = new GameObject("Chronicle reading camera"); cameraHost.transform.SetParent(host.root.transform, false);
            host.Camera = cameraHost.AddComponent<Camera>();
            host.Camera.targetTexture = host.Texture; host.Camera.fieldOfView = 24f;
            host.Camera.clearFlags = CameraClearFlags.SolidColor; host.Camera.backgroundColor = new Color(.02f, .015f, .02f, 0f);
            host.Camera.cullingMask = 1 << Layer; host.Camera.nearClipPlane = .1f; host.Camera.farClipPlane = 60f;
            host.Camera.allowHDR = false; host.Camera.allowMSAA = true;
            host.Camera.enabled = false;   // rendered by hand in Tick, so it never depends on camera scheduling
            host.SetActive(false);
            return host;
        }

        public void SetActive(bool active) { if (root) root.SetActive(active); }

        /// <summary>Advances the book and renders one frame. <paramref name="cameraState"/> carries the scene's CameraMove / CameraZoom.</summary>
        public void Tick(float dt, ChronicleCameraState cameraState)
        {
            if (!root || !root.activeSelf) return;
            if (procedural) procedural.Tick(dt); else if (finished) finished.Tick(dt);
            var frame = Rig.Frame;
            var zoom = cameraState != null && cameraState.zoom > .1f ? cameraState.zoom : 1f;
            var pitch = frame.cameraPitch * Mathf.Deg2Rad;
            var focus = root.transform.position + target + (cameraState != null ? new Vector3(cameraState.x, 0f, cameraState.y) : Vector3.zero);
            var direction = new Vector3(0f, Mathf.Cos(pitch), -Mathf.Sin(pitch));
            Camera.transform.position = focus + direction * (baseDistance / zoom);
            Camera.transform.LookAt(focus, Vector3.forward);
            Camera.Render();
        }

        /// <summary>The page's text area as a normalized viewport rectangle (origin bottom-left, like Unity viewports).</summary>
        public bool TryGetPageViewportRect(ChroniclePageSide side, out Rect rect)
        {
            rect = default;
            if (Surface == null || !Camera || !Surface.TryGetPageCorners(side, corners)) return false;
            // Imported mesh names may use the book's own left/right rather than the reader's.
            // Keep story layouts and Archive navigation on their intended screen side.
            var other = side == ChroniclePageSide.Left ? ChroniclePageSide.Right : ChroniclePageSide.Left;
            if (Surface.TryGetPageCorners(other, otherCorners))
            {
                var x = 0f; var otherX = 0f;
                for (var i = 0; i < corners.Length; i++)
                {
                    x += Camera.WorldToViewportPoint(corners[i]).x;
                    otherX += Camera.WorldToViewportPoint(otherCorners[i]).x;
                }
                if (side == ChroniclePageSide.Left ? x > otherX : x < otherX)
                    System.Array.Copy(otherCorners, corners, corners.Length);
            }
            for (var i = 0; i < corners.Length; i++)
            {
                var v = Camera.WorldToViewportPoint(corners[i]);
                if (v.z <= 0f) return false;
                projected[i] = new Vector2(v.x, v.y);
            }
            // Fit a rectangle inside the perspective trapezoid, rather than around it.
            System.Array.Sort(projected, (a, b) => a.y.CompareTo(b.y));
            var bottomLeft = Mathf.Min(projected[0].x, projected[1].x);
            var bottomRight = Mathf.Max(projected[0].x, projected[1].x);
            var topLeft = Mathf.Min(projected[2].x, projected[3].x);
            var topRight = Mathf.Max(projected[2].x, projected[3].x);
            var xMin = Mathf.Max(0f, Mathf.Max(bottomLeft, topLeft));
            var xMax = Mathf.Min(1f, Mathf.Min(bottomRight, topRight));
            var yMin = Mathf.Max(0f, projected[1].y);
            var yMax = Mathf.Min(1f, projected[2].y);
            rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return xMax > xMin && yMax > yMin;
        }

        static void DestroyStageObject(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }

        public void Dispose()
        {
            if (Texture) { Texture.Release(); DestroyStageObject(Texture); }
            if (root) DestroyStageObject(root);
            Rig = null; Surface = null;
        }
    }
}
