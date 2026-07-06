using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelViewCameraController))]
[CanEditMultipleObjects]
public class LevelViewCameraControllerEditor : Editor
{
    private static readonly GUIContent[] PresetLabels =
    {
        new GUIContent("Small Avatar: 1.00m"),
        new GUIContent("Seated: 1.20m"),
        new GUIContent("Short Avatar: 1.40m"),
        new GUIContent("Average VRChat Avatar: 1.60m"),
        new GUIContent("Tall Avatar: 1.80m"),
        new GUIContent("Custom")
    };

    private SerializedProperty heightPreset;
    private SerializedProperty eyeHeight;
    private SerializedProperty crouchEyeHeight;
    private SerializedProperty walkSpeed;
    private SerializedProperty sprintSpeed;
    private SerializedProperty jumpHeight;
    private SerializedProperty mouseSensitivity;
    private SerializedProperty gravity;
    private SerializedProperty useCollision;
    private SerializedProperty showGizmos;

    private void OnEnable()
    {
        heightPreset = serializedObject.FindProperty("heightPreset");
        eyeHeight = serializedObject.FindProperty("eyeHeight");
        crouchEyeHeight = serializedObject.FindProperty("crouchEyeHeight");
        walkSpeed = serializedObject.FindProperty("walkSpeed");
        sprintSpeed = serializedObject.FindProperty("sprintSpeed");
        jumpHeight = serializedObject.FindProperty("jumpHeight");
        mouseSensitivity = serializedObject.FindProperty("mouseSensitivity");
        gravity = serializedObject.FindProperty("gravity");
        useCollision = serializedObject.FindProperty("useCollision");
        showGizmos = serializedObject.FindProperty("showGizmos");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPresetSection();
        DrawEyeHeightSection();
        DrawMovementSection();
        DrawCollisionSection();
        DrawGizmoSection();
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
                    eyeHeight.floatValue = LevelViewCameraController.GetPresetHeight(preset);
                }
            }
        }

        if (heightPreset.hasMultipleDifferentValues)
        {
            EditorGUILayout.HelpBox("Multiple preset values are selected.", MessageType.Info);
        }
    }

    private void DrawEyeHeightSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Eye Height", EditorStyles.boldLabel);

        bool isCustom = !heightPreset.hasMultipleDifferentValues &&
            (LevelViewCameraController.HeightPreset)heightPreset.enumValueIndex == LevelViewCameraController.HeightPreset.Custom;

        using (new EditorGUI.DisabledScope(!isCustom))
        {
            EditorGUILayout.PropertyField(eyeHeight, new GUIContent("Eye Height"));
        }

        EditorGUILayout.PropertyField(crouchEyeHeight, new GUIContent("Crouch Eye Height"));
    }

    private void DrawMovementSection()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(walkSpeed, new GUIContent("Walk Speed"));
        EditorGUILayout.PropertyField(sprintSpeed, new GUIContent("Sprint Speed"));
        EditorGUILayout.PropertyField(jumpHeight, new GUIContent("Jump Height"));
        EditorGUILayout.PropertyField(mouseSensitivity, new GUIContent("Mouse Sensitivity"));
        EditorGUILayout.PropertyField(gravity, new GUIContent("Gravity"));
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
            EditorGUILayout.TextField("Current Eye Height", FormatMeters(controller.CurrentEyeHeight));
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
}
