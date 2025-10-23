using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class LockStemOnSocket : MonoBehaviour
{
    public Transform stemRoot;                 // assign Stem
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable baseGrab;        // assign Base's XR Grab Interactable
    public GameObject stemTopSocketToEnable;   // assign Stem/StemTopSocket (disabled at start)

    UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor socket;

    void Awake() => socket = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
    void OnEnable()  => socket.selectEntered.AddListener(OnInserted);
    void OnDisable() => socket.selectEntered.RemoveListener(OnInserted);

    void OnInserted(SelectEnterEventArgs _)
    {
        LampAssemblyUtils.MergePartIntoBase(stemRoot, baseGrab);
        if (stemTopSocketToEnable) stemTopSocketToEnable.SetActive(true);
    }
}