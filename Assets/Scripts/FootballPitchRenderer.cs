using UnityEngine;

public class FootballPitchRenderer : MonoBehaviour
{

    [Header("Line Settings")]
    public float lineWidth = 0.1f;

    // Standard marking dimensions (in meters)
    private float penaltyAreaDepth = 16.5f;
    private float penaltyAreaWidth = 40.32f; // not sure about this
    private float goalAreaDepth = 5.5f;
    private float goalAreaWidth = 18.32f; // not sure about this
    private float centerCircleRadius = 9.15f;
    private float penaltyMarkDistance = 11f;
    private float cornerArcRadius = 0.91f;

    // How many segments to use for circles/arcs
    private int circleSegments = 50;
    private int arcSegments = 10;

    public void DrawPitchMarkings(float pitchWidth, float pitchHeight)
    {
        // Clear any previous markings
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        // Compute half dimensions:
        float halfLength = pitchHeight / 2f; // along x
        float halfWidth = pitchWidth / 2f;   // along z

        // 1. Outer boundaries (rectangle)
        Vector3[] outer = new Vector3[5];
        outer[0] = new Vector3(-halfLength, 0, -halfWidth);
        outer[1] = new Vector3(halfLength, 0, -halfWidth);
        outer[2] = new Vector3(halfLength, 0, halfWidth);
        outer[3] = new Vector3(-halfLength, 0, halfWidth);
        outer[4] = outer[0];
        CreateLine("Boundary", outer);

        // 2. Center line (divides the pitch along its length).
        // Since the long axis is along x, the center line is drawn at x = 0, spanning from one touchline to the other.
        Vector3[] centerLine = new Vector3[2];
        centerLine[0] = new Vector3(0, 0, -halfWidth);
        centerLine[1] = new Vector3(0, 0, halfWidth);
        CreateLine("CenterLine", centerLine);

        // 3. Center circle (centered at the origin)
        Vector3 center = Vector3.zero;
        Vector3[] centerCircle = CreateCirclePoints(center, centerCircleRadius, circleSegments);
        CreateLine("CenterCircle", centerCircle, true);

        // 4. Penalty areas (goals are on the left and right sides, along x)
        // Right penalty area (at x = halfLength)
        Vector3[] rightPenalty = new Vector3[5];
        rightPenalty[0] = new Vector3(halfLength, 0, -penaltyAreaWidth / 2f);
        rightPenalty[1] = new Vector3(halfLength, 0, penaltyAreaWidth / 2f);
        rightPenalty[2] = new Vector3(halfLength - penaltyAreaDepth, 0, penaltyAreaWidth / 2f);
        rightPenalty[3] = new Vector3(halfLength - penaltyAreaDepth, 0, -penaltyAreaWidth / 2f);
        rightPenalty[4] = rightPenalty[0];
        CreateLine("RightPenaltyArea", rightPenalty);

        // Left penalty area (at x = -halfLength)
        Vector3[] leftPenalty = new Vector3[5];
        leftPenalty[0] = new Vector3(-halfLength, 0, -penaltyAreaWidth / 2f);
        leftPenalty[1] = new Vector3(-halfLength, 0, penaltyAreaWidth / 2f);
        leftPenalty[2] = new Vector3(-halfLength + penaltyAreaDepth, 0, penaltyAreaWidth / 2f);
        leftPenalty[3] = new Vector3(-halfLength + penaltyAreaDepth, 0, -penaltyAreaWidth / 2f);
        leftPenalty[4] = leftPenalty[0];
        CreateLine("LeftPenaltyArea", leftPenalty);

        // 5. Goal areas (goal boxes)
        // Right goal area
        Vector3[] rightGoalArea = new Vector3[5];
        rightGoalArea[0] = new Vector3(halfLength, 0, -goalAreaWidth / 2f);
        rightGoalArea[1] = new Vector3(halfLength, 0, goalAreaWidth / 2f);
        rightGoalArea[2] = new Vector3(halfLength - goalAreaDepth, 0, goalAreaWidth / 2f);
        rightGoalArea[3] = new Vector3(halfLength - goalAreaDepth, 0, -goalAreaWidth / 2f);
        rightGoalArea[4] = rightGoalArea[0];
        CreateLine("RightGoalArea", rightGoalArea);

        // Left goal area
        Vector3[] leftGoalArea = new Vector3[5];
        leftGoalArea[0] = new Vector3(-halfLength, 0, -goalAreaWidth / 2f);
        leftGoalArea[1] = new Vector3(-halfLength, 0, goalAreaWidth / 2f);
        leftGoalArea[2] = new Vector3(-halfLength + goalAreaDepth, 0, goalAreaWidth / 2f);
        leftGoalArea[3] = new Vector3(-halfLength + goalAreaDepth, 0, -goalAreaWidth / 2f);
        leftGoalArea[4] = leftGoalArea[0];
        CreateLine("LeftGoalArea", leftGoalArea);

        // 6. Penalty marks (drawn as small filled circles)
        float penaltyMarkRadius = 0.1f; // Reduced radius for a smaller penalty point
                                        // Right penalty mark: measured from the right goal line (x = halfLength)
        Vector3 rightPenaltyMarkCenter = new Vector3(halfLength - penaltyMarkDistance, 0, 0);
        CreateFilledCircle("RightPenaltyMark", rightPenaltyMarkCenter, penaltyMarkRadius, circleSegments);
        // Left penalty mark:
        Vector3 leftPenaltyMarkCenter = new Vector3(-halfLength + penaltyMarkDistance, 0, 0);
        CreateFilledCircle("LeftPenaltyMark", leftPenaltyMarkCenter, penaltyMarkRadius, circleSegments);

        // 7. Corner arcs (quarter-circles at each corner)
        // For each corner, the arc is drawn inside the pitch.
        // Top Right corner (x = halfLength, z = halfWidth): 
        //   The arc goes from the top boundary (point: (halfLength - cornerArcRadius, halfWidth)) to the right boundary (point: (halfLength, halfWidth - cornerArcRadius)).
        //   In polar coordinates relative to (halfLength, halfWidth): these correspond to angles 180° and 270°.
        Vector3 topRightCorner = new Vector3(halfLength, 0, halfWidth);
        Vector3[] arcTopRight = CreateArcPoints(topRightCorner, cornerArcRadius, 180f, 270f, arcSegments);
        CreateLine("CornerArcTopRight", arcTopRight);

        // Bottom Right corner (x = halfLength, z = -halfWidth):
        //   The arc goes from the right boundary (point: (halfLength, -halfWidth + cornerArcRadius)) to the bottom boundary (point: (halfLength - cornerArcRadius, -halfWidth)).
        //   Relative angles: from 90° to 180°.
        Vector3 bottomRightCorner = new Vector3(halfLength, 0, -halfWidth);
        Vector3[] arcBottomRight = CreateArcPoints(bottomRightCorner, cornerArcRadius, 90f, 180f, arcSegments);
        CreateLine("CornerArcBottomRight", arcBottomRight);

        // Bottom Left corner (x = -halfLength, z = -halfWidth):
        //   The arc goes from the bottom boundary (point: (-halfLength + cornerArcRadius, -halfWidth)) to the left boundary (point: (-halfLength, -halfWidth + cornerArcRadius)).
        //   Relative angles: from 0° to 90°.
        Vector3 bottomLeftCorner = new Vector3(-halfLength, 0, -halfWidth);
        Vector3[] arcBottomLeft = CreateArcPoints(bottomLeftCorner, cornerArcRadius, 0f, 90f, arcSegments);
        CreateLine("CornerArcBottomLeft", arcBottomLeft);

        // Top Left corner (x = -halfLength, z = halfWidth):
        //   The arc goes from the left boundary (point: (-halfLength, halfWidth - cornerArcRadius)) to the top boundary (point: (-halfLength + cornerArcRadius, halfWidth)).
        //   Relative angles: from 270° to 360°.
        Vector3 topLeftCorner = new Vector3(-halfLength, 0, halfWidth);
        Vector3[] arcTopLeft = CreateArcPoints(topLeftCorner, cornerArcRadius, 270f, 360f, arcSegments);
        CreateLine("CornerArcTopLeft", arcTopLeft);
    }

    // Creates a LineRenderer child object with the given points.
    void CreateLine(string name, Vector3[] points, bool loop = false)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.parent = transform;
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.positionCount = points.Length;
        lr.SetPositions(points);
        lr.loop = loop;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default")); // using a simple unlit material
        lr.startColor = Color.black;
        lr.endColor = Color.black;
        lr.useWorldSpace = true;
    }

    // Generates points for a full circle on the XZ plane.
    Vector3[] CreateCirclePoints(Vector3 center, float radius, int segments)
    {
        Vector3[] points = new Vector3[segments + 1];
        float angleStep = 360f / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angleRad = Mathf.Deg2Rad * (i * angleStep);
            float x = center.x + Mathf.Cos(angleRad) * radius;
            float z = center.z + Mathf.Sin(angleRad) * radius;
            points[i] = new Vector3(x, center.y, z);
        }
        return points;
    }

    // Generates points for an arc (a segment of a circle) on the XZ plane.
    // startAngle and endAngle are given in degrees from the positive x‑axis.
    Vector3[] CreateArcPoints(Vector3 center, float radius, float startAngle, float endAngle, int segments)
    {
        Vector3[] points = new Vector3[segments + 1];
        float angleStep = (endAngle - startAngle) / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angle = startAngle + i * angleStep;
            float angleRad = Mathf.Deg2Rad * angle;
            float x = center.x + Mathf.Cos(angleRad) * radius;
            float z = center.z + Mathf.Sin(angleRad) * radius;
            points[i] = new Vector3(x, center.y, z);
        }
        return points;
    }

    // Create a filled circle mesh.
    void CreateFilledCircle(string name, Vector3 center, float radius, int segments)
    {
        GameObject circle = new GameObject(name);
        circle.transform.parent = transform;
        MeshFilter mf = circle.AddComponent<MeshFilter>();
        MeshRenderer mr = circle.AddComponent<MeshRenderer>();
        Mesh mesh = new Mesh();

        Vector3[] vertices = new Vector3[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = center;
        float angleStep = 360f / segments;
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Deg2Rad * (i * angleStep);
            vertices[i] = new Vector3(center.x + Mathf.Cos(angle) * radius, center.y, center.z + Mathf.Sin(angle) * radius);
        }
        for (int i = 0; i < segments; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 2 > segments) ? 1 : i + 2;
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mf.mesh = mesh;

        mr.material = new Material(Shader.Find("Sprites/Default"));
        mr.material.color = Color.black;
    }
}
