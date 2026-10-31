using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PhysicsLab.Lab
{
    /// <summary>
    /// Shows where the desktop pointer aims. A small dot marks the screen centre (the aim point) while the
    /// mouse is captured, and a right hand floats at the aim point: open when idle, half closed over a part
    /// you can grab, a fist while you hold one. When the mouse is free, nothing is drawn and the normal
    /// mouse cursor shows. Put it on the Desktop Rig, next to DesktopPointer.
    /// </summary>
    public class AimCursor : MonoBehaviour
    {
        private const float DotSizePixels = 12f;
        private const float HandAngularSize = 0.07f;   // hand length as a share of the distance (about 4 degrees of view)
        private const float MinHandMetres = 0.04f;
        private const float MaxHandMetres = 0.16f;
        private const float HoverCurl = 0.25f;
        private const float CurlSpeedPerSecond = 8f;

        private static readonly Color IdleColor = Color.white;
        private static readonly Color HoverColor = new Color(0.3f, 1f, 0.5f, 1f);
        private static readonly Color HoldColor = new Color(1f, 0.65f, 0.2f, 1f);

        [SerializeField] private DesktopPointer pointer;
        [SerializeField] private Camera viewCamera;
        [Tooltip("The right hand model. Leave empty to show only the dot.")]
        [SerializeField] private GameObject handPrefab;
        [Tooltip("Tick this if the fingers bend backward.")]
        [SerializeField] private bool flipCurl;
        [Tooltip("Writes the name of the part you aim at next to the dot.")]
        [SerializeField] private bool showPartName = true;

        private Image _dot;
        private HandPose _hand;
        private float _curl;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            BuildDot();

            if (handPrefab != null)
            {
                _hand = new HandPose(handPrefab, flipCurl);
                if (!_hand.IsValid)
                {
                    _hand.Destroy();
                    _hand = null;
                }
            }
            SetVisible(false);
        }

        // The rig is switched off in VR. The hand is a separate object, so it must be hidden here.
        private void OnDisable()
        {
            if (_dot != null) SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_hand != null) _hand.Destroy();
        }

        // LateUpdate runs after DesktopPointer, so the hand always follows this frame's aim.
        private void LateUpdate()
        {
            bool aiming = pointer != null && pointer.IsAiming;
            SetVisible(aiming);
            if (!aiming) return;

            _dot.color = pointer.Dragged != null ? HoldColor : (pointer.Hovered != null ? HoverColor : IdleColor);
            if (_hand != null) UpdateHand();
        }

        // A plain text label, so you can always tell what is under the aim.
        private void OnGUI()
        {
            if (!showPartName || pointer == null || !pointer.IsAiming) return;

            LabObject part = pointer.Dragged != null ? pointer.Dragged : pointer.Hovered;
            if (part == null) return;
            GUI.Label(new Rect(Screen.width * 0.5f + 14f, Screen.height * 0.5f - 10f, 300f, 24f), part.name);
        }

        private void UpdateHand()
        {
            float target = pointer.Dragged != null ? 1f : (pointer.Hovered != null ? HoverCurl : 0f);
            _curl = Mathf.MoveTowards(_curl, target, CurlSpeedPerSecond * Time.unscaledDeltaTime);
            _hand.SetCurl(_curl);

            Vector3 aim = pointer.AimPoint;
            float distance = Mathf.Max(0.3f, Vector3.Distance(viewCamera.transform.position, aim));
            float length = Mathf.Clamp(distance * HandAngularSize, MinHandMetres, MaxHandMetres);

            // Fingers point away from the player along the floor. Looking straight down, up on the screen is forward.
            Vector3 forward = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = viewCamera.transform.up;

            Vector3 palm = aim + pointer.AimNormal * (length * 0.5f);   // the palm floats just above the surface
            _hand.Place(palm, forward.normalized, length);
        }

        private void SetVisible(bool visible)
        {
            _dot.gameObject.SetActive(visible);
            if (_hand != null) _hand.SetActive(visible);
        }

        // A screen-space dot made at run time, so no UI setup is needed. It cannot be clicked.
        private void BuildDot()
        {
            var canvasObject = new GameObject("AimDotCanvas");
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var dotObject = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dotObject.transform.SetParent(canvasObject.transform, false);
            ((RectTransform)dotObject.transform).sizeDelta = new Vector2(DotSizePixels, DotSizePixels);

            _dot = dotObject.GetComponent<Image>();
            _dot.sprite = MakeDotSprite();
            _dot.raycastTarget = false;
        }

        private static Sprite MakeDotSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float centre = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float radius = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    Color pixel = radius <= 9f ? Color.white : (radius <= 13f ? Color.black : Color.clear);
                    texture.SetPixel(x, y, pixel);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }

    /// <summary>
    /// Poses the right hand of the "Free VR Hands" model without needing to know its bone axes. At start it reads the
    /// bone positions, works out which way the fingers point and which way they bend, and builds the fist from that.
    /// </summary>
    internal sealed class HandPose
    {
        private struct Bend
        {
            public Transform joint;
            public Quaternion bindRotation;
            public Vector3 localAxis;
            public float maxDegrees;
        }

        private static readonly string[] FingerNames = { "Index", "Middle", "Ring", "Pinky" };
        private static readonly float[] FingerDegrees = { 70f, 80f, 50f };   // knuckle, middle joint, tip joint
        private static readonly float[] ThumbDegrees = { 20f, 35f, 30f };

        private readonly GameObject _instance;
        private readonly Transform _root;
        private readonly float _flip;
        private Bend[] _bends = new Bend[0];
        private Quaternion _modelFrameInverse = Quaternion.identity;
        private Vector3 _palmLocal;
        private float _lengthLocal = 1f;

        public bool IsValid => _bends.Length > 0;

        public HandPose(GameObject prefab, bool flipCurl)
        {
            _instance = Object.Instantiate(prefab);
            _instance.name = "AimHand";
            _root = _instance.transform;
            _root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _root.localScale = Vector3.one;
            _flip = flipCurl ? -1f : 1f;

            PrepareInstance();
            Measure();
        }

        // The hand can already be destroyed when Play stops, so check before touching it.
        public void SetActive(bool active)
        {
            if (_instance != null) _instance.SetActive(active);
        }

        public void Destroy() => Object.Destroy(_instance);

        /// <summary>0 is the open hand, 1 is a closed fist.</summary>
        public void SetCurl(float amount)
        {
            foreach (Bend bend in _bends)
            {
                float degrees = bend.maxDegrees * amount * _flip;
                bend.joint.localRotation = bend.bindRotation * Quaternion.AngleAxis(degrees, bend.localAxis);
            }
        }

        /// <summary>Puts the palm centre at palmWorld, the fingers along forward, the palm facing down.</summary>
        public void Place(Vector3 palmWorld, Vector3 forward, float handLengthMetres)
        {
            float scale = handLengthMetres / _lengthLocal;
            _root.localScale = Vector3.one * scale;
            _root.rotation = Quaternion.LookRotation(forward, Vector3.up) * _modelFrameInverse;
            _root.position = palmWorld - _root.rotation * (_palmLocal * scale);
        }

        // The pointer ray must never hit the hand, the animation must not fight the pose, and only one hand shows.
        private void PrepareInstance()
        {
            foreach (Animator animator in _instance.GetComponentsInChildren<Animator>()) animator.enabled = false;
            foreach (Collider collider in _instance.GetComponentsInChildren<Collider>()) Object.Destroy(collider);
            foreach (SkinnedMeshRenderer skin in _instance.GetComponentsInChildren<SkinnedMeshRenderer>()) skin.updateWhenOffscreen = true;

            Transform left = Find("J_Left");
            if (left != null) left.localScale = Vector3.zero;   // the mesh holds both hands, so the left one is shrunk away
        }

        private void Measure()
        {
            Transform wrist = Find("J_Right_Hand");
            Transform index = Find("J_Right_HandIndex1");
            Transform middle = Find("J_Right_HandMiddle1");
            Transform pinky = Find("J_Right_HandPinky1");
            Transform ring = Find("J_Right_HandRing1");
            Transform thumb = Find("J_Right_HandThumb1");
            Transform tip = Find("J_Right_HandMiddle4");
            if (wrist == null || index == null || middle == null || pinky == null || ring == null || thumb == null || tip == null)
            {
                Debug.LogWarning("[AimCursor] The hand model has no bones named like J_Right_HandIndex1. Only the dot will show.");
                return;
            }

            // Right hand: the palm faces cross(fingers, thumb side). The fingers bend toward it around the knuckle line.
            Vector3 fingers = (middle.position - wrist.position).normalized;
            Vector3 thumbSide = Vector3.ProjectOnPlane(thumb.position - index.position, fingers).normalized;
            Vector3 palmNormal = Vector3.Cross(fingers, thumbSide).normalized;
            Vector3 bendAxis = Vector3.Cross(fingers, palmNormal).normalized;

            _palmLocal = (index.position + middle.position + ring.position + pinky.position) * 0.25f;
            _lengthLocal = Mathf.Max(0.001f, Vector3.Distance(wrist.position, tip.position));
            _modelFrameInverse = Quaternion.Inverse(Quaternion.LookRotation(fingers, -palmNormal));
            _bends = BuildBends(bendAxis, palmNormal, thumbSide);
        }

        private Bend[] BuildBends(Vector3 bendAxis, Vector3 palmNormal, Vector3 thumbSide)
        {
            var bends = new List<Bend>();
            foreach (string finger in FingerNames)
            {
                for (int i = 0; i < FingerDegrees.Length; i++)
                {
                    AddBend(bends, Find($"J_Right_Hand{finger}{i + 1}"), bendAxis, FingerDegrees[i]);
                }
            }

            // The thumb closes toward the palm and back across it, so it gets its own axis per joint.
            Vector3 thumbClose = (palmNormal - thumbSide * 0.6f).normalized;
            for (int i = 0; i < ThumbDegrees.Length; i++)
            {
                Transform joint = Find($"J_Right_HandThumb{i + 1}");
                Transform child = Find($"J_Right_HandThumb{i + 2}");
                if (joint == null || child == null) continue;
                Vector3 axis = Vector3.Cross((child.position - joint.position).normalized, thumbClose).normalized;
                AddBend(bends, joint, axis, ThumbDegrees[i]);
            }
            return bends.ToArray();
        }

        private static void AddBend(List<Bend> bends, Transform joint, Vector3 worldAxis, float maxDegrees)
        {
            if (joint == null) return;
            bends.Add(new Bend
            {
                joint = joint,
                bindRotation = joint.localRotation,
                localAxis = joint.InverseTransformDirection(worldAxis).normalized,
                maxDegrees = maxDegrees
            });
        }

        private Transform Find(string boneName) => FindDeep(_root, boneName);

        private static Transform FindDeep(Transform parent, string boneName)
        {
            if (parent.name == boneName) return parent;
            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, boneName);
                if (found != null) return found;
            }
            return null;
        }
    }
}
