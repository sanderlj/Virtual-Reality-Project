using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class EnableWholeLampGrab : MonoBehaviour
{
    [Header("Sockets (drag in from scene)")]
    public XRSocketInteractor stemSocket;
    public XRSocketInteractor bulbSocket;
    public XRSocketInteractor shadeSocket;

    [Tooltip("Require the shade to be snapped before enabling whole-lamp grab")]
    public bool requireShade = true;

    [Header("Assembly Timing")]
    public float assembleDelay = 0.03f;
    public float shadeExtraDelay = 0.05f;

    [Header("Bulb placement tweak")]
    [Tooltip("Move the bulb downward along the bulb socket's up-axis after assembly.")]
    public float bulbDownOffset = 0.08f;

    [Header("Grab Stability")]
    public bool makeTriggerWhileHeld = true;
    public float liftOnGrab = 0.03f;
    public float colliderInset = 0.004f;

    [Header("Parent Grab (assembled)")]
    public Transform combinedAttach;

    [Header("XR")]
    public XRInteractionManager xrManager;

    [Header("Light & Messages")]
    public XRSocketInteractor shadeSocketForLight;
    public Light pointLight;
    public GameObject bulbVisual;
    public GameObject firstMessage;
    public GameObject secondMessage;
    public float lightOnDelay = 0.2f;

    // ---- internals ----
    bool enabledOnce;
    bool assembled;
    XRGrabInteractable parentGrab;
    Rigidbody parentRB;
    BoxCollider parentBox;
    bool originalIsTrigger;

    void Awake()
    {
        if (!xrManager) xrManager = FindFirstObjectByType<XRInteractionManager>();
        if (!shadeSocketForLight) shadeSocketForLight = shadeSocket;

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

        if (parentGrab)
        {
            parentGrab.selectEntered.RemoveListener(OnParentGrabbed);
            parentGrab.selectExited.RemoveListener(OnParentReleased);
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
        => socket && socket.hasSelection && socket.firstInteractableSelected != null;

    static Transform GetSelectedTransform(XRSocketInteractor socket)
    {
        if (!socket || socket.firstInteractableSelected == null) return null;
        var comp = socket.firstInteractableSelected as Component;
        return comp ? comp.transform : null;
    }

    IEnumerator AssembleAndEnableGrab()
    {
        Transform stemT   = GetSelectedTransform(stemSocket);
        Transform bulbT   = GetSelectedTransform(bulbSocket);
        Transform shadeT  = (requireShade || shadeSocket) ? GetSelectedTransform(shadeSocket) : null;

        Transform shadeTarget = shadeSocket ? shadeSocket.attachTransform : null;
        Transform bulbTarget  = bulbSocket ? bulbSocket.attachTransform  : null;

        if (assembleDelay > 0f) yield return new WaitForSeconds(assembleDelay);

        DeselectAndDisable(stemSocket);
        DeselectAndDisable(bulbSocket);
        if (shadeSocket) DeselectAndDisable(shadeSocket);

        yield return null;

        ReparentAndShow(stemT);
        ReparentAndShow(bulbT);
        ReparentAndShow(shadeT);

        if (shadeT && shadeTarget && shadeExtraDelay > 0f)
            yield return new WaitForSeconds(shadeExtraDelay);
        if (shadeT && shadeTarget)
            shadeT.SetPositionAndRotation(shadeTarget.position, shadeTarget.rotation);

        if (bulbT && bulbTarget && bulbDownOffset > 0f)
            bulbT.position = bulbTarget.position - bulbTarget.up * bulbDownOffset;

        StripXRFromChildren();
        EnsureParentColliderFromRenderers();

        parentRB = GetComponent<Rigidbody>();
        if (!parentRB) parentRB = gameObject.AddComponent<Rigidbody>();
        parentRB.useGravity = false;
        parentRB.isKinematic = true; // flip off next fixed step
        parentRB.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        parentRB.interpolation = RigidbodyInterpolation.Interpolate;

        parentGrab = GetComponent<XRGrabInteractable>();
        if (!parentGrab) parentGrab = gameObject.AddComponent<XRGrabInteractable>();
        parentGrab.interactionManager = xrManager;
        parentGrab.movementType = XRGrabInteractable.MovementType.VelocityTracking;
        parentGrab.throwOnDetach = false;                // <- hard-disable throws (prevents the warning)
        parentGrab.trackPosition = true;
        parentGrab.trackRotation = true;

        parentGrab.colliders.Clear();
        if (parentBox) parentGrab.colliders.Add(parentBox);

        if (!combinedAttach) combinedAttach = CreateAttachPointNear(stemT);
        parentGrab.attachTransform = combinedAttach;

        // subscribe (idempotent)
        parentGrab.selectEntered.RemoveListener(OnParentGrabbed);
        parentGrab.selectExited.RemoveListener(OnParentReleased);
        parentGrab.selectEntered.AddListener(OnParentGrabbed);
        parentGrab.selectExited.AddListener(OnParentReleased);

        yield return new WaitForFixedUpdate();
        parentRB.isKinematic = false;  // ensure non-kinematic for VelocityTracking
        parentRB.useGravity  = true;

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

        // inset a hair to reduce surface scraping
        sizeLocal = new Vector3(
            Mathf.Max(0.001f, sizeLocal.x - colliderInset * 2f),
            Mathf.Max(0.001f, sizeLocal.y - colliderInset * 2f),
            Mathf.Max(0.001f, sizeLocal.z - colliderInset * 2f)
        );

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
            g.transform.position = stem.position + stem.up * 0.05f;
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

    // ---------- Stability while grabbing ----------
    void OnParentGrabbed(SelectEnterEventArgs _)
    {
        if (!parentRB) return;

        // Never be kinematic while held (prevents "cannot throw a kinematic RB" spam)
        parentRB.isKinematic = false;
        parentRB.useGravity = true;

        if (parentBox)
        {
            originalIsTrigger = parentBox.isTrigger;
            if (makeTriggerWhileHeld) parentBox.isTrigger = true;
        }

        if (liftOnGrab != 0f)
            transform.position += Vector3.up * liftOnGrab;
    }

    void OnParentReleased(SelectExitEventArgs _)
    {
        if (parentBox) parentBox.isTrigger = originalIsTrigger;

        // We don't throw; make sure we leave clean
        if (parentRB)
        {
            parentRB.linearVelocity = Vector3.zero;
            parentRB.angularVelocity = Vector3.zero;
            parentRB.isKinematic = false;   // keep non-kinematic for normal physics after release
            parentRB.useGravity = true;
        }
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