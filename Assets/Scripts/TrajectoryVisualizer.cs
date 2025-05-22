using System.Collections.Generic;
using TMPro;
using UnityEngine;


public class SetPiece
{
    public int matchID;
    public float timestamp;
    public string type;
    public string team;
    public string player;
    public bool accurate;
    public string matchPeriod;
    public bool teamHome;
    public float duration;
}

public class TrajectoryVisualizer : MonoBehaviour
{
    [SerializeField] private FootballTrackingManager ftm;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private bool useAllMatches = false;

    private GameObject setPiecesParent;

    // Generic collections that support many matches simultaneously
    private readonly List<SetPiece> setPieces = new List<SetPiece>();
    private readonly List<SetPiece> setPiecesFiltered = new List<SetPiece>();

    // matchID -> "1H"/"2H" -> keyframes
    private readonly Dictionary<int, Dictionary<string, List<TrackingData>>> ballTrackingData
        = new Dictionary<int, Dictionary<string, List<TrackingData>>>();

    private readonly Dictionary<int, bool> teamHome // matchID -> isTeamHome?
        = new Dictionary<int, bool>();

    // Unique key =  "<matchID>_<floor(timestamp)>"
    private readonly Dictionary<string, GameObject> balls = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, int> trackingIndices = new Dictionary<string, int>();
    private readonly Dictionary<string, float> simulationTimes = new Dictionary<string, float>();

    private bool visualize = false;

    // TODO: populate with offsets between Wyscout and Skillcorner for each period for your game matchids
    private readonly Dictionary<int, (float, float)> offsets = new Dictionary<int, (float, float)>();

    // TODO: populate with matchIDs that need to be flipped (usually set to true, rarely needs to be set to false)
    private readonly Dictionary<int, bool> flipPositions = new Dictionary<int, bool>();

    [SerializeField]
    private TMP_Dropdown matchPickerDropdown;

    public void Reset() {
        InternalReset();
    }

    public void AddMatch(int id) { InternalAddMatch(id); }

    void Awake() { 
        setPiecesParent = new GameObject("SetPieces");

        if (useAllMatches)
            matchPickerDropdown.interactable = false;
    }

    void InternalReset()
    {
        // wipe objects in hierarchy
        foreach (Transform child in setPiecesParent.transform)
            Destroy(child.gameObject);

        // wipe collections
        setPieces.Clear(); setPiecesFiltered.Clear();
        ballTrackingData.Clear(); teamHome.Clear();
        balls.Clear(); trackingIndices.Clear(); simulationTimes.Clear();

        // Always (re-)load the match that the FT-manager currently shows
        InternalAddMatch(ftm.matchID);

        // Add further matches depending on inspector settings
        if (useAllMatches)
        {
            List<int> matchIDs = ftm.GetMatchIDs();
            foreach (int id in matchIDs)
                if (id != ftm.matchID) InternalAddMatch(id);
        }
    }

    public void ToggleMatchesUsed(bool useAll)
    {
        matchPickerDropdown.interactable = !useAll;
        useAllMatches = useAll;

        if (useAll)
        {
            InternalReset();
        }
        else
        {
            // Remove all matches except the one currently shown
            foreach (int id in ballTrackingData.Keys)
                if (id != ftm.matchID) InternalReset();
        }
    }

    void InternalAddMatch(int id)
    {
        if (ballTrackingData.ContainsKey(id)) return; // already added

        // Tracking data
        ballTrackingData[id] = FootballTrackingManager.LoadBallTrackingData(id);
        string teamName = PathPicker.TeamName == null || PathPicker.TeamName == "" ? "Denmark" : PathPicker.TeamName;
        teamHome[id] = FootballTrackingManager.GetHomeTeamName(id) == teamName;

        LoadSetPieces(id);
    }

    void LoadSetPieces(int matchID)
    {
        string[] lines = FootballTrackingManager.GetCSVLines($"{matchID}_set_pieces.csv");
        if (lines == null) return;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrEmpty(line)) continue;

            // format: timestamp,type,team,player,accurate,matchPeriod
            string[] t = line.Split(',');
            if (t.Length < 7 || !float.TryParse(t[0], out float ts)) continue;

            if (!offsets.ContainsKey(matchID))
            {
                Debug.LogWarning($"Match ID {matchID} not found in offsets dictionary, this may lead to unaligned Skillcorner/Wyscout data.");
            } else
            {
                ts += (t[5].Trim() == "1H") ? offsets[matchID].Item1 : offsets[matchID].Item2;
            }

            setPieces.Add(new SetPiece
            {
                matchID = matchID,
                timestamp = ts,
                type = t[1].Trim(),
                team = t[2].Trim(),
                player = t[3].Trim(),
                accurate = t[4].Trim().ToLower() == "true",
                matchPeriod = t[5].Trim(),
                teamHome = teamHome[matchID],
                duration = float.TryParse(t[6], out float d) ? d : 0f
            });
        }
    }

    int GetIndex(int matchID, string key, int pointer,
                 float simTime, string period)
    {
        var data = ballTrackingData[matchID][period];

        if (simTime <= data[0].time || simTime >= data[^1].time)
            return -1;

        while (pointer < data.Count - 2 && simTime > data[pointer + 1].time)
            pointer++;
        trackingIndices[key] = pointer;

        return pointer;
    }


    public void VisualizeSetPieces(string type)
    {
        visualize = false;

        // Clean out previous visualisation
        foreach (Transform child in setPiecesParent.transform)
            Destroy(child.gameObject);

        balls.Clear(); trackingIndices.Clear(); simulationTimes.Clear();

        // Filter team and type
        string teamName = PathPicker.TeamName == null || PathPicker.TeamName == "" ? "Denmark" : PathPicker.TeamName;
        setPiecesFiltered.Clear();
        foreach (var sp in setPieces)
            if (sp.type == type && sp.team == teamName)
                setPiecesFiltered.Add(sp);

        label.text = $"Visualising {setPiecesFiltered.Count} “{type}” set-pieces";

        // Instantiate ball clones
        foreach (var sp in setPiecesFiltered)
        {
            string key = $"{sp.matchID}_{(int)sp.timestamp}";
            if (balls.ContainsKey(key)) continue; // already done

            GameObject ball = Instantiate(ftm.ballPrefab, Vector3.zero, Quaternion.identity, setPiecesParent.transform);

            // Trail colour
            TrailRenderer tr = ball.GetComponent<TrailRenderer>();
            Color ok, ko; ColorUtility.TryParseHtmlString("#627313", out ok);
            ColorUtility.TryParseHtmlString("#B7352D", out ko);
            tr.startColor = tr.endColor = sp.accurate ? ok : ko;

            // Make trail not disappear
            tr.time = 1000f;

            ball.name = $"{sp.matchID}_{sp.player}_{sp.type}_{sp.team}_{sp.matchPeriod}";
            balls[key] = ball;

            // Initial time + ptr
            float t0 = sp.timestamp - (sp.matchPeriod == "2H" ? 45 * 60 : 0);
            simulationTimes[key] = t0;
            trackingIndices[key] = 0;
            GetIndex(sp.matchID, key, 0, t0, sp.matchPeriod);
        }

        visualize = true;
    }


    void Update()
    {
        if (!visualize) return;

        foreach (var sp in setPiecesFiltered)
        {
            string key = $"{sp.matchID}_{(int)sp.timestamp}";
            if (!balls.TryGetValue(key, out GameObject ball)) continue;

            // Hide ball once its duration elapsed
            float startTime = sp.timestamp - (sp.matchPeriod == "2H" ? 45 * 60 : 0);
            if (simulationTimes[key] - startTime > sp.duration)
            {
                if (ball.activeSelf) ball.SetActive(false);
                continue;
            }
            else if (!ball.activeSelf) ball.SetActive(true);

            int ptr = GetIndex(sp.matchID, key, trackingIndices[key],
                               simulationTimes[key], sp.matchPeriod);
            if (ptr == -1) continue;

            var data = ballTrackingData[sp.matchID][sp.matchPeriod];
            float t = (simulationTimes[key] - data[ptr].time) /
                            (data[ptr + 1].time - data[ptr].time);
            Vector3 pos = Vector3.Lerp(data[ptr].position, data[ptr + 1].position, t);

            /* ----------------------------------------------------------
             *  Make chosen team always attack the same direction.
             *  Flip when:
             *     Team is home -> second half
             *     Team is away -> first half
             *---------------------------------------------------------- */

            bool matchFlipped = true;
            if (flipPositions.ContainsKey(sp.matchID))
            {
                matchFlipped = flipPositions[sp.matchID];
            } else
            {
                Debug.LogWarning($"Match ID {sp.matchID} not found in flipPositions dictionary, this may lead to tracking data on wrong side of pitch.");
            }

            bool flip = matchFlipped ? !data[ptr].firstHalf : data[ptr].firstHalf;
            if (flip) { 
                pos.x = -pos.x;
                pos.z = -pos.z;
            }

            ball.transform.position = pos;
            simulationTimes[key] += Time.deltaTime;
        }
    }
}
