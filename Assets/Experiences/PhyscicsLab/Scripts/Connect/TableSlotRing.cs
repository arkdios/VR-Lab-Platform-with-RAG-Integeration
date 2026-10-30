using System.Collections.Generic;
using UnityEngine;

namespace PhysicsLab.Lab.Connect
{
    /// <summary>
    /// Makes the ring of clamp spots around the table rim when the scene starts: 24 spots, one every 15 degrees.
    /// Each spot is a flat glowing disc that is hidden until a part is held. Put it on any object, and set
    /// the table centre and the rim radius. The spots sit at the same place the Pulley script puts a pulley.
    /// </summary>
    public class TableSlotRing : MonoBehaviour
    {
        [SerializeField] private Transform tableCenter;
        [Tooltip("Distance from the table centre to a clamp, in metres. Same as the pulley rim radius.")]
        [SerializeField] private float radiusMetres = 0.4f;
        [SerializeField, Range(4, 72)] private int slotCount = 24;
        [Tooltip("Material for the glow discs. An unlit material is best.")]
        [SerializeField] private Material glowMaterial;
        [SerializeField] private float markerDiameterMetres = 0.05f;

        private readonly List<ConnectPoint> _slots = new List<ConnectPoint>();

        public IReadOnlyList<ConnectPoint> Slots => _slots;

        private void Awake()
        {
            if (tableCenter == null || glowMaterial == null)
            {
                Debug.LogError("[TableSlotRing] Assign Table Center and Glow Material.", this);
                enabled = false;
                return;
            }
            Build();
        }

        /// <summary>Hides every glow. Call when no part is held.</summary>
        public void HideAll()
        {
            foreach (ConnectPoint slot in _slots) slot.SetGlow(GlowState.Hidden);
        }

        private void Build()
        {
            float step = 360f / slotCount;
            for (int i = 0; i < slotCount; i++)
            {
                float degrees = i * step;
                float radians = degrees * Mathf.Deg2Rad;
                Vector3 outward = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));

                var spot = new GameObject("Slot_" + Mathf.RoundToInt(degrees));
                spot.transform.SetParent(transform, false);
                spot.transform.position = tableCenter.position + outward * radiusMetres;
                // Same turn as Pulley.PlaceOnRim: the part's +Z axis points at the table centre.
                spot.transform.rotation = Quaternion.LookRotation(-outward, Vector3.up);

                ConnectPoint point = spot.AddComponent<ConnectPoint>();
                point.Setup(ConnectKind.TableSlot, CreateMarker(spot.transform), degrees);
                _slots.Add(point);
            }
        }

        // A flat disc lying on the table. It has no collider, so it never blocks the mouse ray or a part.
        private Renderer CreateMarker(Transform parent)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Glow";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(markerDiameterMetres, 0.002f, markerDiameterMetres);

            Renderer markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = glowMaterial;
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
            return markerRenderer;
        }

        // Shows the spots in the Scene view, so you can check the radius before pressing Play.
        private void OnDrawGizmosSelected()
        {
            if (tableCenter == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < slotCount; i++)
            {
                float radians = i * (360f / slotCount) * Mathf.Deg2Rad;
                Vector3 position = tableCenter.position +
                                   new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radiusMetres;
                Gizmos.DrawWireSphere(position, markerDiameterMetres * 0.5f);
            }
        }
    }
}
