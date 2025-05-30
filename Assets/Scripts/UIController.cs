using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    public FootballTrackingManager ftm;

    // Assign these in the Inspector.
    public Toggle numberToggle;
    public Toggle nameToggle;
    public TextMeshProUGUI homeText;
    public TextMeshProUGUI awayText;
    public TextMeshProUGUI time;
    public RawImage homeImage;
    public RawImage awayImage;

    void Start()
    {
        // Register callback events for each toggle.
        if (numberToggle != null)
            numberToggle.onValueChanged.AddListener((value) => ToggleObjects("Number", value));

        if (nameToggle != null)
            nameToggle.onValueChanged.AddListener((value) => ToggleObjects("Name", value));
    }

    public void SetMetadata(string home, string away, Color homeColor, Color awayColor)
    {
        // Set text for home and away to first three characters capitalized
        homeText.text = FootballTrackingManager.CustomAcronyms.ContainsKey(home) ? FootballTrackingManager.CustomAcronyms[home] : home[..3].ToUpper();
        awayText.text = FootballTrackingManager.CustomAcronyms.ContainsKey(away) ? FootballTrackingManager.CustomAcronyms[away] : away[..3].ToUpper();

        homeImage.color = homeColor;
        awayImage.color = awayColor;
    }

    // Toggles active state for all GameObjects with the specified tag.
    void ToggleObjects(string tag, bool isVisible)
    {
        GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
        // Keep only objects of type TextMeshProUGUI
        foreach (GameObject obj in objects)
        {
            TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.enabled = isVisible;
        }
    }
}
