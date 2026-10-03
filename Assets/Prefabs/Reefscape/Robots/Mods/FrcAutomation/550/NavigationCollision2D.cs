using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class NavigationCollision2D : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField]
    private FieldMap2D fieldMap;


    // ============================================================
    // FIELD WALL CLEARANCE
    // ============================================================

    [Header("Field Wall Clearance")]

    [Tooltip("Robot width in meters.")]
    [SerializeField]
    private float robotWidth = 0.915f;

    [Tooltip(
        "Extra clearance added between the robot and field walls."
    )]
    [SerializeField]
    private float fieldSafetyMargin = 0.0f;


    // ============================================================
    // REEF CLEARANCE
    // ============================================================

    [Header("Reef Clearance")]

    [Tooltip(
        "Additional distance around the physical Reef polygon " +
        "that the robot center should normally avoid."
    )]
    [SerializeField]
    private float reefNavigationClearance = 0.30f;


    // ============================================================
    // SEGMENT CHECKING
    // ============================================================

    [Header("Segment Checking")]

    [Tooltip(
        "Distance in meters between collision samples along a path."
    )]
    [SerializeField]
    private float segmentCheckSpacing = 0.10f;


    // ============================================================
    // DEBUG VISUALIZATION
    // ============================================================

    [Header("Debug Visualization")]

    [SerializeField]
    private bool showReefClearance = true;

    [SerializeField]
    private bool showCageBlockedRegions = true;

    [SerializeField]
    private bool showFieldClearance = true;

    [SerializeField]
    private bool showScoringPoseChecks = true;


    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void OnEnable()
    {
        FindFieldMap();
    }


    private void OnValidate()
    {
        robotWidth =
            Mathf.Max(
                0.0f,
                robotWidth
            );

        fieldSafetyMargin =
            Mathf.Max(
                0.0f,
                fieldSafetyMargin
            );

        reefNavigationClearance =
            Mathf.Max(
                0.0f,
                reefNavigationClearance
            );

        segmentCheckSpacing =
            Mathf.Max(
                0.01f,
                segmentCheckSpacing
            );

        FindFieldMap();
    }


    private void FindFieldMap()
    {
        if (fieldMap == null)
        {
            fieldMap =
                GetComponent<FieldMap2D>();
        }

        if (fieldMap == null)
        {
            fieldMap =
                FindAnyObjectByType<FieldMap2D>();
        }
    }


    // ============================================================
    // FIELD WALL CLEARANCE
    // ============================================================

    public float GetFieldClearanceRadius()
    {
        return robotWidth * 0.5f +
               fieldSafetyMargin;
    }


    // ============================================================
    // POINT NAVIGATION CHECK
    // ============================================================

    /// <summary>
    /// Returns true if the robot center can occupy this X-Z point.
    ///
    /// Field:
    ///     Keeps the robot center far enough from the field wall
    ///     that approximately half the robot width remains inside.
    ///
    /// Reef:
    ///     Uses a separately tuned Reef navigation clearance.
    ///
    /// Cage:
    ///     Only blocks the existing small cage polygon.
    /// </summary>
    public bool IsPointNavigable(
        Vector2 pointXZ)
    {
        if (fieldMap == null)
        {
            FindFieldMap();
        }

        if (fieldMap == null)
        {
            return false;
        }


        // --------------------------------------------------------
        // FIELD BOUNDARY
        // --------------------------------------------------------

        float fieldClearance =
            GetFieldClearanceRadius();


        if (!IsCircleInsidePolygon(
                pointXZ,
                fieldClearance,
                fieldMap.fieldBoundary))
        {
            return false;
        }


        // --------------------------------------------------------
        // STATIC NAVIGATION OBSTACLES
        // --------------------------------------------------------

        if (fieldMap.navigationObstacles != null)
        {
            foreach (
                FieldMap2D.NavigationObstacle obstacle
                in fieldMap.navigationObstacles)
            {
                if (obstacle == null ||
                    obstacle.vertices == null ||
                    obstacle.vertices.Count < 3)
                {
                    continue;
                }


                // ------------------------------------------------
                // REEF
                // ------------------------------------------------

                if (IsReefObstacle(obstacle))
                {
                    if (DoesCircleOverlapPolygon(
                            pointXZ,
                            reefNavigationClearance,
                            obstacle.vertices))
                    {
                        return false;
                    }

                    continue;
                }


                // ------------------------------------------------
                // CAGE
                // ------------------------------------------------

                if (IsCageObstacle(obstacle))
                {
                    if (IsPointInsidePolygon(
                            pointXZ,
                            obstacle.vertices))
                    {
                        return false;
                    }

                    continue;
                }


                // ------------------------------------------------
                // OTHER STATIC OBSTACLES
                // ------------------------------------------------

                if (IsPointInsidePolygon(
                        pointXZ,
                        obstacle.vertices))
                {
                    return false;
                }
            }
        }


        return true;
    }


    // ============================================================
    // SEGMENT NAVIGATION CHECK
    // ============================================================

    public bool IsSegmentNavigable(
        Vector2 startXZ,
        Vector2 endXZ)
    {
        float distance =
            Vector2.Distance(
                startXZ,
                endXZ
            );


        if (distance <= Mathf.Epsilon)
        {
            return IsPointNavigable(
                startXZ
            );
        }


        int numberOfSteps =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    distance /
                    segmentCheckSpacing
                )
            );


        for (int i = 0;
             i <= numberOfSteps;
             i++)
        {
            float t =
                (float)i /
                numberOfSteps;


            Vector2 samplePoint =
                Vector2.Lerp(
                    startXZ,
                    endXZ,
                    t
                );


            if (!IsPointNavigable(
                    samplePoint))
            {
                return false;
            }
        }


        return true;
    }


    // ============================================================
    // OBSTACLE TYPE HELPERS
    // ============================================================

    private bool IsReefObstacle(
        FieldMap2D.NavigationObstacle obstacle)
    {
        if (obstacle == null ||
            string.IsNullOrEmpty(obstacle.name))
        {
            return false;
        }


        return obstacle.name.Contains("Reef");
    }


    private bool IsCageObstacle(
        FieldMap2D.NavigationObstacle obstacle)
    {
        if (obstacle == null ||
            string.IsNullOrEmpty(obstacle.name))
        {
            return false;
        }


        return obstacle.name.Contains("Cage");
    }


    // ============================================================
    // CIRCLE INSIDE FIELD POLYGON
    // ============================================================

    private bool IsCircleInsidePolygon(
        Vector2 center,
        float radius,
        List<Vector2> polygon)
    {
        if (polygon == null ||
            polygon.Count < 3)
        {
            return false;
        }


        if (!IsPointInsidePolygon(
                center,
                polygon))
        {
            return false;
        }


        for (int i = 0;
             i < polygon.Count;
             i++)
        {
            Vector2 start =
                polygon[i];

            Vector2 end =
                polygon[
                    (i + 1) %
                    polygon.Count
                ];


            float distance =
                DistancePointToSegment(
                    center,
                    start,
                    end
                );


            if (distance < radius)
            {
                return false;
            }
        }


        return true;
    }


    // ============================================================
    // CIRCLE / POLYGON OVERLAP
    // ============================================================

    private bool DoesCircleOverlapPolygon(
        Vector2 center,
        float radius,
        List<Vector2> polygon)
    {
        if (polygon == null ||
            polygon.Count < 3)
        {
            return false;
        }


        if (IsPointInsidePolygon(
                center,
                polygon))
        {
            return true;
        }


        for (int i = 0;
             i < polygon.Count;
             i++)
        {
            Vector2 start =
                polygon[i];

            Vector2 end =
                polygon[
                    (i + 1) %
                    polygon.Count
                ];


            float distance =
                DistancePointToSegment(
                    center,
                    start,
                    end
                );


            if (distance <= radius)
            {
                return true;
            }
        }


        return false;
    }


    // ============================================================
    // POINT IN POLYGON
    // ============================================================

    private bool IsPointInsidePolygon(
        Vector2 point,
        List<Vector2> polygon)
    {
        if (polygon == null ||
            polygon.Count < 3)
        {
            return false;
        }


        bool inside = false;


        for (
            int i = 0,
            j = polygon.Count - 1;

            i < polygon.Count;

            j = i++)
        {
            Vector2 pi =
                polygon[i];

            Vector2 pj =
                polygon[j];


            bool intersects =
                ((pi.y > point.y) !=
                 (pj.y > point.y))
                &&
                (
                    point.x <
                    (pj.x - pi.x) *
                    (point.y - pi.y) /
                    (pj.y - pi.y) +
                    pi.x
                );


            if (intersects)
            {
                inside = !inside;
            }
        }


        return inside;
    }


    // ============================================================
    // POINT TO SEGMENT DISTANCE
    // ============================================================

    private float DistancePointToSegment(
        Vector2 point,
        Vector2 segmentStart,
        Vector2 segmentEnd)
    {
        Vector2 segment =
            segmentEnd -
            segmentStart;


        float segmentLengthSquared =
            segment.sqrMagnitude;


        if (segmentLengthSquared <=
            Mathf.Epsilon)
        {
            return Vector2.Distance(
                point,
                segmentStart
            );
        }


        float t =
            Vector2.Dot(
                point - segmentStart,
                segment
            ) /
            segmentLengthSquared;


        t =
            Mathf.Clamp01(t);


        Vector2 closestPoint =
            segmentStart +
            t * segment;


        return Vector2.Distance(
            point,
            closestPoint
        );
    }


    // ============================================================
    // DEBUG VISUALIZATION
    // ============================================================

    private void OnDrawGizmos()
    {
        if (fieldMap == null)
        {
            FindFieldMap();
        }

        if (fieldMap == null)
        {
            return;
        }


        if (showReefClearance)
        {
            DrawReefClearance();
        }


        if (showCageBlockedRegions)
        {
            DrawCageBlockedRegions();
        }


        if (showFieldClearance)
        {
            DrawFieldClearance(
                GetFieldClearanceRadius()
            );
        }


        if (showScoringPoseChecks)
        {
            DrawScoringPoseChecks();
        }
    }


    // ============================================================
    // DRAW REEF CLEARANCE
    // ============================================================

    private void DrawReefClearance()
    {
        if (fieldMap.navigationObstacles == null)
        {
            return;
        }


        Gizmos.color =
            new Color(
                1.0f,
                0.35f,
                0.0f,
                1.0f
            );


        foreach (
            FieldMap2D.NavigationObstacle obstacle
            in fieldMap.navigationObstacles)
        {
            if (!IsReefObstacle(obstacle))
            {
                continue;
            }


            if (obstacle.vertices == null ||
                obstacle.vertices.Count < 3)
            {
                continue;
            }


            List<Vector2> expandedPolygon =
                ExpandPolygonFromCenter(
                    obstacle.vertices,
                    reefNavigationClearance
                );


            DrawPolygonXZ(
                expandedPolygon,
                0.19f
            );
        }
    }


    // ============================================================
    // DRAW CAGE BLOCKED REGIONS
    // ============================================================

    private void DrawCageBlockedRegions()
    {
        if (fieldMap.navigationObstacles == null)
        {
            return;
        }


        Gizmos.color =
            Color.yellow;


        foreach (
            FieldMap2D.NavigationObstacle obstacle
            in fieldMap.navigationObstacles)
        {
            if (!IsCageObstacle(obstacle))
            {
                continue;
            }


            if (obstacle.vertices == null ||
                obstacle.vertices.Count < 3)
            {
                continue;
            }


            DrawPolygonXZ(
                obstacle.vertices,
                0.20f
            );
        }
    }


    // ============================================================
    // DRAW FIELD CLEARANCE
    // ============================================================

    private void DrawFieldClearance(
        float clearance)
    {
        if (fieldMap.fieldBoundary == null ||
            fieldMap.fieldBoundary.Count < 3)
        {
            return;
        }


        Gizmos.color =
            Color.white;


        Vector2 fieldCenter =
            GetPolygonCenter(
                fieldMap.fieldBoundary
            );


        for (int i = 0;
             i < fieldMap.fieldBoundary.Count;
             i++)
        {
            Vector2 start =
                fieldMap.fieldBoundary[i];

            Vector2 end =
                fieldMap.fieldBoundary[
                    (i + 1) %
                    fieldMap.fieldBoundary.Count
                ];


            Vector2 direction =
                end - start;


            if (direction.sqrMagnitude <=
                Mathf.Epsilon)
            {
                continue;
            }


            direction.Normalize();


            Vector2 normal =
                new Vector2(
                    -direction.y,
                    direction.x
                );


            Vector2 midpoint =
                (start + end) *
                0.5f;


            if (Vector2.Dot(
                    fieldCenter - midpoint,
                    normal) < 0.0f)
            {
                normal =
                    -normal;
            }


            Vector2 offset =
                normal *
                clearance;


            DrawLineXZ(
                start + offset,
                end + offset,
                0.21f
            );
        }
    }


    // ============================================================
    // DRAW SCORING POSE CHECKS
    // ============================================================

    /// <summary>
    /// Draws a small marker at every known Reef alignment pose.
    ///
    /// Green = IsPointNavigable returned true.
    /// Red   = IsPointNavigable returned false.
    ///
    /// These poses came directly from MoSim's Reef alignment
    /// system, so they are useful known-valid reference poses.
    /// </summary>
    private void DrawScoringPoseChecks()
    {
        if (fieldMap.reefScoringNodes == null)
        {
            return;
        }


        foreach (
            FieldMap2D.ReefScoringNode node
            in fieldMap.reefScoringNodes)
        {
            bool navigable =
                IsPointNavigable(
                    node.positionXZ
                );


            if (navigable)
            {
                Gizmos.color =
                    Color.green;
            }
            else
            {
                Gizmos.color =
                    Color.red;
            }


            Vector3 position =
                new Vector3(
                    node.positionXZ.x,
                    0.42f,
                    node.positionXZ.y
                );


            Gizmos.DrawWireSphere(
                position,
                0.16f
            );
        }
    }


    // ============================================================
    // EXPAND POLYGON FROM CENTER
    // ============================================================

    /// <summary>
    /// Used only for debug visualization.
    ///
    /// Actual Reef collision checking uses distance from the
    /// robot-center point to the physical Reef polygon.
    /// </summary>
    private List<Vector2> ExpandPolygonFromCenter(
        List<Vector2> polygon,
        float expansion)
    {
        List<Vector2> expanded =
            new List<Vector2>();


        Vector2 center =
            GetPolygonCenter(
                polygon
            );


        foreach (Vector2 vertex in polygon)
        {
            Vector2 direction =
                vertex - center;


            if (direction.sqrMagnitude <=
                Mathf.Epsilon)
            {
                expanded.Add(vertex);
                continue;
            }


            direction.Normalize();


            expanded.Add(
                vertex +
                direction * expansion
            );
        }


        return expanded;
    }


    // ============================================================
    // POLYGON CENTER
    // ============================================================

    private Vector2 GetPolygonCenter(
        List<Vector2> vertices)
    {
        Vector2 center =
            Vector2.zero;


        if (vertices == null ||
            vertices.Count == 0)
        {
            return center;
        }


        foreach (Vector2 vertex in vertices)
        {
            center += vertex;
        }


        center /=
            vertices.Count;


        return center;
    }


    // ============================================================
    // DRAW POLYGON
    // ============================================================

    private void DrawPolygonXZ(
        List<Vector2> vertices,
        float height)
    {
        if (vertices == null ||
            vertices.Count < 2)
        {
            return;
        }


        for (int i = 0;
             i < vertices.Count;
             i++)
        {
            Vector2 start =
                vertices[i];

            Vector2 end =
                vertices[
                    (i + 1) %
                    vertices.Count
                ];


            DrawLineXZ(
                start,
                end,
                height
            );
        }
    }


    // ============================================================
    // DRAW X-Z LINE
    // ============================================================

    private void DrawLineXZ(
        Vector2 start,
        Vector2 end,
        float height)
    {
        Gizmos.DrawLine(
            new Vector3(
                start.x,
                height,
                start.y
            ),

            new Vector3(
                end.x,
                height,
                end.y
            )
        );
    }
}