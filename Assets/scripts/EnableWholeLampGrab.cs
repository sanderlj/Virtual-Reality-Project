using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class LampOneObjectFinalizer : MonoBehaviour
{
    [Header("Hierarchy")]
    public GameObject lampBase;
    public GameObject lampStem;
    public GameObject lampShade;
    public GameObject lightBulb; // kept active

    [Tooltip("Attach/handle transform for the final grabbable Lamp.")]
    public Transform lampGrab;

    [Header("Assembly Detection")]
    [Tooltip("Bulb socket that receives the shade. When it selects, we're done.")]
    public XRSocketInteractor shadeSocketOnBulb;
    [Tooltip("Optional: require this exact shade to be snapped.")]
    public XRBaseInteractable expectedShade;

    [Header("Effects")]
    public Light pointLightToTurnOn;
    [Tooltip("All text objects that should be revealed when the lamp is assembled.")]
    public GameObject[] textsToReveal;

    [Header("Parent Body Settings (post-assembly)")]
    public float finalMass = 1.2f;
    public float angularDamping = 0.05f;
    public CollisionDetectionMode collisionMode = CollisionDetectionMode.ContinuousDynamic;
    public RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;
    public bool parentUseGravity = true;            // dynamic body w/ gravity
    public bool parentIsKinematic = false;          // dynamic, not kinematic

    [Header("Collider")]
    [Tooltip("If NO collider exists anywhere on the Lamp hierarchy, add an auto-sized BoxCollider on the parent.")]
    public bool addAutoBoundsColliderIfMissing = true;

    bool finalized;

    void Reset()
    {
        if (!shadeSocketOnBulb && lightBulb)
            shadeSocketOnBulb = lightBulb.GetComponentInChildren<XRSocketInteractor>(true);
    }

    void OnEnable()
    {
        if (shadeSocketOnBulb)
            shadeSocketOnBulb.selectEntered.AddListener(OnShadeSnapped);
    }

    void OnDisable()
    {
        if (shadeSocketOnBulb)
            shadeSocketOnBulb.selectEntered.RemoveListener(OnShadeSnapped);
    }

    void OnShadeSnapped(SelectEnterEventArgs args)
    {
        if (finalized) return;
        if (expectedShade && args.interactableObject != expectedShade) return;
        FinalizeLamp();
    }

    void FinalizeLamp()
    {
        if (finalized) return;
        finalized = true;

        // keep the bulb visible no matter what
        ForceBulbActiveNowAndForAFrame();

        // 1) Stop sockets from allowing further disassembly
        DisableAll<XRSocketInteractor>(lampBase, lampStem, lightBulb, lampShade);

        // 2) Remove XRGrabInteractable from all child parts (so only the parent is grabbable)
        foreach (var grab in GetComponentsInChildren<XRGrabInteractable>(true))
            if (grab && grab.gameObject != gameObject) Destroy(grab);

        // 3) Remove child rigidbodies so their colliders become part of the parent compound
        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
            if (rb && rb.gameObject != gameObject) Destroy(rb);

        // 4) Parent Rigidbody (single dynamic body that uses gravity)
        var parentRB = GetComponent<Rigidbody>();
        if (!parentRB) parentRB = gameObject.AddComponent<Rigidbody>();
        parentRB.mass = finalMass;
        parentRB.angularDamping = angularDamping;
        parentRB.collisionDetectionMode = collisionMode;
        parentRB.interpolation = interpolation;
        parentRB.useGravity = parentUseGravity;     // true
        parentRB.isKinematic = parentIsKinematic;   // false

        // 5) Make sure we have at least one collider in the whole hierarchy
        if (!HasAnyEnabledCollider(gameObject) && addAutoBoundsColliderIfMissing)
            AddAutoBoxColliderFromChildren(gameObject);

        // 6) Parent grab: dynamic/velocity tracking pairs best with non-kinematic RB
        var grabParent = GetComponent<XRGrabInteractable>();
        if (!grabParent) grabParent = gameObject.AddComponent<XRGrabInteractable>();
#if UNITY_XR_INTERACTION_TOOLKIT
        grabParent.movementType = XRGrabInteractable.MovementType.VelocityTracking;
#endif
        if (lampGrab) grabParent.attachTransform = lampGrab;
        grabParent.selectMode = InteractableSelectMode.Single;

        // 7) Effects: light + texts
        if (pointLightToTurnOn) pointLightToTurnOn.enabled = true;
        if (textsToReveal != null)
        {
            foreach (var go in textsToReveal)
                if (go) go.SetActive(true);
        }
    }

    // ---------- helpers ----------

    void DisableAll<T>(params GameObject[] roots) where T : Behaviour
    {
        foreach (var go in roots.Where(r => r))
        {
            foreach (var c in go.GetComponentsInChildren<T>(true))
                c.enabled = false;
        }
    }

    bool HasAnyEnabledCollider(GameObject root)
    {
        return root.GetComponentsInChildren<Collider>(true)
                   .Any(c => c.enabled && c.gameObject.activeInHierarchy);
    }

    void AddAutoBoxColliderFromChildren(GameObject parent)
    {
        // compute world-space bounds from child colliders (prefer) or renderers
        var cols = parent.GetComponentsInChildren<Collider>(true)
                         .Where(c => c.gameObject != parent).ToArray();

        Bounds b;
        if (cols.Length > 0)
        {
            b = new Bounds(cols[0].bounds.center, cols[0].bounds.size);
            foreach (var c in cols) b.Encapsulate(c.bounds);
        }
        else
        {
            var rends = parent.GetComponentsInChildren<Renderer>(true)
                              .Where(r => r.gameObject != parent).ToArray();
            if (rends.Length == 0) return;
            b = new Bounds(rends[0].bounds.center, rends[0].bounds.size);
            foreach (var r in rends) b.Encapsulate(r.bounds);
        }

        var box = parent.AddComponent<BoxCollider>();
        box.center = parent.transform.InverseTransformPoint(b.center);

        // convert world size to local (accounting for lossy scale)
        Vector3 ls = parent.transform.lossyScale;
        Vector3 ws = b.size;
        box.size = new Vector3(
            ls.x != 0 ? ws.x / ls.x : ws.x,
            ls.y != 0 ? ws.y / ls.y : ws.y,
            ls.z != 0 ? ws.z / ls.z : ws.z
        );
        box.isTrigger = false;
    }

    void ForceBulbActiveNowAndForAFrame()
    {
        if (lightBulb)
        {
            lightBulb.SetActive(true);
            StartCoroutine(ReassertBulbActiveNextFrame());
        }
    }

    IEnumerator ReassertBulbActiveNextFrame()
    {
        yield return null; // next frame – covers any toolkit toggles during finalize
        if (lightBulb && !lightBulb.activeSelf) lightBulb.SetActive(true);
    }
}