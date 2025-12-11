using UnityEngine;

public class CarryOnEStable : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public Transform holdPoint;

    [Header("Pickup Settings")]
    public float maxPickupDistance = 3f;
    public float maxCarryMass = 40f;
    public LayerMask interactMask = ~0;   // np. tylko Interactable

    [Header("Key")]
    public KeyCode interactKey = KeyCode.E;

    Rigidbody held;
    bool prevKinematic, prevUseGravity;
    Transform prevParent;

    void Reset()
    {
        cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (Input.GetKeyDown(interactKey))
        {
            if (held) Drop();
            else TryPickup();
        }

        // jeœli trzymamy – trzymaj cube’a dok³adnie w punkcie
        if (held && holdPoint)
        {
            held.transform.position = holdPoint.position;
            held.transform.rotation = holdPoint.rotation;
        }
    }

    void TryPickup()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxPickupDistance, interactMask, QueryTriggerInteraction.Ignore))
            return;

        var rb = hit.rigidbody;
        if (!rb || rb.mass > maxCarryMass) return;

        held = rb;

        // zapamiêtaj
        prevKinematic = held.isKinematic;
        prevUseGravity = held.useGravity;
        prevParent = held.transform.parent;

        // wy³¹cz fizykê, ¿eby nie drga³
        held.isKinematic = true;
        held.useGravity = false;

        // podepnij pod punkt trzymania
        if (holdPoint)
        {
            held.transform.SetParent(holdPoint, true);
            held.transform.position = holdPoint.position;
            held.transform.rotation = holdPoint.rotation;
        }
    }

    void Drop()
    {
        if (!held) return;

        // odczep z powrotem
        held.transform.SetParent(prevParent, true);

        // przywróæ fizykê
        held.isKinematic = prevKinematic;
        held.useGravity = prevUseGravity;

        held = null;
    }
}
