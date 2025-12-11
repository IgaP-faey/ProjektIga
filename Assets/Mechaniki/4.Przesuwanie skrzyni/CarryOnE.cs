using UnityEngine;

public class CarryOnE : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public Transform holdPoint;

    [Header("Pickup Settings")]
    public float maxPickupDistance = 3f;
    public float maxCarryMass = 40f;
    public LayerMask interactMask = ~0;   // ustaw np. tylko Interactable

    [Header("Carry Feel")]
    public float keepDistance = 2f;       // docelowa odleg�o�� od kamery
    public float followStrength = 50f;    // si�a �doci�gania�
    public float maxFollowSpeed = 12f;    // ograniczenie pr�dko�ci
    public float angularDamp = 8f;        // wygaszanie obrotu
    public float sphereCastRadius = 0.28f;// anty-przenikanie
    public float dropIfTooFar = 5f;       // zabezpieczenie

    [Header("Key")]
    public KeyCode interactKey = KeyCode.E;

    Rigidbody held;
    bool prevKinematic, prevUseGravity;
    Vector3 localGrabOffset;

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
    }

    void FixedUpdate()
    {
        if (!held) return;

        // docelowa pozycja: punkt przed kamer� (z anty-kolizj�)
        Vector3 from = cam.transform.position;
        Vector3 desired = holdPoint ? holdPoint.TransformPoint(localGrabOffset)
                                    : (cam.transform.position + cam.transform.forward * keepDistance);

        Vector3 dir = (desired - from);
        float dist = Mathf.Min(dir.magnitude, keepDistance);
        Vector3 dirN = dir.sqrMagnitude > 0.0001f ? dir.normalized : cam.transform.forward;

        if (Physics.SphereCast(from, sphereCastRadius, dirN, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
            desired = hit.point - dirN * sphereCastRadius;
        else
            desired = from + dirN * dist;

        // doci�ganie pozycj� przez pr�dko�� (stabilne z fizyk�)
        Vector3 toTarget = (desired - held.position);
        Vector3 vel = Vector3.ClampMagnitude(toTarget * followStrength, maxFollowSpeed);
        held.linearVelocity = vel;

        // wygaszanie rotacji
        held.angularVelocity = Vector3.Lerp(held.angularVelocity, Vector3.zero, Time.fixedDeltaTime * angularDamp);

        // auto-drop w razie k�opot�w
        if (Vector3.Distance(transform.position, held.position) > dropIfTooFar)
            Drop();
    }

    void TryPickup()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxPickupDistance, interactMask, QueryTriggerInteraction.Ignore))
            return;

        var rb = hit.rigidbody;
        if (!rb || rb.mass > maxCarryMass) return;

        held = rb;

        // zapami�taj stan i wy��cz grawitacj� na czas trzymania
        prevKinematic = held.isKinematic;
        prevUseGravity = held.useGravity;
        held.isKinematic = false;
        held.useGravity = false;
        held.interpolation = RigidbodyInterpolation.Interpolate;

        // offset �miejsca z�apania� wzgl�dem holdPoint (je�li go u�ywasz)
        if (holdPoint)
            localGrabOffset = holdPoint.InverseTransformPoint(hit.point);
        else
            localGrabOffset = Vector3.forward * keepDistance; // awaryjnie

        // ma�e u�atwienie � je�li z�apa�e� bli�ej, dopasuj minimaln� odleg�o��
        keepDistance = Mathf.Max(keepDistance, Vector3.Distance(cam.transform.position, hit.point));
    }

    void Drop()
    {
        if (!held) return;
        held.useGravity = prevUseGravity;
        held.isKinematic = prevKinematic;
        held = null;
    }
}
