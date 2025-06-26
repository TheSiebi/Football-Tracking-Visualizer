# 3D Real-Time Viewer for Football Tracking Data
This Unity application allows interactive, real-time exploration of Skillcorner and Wyscout football tracking data in 3D, and was created as part of the Soccer Analytics course at ETH Zurich. **[Try it on itch.io](https://msiebenmann.itch.io/football-tracking-visualizer)**

![image](https://github.com/user-attachments/assets/e6993ce8-6862-44ca-b731-37ac94aaeace)

## Features
- Real-time or custom-speed replay of full football matches, including timestamp seeking
- Flexible camera controls
- Visualization of all set pieces of a given type (goal kicks, free kicks, corner kicks, throw-ins) across matches or for a single match

https://github.com/user-attachments/assets/c0aa5cd4-4d0d-4afa-88eb-01a940fb0721

## Setup
This viewer now includes pre-processed [SkillCorner open tracking data](https://github.com/SkillCorner/opendata) for 9 matches from the 2019/2020 season. However, no Wyscout event data is included.
Both SkillCorner tracking data and Wyscout event data (if used) must be pre-processed into a CSV format.
When starting the viewer, you can set the path to the folder where this CSV data is stored.

### Pre-Processing Data
For every game you wish to visualize, you need to prepare four CSV files, with the following columns present:
#### `{matchid}_lineup.csv`
| match_id | team_name | player_id | player_first_name | player_last_name | player_shirt_number | player_position | player_birthdate | start_time | end_time | yellow_card | red_card | injured | goal | own_goal |
|----------|-----------|-----------|--------------------|-----------------|---------------------|-----------------|------------------|------------|----------|-------------|----------|---------|------|----------|
| 12345    | Falcons   | 10        | John               | Doe             | 9                   | Center Forward  | 1995-04-12       | 00:00:00   | 01:30:00 | 0           | 0        | False   | 0    | 0        |

#### `{matchid}_metadata.csv`
| match_id | match_date       | competition     | season   | home_team | away_team | home_score | away_score | home_team_jersey_color | away_team_jersey_color | home_team_number_color | away_team_number_color | home_team_coach | away_team_coach | pitch_name     | pitch_length | pitch_width | provider    | fps |
|----------|------------------|-----------------|----------|-----------|-----------|------------|------------|------------------------|------------------------|------------------------|------------------------|-----------------|-----------------|----------------|--------------|-------------|-------------|-----|
| 12345    | 31/12/2000 16:00 | Premier League  | 2024/25  | Falcons   | Hawks     | 2          | 1          | #ff0000                | #367eef                | #ffffff                | #000000                | Alex Smith      | Jamie Lee       | National Arena | 105          | 68          | SkillCorner | 10  |

#### `{matchid}_set_pieces.csv`
| timestamp | type       | team     | player     | accurate | matchPeriod | duration |
|-----------|------------|----------|------------|----------|-------------|----------|
| 14.123    | throw_in   | Falcons  | John Doe   | True     | 1H          | 3.2      |

**Notes**:
- type should be one of `{throw_in, goal_kick, free_kick, corner}`
- matchPeriod should be one of `{1H, 2H}`
- timestamp (w.r.t. start of match) and duration in seconds

#### `{matchid}_tracking.csv`
| match_id | half | frame_id | timestamp | object_id | x      | y      | z     | extrapolated |
|----------|------|----------|-----------|-----------|--------|--------|-------|--------------|
| 12345    | 1    | 452      | 14123    | 7         | 34.2   | 21.8   | 0.0   | False        |

**Notes**:
- timestamp in milliseconds (w.r.t. start of half-time)
- x, y, z in meters
- ball should have object_id -1

If you are interested in set pieces: There may be a mismatch in seconds between Wyscout and Skillcorner timestamps. Therefore, you should populate the dictionary `offsets` (inside `TrajectoryVisualizer.cs`) with the difference in seconds for each matchid, and period.
Also, we noticed some inconsistencies with the playing direction.
You may want to add certain matchids to the `flipPositions` dictionary (inside `TrajectoryVisualizer.cs`).

### Running the Application
If you are on Windows, you can download a build [here](https://github.com/TheSiebi/Football-Tracking-Visualizer/releases/).
If you are on another OS and/or want to build the application yourself/run it through the editor, install Unity 6000.0.23f1 (more recent versions likely work as well).

#### Controls
- WASD: Move forward/left/backward/right
- QE: Move down/up
- Shift: Increase move speed
- Mouse: look around
- F: Toggle view direction lock (useful when interacting with UI)

## Attributions
- 3D Goal Models by Emmanuel PUYBARET / eTeks <info@eteks.com>  and Scopia Visual Interfaces Systems, s.l. (http://www.scopia.es)
- [Standalone File Browser](https://github.com/gkngkc/UnityStandaloneFileBrowser) by gkngkc
- [SkillCorner Open Data](https://github.com/SkillCorner/opendata)

