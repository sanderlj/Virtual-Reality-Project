using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class EnableWholeLampGrab : MonoBehaviour
{
    [Header("Sockets (drag in from scene)")]
    public XRSocketInteractor stemSocket;   // Base/Stem socket
    public XRSocketInteractor bulbSocket;   // Bulb socket
    public XRSocketInteractor shadeSocket;  // Shade socket (optional)

    [Tooltip("Require the shade to be snapped before enabling whole-lamp grab")]
    public bool requireShade = true;

    [Header("Assembly Timing")]
    [Tooltip("Tiny delay after sockets are filled so XR finishes snapping.")]
    public float assembleDelay = 0.03f;
    [Tooltip("Extra delay JUST for the shade so it’s perfectly centered before we lock.")]
    public float shadeExtraDelay = 0.05f;

    [Header("Bulb placement tweak")]
    [Tooltip("Move the bulb downward along the bulb socket's up-axis after assembly.")]
    public float bulbDownOffset = 0.08f;   // <— adjust this until the bulb sits flush in the shade

    [Header("Parent Grab (assembled)")]
    [Tooltip("Optional attach point for the final parent grab (if null we'll create one by the stem).")]
    public Transform combinedAttach;

    [Header("XR")]
    public XRInteractionManager xrManager;

    [Header("Light & Messages")]
    [Tooltip("Socket to watch for shade attach/detach (defaults to shadeSocket)")]
    public XRSocketInteractor shadeSocketForLight;
    public Light pointLight;          // The bulb's Light
    public GameObject bulbVisual;     // Bulb mesh GameObject (optional)
    public GameObject firstMessage;   // TV text #1
    public GameObject secondMessage;  // TV text #2
    public float lightOnDelay = 0.2f;

    // ---- internals ----
    bool enabledOnce;
    bool assembled;
    XRGrabInteractable parentGrab;
    Rigidbody parentRB;
    BoxCollider parentBox;

    void Awake()
    {
        if (!xrManager) xrManager = FindFirstObjectByType<XRInteractionManager>();
        if (!shadeSocketForLight) shadeSocketForLight = shadeSocket;

        // init light/UI off
        if (pointLight) pointLight.enabled = false;
        if (firstMessage) firstMessage.SetActive(false);
        if (secondMessage) secondMessage.SetActive(false);

        if (shadeSocketForLight)
        {
            shadeSocketForLight.selectEntered.AddListener(OnShadeEntered);
            shadeSocketForLight.selectExited.AddListener(OnShadeExited);
        }
    }

    void OnDestroy()
    {
        if (shadeSocketForLight)
        {
            shadeSocketForLight.selectEntered.RemoveListener(OnShadeEntered);
            shadeSocketForLight.selectExited.RemoveListener(OnShadeExited);
        }
    }

    void Update()
    {
        if (enabledOnce) return;

        if (!IsFilled(stemSocket) || !IsFilled(bulbSocket)) return;
        if (requireShade && !IsFilled(shadeSocket)) return;

        StartCoroutine(AssembleAndEnableGrab());
        enabledOnce = true;
    }

    static bool IsFilled(XRSocketInteractor socket)
    {
        return socket && socket.hasSelection && socket.firstInteractableSelected != null;
    }

    static Transform GetSelectedTransform(XRSocketInteractor socket)
    {
        if (!socket || socket.firstInteractableSelected == null) return null;
        var comp = socket.firstInteractableSelected as Component; // interface -> Component
        return comp ? comp.transform : null;
    }

    IEnumerator AssembleAndEnableGrab()
    {
        // --- cache BEFORE we exit from sockets ---
        Transform stemT   = GetSelectedTransform(stemSocket);
        Transform bulbT   = GetSelectedTransform(bulbSocket);
        Transform shadeT  = (requireShade || shadeSocket) ? GetSelectedTransform(shadeSocket) : null;

        Transform shadeTarget = shadeSocket ? shadeSocket.attachTransform : null;
        Transform bulbTarget  = bulbSocket ? bulbSocket.attachTransform  : null;

        // small settle so XR completes its own snap
        if (assembleDelay > 0f) yield return new WaitForSeconds(assembleDelay);

        // cleanly release from sockets
        DeselectAndDisable(stemSocket);
        DeselectAndDisable(bulbSocket);
        if (shadeSocket) DeselectAndDisable(shadeSocket);

        yield return null; // let select-exit finish

        // reparent and ensure visuals on
        ReparentAndShow(stemT);
        ReparentAndShow(bulbT);
        ReparentAndShow(shadeT);

        // Only align the shade (prevents base tilt). Do it after a tiny extra settle.
        if (shadeT && shadeTarget && shadeExtraDelay > 0f)
            yield return new WaitForSeconds(shadeExtraDelay);
        if (shadeT && shadeTarget)
            shadeT.SetPositionAndRotation(shadeTarget.position, shadeTarget.rotation);

        // NUDGE BULB DOWN a little so it sits under the shade nicely.
        if (bulbT && bulbTarget && bulbDownOffset > 0f)
        {
            // Keep current rotation; just move along the socket’s -up:
            Vector3 pos = bulbTarget.position - bulbTarget.up * bulbDownOffset;
            bulbT.position = pos;
        }

        // remove per-child XR & physics
        StripXRFromChildren();

        // single collider on parent from renderers
        EnsureParentColliderFromRenderers();

        // parent rigidbody
        parentRB = GetComponent<Rigidbody>();
        if (!parentRB) parentRB = gameObject.AddComponent<Rigidbody>();
        parentRB.useGravity = false;
        parentRB.isKinematic = true; // set false next fixed step
        parentRB.collisionDetectionMode = CollisionDetectionMode.Continuous;
        parentRB.interpolation = RigidbodyInterpolation.Interpolate;

        // parent grab
        parentGrab = GetComponent<XRGrabInteractable>();
        if (!parentGrab) parentGrab = gameObject.AddComponent<XRGrabInteractable>();
        parentGrab.interactionManager = xrManager;
        parentGrab.movementType = XRGrabInteractable.MovementType.VelocityTracking;
        parentGrab.throwOnDetach = false;
        parentGrab.trackPosition = true;
        parentGrab.trackRotation = true;

        parentGrab.colliders.Clear();
        if (parentBox) parentGrab.colliders.Add(parentBox);

        if (!combinedAttach) combinedAttach = CreateAttachPointNear(stemT);
        parentGrab.attachTransform = combinedAttach;

        yield return new WaitForFixedUpdate();
        parentRB.isKinematic = false;
        parentRB.useGravity  = true;

        // Light/UI
        assembled = true;
        if (bulbVisual) bulbVisual.SetActive(true);
        if (firstMessage) firstMessage.SetActive(true);
        if (secondMessage) secondMessage.SetActive(true);
        if (pointLight) Invoke(nameof(EnableLight), lightOnDelay);
    }

    void DeselectAndDisable(XRSocketInteractor socket)
    {
        if (!socket) return;
        if (socket.hasSelection && socket.firstInteractableSelected != null)
        {
            var inter = socket.firstInteractableSelected;
            (xrManager ? xrManager : FindFirstObjectByType<XRInteractionManager>())
                .SelectExit(socket, inter);
        }
        socket.socketActive = false;
    }

    void ReparentAndShow(Transform t)
    {
        if (!t) return;
        t.SetParent(transform, true);
        t.gameObject.SetActive(true);
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            r.enabled = true;
    }

    void StripXRFromChildren()
    {
        foreach (var grab in GetComponentsInChildren<XRGrabInteractable>(true))
            if (grab && grab.gameObject != gameObject) Destroy(grab);

        foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
            if (rb && rb.gameObject != gameObject) Destroy(rb);

        foreach (var col in GetComponentsInChildren<Collider>(true))
            if (col && col.gameObject != gameObject) Destroy(col);
    }

    void EnsureParentColliderFromRenderers()
    {
        parentBox = GetComponent<BoxCollider>();
        if (!parentBox) parentBox = gameObject.AddComponent<BoxCollider>();

        var rends = GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.gameObject.activeInHierarchy)
                    .ToArray();

        if (rends.Length == 0)
        {
            parentBox.center = Vector3.zero;
            parentBox.size = Vector3.one * 0.1f;
            parentBox.isTrigger = false;
            return;
        }

        Bounds world = new Bounds(rends[0].bounds.center, rends[0].bounds.size);
        for (int i = 1; i < rends.Length; i++) world.Encapsulate(rends[i].bounds);

        Vector3 centerLocal = transform.InverseTransformPoint(world.center);
        Vector3 sizeLocal = transform.InverseTransformVector(world.size);
        sizeLocal = new Vector3(Mathf.Abs(sizeLocal.x), Mathf.Abs(sizeLocal.y), Mathf.Abs(sizeLocal.z));

        parentBox.center = centerLocal;
        parentBox.size   = sizeLocal;
        parentBox.isTrigger = false;
    }

    Transform CreateAttachPointNear(Transform stem)
    {
        var g = new GameObject("Attach_Lamp");
        g.transform.SetParent(transform, false);

        if (stem)
        {
            g.transform.position = stem.position + stem.up * 0.05f; // slight offset above stem
            g.transform.rotation = stem.rotation;
        }
        else if (parentBox)
        {
            g.transform.position = transform.TransformPoint(parentBox.center);
            g.transform.rotation = Quaternion.identity;
        }
        else
        {
            g.transform.localPosition = Vector3.zero;
            g.transform.localRotation = Quaternion.identity;
        }

        return g.transform;
    }

    // ---------- Light/message behavior ----------
    void OnShadeEntered(SelectEnterEventArgs _)
    {
        if (bulbVisual) bulbVisual.SetActive(true);
        if (firstMessage) firstMessage.SetActive(true);
        if (secondMessage) secondMessage.SetActive(true);
        if (pointLight) Invoke(nameof(EnableLight), lightOnDelay);
    }

    void OnShadeExited(SelectExitEventArgs _)
    {
        assembled = false;
        if (bulbVisual) bulbVisual.SetActive(false);
        if (pointLight) pointLight.enabled = false;
        if (firstMessage) firstMessage.SetActive(false);
        if (secondMessage) secondMessage.SetActive(false);
    }

    void EnableLight()
    {
        if (assembled && pointLight) pointLight.enabled = true;
    }
}