using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#else
using SFB;
#endif

public class PathPicker : MonoBehaviour
{
    public static string SelectedPath { get; private set; } = "";
    public static string TeamName { get; private set; }

    [SerializeField]
    private TMPro.TextMeshProUGUI pathLabel;

    [SerializeField]
    private Button loadReplayButton;

    [SerializeField]
    private Button loadSetPiecesButton;

    [SerializeField]
    private TMPro.TMP_InputField inputField;


    public void PickFolder()
    {
#if UNITY_EDITOR
        SelectedPath = EditorUtility.OpenFolderPanel("Select a folder", Application.dataPath, "");
#else
        var paths = StandaloneFileBrowser.OpenFolderPanel("Select a folder", Application.dataPath, false);
        if (paths.Length > 0) SelectedPath = paths[0];
#endif
        if (pathLabel) pathLabel.text = SelectedPath;
        Debug.Log("Folder chosen: " + SelectedPath);
        CheckValidity();
    }

    public void CheckValidity()
    {
        TeamName = inputField.text;
        bool valid = SelectedPath != "" && TeamName != "";

        loadReplayButton.interactable = valid;
        loadSetPiecesButton.interactable = valid;
    }
}
