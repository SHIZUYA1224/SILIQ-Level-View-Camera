using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelViewCameraController))]
[CanEditMultipleObjects]
public class LevelViewCameraControllerEditor : Editor
{
    private static readonly GUIContent[] PresetLabels =
    {
        new GUIContent("Small Avatar: 1.10m body"),
        new GUIContent("Seated: 1.30m body"),
        new GUIContent("Short Avatar: 1.50m body"),
        new GUIContent("Average VRChat Avatar: 1.70m body"),
        new GUIContent("Tall Avatar: 1.90m body"),
        new GUIContent("Custom")
    };

    private SerializedProperty heightPreset;
    private SerializedProperty bodyHeight;
    private SerializedProperty crouchBodyHeight;
    private SerializedProperty eyeHeight;
    private SerializedProperty crouchEyeHeight;
    private SerializedProperty walkSpeed;
    private SerializedProperty sprintSpeed;
    private SerializedProperty jumpHeight;
    private SerializedProperty mouseSensitivity;
    private SerializedProperty inputSystemMouseScale;
    private SerializedProperty invertMouseY;
    private SerializedProperty gravity;
    private SerializedProperty useCollision;
    private SerializedProperty showGizmos;

    private void OnEnable()
    {
        heightPreset = serializedObject.FindProperty("heightPreset");
        bodyHeight = serializedObject.FindProperty("bodyHeight");
        crouchBodyHeight = serializedObject.FindProperty("crouchBodyHeight");
        eyeHeight = serializedObject.FindProperty("eyeHeight");
        crouchEyeHeight = serializedObject.FindProperty("crouchEyeHeight");
        walkSpeed = serializedObject.FindProperty("walkSpeed");
        sprintSpeed = serializedObject.FindProperty("sprintSpeed");
        jumpHeight = serializedObject.FindProperty("jumpHeight");
        mouseSensitivity = serializedObject.FindProperty("mouseSensitivity");
        inputSystemMouseScale = serializedObject.FindProperty("inputSystemMouseScale");
        invertMouseY = serializedObject.FindProperty("invertMouseY");
        gravity = serializedObject.FindProperty("gravity");
        useCollision = serializedObject.FindProperty("useCollision");
        showGizmos = serializedObject.FindProperty("showGizmos");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPresetSection();
        DrawBodyHeightSection();
        DrawMovementSection();
        DrawCollisionSection();
        DrawGizmoSection();
        DrawEditorNavigationSection();
        DrawCurrentStatusSection();

        bool changed = serializedObject.ApplyModifiedProperties();
        if (changed)
        {
            foreach (Object editedTarget in targets)
            {
                LevelViewCameraController controller = editedTarget as LevelViewCameraController;
                if (controller == null)
                {
                    continue;
                }

                controller.ApplyHeightPreset();
                EditorUtility.SetDirty(controller);
            }

            SceneView.RepaintAll();
        }
    }

    private void DrawPresetSection()
    {
        EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(heightPreset.hasMultipleDifferentValues))
        {
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup(new GUIContent("Height Preset"), heightPreset.enumValueIndex, PresetLabels);
            if (EditorGUI.EndChangeCheck())
            {
                heightPreset.enumValueIndex = selectedIndex;
                LevelViewCameraController.HeightPreset preset = (LevelViewCameraController.HeightPreset)selectedIndex;
                if (preset != LevelViewCameraController.HeightPreset.Custom)
                {
                    bodyHeight.floatValue = LevelViewCameraController.GetPresetBodyHeight(preset);
                    crouchBodyHeight.floatValue = LevelViewCameraController.CalculateDefaultCrouchBodyHeight(bodyHeight.floatValue);
                    eyeHeight.floatValue = LevelViewCameraController.CalculateEyeHeight(bodyHeight.floatValue);
                    crouchEyeHeight.floatValue = LevelViewCameraController.CalculateEyeHeight(crouchBodyHeight.floatValue);
                }
            }
        }

        if (heightPreset.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox("Multiple preset values are selected.", MessageType.Info);
        }
    }

    private void DrawBodyHeightSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Body Height", EditorStyles.boldLabel);

        bool isCustom = !heightPreset.hasMultipleDifferentValues &&
            (LevelViewCameraController.HeightPreset)heightPreset.enumValueIndex == LevelViewCameraController.HeightPreset.Custom;

        using (new EditorGUI.DisabledScope(!isCustom))
        {
            EditorGUILayout.PropertyField(bodyHeight, new GUIContent("Body Height"));
        }

        EditorGUILayout.PropertyField(crouchBodyHeight, new GUIContent("Crouch Body Height"));

        float standingEyeHeight = LevelViewCameraController.CalculateEyeHeight(bodyHeight.floatValue);
        float crouchingEyeHeight = LevelViewCameraController.CalculateEyeHeight(crouchBodyHeight.floatValue);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Calculated Eye Height", FormatMeters(standingEyeHeight));
            EditorGUILayout.TextField("Calculated Crouch Eye Height", FormatMeters(crouchingEyeHeight));
        }

        eyeHeight.floatValue = standingEyeHeight;
        crouchEyeHeight.floatValue = crouchingEyeHeight;
    }

    private void DrawMovementSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(walkSpeed, new GUIContent("Walk Speed"));
        EditorGUILayout.PropertyField(sprintSpeed, new GUIContent("Sprint Speed"));
        EditorGUILayout.PropertyField(jumpHeight, new GUIContent("Jump Height"));
        EditorGUILayout.PropertyField(gravity, new GUIContent("Gravity"));

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Look", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(mouseSensitivity, new GUIContent("Mouse Sensitivity"));
        EditorGUILayout.PropertyField(inputSystemMouseScale, new GUIContent("Input System Mouse Scale"));
        EditorGUILayout.PropertyField(invertMouseY, new GUIContent("Invert Mouse Y"));
    }

    private void DrawCollisionSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Collision", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useCollision, new GUIContent("Use Collision"));
    }

    private void DrawGizmoSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Gizmo", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(showGizmos, new GUIContent("Show Gizmos"));
    }

    private void DrawEditorNavigationSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Editor Navigation", EditorStyles.boldLabel);

        LevelViewCameraController controller = target as LevelViewCameraController;
        if (controller == null)
        {
            return;
        }

        if (GUILayout.Button("Move Scene View To This Camera"))
        {
            serializedObject.ApplyModifiedProperties();
            MoveSceneViewToController(controller);
        }

        if (GUILayout.Button("Move This Camera To Scene View"))
        {
            serializedObject.ApplyModifiedProperties();
            MoveControllerToSceneView(controller);
        }
    }

    private void DrawCurrentStatusSection()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);

        LevelViewCameraController controller = target as LevelViewCameraController;
        if (controller == null)
        {
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Current Body Height", FormatMeters(controller.CurrentBodyHeight));
            EditorGUILayout.TextField("Current Eye Height", FormatMeters(controller.CurrentEyeHeight));
            EditorGUILayout.TextField("Current Collider Height", FormatMeters(controller.CurrentBodyHeight));
            EditorGUILayout.TextField("Collision Mode", controller.CollisionMode);
            EditorGUILayout.TextField("Is Grounded", controller.IsGrounded ? "Yes" : "No");
            EditorGUILayout.TextField("Current Speed", FormatSpeed(controller.CurrentSpeed));
            EditorGUILayout.TextField("Initial Position", FormatVector3(controller.InitialPosition));
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Camera controls run only in Play Mode. Gizmos are available in Edit Mode.", MessageType.Info);
        }
    }

    private string FormatMeters(float value)
    {
        return string.Format("{0:0.00} m", value);
    }

    private string FormatSpeed(float value)
    {
        return string.Format("{0:0.00} m/s", value);
    }

    private string FormatVector3(Vector3 value)
    {
        return string.Format("({0:0.00}, {1:0.00}, {2:0.00})", value.x, value.y, value.z);
    }

    [MenuItem("Tools/SILIQ/Level View Camera/Move Scene View To Selected Camera", false, 2000)]
    private static void MoveSceneViewToSelectedController()
    {
        LevelViewCameraController controller = GetSelectedController();
        if (controller != null)
        {
            MoveSceneViewToController(controller);
        }
    }

    [MenuItem("Tools/SILIQ/Level View Camera/Move Scene View To Selected Camera", true)]
    private static bool ValidateMoveSceneViewToSelectedController()
    {
        return GetSelectedController() != null;
    }

    [MenuItem("Tools/SILIQ/Level View Camera/Move Selected Camera To Scene View", false, 2001)]
    private static void MoveSelectedControllerToSceneView()
    {
        LevelViewCameraController controller = GetSelectedController();
        if (controller != null)
        {
            MoveControllerToSceneView(controller);
        }
    }

    [MenuItem("Tools/SILIQ/Level View Camera/Move Selected Camera To Scene View", true)]
    private static bool ValidateMoveSelectedControllerToSceneView()
    {
        return GetSelectedController() != null && GetSceneView() != null;
    }

    private static LevelViewCameraController GetSelectedController()
    {
        GameObject selectedObject = Selection.activeGameObject;
        return selectedObject != null ? selectedObject.GetComponent<LevelViewCameraController>() : null;
    }

    private static void MoveSceneViewToController(LevelViewCameraController controller)
    {
        SceneView sceneView = GetSceneView();
        if (sceneView == null || controller == null)
        {
            return;
        }

        sceneView.AlignViewToObject(controller.transform);
        sceneView.Repaint();
    }

    private static void MoveControllerToSceneView(LevelViewCameraController controller)
    {
        SceneView sceneView = GetSceneView();
        if (sceneView == null || sceneView.camera == null || controller == null)
        {
            return;
        }

        Transform sceneCamera = sceneView.camera.transform;
        Undo.RecordObject(controller.transform, "Move Level View Camera To Scene View");
        controller.transform.SetPositionAndRotation(sceneCamera.position, sceneCamera.rotation);
        EditorUtility.SetDirty(controller.transform);
        Selection.activeGameObject = controller.gameObject;
        SceneView.RepaintAll();
    }

    private static SceneView GetSceneView()
    {
        if (SceneView.lastActiveSceneView != null)
        {
            return SceneView.lastActiveSceneView;
        }

        return EditorWindow.GetWindow<SceneView>();
    }
}
