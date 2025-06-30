using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

/// <summary>
/// Struct representing a single keyframe for an object's position at a specific time.
/// </summary>
public struct TrackingData
{
    public float time;
    public Vector3 position;
    public bool extrapolated;
    public bool firstHalf;

    public TrackingData(float time, Vector3 position, bool extrapolated, bool firstHalf)
    {
        this.time = time;
        this.position = position;
        this.extrapolated = extrapolated;
        this.firstHalf = firstHalf;
    }
}

public class Player
{
    public string name;
    public string team;
    public string position;
    public int number;
}

public class Match
{
    public string date;
    public string competition;
    public string homeTeam;
    public string awayTeam;
    public float pitchWidth;
    public float pitchLength;
    public Color homeTeamJerseyColor;
    public Color homeTeamNumberColor;
    public Color awayTeamJerseyColor;
    public Color awayTeamNumberColor;
}

/// <summary>
/// Manager for loading and replaying football 3D tracking data with optimized interpolation.
/// </summary>
public class FootballTrackingManager : MonoBehaviour
{
    // CSV file name located in Assets/data/
    public int matchID;

    public bool replay = true; // whether to instantiate objects and move them in the scene
    public bool normalizePitchSize = false;   // scales tracking to 105 m x 68 m when true

    public Match match;

    [SerializeField]
    private UIController uiController;

    [SerializeField]
    private FootballPitchRenderer pitchRenderer;

    [SerializeField]
    private TrajectoryVisualizer tv;

    [SerializeField]
    private TextMeshProUGUI errorText;


    [SerializeField]
    private Slider timeSlider; // scrubs current match time

    [SerializeField]
    private TMP_Dropdown speedDropdown; // sets playback speed
    private float simulationSpeed = 1f; // current speed multiplier

    [SerializeField]
    private TMP_Dropdown periodDropdown;
    private string currentPeriod = "1H";

    public GameObject ballPrefab;
    public GameObject playerPrefab;
    public GameObject goalPrefab;
    public TMP_Dropdown dropdown;

    public static readonly Dictionary<string, string> CustomAcronyms = new Dictionary<string, string>
    {
        { "Juventus", "JUV" },
        { "Inter Milan", "INT" },
        { "Olympique de Marseille", "MAR" },
        { "Paris Saint-Germain", "PSG" },
        { "Borussia Dortmund", "BVB" },
        { "FC Bayern Munchen", "BMU" },
        { "Manchester City", "MCI" },
        { "Liverpool Football Club", "LFC" },
        { "Real Madrid CF", "RMA" },
        { "FC Barcelona", "BAR" },
    };

    // Dictionary to hold tracking data for each object_id.
    // Each object_id maps to a dict of keyframes, with keys being match periods
    private Dictionary<int, Dictionary<string, List<TrackingData>>> trackingDataDict = new Dictionary<int, Dictionary<string, List<TrackingData>>>();

    // Dictionary mapping object_id to its instantiated GameObject.
    private Dictionary<int, GameObject> objectInstances = new Dictionary<int, GameObject>();
    private Dictionary<int, bool> objectExtrapolated = new Dictionary<int, bool>();

    // Dictionary mapping object_id to player
    private Dictionary<int, Player> playerDict = new Dictionary<int, Player>();

    // Dictionary to store the current pointer (index) for the tracking keyframe for each object.
    private Dictionary<int, int> trackingIndices = new Dictionary<int, int>();

    private GameObject homeTeam;
    private GameObject awayTeam;

    // List to store keys corresponding to the dropdown options.
    private List<int> keyList = new List<int>();

    private Dictionary<int, string> matchIDs = new Dictionary<int, string>();

    private float simulationTime = 0f; // in seconds



    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
        {
            errorText.text = logString;
        }
    }

    void Awake()
    {
        Application.logMessageReceived += HandleLog;
        DetectAvailableMatches();
        InitDropdown();

        InitSpeedDropdown();
        timeSlider?.onValueChanged.AddListener(v =>
        {
            simulationTime = v;
        });

        InitPeriodDropdown();
    }

    void Start()
    {
        if (dropdown != null && dropdown.options.Count > 0 && keyList.Count > 0)
        {
            // Load first match in dropdown list
            dropdown.value = 0;
            LoadMatch(keyList[0]);
        }
        else
        {
            Debug.LogError("Dropdown or keylist is not initialized or no matches found.");
        }
    }

    void DetectAvailableMatches()
    {
        // Get all files in data directory that end with "_metadata.csv"
        string directory = PathPicker.SelectedPath == null || PathPicker.SelectedPath == "" ? Path.Combine(Application.streamingAssetsPath) : PathPicker.SelectedPath;
        string[] files = Directory.GetFiles(directory, "*_metadata.csv");

        foreach (string file in files)
        {
            // Load file metadata and populate matchID dictionary
            string fileName = Path.GetFileName(file);
            string[] lines = GetCSVLines(fileName);

            if (lines == null || lines.Length < 2)
            {
                Debug.LogWarning($"File {fileName} is empty or missing header.");
                continue;
            }

            string line = lines[1];
            line = Regex.Replace(line, @"""([^,]+),[^""]+""", "$1");
            string[] tokens = line.Split(',');

            int matchID = int.Parse(tokens[0]);
            string homeTeamName = tokens[4].Trim();
            string homeTeam = CustomAcronyms.ContainsKey(homeTeamName) ? CustomAcronyms[homeTeamName] : homeTeamName[..3].ToUpper();

            string awayTeamName = tokens[5].Trim();
            string awayTeam = CustomAcronyms.ContainsKey(awayTeamName) ? CustomAcronyms[awayTeamName] : awayTeamName[..3].ToUpper();


            matchIDs[matchID] = $"{homeTeam} vs {awayTeam}";
            Debug.Log($"Detected match: {matchIDs[matchID]} (ID: {matchID})");
        }

        // Set matchID to first key
        if (matchIDs.Count > 0)
        {
            matchID = matchIDs.Keys.First();
        } else
        {
            Debug.LogError("No matches found in the selected directory.");
        }
    }

    void InitDropdown()
    {
        dropdown.ClearOptions();

        List<string> options = new List<string>();

        foreach (KeyValuePair<int, string> pair in matchIDs)
        {
            keyList.Add(pair.Key); // defeats the purpose of a dict a lil bit, but oh well
            options.Add(pair.Value);
        }

        dropdown.AddOptions(options);

        // Set up the callback for when the dropdown selection changes.
        dropdown.onValueChanged.AddListener(delegate
        {
            OnDropdownValueChanged(dropdown.value);
        });
    }

    void InitSpeedDropdown()
    {
        if (speedDropdown == null) return;

        speedDropdown.ClearOptions();
        speedDropdown.AddOptions(new List<string> { "0x", "0.5x", "1x", "2x", "4x" });
        speedDropdown.value = 2;                 // default 1 ×
        speedDropdown.onValueChanged.AddListener(OnSpeedChanged);
    }

    void InitPeriodDropdown()
    {
        if (periodDropdown == null) return;

        periodDropdown.ClearOptions();
        periodDropdown.AddOptions(new List<string> { "1H", "2H" });
        periodDropdown.value = 0;            // default first-half
        periodDropdown.onValueChanged.AddListener(idx =>
        {
            currentPeriod = idx == 0 ? "1H" : "2H";
            simulationTime = 0f;

            // reset all keyframe pointers
            foreach (int k in new List<int>(trackingIndices.Keys))
                trackingIndices[k] = 0;

            RefreshTimeSliderRange();
        });
    }

    public List<int> GetMatchIDs()
    {
        return matchIDs.Keys.ToList();
    }

    void RefreshTimeSliderRange()
    {
        if (timeSlider == null) return;

        float tMin = float.MaxValue;
        float tMax = 0f;

        foreach (var kvp in trackingDataDict)
        {
            if (kvp.Value.ContainsKey(currentPeriod))
            {
                var kfs = kvp.Value[currentPeriod];
                if (kfs.Count > 0)
                {
                    float start = kfs[0].time;
                    float end = kfs[kfs.Count - 1].time;

                    if (start < tMin) tMin = start;
                    if (end > tMax) tMax = end;
                }
            }
        }

        timeSlider.minValue = (tMin == float.MaxValue) ? 0f : tMin;
        timeSlider.maxValue = tMax;
        timeSlider.value = timeSlider.minValue;
    }

    void OnSpeedChanged(int idx)
    {
        simulationSpeed = idx switch
        {
            0 => 0f,
            1 => 0.5f,
            2 => 1f,
            3 => 2f,
            4 => 4f,
            _ => 1f
        };
    }

    void OnDropdownValueChanged(int selectedIndex)
    {
        // Check that the index is within range.
        if (selectedIndex >= 0 && selectedIndex < keyList.Count && matchID != keyList[selectedIndex])
        {
            LoadMatch(keyList[selectedIndex]);
        }
    }

    public void LoadMatch(int newID)
    {
        Debug.Log("Loading match with ID: " + newID);

        // Set new ID and reset current scene
        matchID = newID;

        // Clear all dictionaries and remove spawned objects
        trackingDataDict.Clear();
        playerDict.Clear();
        objectInstances.Clear();
        trackingIndices.Clear();
        objectExtrapolated.Clear();
        simulationTime = 0f;

        // Remove objects with "Ball" tag
        GameObject[] ballObjects = GameObject.FindGameObjectsWithTag("Ball");
        foreach (GameObject ball in ballObjects)
        {
            Destroy(ball);
        }


        // Remove spawned players
        Destroy(homeTeam);
        Destroy(awayTeam);

        // Remove objects with "Goal" tag
        GameObject[] goalObjects = GameObject.FindGameObjectsWithTag("Goal");
        foreach (GameObject goal in goalObjects)
        {
            Destroy(goal);
        }

        // Reload all data
        LoadMetaData();
        LoadPlayerData();
        LoadTrackingData();

        if (tv != null)
            tv.Reset();
    }

    public static string[] GetCSVLines(string csvFileName)
    {
        // Build the file path, depending on whether a path was selected (always the case for builds, but not required in editor)
        string filePath;
        if (PathPicker.SelectedPath == null || PathPicker.SelectedPath == "")
        {
            filePath = Path.Combine(Application.streamingAssetsPath, csvFileName);
        } else
        {
            filePath = Path.Combine(PathPicker.SelectedPath, csvFileName);
        }

        if (!File.Exists(filePath))
        {
            Debug.LogError("CSV file not found: " + filePath);
            return null;
        }

        // Read all lines from the CSV file.
        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length <= 1)
        {
            Debug.LogError("CSV file is empty or missing header.");
            return null;
        }

        return lines;
    }

    public Dictionary<string, List<TrackingData>> GetTrackingData(int objID)
    {
        return trackingDataDict[objID];
    }


    public static string GetHomeTeamName(int matchID)
    {
        string[] lines = GetCSVLines(matchID + "_metadata.csv");

        // Process second line (skip header)
        string line = lines[1];

        // Replace e.g. "Spain, Women" in line with Spain and same for other team
        line = Regex.Replace(line, @"""([^,]+),[^""]+""", "$1");

        // Expected CSV format: match_id,match_date,competition,season,home_team,away_team,
        // home_score,away_score,home_team_jersey_color,away_team_jersey_color,home_team_number_color,
        // away_team_number_color,home_team_coach,away_team_coach,pitch_name,pitch_length,pitch_width,provider,fps
        string[] tokens = line.Split(',');

        return tokens[4].Trim();
    }

    void LoadMetaData()
    {
        string[] lines = GetCSVLines(matchID + "_metadata.csv");

        // Process second line (skip header)
        string line = lines[1];

        if (string.IsNullOrEmpty(line))
        {
            Debug.LogWarning("Empty line found in CSV.");
            return;
        }

        // Replace e.g. "Spain, Women" in line with Spain and same for other team
        line = Regex.Replace(line, @"""([^,]+),[^""]+""", "$1");

        // Expected CSV format: match_id,match_date,competition,season,home_team,away_team,
        // home_score,away_score,home_team_jersey_color,away_team_jersey_color,home_team_number_color,
        // away_team_number_color,home_team_coach,away_team_coach,pitch_name,pitch_length,pitch_width,provider,fps
        string[] tokens = line.Split(',');

        Color homeTeamJerseyColor, homeTeamNumberColor, awayTeamJerseyColor, awayTeamNumberColor;
        ColorUtility.TryParseHtmlString(tokens[8], out homeTeamJerseyColor);
        ColorUtility.TryParseHtmlString(tokens[9], out awayTeamJerseyColor);
        ColorUtility.TryParseHtmlString(tokens[10], out homeTeamNumberColor);
        ColorUtility.TryParseHtmlString(tokens[11], out awayTeamNumberColor);

        match = new Match
        {
            date = tokens[1],
            competition = tokens[2],
            homeTeam = tokens[4].Trim(),
            awayTeam = tokens[5].Trim(),
            pitchWidth = float.Parse(tokens[16]),
            pitchLength = float.Parse(tokens[15]),
            homeTeamJerseyColor = homeTeamJerseyColor,
            homeTeamNumberColor = homeTeamNumberColor,
            awayTeamJerseyColor = awayTeamJerseyColor,
            awayTeamNumberColor = awayTeamNumberColor
        };

        homeTeam = new GameObject(match.homeTeam);
        awayTeam = new GameObject(match.awayTeam);

        if (match.homeTeam == "Spain" && match.awayTeam == "Denmark")
        {
            // Fix incorrect pitch size
            match.pitchWidth = 68f;
            match.pitchLength = 105f;
            Debug.Log("Corrected pitch size for Spain vs Denmark match.");
        }

        if (replay)
            uiController.SetMetadata(match.homeTeam, match.awayTeam, homeTeamJerseyColor, awayTeamJerseyColor);

        // Instantiate goals
        GameObject homeGoal = Instantiate(goalPrefab);
        if (normalizePitchSize)
            homeGoal.transform.position = new Vector3(-105f / 2, 0, 0);
        else
            homeGoal.transform.position = new Vector3(-match.pitchLength / 2, 0, 0);
        homeGoal.transform.Rotate(0, 90, 0);

        GameObject awayGoal = Instantiate(goalPrefab);
        if (normalizePitchSize)
            awayGoal.transform.position = new Vector3(105f / 2, 0, 0);
        else
            awayGoal.transform.position = new Vector3(match.pitchLength / 2, 0, 0);
        awayGoal.transform.Rotate(0, 270, 0);

        if (normalizePitchSize)
            pitchRenderer.DrawPitchMarkings(68f, 105f);
        else
            pitchRenderer.DrawPitchMarkings(match.pitchWidth, match.pitchLength);
    }


    void LoadPlayerData()
    {
        string[] lines = GetCSVLines(matchID + "_lineup.csv");

        // Process each line (skip header)
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrEmpty(line))
                continue;

            // Replace e.g. "Spain, Women" in line with Spain
            line = Regex.Replace(line, @"""([^,]+),[^""]+""", "$1");

            // Expected CSV format: match_id,team_name,player_id,player_first_name,player_last_name,player_shirt_number,
            // player_position,player_birthdate,start_time,end_time,yellow_card,red_card,injured,goal,own_goal
            // Parse values (ignoring match_id in this example)
            string[] tokens = line.Split(',');
            string name = tokens[3] != "" ? tokens[3][0] + ". " + tokens[4] : tokens[4]; // some players apparently don't have a first name
            string team = tokens[1].Split(',')[0]; // Team name has format Spain, Women
            int number = int.Parse(tokens[5]);
            string position = tokens[6];
            int playerID = int.Parse(tokens[2]);

            // Create a new player object.
            Player player = new Player
            {
                name = name,
                team = team,
                number = number,
                position = position
            };

            // Add this player to the dictionary.
            playerDict[playerID] = player;
        }
    }


    public static Dictionary<string, List<TrackingData>> LoadBallTrackingData(int matchID)
    {
        // Read pitch dimensions from *_metadata.csv (needed for normalising)
        float pitchLength = 0f;
        float pitchWidth = 0f;

        string[] metaLines = GetCSVLines($"{matchID}_metadata.csv");
        if (metaLines != null && metaLines.Length > 1)
        {
            // Collapse quoted team names that contain commas
            string meta = Regex.Replace(metaLines[1], @"""([^,]+),[^""]+""", "$1");
            string[] m = meta.Split(',');
            if (m.Length > 16 &&
                float.TryParse(m[15], out float len) &&
                float.TryParse(m[16], out float wid))
            {
                pitchLength = len;
                pitchWidth = wid;
            }
        }

        // Parse *_tracking.csv and keep only object_id == -1 (the ball)
        var ballDict = new Dictionary<string, List<TrackingData>>
        {
            { "1H", new List<TrackingData>() },
            { "2H", new List<TrackingData>() }
        };

        string[] lines = GetCSVLines($"{matchID}_tracking.csv");
        if (lines == null) return ballDict; // nothing we can do

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrEmpty(line)) continue;

            string[] t = line.Split(',');
            if (t.Length < 9) continue;// malformed

            if (!int.TryParse(t[4], out int objectId) || objectId != -1)
                continue; // not the ball

            bool firstHalf = int.Parse(t[1]) == 1;
            string period = firstHalf ? "1H" : "2H";

            float timeSec = float.Parse(t[3]) / 1000f;

            float x = float.Parse(t[5]);
            float z = float.Parse(t[6]); // Unity-z <-> CSV-y
            float y = float.Parse(t[7]); // keep original y for ball

            x = x / pitchLength * 105f;
            z = z / pitchWidth * 68f;

            bool extrapolated = bool.Parse(t[8]);

            ballDict[period].Add(new TrackingData(timeSec, new Vector3(x, y, z), extrapolated, firstHalf));
        }

        // Ensure chronological order
        ballDict["1H"].Sort((a, b) => a.time.CompareTo(b.time));
        ballDict["2H"].Sort((a, b) => a.time.CompareTo(b.time));

        return ballDict;
    }


void LoadTrackingData()
    {
        string[] lines = GetCSVLines(matchID + "_tracking.csv");

        // Process each line (skip header)
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrEmpty(line))
            {
                Debug.LogWarning("Empty line found in CSV.");
                continue;
            }

            // Expected CSV format: match_id,half,frame_id,timestamp,object_id,x,y,z,extrapolated
            string[] tokens = line.Split(',');
            if (tokens.Length < 9)
            {
                Debug.LogWarning("Invalid CSV line: " + line);
                continue;
            }

            // Parse values (ignoring match_id, half, frame_id, and extrapolated in this example)
            if (!int.TryParse(tokens[4], out int objectId))
            {
                Debug.LogWarning("Invalid object_id: " + tokens[4]);
                continue;
            }

            // Timestamp is in milliseconds; convert to seconds.
            float timestamp = float.Parse(tokens[3]);
            float timeSec = timestamp / 1000f;

            float x = float.Parse(tokens[5]);
            // Invert z-axis to match Unity's coordinate system.
            float z = float.Parse(tokens[6]);
            float y = 0f;
            float.TryParse(tokens[7], out y);

            // normalize to a 100.6 m x 64 m pitch if enabled
            if (normalizePitchSize && match != null && match.pitchLength > 0f && match.pitchWidth > 0f)
            {
                x = x / match.pitchLength * 105f;
                z = z / match.pitchWidth * 68f;
            }

            bool extrapolated = bool.Parse(tokens[8]);

            if (objectId != -1)
            {
                y += 0.8125f; // Add Player height/2 if not ball
            }

            bool firstHalf = int.Parse(tokens[1]) == 1;

            // Create a new keyframe.
            TrackingData data = new TrackingData(timeSec, new Vector3(x, y, z), extrapolated, firstHalf);

            string period = firstHalf ? "1H" : "2H";

            // Check if the object_id already exists in the dictionary.
            if (!trackingDataDict.ContainsKey(objectId))
            {
                trackingDataDict[objectId] = new Dictionary<string, List<TrackingData>>();
                trackingDataDict[objectId][period] = new List<TrackingData>();
            }
            else
            {
                if (!trackingDataDict[objectId].ContainsKey(period))
                {
                    trackingDataDict[objectId][period] = new List<TrackingData>();
                }
            }

            trackingDataDict[objectId][period].Add(data);
        }


        // Instantiate a 3D object (a sphere in this example) for each unique object_id and initialize the pointer.
        // But only if replay is true
        if (!replay)
            return;

        foreach (int objectId in trackingDataDict.Keys)
        {
            objectExtrapolated[objectId] = false;

            if (objectId == -1)
            {
                // We handle ball object separately
                GameObject obj = Instantiate(ballPrefab);
                obj.name = "Ball";
                objectInstances.Add(objectId, obj);
                trackingIndices.Add(objectId, 0);
            }
            else
            {
                // Check if in player dict
                if (!playerDict.ContainsKey(objectId))
                {
                    Debug.Log($"Player with ID {objectId} not found in player dict");
                    continue;
                }
                    
                
                // Get player with object ID
                Player player = playerDict[objectId];
                GameObject obj = Instantiate(playerPrefab);

                // Rotate 90 degrees
                obj.transform.Rotate(0, 90, 0);

                // Set player's team color
                Renderer rend = obj.GetComponent<Renderer>();
                rend.material.color = player.team == homeTeam.name ? match.homeTeamJerseyColor : match.awayTeamJerseyColor;

                // Special case if goalkeeper
                if (player.position == "Goalkeeper")
                {
                    rend.material.color = Color.black;
                }

                // Set player's number
                TextMeshProUGUI[] textMeshes = obj.GetComponentsInChildren<TextMeshProUGUI>();
                // Filter by tag number
                foreach (TextMeshProUGUI textMesh in textMeshes)
                {
                    if (textMesh.tag == "Number")
                    {
                        textMesh.text = player.number.ToString();
                        textMesh.color = player.team == homeTeam.name ? match.homeTeamNumberColor : match.awayTeamNumberColor;
                    }
                    else if (textMesh.tag == "Name")
                    {
                        textMesh.text = player.name;
                        //leave name black
                        //textMesh.color = player.team == homeTeam.name ? match.homeTeamNumberColor : match.awayTeamNumberColor;
                    }
                }

                obj.transform.parent = player.team == homeTeam.name ? homeTeam.transform : awayTeam.transform;
                obj.name = player.name;
                objectInstances.Add(objectId, obj);
                trackingIndices.Add(objectId, 0);
            }
        }

        if (timeSlider != null)
        {
            RefreshTimeSliderRange();
        }
    }



    void Update()
    {
        // Only runs if replay is true
        if (!replay)
            return;

        // Advance the simulation time.
        simulationTime += Time.deltaTime * simulationSpeed;

        if (timeSlider != null)
        {
            simulationTime = Mathf.Clamp(simulationTime, 0f, timeSlider.maxValue);
            timeSlider.SetValueWithoutNotify(simulationTime);
        }

        string minutes = Mathf.Floor(simulationTime / 60).ToString("00");
        string seconds = (simulationTime % 60).ToString("00");
        uiController.time.text = minutes + ":" + seconds;

        // Update each object's position based on the current simulation time.
        foreach (var kvp in trackingDataDict)
        {
            int objectId = kvp.Key;

            if (!objectInstances.ContainsKey(objectId))
                continue;

            GameObject obj = objectInstances[objectId];

            if (!kvp.Value.ContainsKey(currentPeriod))
            {
                obj.transform.position = new Vector3(0, -10, 0); // hide
                continue;
            }

            List<TrackingData> keyframes = kvp.Value[currentPeriod];

            // If simulation time is before the first keyframe, hide below ground
            if (simulationTime <= keyframes[0].time)
            {
                obj.transform.position = new Vector3(0, -10, 0);
            }
            // If simulation time is after the last keyframe, hide below ground
            else if (simulationTime >= keyframes[keyframes.Count - 1].time)
            {
                obj.transform.position = new Vector3(0, -10, 0);
            }
            else
            {
                // Retrieve the current pointer for this object.
                int pointer = trackingIndices[objectId];

                // Update the pointer if simulationTime has passed the next keyframe.
                while (pointer < keyframes.Count - 2 && simulationTime > keyframes[pointer + 1].time)
                    pointer++;
                while (pointer > 0 && simulationTime < keyframes[pointer].time)
                    pointer--;

                trackingIndices[objectId] = pointer;

                // Interpolate between the current keyframe and the next keyframe.
                float t = (simulationTime - keyframes[pointer].time) /
                          (keyframes[pointer + 1].time - keyframes[pointer].time);
                obj.transform.position = Vector3.Lerp(keyframes[pointer].position,
                                                       keyframes[pointer + 1].position, t);

                // If the current keyframe is extrapolated, make the object slightly transparent
                // Getting renderer every time and creating a new color is inefficient, but let's keep it simple for now.
                if (objectExtrapolated[objectId] != keyframes[pointer].extrapolated)
                {
                    objectExtrapolated[objectId] = keyframes[pointer].extrapolated;
                    Renderer rend = obj.GetComponent<Renderer>();
                    Color color = rend.material.color;
                    color.a = keyframes[pointer].extrapolated ? 0.42f : 1f;
                    rend.material.color = color;
                }
            }
        }
    }
}
