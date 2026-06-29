using UnityEngine;

[DisallowMultipleComponent]
public class CarryOnE : MonoBehaviour
{
    [Header("Refs")]
    public Camera cam;
    public Transform holdPoint;
    public bool useHoldPointPosition = false;

    [Header("Pickup")]
    public KeyCode interactKey = KeyCode.E;
    public KeyCode throwKey = KeyCode.Mouse0;
    public float maxPickupDistance = 3f;
    public float maxCarryMass = 40f;
    public LayerMask interactMask = ~0;
    public bool requirePickupableMarker = false;

    [Header("Carry Position")]
    public float keepDistance = 2.6f;
    public float minCarryDistance = 0.9f;
    public float maxCarryDistance = 4f;
    public float distanceScrollSpeed = 0.7f;
    public float followStrength = 18f;
    public float velocityDamping = 7f;
    public float maxFollowSpeed = 14f;
    public float sphereCastRadius = 0.3f;
    public float wallPadding = 0.08f;
    public float dropIfTooFar = 5f;
    public LayerMask blockingMask = ~0;

    [Header("Carry Rotation")]
    public bool rotateWithCamera = true;
    public bool lockRotationToCamera = true;
    public float rotationStrength = 22f;
    public float rotationDamping = 8f;
    public float maxAngularSpeed = 35f;
    public KeyCode rotateKey = KeyCode.R;
    public float rotateSensitivity = 7f;
    public bool tiltWithMouse = true;
    public float mouseTiltSensitivity = 2.5f;
    public float maxMouseTiltAngle = 18f;
    public float mouseTiltReturnSpeed = 8f;

    [Header("Throw")]
    public float throwForce = 8f;

    [Header("Quality")]
    public bool ignorePlayerCollision = true;
    public bool showPrompt = true;
    public bool drawDebug = false;

    [HideInInspector] public float angularDamp = 8f;

    Rigidbody held;
    Collider[] heldColliders;
    Collider[] playerColliders;

    bool previousUseGravity;
    bool previousIsKinematic;
    RigidbodyInterpolation previousInterpolation;
    CollisionDetectionMode previousCollisionDetection;
    int previousSolverIterations;
    int previousSolverVelocityIterations;

    float currentDistance;
    Quaternion cameraRotationOffset = Quaternion.identity;
    Vector2 mouseTilt;
    readonly RaycastHit[] hits = new RaycastHit[32];

    void Reset()
    {
        cam = GetComponentInChildren<Camera>();
    }

    void Awake()
    {
        ResolveReferences();
    }

    void OnDisable()
    {
        Drop();
    }

    void OnDestroy()
    {
        Drop();
    }

    void Update()
    {
        ResolveReferences();

        if (Input.GetKeyDown(interactKey))
        {
            if (held)
                Drop();
            else
                TryPickup();
        }

        if (!held)
            return;

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
            currentDistance = ClampDistance(currentDistance + scroll * distanceScrollSpeed);

        if (Input.GetKey(rotateKey))
            RotateHeldOffsetFromMouse();
        else
            UpdateMouseTilt();

        if (Input.GetKeyDown(throwKey))
            Throw();
    }

    void FixedUpdate()
    {
        if (!held)
            return;

        Vector3 targetPosition = GetSafeTargetPosition();
        Quaternion targetRotation = GetTargetRotation();

        MoveHeldTo(targetPosition);
        RotateHeldTo(targetRotation);

        if (Vector3.Distance(held.worldCenterOfMass, targetPosition) > dropIfTooFar)
            Drop();
    }

    void TryPickup()
    {
        if (!cam || !FindPickupTarget(out RaycastHit hit, out Rigidbody target))
            return;

        if (target.mass > maxCarryMass)
            return;

        if (requirePickupableMarker && !hit.collider.GetComponentInParent<Pickupable>())
            return;

        Pickup(target);
    }

    void Pickup(Rigidbody target)
    {
        held = target;
        heldColliders = held.GetComponentsInChildren<Collider>();
        SaveRigidbodyState();

        currentDistance = GetInitialDistance();
        cameraRotationOffset = Quaternion.Inverse(GetViewRotation()) * held.rotation;
        mouseTilt = Vector2.zero;

        held.isKinematic = false;
        held.useGravity = false;
        held.interpolation = RigidbodyInterpolation.Interpolate;
        held.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        held.solverIterations = Mathf.Max(previousSolverIterations, 12);
        held.solverVelocityIterations = Mathf.Max(previousSolverVelocityIterations, 12);

        SetPlayerCollisionIgnored(true);
        ShowCarryPrompt();
    }

    bool FindPickupTarget(out RaycastHit bestHit, out Rigidbody target)
    {
        bestHit = default(RaycastHit);
        target = null;

        int hitCount = Physics.RaycastNonAlloc(
            cam.transform.position,
            cam.transform.forward,
            hits,
            maxPickupDistance,
            interactMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (!hit.collider || ShouldIgnoreCollider(hit.collider))
                continue;

            Rigidbody body = hit.rigidbody;
            if (!body || body.transform.IsChildOf(transform))
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
                target = body;
            }
        }

        return target != null;
    }

    Vector3 GetSafeTargetPosition()
    {
        Vector3 origin = cam.transform.position;
        Vector3 wanted = GetWantedTargetPosition();
        Vector3 direction = wanted - origin;
        float distance = direction.magnitude;

        if (distance < 0.05f)
        {
            direction = cam.transform.forward;
            distance = currentDistance;
        }
        else
        {
            direction /= distance;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            sphereCastRadius,
            direction,
            hits,
            distance,
            blockingMask,
            QueryTriggerInteraction.Ignore);

        float nearestBlock = distance;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (!hit.collider || ShouldIgnoreCollider(hit.collider))
                continue;

            nearestBlock = Mathf.Min(nearestBlock, hit.distance);
        }

        float safeDistance = Mathf.Max(0.05f, nearestBlock - wallPadding);
        return origin + direction * safeDistance;
    }

    Vector3 GetWantedTargetPosition()
    {
        if (useHoldPointPosition && holdPoint)
            return holdPoint.position;

        return cam.transform.position + cam.transform.forward * currentDistance;
    }

    Quaternion GetTargetRotation()
    {
        if (!rotateWithCamera)
            return held.rotation;

        Quaternion targetRotation = GetViewRotation() * cameraRotationOffset;
        return ApplyMouseTilt(targetRotation);
    }

    Quaternion GetViewRotation()
    {
        return cam.transform.rotation;
    }

    void MoveHeldTo(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - held.worldCenterOfMass;
        Vector3 wantedVelocity = Vector3.ClampMagnitude(toTarget * followStrength, maxFollowSpeed);
        Vector3 velocityChange = wantedVelocity - held.linearVelocity;
        held.linearVelocity += velocityChange * Mathf.Clamp01(velocityDamping * Time.fixedDeltaTime);
    }

    void RotateHeldTo(Quaternion targetRotation)
    {
        if (!rotateWithCamera)
            return;

        if (lockRotationToCamera)
        {
            held.MoveRotation(targetRotation);
            held.angularVelocity = Vector3.Lerp(held.angularVelocity, Vector3.zero, angularDamp * Time.fixedDeltaTime);
            return;
        }

        Quaternion delta = targetRotation * Quaternion.Inverse(held.rotation);
        delta.ToAngleAxis(out float angle, out Vector3 axis);

        if (float.IsNaN(axis.x) || axis.sqrMagnitude < 0.0001f)
            return;

        if (angle > 180f)
            angle -= 360f;

        Vector3 wantedAngularVelocity = axis.normalized * (angle * Mathf.Deg2Rad * rotationStrength);
        wantedAngularVelocity = Vector3.ClampMagnitude(wantedAngularVelocity, maxAngularSpeed);
        Vector3 angularChange = wantedAngularVelocity - held.angularVelocity;
        held.angularVelocity += angularChange * Mathf.Clamp01(rotationDamping * Time.fixedDeltaTime);
    }

    void RotateHeldOffsetFromMouse()
    {
        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        if (Mathf.Abs(mouseX) < 0.01f && Mathf.Abs(mouseY) < 0.01f)
            return;

        Quaternion viewRotation = GetViewRotation();
        Quaternion targetRotation = viewRotation * cameraRotationOffset;
        Quaternion yaw = Quaternion.AngleAxis(mouseX * rotateSensitivity, viewRotation * Vector3.up);
        Quaternion pitch = Quaternion.AngleAxis(-mouseY * rotateSensitivity, viewRotation * Vector3.right);

        targetRotation = yaw * pitch * targetRotation;
        cameraRotationOffset = Quaternion.Inverse(viewRotation) * targetRotation;
        mouseTilt = Vector2.zero;
    }

    void UpdateMouseTilt()
    {
        if (!tiltWithMouse)
        {
            mouseTilt = Vector2.zero;
            return;
        }

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");

        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            mouseTilt.x = Mathf.Clamp(mouseTilt.x - mouseY * mouseTiltSensitivity, -maxMouseTiltAngle, maxMouseTiltAngle);
            mouseTilt.y = Mathf.Clamp(mouseTilt.y - mouseX * mouseTiltSensitivity, -maxMouseTiltAngle, maxMouseTiltAngle);
            return;
        }

        mouseTilt = Vector2.Lerp(mouseTilt, Vector2.zero, mouseTiltReturnSpeed * Time.deltaTime);
    }

    Quaternion ApplyMouseTilt(Quaternion targetRotation)
    {
        if (!tiltWithMouse)
            return targetRotation;

        Quaternion viewRotation = GetViewRotation();
        Quaternion pitch = Quaternion.AngleAxis(mouseTilt.x, viewRotation * Vector3.right);
        Quaternion roll = Quaternion.AngleAxis(mouseTilt.y, viewRotation * Vector3.forward);
        return roll * pitch * targetRotation;
    }

    float GetInitialDistance()
    {
        if (useHoldPointPosition && holdPoint)
            return ClampDistance(Vector3.Distance(cam.transform.position, holdPoint.position));

        return ClampDistance(keepDistance);
    }

    float ClampDistance(float distance)
    {
        float min = Mathf.Max(0.05f, minCarryDistance);
        float max = Mathf.Max(min, maxCarryDistance);
        return Mathf.Clamp(distance, min, max);
    }

    bool ShouldIgnoreCollider(Collider candidate)
    {
        if (!candidate)
            return true;

        if (held && candidate.attachedRigidbody == held)
            return true;

        if (candidate.transform.IsChildOf(transform))
            return true;

        if (playerColliders == null)
            return false;

        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (candidate == playerColliders[i])
                return true;
        }

        return false;
    }

    void SaveRigidbodyState()
    {
        previousUseGravity = held.useGravity;
        previousIsKinematic = held.isKinematic;
        previousInterpolation = held.interpolation;
        previousCollisionDetection = held.collisionDetectionMode;
        previousSolverIterations = held.solverIterations;
        previousSolverVelocityIterations = held.solverVelocityIterations;
    }

    void RestoreRigidbodyState(Rigidbody body)
    {
        body.useGravity = previousUseGravity;
        body.isKinematic = previousIsKinematic;
        body.interpolation = previousInterpolation;
        body.collisionDetectionMode = previousCollisionDetection;
        body.solverIterations = previousSolverIterations;
        body.solverVelocityIterations = previousSolverVelocityIterations;
    }

    void SetPlayerCollisionIgnored(bool ignored)
    {
        if (!ignorePlayerCollision || heldColliders == null || playerColliders == null)
            return;

        for (int i = 0; i < heldColliders.Length; i++)
        {
            Collider heldCollider = heldColliders[i];
            if (!heldCollider)
                continue;

            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (!playerCollider || heldCollider == playerCollider)
                    continue;

                Physics.IgnoreCollision(heldCollider, playerCollider, ignored);
            }
        }
    }

    void ResolveReferences()
    {
        if (!cam)
            cam = GetComponentInChildren<Camera>();

        if (!cam)
            cam = Camera.main;

        if (playerColliders == null || playerColliders.Length == 0)
            playerColliders = GetComponentsInChildren<Collider>();
    }

    void ShowCarryPrompt()
    {
        if (!showPrompt)
            return;

        InteractionPromptUI.Instance?.Show("E - upusc | LPM - rzuc | Rolka - dystans | R - obrot");
    }

    public void Drop()
    {
        if (!held)
            return;

        SetPlayerCollisionIgnored(false);
        RestoreRigidbodyState(held);

        held = null;
        heldColliders = null;
        mouseTilt = Vector2.zero;

        if (showPrompt)
            InteractionPromptUI.Instance?.Hide();
    }

    public void Throw()
    {
        if (!held)
            return;

        Rigidbody thrown = held;
        Drop();

        if (cam)
            thrown.AddForce(cam.transform.forward * throwForce, ForceMode.VelocityChange);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawDebug || !cam)
            return;

        Gizmos.color = held ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(GetWantedTargetPosition(), sphereCastRadius);
    }
}
