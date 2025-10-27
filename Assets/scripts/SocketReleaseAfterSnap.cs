using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public class SocketReleaseAfterSnap : MonoBehaviour
{
    private XRSocketInteractor socket;

    [Tooltip("Delay before locking the bulb in place.")]
    public float releaseDelay = 0.05f;

    private void Awake() => socket = GetComponent<XRSocketInteractor>();

    private void OnEnable() => socket.selectEntered.AddListener(OnSocketSnap);
    private void OnDisable() => socket.selectEntered.RemoveListener(OnSocketSnap);

    private void OnSocketSnap(SelectEnterEventArgs args)
    {
        var grab = args.interactableObject as XRGrabInteractable;
        if (grab == null) return;

        StartCoroutine(LockAfterSnap(grab));
    }

    private IEnumerator LockAfterSnap(XRGrabInteractable grab)
    {
        yield return new WaitForSeconds(releaseDelay);

        // ✅ Force release from any other interactor (hand)
        var selecting = grab.interactorsSelecting;
        if (selecting != null && selecting.Count > 0)
        {
            var interactor = selecting[0];
            if ((Object)interactor != (Object)socket)
                socket.interactionManager.SelectExit(interactor, grab);
        }

        // ✅ Make bulb kinematic & disable grabbing
        var rb = grab.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;

        grab.interactionLayers = new InteractionLayerMask();
        grab.movementType = XRGrabInteractable.MovementType.Kinematic;

        // ✅ Keep socket in control
        socket.socketActive = false;

        // ✅ Start screw motion
        grab.GetComponent<ScrewDownMotion>()?.BeginScrewing();
    }
}



