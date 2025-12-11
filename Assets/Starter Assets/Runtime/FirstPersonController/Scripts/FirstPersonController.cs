using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
[RequireComponent(typeof(PlayerInput))]
#endif
    public class FirstPersonController : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 4.0f;
        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 6.0f;
        [Tooltip("Rotation speed of the character")]
        public float RotationSpeed = 1.0f;
        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;
        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.1f;
        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]
        [Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
        public bool Grounded = true;
        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;
        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.5f;
        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        [Header("Cinemachine")]
        [Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
        public GameObject CinemachineCameraTarget;
        [Tooltip("How far in degrees can you move the camera up")]
        public float TopClamp = 90.0f;
        [Tooltip("How far in degrees can you move the camera down")]
        public float BottomClamp = -90.0f;

        // ---------- CROUCH (MEGA PROSTY) ----------
        [Header("Crouch (Simple)")]
        [Tooltip("Hold C to crouch. If false, C toggles crouch on/off.")]
        public bool HoldToCrouch = false;
        [Tooltip("Target CharacterController height while crouched")]
        public float CrouchHeight = 1.0f;
        [Tooltip("How much to lower the camera target (local Y) while crouched")]
        public float CrouchCameraOffset = -0.5f;
        [Tooltip("Speed multiplier while crouched (0-1)")]
        [Range(0.1f, 1f)]
        public float CrouchSpeedMultiplier = 0.5f;
        [Tooltip("How fast to blend heights/offsets")]
        public float CrouchTransitionSpeed = 12f;
        // -----------------------------------------

        // cinemachine
        private float _cinemachineTargetPitch;

        // player
        private float _speed;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

#if ENABLE_INPUT_SYSTEM
private PlayerInput _playerInput;
#endif
        private CharacterController _controller;
        private StarterAssetsInputs _input;
        private GameObject _mainCamera;

        private const float _threshold = 0.01f;

        // CROUCH internals
        private bool _isCrouching;
        private float _standingHeight;
        private Vector3 _standingCenter;
        private float _standingCameraLocalY;
        private float _targetHeight;
        private float _targetCameraLocalY;

        private bool IsCurrentDeviceMouse
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
return _playerInput.currentControlScheme == "KeyboardMouse";
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            // get a reference to our main camera
            if (_mainCamera == null)
            {
                _mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
        }

        private void Start()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
_playerInput = GetComponent<PlayerInput>();
#else
            Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif

            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;

            // cache standing values for crouch
            _standingHeight = _controller.height;
            _standingCenter = _controller.center;
            _standingCameraLocalY = CinemachineCameraTarget != null ? CinemachineCameraTarget.transform.localPosition.y : 0f;

            // init targets
            _targetHeight = _standingHeight;
            _targetCameraLocalY = _standingCameraLocalY;
        }

        private void Update()
        {
            HandleCrouchInput();     // <<<<<<<<<<<<<<<<<<<<<<<<<< ADDED
            UpdateCrouchBlend();     // <<<<<<<<<<<<<<<<<<<<<<<<<< ADDED

            JumpAndGravity();
            GroundedCheck();
            Move();
        }

        private void LateUpdate()
        {
            CameraRotation();
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
        }

        private void CameraRotation()
        {
            // if there is an input
            if (_input.look.sqrMagnitude >= _threshold)
            {
                //Don't multiply mouse input by Time.deltaTime
                float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

                _cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
                _rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

                // clamp our pitch rotation
                _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

                // Update Cinemachine camera target pitch
                CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

                // rotate the player left and right
                transform.Rotate(Vector3.up * _rotationVelocity);
            }
        }

        private void Move()
        {
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

            // jeśli kucamy — spowolnij i wyłącz sprint
            if (_isCrouching)
            {
                targetSpeed = MoveSpeed * CrouchSpeedMultiplier;
            }

            // if there is no input, set the target speed to 0
            if (_input.move == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            // normalise input direction
            Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

            // rotate + move when there is input
            if (_input.move != Vector2.zero)
            {
                inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
            }

            // move the player
            _controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                _fallTimeoutDelta = FallTimeout;

                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // blokada skoku podczas kucania (opcjonalnie: usuń, jeśli chcesz pozwolić skakać z kuca)
                if (_isCrouching)
                {
                    _input.jump = false;
                }

                // Jump
                if (_input.jump && _jumpTimeoutDelta <= 0.0f)
                {
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                _jumpTimeoutDelta = JumpTimeout;

                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }

                // if we are not grounded, do not jump
                _input.jump = false;
            }

            // apply gravity
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
        {
            if (lfAngle < -360f) lfAngle += 360f;
            if (lfAngle > 360f) lfAngle -= 360f;
            return Mathf.Clamp(lfAngle, lfMin, lfMax);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
        }

        // ----------------- CROUCH METHODS -----------------

        private void HandleCrouchInput()
        {
            bool wantCrouch;

            if (HoldToCrouch)
            {
                wantCrouch = Input.GetKey(KeyCode.C);
            }
            else
            {
                // toggle
                if (Input.GetKeyDown(KeyCode.C))
                    _isCrouching = !_isCrouching;

                wantCrouch = _isCrouching;
            }

            // set targets from desire
            float desiredHeight = wantCrouch ? CrouchHeight : _standingHeight;
            float desiredCamY = wantCrouch ? _standingCameraLocalY + CrouchCameraOffset : _standingCameraLocalY;

            _targetHeight = desiredHeight;
            _targetCameraLocalY = desiredCamY;

            // jeśli tryb hold – aktualny stan wynika bezpośrednio z wejścia
            if (HoldToCrouch) _isCrouching = wantCrouch;
        }

        private void UpdateCrouchBlend()
        {
            // płynnie blenduj wysokość kontrolera
            if (Mathf.Abs(_controller.height - _targetHeight) > 0.001f)
            {
                _controller.height = Mathf.Lerp(_controller.height, _targetHeight, Time.deltaTime * CrouchTransitionSpeed);
            }

            // aktualizuj center tak, by stopa była na ziemi (połowa wysokości)
            Vector3 center = _controller.center;
            center.y = _controller.height * 0.5f;
            _controller.center = Vector3.Lerp(_controller.center, center, Time.deltaTime * CrouchTransitionSpeed);

            // płynnie obniż kamerę
            if (CinemachineCameraTarget != null)
            {
                Vector3 lp = CinemachineCameraTarget.transform.localPosition;
                lp.y = Mathf.Lerp(lp.y, _targetCameraLocalY, Time.deltaTime * CrouchTransitionSpeed);
                CinemachineCameraTarget.transform.localPosition = lp;
            }
        }
    }
}