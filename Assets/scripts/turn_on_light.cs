using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TurnOnLight : MonoBehaviour
{
    [Header("References")]
    public XRSocketInteractor shadeSocket;  // socket where the shade connects
    public Light pointLight;                // the bulb's light
    public GameObject bulbVisual;           // optional: bulb mesh
    public GameObject firstMessage;         // TV text #1
    public GameObject secondMessage;        // TV text #2

    [Header("Settings")]
    public float lightOnDelay = 0.2f;       // delay before light turns on

    private bool assembled = false;

    void Awake()
    {
        if (pointLight) pointLight.enabled = false;

        // make sure messages are off initially
        if (firstMessage) firstMessage.SetActive(false);
        if (secondMessage) secondMessage.SetActive(false);

        if (shadeSocket)
        {
            shadeSocket.selectEntered.AddListener(OnShadeAttached);
            shadeSocket.selectExited.AddListener(OnShadeDetached);
        }
    }

    void OnDestroy()
    {
        if (shadeSocket)
        {
            shadeSocket.selectEntered.RemoveListener(OnShadeAttached);
            shadeSocket.selectExited.RemoveListener(OnShadeDetached);
        }
    }

    void OnShadeAttached(SelectEnterEventArgs args)
    {
        assembled = true;

        if (bulbVisual) bulbVisual.SetActive(true);
        if (pointLight) Invoke(nameof(EnableLight), lightOnDelay);

        // turn on text messages
        if (firstMessage) firstMessage.SetActive(true);
        if (secondMessage) secondMessage.SetActive(true);
    }

    void OnShadeDetached(SelectExitEventArgs args)
    {
        assembled = false;

        if (bulbVisual) bulbVisual.SetActive(false);
        if (pointLight) pointLight.enabled = false;

        // turn off text messages
        if (firstMessage) firstMessage.SetActive(false);
        if (secondMessage) secondMessage.SetActive(false);
    }

    void EnableLight()
    {
        if (assembled && pointLight)
            pointLight.enabled = true;
    }
}