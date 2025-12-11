using UnityEngine;

public class PlayerCarry : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public Transform holdPoint;

    [Header("Pickup")]
    public float maxPickupDistance = 3.0f;
    public float maxCarryMass = 30f;
    public LayerMask interactMask;

    [Header("Carry Tuning")]
    public float followStrength = 50f;      // im wi�ksze, tym szybciej dogania punkt
    public float maxFollowSpeed = 10f;      // cap pr�dko�ci, by nie teleportowa�
    public float rotateDamp = 8f;           // t�umienie obrotu
    public float keepDistance = 2.0f;       // docelowa odleg�o�� od kamery
    public float sphereCastRadius = 0.3f;   // anty-przenikanie
    public float dropDistance = 4.0f;       // je�li odejdziemy dalej � upu��

    [Header("Throw")]
    public float throwForce = 10f;

    Rigidbody held;
    Vector3 holdLocal; // offset wzgl�dem holdPoint (zachowuje uchwyt)
    bool wasKinematic;
    bool wasUseGravity;

    void Reset()
    {
        cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            if (held) Drop();
            else TryPickup();
        }

        if (held && Input.GetMouseButtonDown(0))
        {
            Throw();
        }
    }

    void FixedUpdate()
    {
        if (!held) return;

        // 1) Oblicz docelow� pozycj� z anty-przenikaniem (spherecast z kamery)
        Vector3 targetWorld = holdPoint.TransformPoint(holdLocal);
        Vector3 from = cam.transform.position;
        Vector3 dir = (targetWorld - from).normalized;
        float dist = Mathf.Min(Vector3.Distance(from, targetWorld), keepDistance);

        if (Physics.SphereCast(from, sphereCastRadius, dir, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
        {
            targetWorld = hit.point - dir * sphereCastRadius; // zatrzymaj przed przeszkod�
        }
        else
        {
            targetWorld = from + dir * dist;
        }

        // 2) Doci�gaj pozycj� �si�owo� (stabilne z fizyk�)
        Vector3 toTarget = (targetWorld - held.position);
        Vector3 desiredVel = Vector3.ClampMagnitude(toTarget * followStrength, maxFollowSpeed);
        held.linearVelocity = desiredVel;

        // 3) Wygaszaj obr�t (�eby skrzynia si� nie kr�ci�a)
        held.angularVelocity = Vector3.Lerp(held.angularVelocity, Vector3.zero, Time.fixedDeltaTime * rotateDamp);

        // 4) Zabezpieczenie: je�li gracz oddali si� za bardzo � upu��
        if (Vector3.Distance(held.position, transform.position) > dropDistance)
            Drop();
    }

    void TryPickup()
    {
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, maxPickupDistance, interactMask, QueryTriggerInteraction.Ignore))
        {
            var rb = hit.rigidbody;
            var pick = hit.collider.GetComponentInParent<Pickupable>();
            if (rb != null && pick != null && rb.mass <= maxCarryMass)
            {
                held = rb;
                wasKinematic = held.isKinematic;
                wasUseGravity = held.useGravity;

                // no gravity, �eby nie �opada�o� podczas trzymania
                held.isKinematic = false;
                held.useGravity = false;
                held.interpolation = RigidbodyInterpolation.Interpolate;

                // offset uchwytu wzgl�dem holdPoint (trzymamy �za miejsce z�apania�)
                holdLocal = holdPoint.InverseTransformPoint(hit.point);

                // podbij minimaln� odleg�o��
                keepDistance = Mathf.Max(keepDistance, Vector3.Distance(cam.transform.position, hit.point));

                InteractionPromptUI.Instance?.Show("F � upu��   LPM � rzu�");
            }
        }
    }

    void Drop()
    {
        if (!held) return;
        held.useGravity = wasUseGravity;
        held.isKinematic = wasKinematic;
        held = null;
        InteractionPromptUI.Instance?.Hide();
    }

    void Throw()
    {
        if (!held) return;
        var rb = held;
        Drop();
        rb.AddForce(cam.transform.forward * throwForce, ForceMode.VelocityChange);
    }
}
