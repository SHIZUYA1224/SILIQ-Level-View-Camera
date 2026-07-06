using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class LevelViewCameraController : MonoBehaviour
{
    public enum HeightPreset
    {
        SmallAvatar,
        Seated,
        ShortAvatar,
        AverageVRChatAvatar,
        TallAvatar,
        Custom
    }

    private const float ControllerRadius = 0.3f;
    private const float EyeOffsetFromTop = 0.1f;
    private const float DefaultCrouchBodyHeightRatio = 0.75f;
    private const float MinBodyHeight = 0.4f;
    private const float MinEyeHeight = 0.2f;

    [SerializeField] private HeightPreset heightPreset = HeightPreset.AverageVRChatAvatar;

    [Min(MinBodyHeight)] public float bodyHeight = 1.7f;
    [Min(MinBodyHeight)] public float crouchBodyHeight = 1.3f;
    [HideInInspector] public float eyeHeight = 1.6f;
    [HideInInspector] public float crouchEyeHeight = 1.2f;
    [Min(0f)] public float walkSpeed = 2.0f;
    [Min(0f)] public float sprintSpeed = 4.0f;
    [Min(0f)] public float jumpHeight = 0.5f;
    [Min(0.01f)] public float mouseSensitivity = 2.0f;
    [Min(0.01f)] public float gravity = 9.81f;
    public bool useCollision = true;
    public bool showGizmos = true;

    private CharacterController characterController;
    private Transform movementRoot;
    private GameObject runtimeRig;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool hasInitialState;
    private float yaw;
    private float pitch;
    private float verticalVelocity;
    private float currentBodyHeight;
    private float currentEyeHeight;
    private float currentSpeed;
    private bool isGrounded;
    private bool isMouseLocked;

    public HeightPreset Preset
    {
        get { return heightPreset; }
        set
        {
            heightPreset = value;
            ApplyHeightPreset();
        }
    }

    public float CurrentEyeHeight
    {
        get { return Application.isPlaying ? currentEyeHeight : eyeHeight; }
    }

    public float CurrentBodyHeight
    {
        get { return Application.isPlaying ? currentBodyHeight : bodyHeight; }
    }

    public bool IsGrounded
    {
        get { return isGrounded; }
    }

    public float CurrentSpeed
    {
        get { return currentSpeed; }
    }

    public Vector3 InitialPosition
    {
        get { return hasInitialState ? initialPosition : transform.position; }
    }

    public string CollisionMode
    {
        get { return useCollision ? "Collision ON" : "Collision OFF"; }
    }

    public static float GetPresetBodyHeight(HeightPreset preset)
    {
        switch (preset)
        {
            case HeightPreset.SmallAvatar:
                return 1.10f;
            case HeightPreset.Seated:
                return 1.30f;
            case HeightPreset.ShortAvatar:
                return 1.50f;
            case HeightPreset.AverageVRChatAvatar:
                return 1.70f;
            case HeightPreset.TallAvatar:
                return 1.90f;
            case HeightPreset.Custom:
            default:
                return 1.70f;
        }
    }

    public static float GetPresetHeight(HeightPreset preset)
    {
        return GetPresetBodyHeight(preset);
    }

    public static float CalculateEyeHeight(float sourceBodyHeight)
    {
        return Mathf.Max(MinEyeHeight, sourceBodyHeight - EyeOffsetFromTop);
    }

    public static float CalculateDefaultCrouchBodyHeight(float sourceBodyHeight)
    {
        return Mathf.Max(MinBodyHeight, sourceBodyHeight * DefaultCrouchBodyHeightRatio);
    }

    public void ApplyHeightPreset()
    {
        if (heightPreset != HeightPreset.Custom)
        {
            bodyHeight = GetPresetBodyHeight(heightPreset);
        }

        ClampSettings();
    }

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        InitializeRuntimeState();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && isMouseLocked)
        {
            SetMouseLock(false);
        }
    }

    private void OnValidate()
    {
        ApplyHeightPreset();

        if (Application.isPlaying)
        {
            currentEyeHeight = Mathf.Max(currentEyeHeight, MinEyeHeight);
            currentBodyHeight = Mathf.Max(currentBodyHeight, MinBodyHeight);
            ApplyCharacterControllerSettings(CurrentBodyHeight);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!hasInitialState)
        {
            InitializeRuntimeState();
        }

        UpdateCursorLock();

        if (isMouseLocked)
        {
            UpdateLookRotation();
        }

        if (ReadResetPressed())
        {
            ResetToInitialState();
            return;
        }

        if (useCollision)
        {
            UpdateCollisionMovement();
        }
        else
        {
            UpdateFreeMovement();
        }
    }

    private void InitializeRuntimeState()
    {
        ApplyHeightPreset();

        initialPosition = transform.position;
        initialRotation = transform.rotation;
        hasInitialState = true;

        ExtractYawPitch(initialRotation);
        currentBodyHeight = bodyHeight;
        currentEyeHeight = eyeHeight;
        currentSpeed = 0f;
        verticalVelocity = 0f;
        movementRoot = transform;

        if (useCollision)
        {
            EnsureCharacterController();
        }

        ApplyViewRotation();
        SetMouseLock(true);
    }

    private void UpdateCollisionMovement()
    {
        EnsureCharacterController();

        float targetBodyHeight = ReadCrouchHeld() ? crouchBodyHeight : bodyHeight;
        ApplyBodyHeight(targetBodyHeight);
        ApplyCharacterControllerSettings(currentBodyHeight);

        isGrounded = characterController.isGrounded;
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (ReadJumpPressed() && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
        }

        verticalVelocity -= gravity * Time.deltaTime;

        Vector3 horizontalMove = GetPlanarMoveDirection(ReadMoveInput());
        float targetSpeed = ReadSprintHeld() ? sprintSpeed : walkSpeed;
        Vector3 velocity = horizontalMove * targetSpeed;
        velocity.y = verticalVelocity;

        CollisionFlags flags = characterController.Move(velocity * Time.deltaTime);
        isGrounded = characterController.isGrounded || (flags & CollisionFlags.Below) != 0;
        currentSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
    }

    private void UpdateFreeMovement()
    {
        DisableCollisionControllers();
        ApplyBodyHeight(bodyHeight);

        isGrounded = false;
        verticalVelocity = 0f;

        Vector3 move = GetPlanarMoveDirection(ReadMoveInput());
        if (ReadJumpHeld())
        {
            move += Vector3.up;
        }

        if (ReadCrouchHeld())
        {
            move += Vector3.down;
        }

        move = Vector3.ClampMagnitude(move, 1f);

        float targetSpeed = ReadSprintHeld() ? sprintSpeed : walkSpeed;
        Vector3 velocity = move * targetSpeed;
        GetMovementRoot().position += velocity * Time.deltaTime;
        currentSpeed = velocity.magnitude;
    }

    private void EnsureCharacterController()
    {
        if (movementRoot == null)
        {
            movementRoot = transform;
        }

        if (movementRoot == transform)
        {
            CreateRuntimeRig();
        }

        if (characterController == null)
        {
            characterController = movementRoot.GetComponent<CharacterController>();
        }

        if (characterController == null)
        {
            characterController = movementRoot.gameObject.AddComponent<CharacterController>();
        }

        if (!characterController.enabled)
        {
            characterController.enabled = true;
        }

        ApplyCharacterControllerSettings(CurrentBodyHeight);
    }

    private void CreateRuntimeRig()
    {
        Vector3 footPosition = GetFootPosition(transform.position, currentEyeHeight);
        Transform originalParent = transform.parent;

        runtimeRig = new GameObject(gameObject.name + " Level View Runtime Rig");
        runtimeRig.hideFlags = HideFlags.DontSave;
        movementRoot = runtimeRig.transform;
        movementRoot.SetParent(originalParent, true);
        movementRoot.SetPositionAndRotation(footPosition, Quaternion.Euler(0f, yaw, 0f));

        CharacterController cameraController = GetComponent<CharacterController>();
        if (cameraController != null)
        {
            cameraController.enabled = false;
        }

        transform.SetParent(movementRoot, true);
        SetCameraLocalPose(currentEyeHeight);
    }

    private void DisableCollisionControllers()
    {
        if (characterController != null && characterController.enabled)
        {
            characterController.enabled = false;
        }

        CharacterController cameraController = GetComponent<CharacterController>();
        if (cameraController != null && cameraController.enabled)
        {
            cameraController.enabled = false;
        }
    }

    private void ApplyCharacterControllerSettings(float activeBodyHeight)
    {
        if (characterController == null)
        {
            return;
        }

        float capsuleHeight = Mathf.Max(activeBodyHeight, ControllerRadius * 2f + 0.01f);
        characterController.height = capsuleHeight;
        characterController.radius = ControllerRadius;
        characterController.center = new Vector3(0f, capsuleHeight * 0.5f, 0f);
    }

    private void ApplyBodyHeight(float targetBodyHeight)
    {
        targetBodyHeight = Mathf.Max(MinBodyHeight, targetBodyHeight);
        float targetEyeHeight = CalculateEyeHeight(targetBodyHeight);
        if (Mathf.Approximately(currentEyeHeight, targetEyeHeight))
        {
            currentBodyHeight = targetBodyHeight;
            currentEyeHeight = targetEyeHeight;
            return;
        }

        if (movementRoot != null && movementRoot != transform)
        {
            SetCameraLocalPose(targetEyeHeight);
        }
        else
        {
            Vector3 footPosition = GetFootPosition(transform.position, currentEyeHeight);
            transform.position = footPosition + Vector3.up * targetEyeHeight;
        }

        currentBodyHeight = targetBodyHeight;
        currentEyeHeight = targetEyeHeight;
    }

    private void SetCameraLocalPose(float activeEyeHeight)
    {
        transform.localPosition = Vector3.up * activeEyeHeight;
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private Transform GetMovementRoot()
    {
        return movementRoot != null ? movementRoot : transform;
    }

    private Vector3 GetPlanarMoveDirection(Vector2 input)
    {
        Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);
        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        Vector3 direction = yawRotation * new Vector3(clampedInput.x, 0f, clampedInput.y);
        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void UpdateLookRotation()
    {
        Vector2 lookInput = ReadLookInput();
        yaw += lookInput.x * mouseSensitivity;
        pitch -= lookInput.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        ApplyViewRotation();
    }

    private void ApplyViewRotation()
    {
        if (movementRoot != null && movementRoot != transform)
        {
            movementRoot.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    private void UpdateCursorLock()
    {
        if (ReadUnlockMousePressed())
        {
            SetMouseLock(false);
            return;
        }

        if (!isMouseLocked && ReadLockMousePressed())
        {
            SetMouseLock(true);
        }
    }

    private void SetMouseLock(bool locked)
    {
        isMouseLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void ResetToInitialState()
    {
        if (!hasInitialState)
        {
            return;
        }

        bool wasControllerEnabled = characterController != null && characterController.enabled;
        if (wasControllerEnabled)
        {
            characterController.enabled = false;
        }

        ExtractYawPitch(initialRotation);
        currentBodyHeight = bodyHeight;
        currentEyeHeight = eyeHeight;

        if (movementRoot != null && movementRoot != transform)
        {
            Vector3 footPosition = GetFootPosition(initialPosition, currentEyeHeight);
            movementRoot.SetPositionAndRotation(footPosition, Quaternion.Euler(0f, yaw, 0f));
            SetCameraLocalPose(currentEyeHeight);
        }
        else
        {
            transform.SetPositionAndRotation(initialPosition, initialRotation);
        }

        verticalVelocity = 0f;
        currentSpeed = 0f;

        if (wasControllerEnabled)
        {
            characterController.enabled = true;
            ApplyCharacterControllerSettings(currentBodyHeight);
        }
    }

    private void ExtractYawPitch(Quaternion rotation)
    {
        Vector3 euler = rotation.eulerAngles;
        yaw = euler.y;
        pitch = NormalizeAngle(euler.x);
        pitch = Mathf.Clamp(pitch, -89f, 89f);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private Vector3 GetFootPosition(Vector3 eyePosition, float activeEyeHeight)
    {
        return eyePosition - Vector3.up * Mathf.Max(MinEyeHeight, activeEyeHeight);
    }

    private void ClampSettings()
    {
        bodyHeight = Mathf.Max(MinBodyHeight, bodyHeight);
        crouchBodyHeight = Mathf.Clamp(crouchBodyHeight, MinBodyHeight, bodyHeight);
        eyeHeight = CalculateEyeHeight(bodyHeight);
        crouchEyeHeight = CalculateEyeHeight(crouchBodyHeight);
        walkSpeed = Mathf.Max(0f, walkSpeed);
        sprintSpeed = Mathf.Max(walkSpeed, sprintSpeed);
        jumpHeight = Mathf.Max(0f, jumpHeight);
        mouseSensitivity = Mathf.Max(0.01f, mouseSensitivity);
        gravity = Mathf.Max(0.01f, gravity);
    }

    private Vector2 ReadMoveInput()
    {
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }

    private Vector2 ReadLookInput()
    {
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private bool ReadJumpPressed()
    {
        return Input.GetKeyDown(KeyCode.Space);
    }

    private bool ReadJumpHeld()
    {
        return Input.GetKey(KeyCode.Space);
    }

    private bool ReadSprintHeld()
    {
        return Input.GetKey(KeyCode.LeftShift);
    }

    private bool ReadCrouchHeld()
    {
        return Input.GetKey(KeyCode.LeftControl);
    }

    private bool ReadResetPressed()
    {
        return Input.GetKeyDown(KeyCode.R);
    }

    private bool ReadUnlockMousePressed()
    {
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private bool ReadLockMousePressed()
    {
        return Input.GetMouseButtonDown(0);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        float displayBodyHeight = Mathf.Max(MinBodyHeight, Application.isPlaying ? CurrentBodyHeight : bodyHeight);
        float displayEyeHeight = Mathf.Max(MinEyeHeight, Application.isPlaying ? CurrentEyeHeight : eyeHeight);
        Vector3 footPosition = GetFootPosition(transform.position, displayEyeHeight);
        Vector3 eyePosition = footPosition + Vector3.up * displayEyeHeight;
        Vector3 forward = transform.forward;
        if (Mathf.Abs(Vector3.Dot(forward.normalized, Vector3.up)) > 0.99f)
        {
            forward = transform.rotation * Vector3.forward;
        }

        DrawEyeHeight(footPosition, eyePosition);
        DrawFootPosition(footPosition);
        DrawForwardDirection(eyePosition, forward);
        DrawCharacterControllerCapsule(footPosition, displayBodyHeight);
        DrawHeightLabel(eyePosition, displayBodyHeight, displayEyeHeight);
    }

    private void DrawEyeHeight(Vector3 footPosition, Vector3 eyePosition)
    {
        Gizmos.color = new Color(0.1f, 0.7f, 1f, 1f);
        Gizmos.DrawLine(footPosition, eyePosition);
        Gizmos.DrawLine(eyePosition + Vector3.left * 0.35f, eyePosition + Vector3.right * 0.35f);
    }

    private void DrawFootPosition(Vector3 footPosition)
    {
        Gizmos.color = new Color(1f, 0.75f, 0.15f, 1f);
        Gizmos.DrawWireSphere(footPosition, 0.08f);
        Handles.color = Gizmos.color;
        Handles.DrawWireDisc(footPosition, Vector3.up, ControllerRadius);
    }

    private void DrawForwardDirection(Vector3 eyePosition, Vector3 forward)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        if (planarForward.sqrMagnitude < 0.0001f)
        {
            planarForward = Vector3.forward;
        }

        planarForward.Normalize();
        Gizmos.color = new Color(0.2f, 1f, 0.35f, 1f);
        Gizmos.DrawLine(eyePosition, eyePosition + planarForward * 0.8f);
        Handles.color = Gizmos.color;
        Handles.ArrowHandleCap(0, eyePosition + planarForward * 0.8f, Quaternion.LookRotation(planarForward), 0.2f, EventType.Repaint);
    }

    private void DrawCharacterControllerCapsule(Vector3 footPosition, float height)
    {
        height = Mathf.Max(height, ControllerRadius * 2f + 0.01f);
        Vector3 bottomSphere = footPosition + Vector3.up * ControllerRadius;
        Vector3 topSphere = footPosition + Vector3.up * (height - ControllerRadius);

        Handles.color = new Color(1f, 1f, 1f, 0.8f);
        Handles.DrawWireDisc(bottomSphere, Vector3.up, ControllerRadius);
        Handles.DrawWireDisc(topSphere, Vector3.up, ControllerRadius);
        Handles.DrawWireArc(bottomSphere, Vector3.forward, Vector3.right, 180f, ControllerRadius);
        Handles.DrawWireArc(bottomSphere, Vector3.right, Vector3.back, 180f, ControllerRadius);
        Handles.DrawWireArc(topSphere, Vector3.forward, Vector3.left, 180f, ControllerRadius);
        Handles.DrawWireArc(topSphere, Vector3.right, Vector3.forward, 180f, ControllerRadius);

        Handles.DrawLine(bottomSphere + Vector3.right * ControllerRadius, topSphere + Vector3.right * ControllerRadius);
        Handles.DrawLine(bottomSphere - Vector3.right * ControllerRadius, topSphere - Vector3.right * ControllerRadius);
        Handles.DrawLine(bottomSphere + Vector3.forward * ControllerRadius, topSphere + Vector3.forward * ControllerRadius);
        Handles.DrawLine(bottomSphere - Vector3.forward * ControllerRadius, topSphere - Vector3.forward * ControllerRadius);
    }

    private void DrawHeightLabel(Vector3 eyePosition, float displayBodyHeight, float displayEyeHeight)
    {
        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
        labelStyle.normal.textColor = Color.white;
        Handles.Label(eyePosition + Vector3.up * 0.12f, string.Format("Body {0:0.00} m / Eye {1:0.00} m", displayBodyHeight, displayEyeHeight), labelStyle);
    }
#endif
}
