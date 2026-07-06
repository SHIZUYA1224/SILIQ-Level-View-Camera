using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera), typeof(CharacterController))]
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
    private const float DefaultInputSystemMouseScale = 0.03f;
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
    [Min(0.001f)] public float inputSystemMouseScale = 0.03f;
    public bool invertMouseY;
    [Min(0.01f)] public float gravity = 9.81f;
    public bool useCollision = true;
    public bool activateOnPlay;
    public bool disableOtherCamerasOnActivate = true;
    [Min(0f)] public float activeCameraDepth = 1000f;
    public bool showGizmos = true;

    private static readonly List<CameraState> StoredCameraStates = new List<CameraState>();
    private static bool hasStoredCameraStates;
    private static LevelViewCameraController activePlayCameraController;

    private Camera controlledCamera;
    private CharacterController characterController;
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

#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
    private static bool inputSystemReflectionInitialized;
    private static Type inputSystemKeyboardType;
    private static Type inputSystemMouseType;
    private static PropertyInfo inputSystemKeyboardCurrentProperty;
    private static PropertyInfo inputSystemMouseCurrentProperty;
#endif

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

    public bool IsActivePlayCamera
    {
        get { return activePlayCameraController == this; }
    }

    public static bool HasStoredPlayCameraState
    {
        get { return hasStoredCameraStates; }
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

    public void ActivatePlayCamera()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Camera cameraToActivate = GetControlledCamera();
        if (cameraToActivate == null)
        {
            return;
        }

        if (!hasStoredCameraStates)
        {
            StorePlayCameraStates();
        }

        if (disableOtherCamerasOnActivate)
        {
            Camera[] sceneCameras = GetSceneCameras();
            for (int i = 0; i < sceneCameras.Length; i++)
            {
                Camera sceneCamera = sceneCameras[i];
                if (sceneCamera != null && sceneCamera != cameraToActivate)
                {
                    sceneCamera.enabled = false;
                }
            }
        }

        cameraToActivate.enabled = true;
        cameraToActivate.depth = activeCameraDepth;
        cameraToActivate.targetDisplay = 0;
        activePlayCameraController = this;
    }

    public static void RestorePlayCameraStates()
    {
        if (!Application.isPlaying || !hasStoredCameraStates)
        {
            return;
        }

        for (int i = 0; i < StoredCameraStates.Count; i++)
        {
            StoredCameraStates[i].Restore();
        }

        StoredCameraStates.Clear();
        hasStoredCameraStates = false;
        activePlayCameraController = null;
    }

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        InitializeRuntimeState();

        if (activateOnPlay)
        {
            ActivatePlayCamera();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying && isMouseLocked)
        {
            SetMouseLock(false);
        }

        if (Application.isPlaying && activePlayCameraController == this)
        {
            RestorePlayCameraStates();
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
        transform.position += velocity * Time.deltaTime;
        currentSpeed = velocity.magnitude;
    }

    private void EnsureCharacterController()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }

        if (!characterController.enabled)
        {
            characterController.enabled = true;
        }

        ApplyCharacterControllerSettings(CurrentBodyHeight);
    }

    private Camera GetControlledCamera()
    {
        if (controlledCamera == null)
        {
            controlledCamera = GetComponent<Camera>();
        }

        return controlledCamera;
    }

    private void DisableCollisionControllers()
    {
        if (characterController != null && characterController.enabled)
        {
            characterController.enabled = false;
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
        characterController.center = new Vector3(0f, (capsuleHeight * 0.5f) - CurrentEyeHeight, 0f);
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

        Vector3 footPosition = GetFootPosition(transform.position, currentEyeHeight);
        transform.position = footPosition + Vector3.up * targetEyeHeight;

        currentBodyHeight = targetBodyHeight;
        currentEyeHeight = targetEyeHeight;
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
        pitch += lookInput.y * mouseSensitivity * (invertMouseY ? 1f : -1f);
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        ApplyViewRotation();
    }

    private void ApplyViewRotation()
    {
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
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

        transform.SetPositionAndRotation(initialPosition, initialRotation);

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
        inputSystemMouseScale = inputSystemMouseScale > 0f ? Mathf.Max(0.001f, inputSystemMouseScale) : DefaultInputSystemMouseScale;
        activeCameraDepth = Mathf.Max(0f, activeCameraDepth);
        gravity = Mathf.Max(0.01f, gravity);
    }

    private static void StorePlayCameraStates()
    {
        StoredCameraStates.Clear();

        Camera[] sceneCameras = GetSceneCameras();
        for (int i = 0; i < sceneCameras.Length; i++)
        {
            Camera sceneCamera = sceneCameras[i];
            if (sceneCamera != null)
            {
                StoredCameraStates.Add(new CameraState(sceneCamera));
            }
        }

        hasStoredCameraStates = true;
    }

    private static Camera[] GetSceneCameras()
    {
        Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
        List<Camera> sceneCameras = new List<Camera>(cameras.Length);

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera sceneCamera = cameras[i];
            if (sceneCamera == null || !sceneCamera.gameObject.scene.IsValid())
            {
                continue;
            }

            sceneCameras.Add(sceneCamera);
        }

        return sceneCameras.ToArray();
    }

    private struct CameraState
    {
        private readonly Camera camera;
        private readonly bool enabled;
        private readonly float depth;
        private readonly int targetDisplay;

        public CameraState(Camera sourceCamera)
        {
            camera = sourceCamera;
            enabled = sourceCamera.enabled;
            depth = sourceCamera.depth;
            targetDisplay = sourceCamera.targetDisplay;
        }

        public void Restore()
        {
            if (camera == null)
            {
                return;
            }

            camera.enabled = enabled;
            camera.depth = depth;
            camera.targetDisplay = targetDisplay;
        }
    }

    private Vector2 ReadMoveInput()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
#elif ENABLE_INPUT_SYSTEM
        return ReadInputSystemMoveInput();
#else
        return Vector2.zero;
#endif
    }

    private Vector2 ReadLookInput()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#elif ENABLE_INPUT_SYSTEM
        return ReadInputSystemMouseDelta() * inputSystemMouseScale;
#else
        return Vector2.zero;
#endif
    }

    private bool ReadJumpPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space);
#elif ENABLE_INPUT_SYSTEM
        return ReadInputSystemKeyPressedThisFrame("spaceKey");
#else
        return false;
#endif
    }

    private bool ReadJumpHeld()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.Space);
#elif ENABLE_INPUT_SYSTEM
        return ReadInputSystemKeyHeld("spaceKey");
#else
        return false;
#endif
    }

    private bool ReadSprintHeld()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftShift);
#elif ENABLE_INPUT_SYSTEM
        return ReadInputSystemKeyHeld("leftShiftKey");
#else
        return false;
#endif
    }

    private bool ReadCrouchHeld()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.LeftControl);
#elif ENABLE_INPUT_SYSTEM
        return ReadInputSystemKeyHeld("leftCtrlKey");
#else
        return false;
#endif
    }

    private bool ReadResetPressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#elif ENABLE_INPUT_SYSTEM
        return ReadInputSystemKeyPressedThisFrame("rKey");
#else
        return false;
#endif
    }

    private bool ReadUnlockMousePressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
        return ReadInputSystemKeyPressedThisFrame("escapeKey");
#else
        return false;
#endif
    }

    private bool ReadLockMousePressed()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#elif ENABLE_INPUT_SYSTEM
        return ReadInputSystemMouseButtonPressedThisFrame("leftButton");
#else
        return false;
#endif
    }

#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
    private static Vector2 ReadInputSystemMoveInput()
    {
        object keyboard = GetInputSystemKeyboard();
        float x = 0f;
        float y = 0f;

        if (IsInputSystemControlPressed(GetInputSystemControl(keyboard, "aKey")) ||
            IsInputSystemControlPressed(GetInputSystemControl(keyboard, "leftArrowKey")))
        {
            x -= 1f;
        }

        if (IsInputSystemControlPressed(GetInputSystemControl(keyboard, "dKey")) ||
            IsInputSystemControlPressed(GetInputSystemControl(keyboard, "rightArrowKey")))
        {
            x += 1f;
        }

        if (IsInputSystemControlPressed(GetInputSystemControl(keyboard, "sKey")) ||
            IsInputSystemControlPressed(GetInputSystemControl(keyboard, "downArrowKey")))
        {
            y -= 1f;
        }

        if (IsInputSystemControlPressed(GetInputSystemControl(keyboard, "wKey")) ||
            IsInputSystemControlPressed(GetInputSystemControl(keyboard, "upArrowKey")))
        {
            y += 1f;
        }

        return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    private static Vector2 ReadInputSystemMouseDelta()
    {
        object mouse = GetInputSystemMouse();
        return ReadInputSystemVector2Control(GetInputSystemControl(mouse, "delta"));
    }

    private static bool ReadInputSystemKeyPressedThisFrame(string keyName)
    {
        return IsInputSystemControlPressedThisFrame(GetInputSystemControl(GetInputSystemKeyboard(), keyName));
    }

    private static bool ReadInputSystemKeyHeld(string keyName)
    {
        return IsInputSystemControlPressed(GetInputSystemControl(GetInputSystemKeyboard(), keyName));
    }

    private static bool ReadInputSystemMouseButtonPressedThisFrame(string buttonName)
    {
        return IsInputSystemControlPressedThisFrame(GetInputSystemControl(GetInputSystemMouse(), buttonName));
    }

    private static object GetInputSystemKeyboard()
    {
        EnsureInputSystemReflection();
        return inputSystemKeyboardCurrentProperty != null ? inputSystemKeyboardCurrentProperty.GetValue(null, null) : null;
    }

    private static object GetInputSystemMouse()
    {
        EnsureInputSystemReflection();
        return inputSystemMouseCurrentProperty != null ? inputSystemMouseCurrentProperty.GetValue(null, null) : null;
    }

    private static void EnsureInputSystemReflection()
    {
        if (inputSystemReflectionInitialized)
        {
            return;
        }

        inputSystemReflectionInitialized = true;
        inputSystemKeyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
        inputSystemMouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem");
        inputSystemKeyboardCurrentProperty = inputSystemKeyboardType != null
            ? inputSystemKeyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static)
            : null;
        inputSystemMouseCurrentProperty = inputSystemMouseType != null
            ? inputSystemMouseType.GetProperty("current", BindingFlags.Public | BindingFlags.Static)
            : null;
    }

    private static object GetInputSystemControl(object device, string controlName)
    {
        if (device == null)
        {
            return null;
        }

        PropertyInfo property = device.GetType().GetProperty(controlName, BindingFlags.Public | BindingFlags.Instance);
        return property != null ? property.GetValue(device, null) : null;
    }

    private static bool IsInputSystemControlPressed(object control)
    {
        return ReadInputSystemBoolProperty(control, "isPressed");
    }

    private static bool IsInputSystemControlPressedThisFrame(object control)
    {
        return ReadInputSystemBoolProperty(control, "wasPressedThisFrame");
    }

    private static bool ReadInputSystemBoolProperty(object control, string propertyName)
    {
        if (control == null)
        {
            return false;
        }

        PropertyInfo property = control.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null || property.PropertyType != typeof(bool))
        {
            return false;
        }

        return (bool)property.GetValue(control, null);
    }

    private static Vector2 ReadInputSystemVector2Control(object control)
    {
        if (control == null)
        {
            return Vector2.zero;
        }

        MethodInfo readValue = FindParameterlessMethod(control.GetType(), "ReadValue");
        object value = readValue != null ? readValue.Invoke(control, null) : null;
        return value is Vector2 ? (Vector2)value : Vector2.zero;
    }

    private static MethodInfo FindParameterlessMethod(Type type, string methodName)
    {
        while (type != null)
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method != null)
            {
                return method;
            }

            type = type.BaseType;
        }

        return null;
    }
#endif

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
