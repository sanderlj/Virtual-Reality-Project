using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[DisallowMultipleComponent]
public class EnableOnSocketAttach : MonoBehaviour
{
    [Header("This socket (the one that receives the current part)")]
    [SerializeField] private XRSocketInteractor socket;

    [Header("Only trigger when THIS part is attached (optional)")]
    [SerializeField] private XRBaseInteractable requiredInteractable; // e.g., the Stem

    [Header("Sockets to activate when attached")]
    [SerializeField] private List<XRSocketInteractor> socketsToActivate = new();

    [Header("Behavior")]
    [SerializeField] private bool deactivateThisSocketAfterAttach = true; // turn off this socket once filled
    [SerializeField] private bool revertOnDetach = true;                  // undo if the part is removed

    private void Reset()
    {
        if (!socket) socket = GetComponent<XRSocketInteractor>();
    }

    private void OnEnable()
    {
        if (!socket) socket = GetComponent<XRSocketInteractor>();
        socket.selectEntered.AddListener(OnSelectEntered);
        socket.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        socket.selectEntered.RemoveListener(OnSelectEntered);
        socket.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Only react to the expected part if one is specified
        if (requiredInteractable)
        {
            var attached = args.interactableObject as XRBaseInteractable;
            if (!attached || attached.transform != requiredInteractable.transform)
                return;
        }

        // ✅ Turn on the next sockets by checking "Socket Active"
        foreach (var s in socketsToActivate)
            if (s) s.socketActive = true;

        // Optionally turn off this socket now that it’s filled
        if (deactivateThisSocketAfterAttach && socket)
            socket.socketActive = false;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (!revertOnDetach) return;

        // Turn targets back off if the piece is removed
        foreach (var s in socketsToActivate)
            if (s) s.socketActive = false;

        if (deactivateThisSocketAfterAttach && socket)
            socket.socketActive = true;
    }
}