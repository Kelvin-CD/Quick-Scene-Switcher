using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class QuickSceneSwitcher : EditorWindow
{
    private List<SceneAsset> scenes = null;
    private List<Color> buttonColors;
    private bool editMode = false;

    private Vector2 scrollPosition = Vector2.zero;
    private GUILayoutOption heightLayout;
    private Color darkGray;

    // Prefix used to store preferences
    // Format is: QuickSceneSwitcher/DefaultCompany/ProjectName/
    private string PREFS_PREFIX;

    // Initialization
    void OnEnable()
    {
        PREFS_PREFIX = $"QuickSceneSwitcher/{Application.companyName}/{Application.productName}/";

        scenes = new List<SceneAsset>();
        buttonColors = new List<Color>();
        AddScene();

        // Style vars
        heightLayout = GUILayout.Height(22);
        darkGray = new Color(0.28f, 0.28f, 0.28f);

        // Load preferences and scenes when this window is opened
        LoadPrefs();
    }

    [MenuItem("Window/Quick Scene Switcher")]
    public static void ShowWindow()
    {
        GetWindow<QuickSceneSwitcher>("Scene Switcher");
    }

    void OnGUI()
    {
        // Buttons Style
        GUIStyle buttonStyleBold = new GUIStyle(GUI.skin.button);
        buttonStyleBold.fixedHeight = 22;
        buttonStyleBold.fontStyle = FontStyle.Bold;

        // Title label style
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.fixedHeight = 22;
        labelStyle.fontSize = 14;

        Color originalContentColor = GUI.contentColor;

        // Scrollbar handling
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Label 'Quick Scenes'
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Label(" Quick Scenes", labelStyle);
        GUILayout.FlexibleSpace();

        // Toggle 'EditMode' Button
        GUI.backgroundColor = editMode ? GetButtonColorBasedOnEditorTheme(Color.white) : darkGray;
        UpdateButtonStyleTextColor(GUI.backgroundColor, buttonStyleBold);
        editMode = GUILayout.Toggle(editMode, editMode ? " Exit Edit Mode " : " Edit Mode ", buttonStyleBold);
        GUI.backgroundColor = originalContentColor;

        GUILayout.EndHorizontal();
        GUILayout.Space(3);

        // Draw each scene button (or each scene options if in 'EditMode')
        for (int i = 0; i < scenes.Count; i++)
        {
            if (editMode)
            {
                GUILayout.BeginHorizontal();

                // Remove scene button
                bool sceneRemoved = false;
                if (GUILayout.Button("-", heightLayout, GUILayout.MaxWidth(25)))
                {
                    RemoveScene(i);
                    sceneRemoved = true;
                }

                if (!sceneRemoved)
                {
                    // Move scene up and down buttons
                    if (GUILayout.Button("↑", heightLayout, GUILayout.MaxWidth(25))) MoveScene(i, -1);
                    if (GUILayout.Button("↓", heightLayout, GUILayout.MaxWidth(25))) MoveScene(i, 1);

                    // Scene asset field
                    scenes[i] = EditorGUILayout.ObjectField(scenes[i], typeof(SceneAsset), false, heightLayout) as SceneAsset;
                    // Scene button color field
                    buttonColors[i] = EditorGUILayout.ColorField(buttonColors[i], heightLayout, GUILayout.MaxWidth(80));
                }

                GUILayout.EndHorizontal();
            }
            else // if NOT in 'EditMode'
            {
                // Disable button if it corresponds to the currently open scene
                bool isCurrentScene = IsCurrentScene(scenes[i]);
                if (isCurrentScene) EditorGUI.BeginDisabledGroup(true);

                // Setup button background and text colors
                GUI.backgroundColor = GetButtonColorBasedOnEditorTheme(buttonColors[i]);
                UpdateButtonStyleTextColor(GUI.backgroundColor, buttonStyleBold);

                // Draw scene button
                if (scenes[i] != null && GUILayout.Button($"{scenes[i].name}{(isCurrentScene ? " (current)" : "")}", buttonStyleBold))
                    OpenScene(scenes[i]);

                GUI.backgroundColor = originalContentColor;
                EditorGUI.EndDisabledGroup();
            }
        }

        if (editMode) // 'EditMode' bottom options
        {
            GUILayout.Space(5);

            // 'Add Scene' Button style
            GUI.backgroundColor = darkGray;
            UpdateButtonStyleTextColor(GUI.backgroundColor, buttonStyleBold);
            float previousHeight = buttonStyleBold.fixedHeight;
            buttonStyleBold.fixedHeight = 25;

            if (GUILayout.Button("Add Scene", buttonStyleBold))
                AddScene();

            GUILayout.Space(1);

            // 'Load From BuildSettings' Button style
            buttonStyleBold.fixedHeight = previousHeight;
            GUI.backgroundColor = GetButtonColorBasedOnEditorTheme(originalContentColor);
            UpdateButtonStyleTextColor(GUI.backgroundColor, buttonStyleBold);

            if (GUILayout.Button("Load From BuildSettings", buttonStyleBold))
                LoadScenesFromBuildSettings();
        }
        else
        {
            // If NOT in 'EditMode' and there are no scenes buttons to show,
            // display an info message about how to add them
            if (scenes.Count == 0 || !(scenes.Any(i => i != null)))
            {
                GUILayout.Space(2);
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button($"Enter 'EditMode' and add scenes\nfor them to appear here", GUILayout.Height(50), GUILayout.MinWidth(10));
                EditorGUI.EndDisabledGroup();
            }
        }

        // Draw Footer
        if (editMode)
        {
            GUILayout.FlexibleSpace();
            GUIStyle footerStyle = EditorStyles.centeredGreyMiniLabel;
            Color footerColor = (EditorGUIUtility.isProSkin ? Color.white : Color.black);
            footerColor.a = 0.25f;
            footerStyle.normal.textColor = footerColor;
            GUILayout.Label("Made by Kelvip", footerStyle);
            GUILayout.Space(5);
        }

        GUILayout.EndScrollView();

        // If any preferences have changed, save them to persistent data
        if (GUI.changed) SavePrefs();
    }

    #region AuxiliarFunctions

    // This function chooses black or white for the scene button texts based on the color of the button
    private void UpdateButtonStyleTextColor(Color color, GUIStyle style)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        bool whiteText = (v < 0.7f || (s > 0.4f && (h < 0.1f || h > 0.55f)));
        Color textColor = whiteText ? Color.white : Color.black;
        Color hoverTextColor = textColor + (whiteText ? Color.black * -0.2f : Color.white * 0.2f);

        style.normal.textColor = textColor;
        style.hover.textColor = hoverTextColor;
        style.focused.textColor = textColor;
        style.active.textColor = textColor;

        style.onNormal.textColor = textColor;
        style.onHover.textColor = hoverTextColor;
        style.onFocused.textColor = textColor;
        style.onActive.textColor = textColor;
    }

    // Returns the buttonColor compensating for the editor theme skin
    private Color GetButtonColorBasedOnEditorTheme(Color buttonColor)
    {
        // If isProSkin is true, the editor is in dark mode
        Color editorColor = EditorGUIUtility.isProSkin ? (Color.white * 0.3f) : Color.black;
        return buttonColor * (EditorGUIUtility.isProSkin ? 2f : 1f) + editorColor;
    }

    #endregion

    #region SceneManagement

    // Closes the current scene and opens the specified scene
    private void OpenScene(SceneAsset scene)
    {
        string scenePath = AssetDatabase.GetAssetPath(scene);

        // Check if there are unsaved changes in the current scene and
        // ask if the user wants to save them before switching to the new scene
        // If the user chooses 'cancel', don't switch scenes
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath);
            SavePrefs();
        }
    }

    // Add another scene slot
    private void AddScene()
    {
        scenes.Add(null);
        buttonColors.Add(Color.white);
    }

    // Remove the specified scene slot
    private void RemoveScene(int index)
    {
        scenes.RemoveAt(index);
        buttonColors.RemoveAt(index);
        SavePrefs();
    }

    // Move the specified scene up or down on the list based on the increment -1[UP] +1[DOWN]
    private void MoveScene(int index, int increment)
    {
        // Make sure the new index is not out of range
        int newIndex = Mathf.Clamp(index + increment, 0, scenes.Count - 1);
        if (newIndex != index)
        {
            SceneAsset scene = scenes[index];
            Color buttonColor = buttonColors[index];
            RemoveScene(index);

            scenes.Insert(newIndex, scene);
            buttonColors.Insert(newIndex, buttonColor);
        }
        SavePrefs();
    }

    // Returns true if 'scene' is the scene currently open
    private bool IsCurrentScene(SceneAsset scene)
    {
        return EditorSceneManager.GetActiveScene().path == AssetDatabase.GetAssetPath(scene);
    }

    // Loads all the scenes from the build settings (only if they are not already added)
    private void LoadScenesFromBuildSettings()
    {
        foreach (var scene in EditorBuildSettings.scenes)
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);

            if (!scenes.Contains(sceneAsset))
            {
                int index;
                if (scenes.Contains(null))
                    index = scenes.IndexOf(null);
                else
                {
                    AddScene();
                    index = scenes.Count - 1;
                }

                scenes[index] = sceneAsset;
            }
        }
    }

    #endregion

    #region PersistentData

    // Save current preferences to persistent data
    private void SavePrefs()
    {
        // Scenes Count
        EditorPrefs.SetInt(PREFS_PREFIX + "ScenesCount", scenes.Count);

        for (int i = 0; i < scenes.Count; i++)
        {
            // Scenes
            EditorPrefs.SetString(PREFS_PREFIX + $"Scene_{i}", AssetDatabase.GetAssetPath(scenes[i]));

            // Button Colors
            string sceneColor = ColorUtility.ToHtmlStringRGBA(buttonColors[i]);
            EditorPrefs.SetString(PREFS_PREFIX + $"SceneColor_{i}", $"#{sceneColor}");
        }

        // Edit Mode
        EditorPrefs.SetBool(PREFS_PREFIX + $"EditMode", editMode);
    }

    // Load and parse preferences from persistent saved data
    private void LoadPrefs()
    {
        // Scenes Count
        int scenesCount = EditorPrefs.GetInt(PREFS_PREFIX + "ScenesCount");

        scenes.Clear();
        buttonColors.Clear();

        for (int i = 0; i < scenesCount; i++)
        {
            // Scenes
            AddScene();
            string scenePath = EditorPrefs.GetString(PREFS_PREFIX + $"Scene_{i}");
            scenes[i] = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

            // Button Colors
            string sceneColorString = EditorPrefs.GetString(PREFS_PREFIX + $"SceneColor_{i}");
            ColorUtility.TryParseHtmlString(sceneColorString, out Color sceneColor);
            buttonColors[i] = sceneColor;
        }

        // Edit Mode
        editMode = EditorPrefs.GetBool(PREFS_PREFIX + $"EditMode");
    }

    #endregion
}