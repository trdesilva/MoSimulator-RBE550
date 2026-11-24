using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class FieldMap2D : MonoBehaviour
{
    // ============================================================
    // DATA TYPES
    // ============================================================

    [System.Serializable]
    public class NavigationObstacle
    {
        public string name;

        // Planner coordinates:
        // Vector2.x = Unity X
        // Vector2.y = Unity Z
        public List<Vector2> vertices;

        public NavigationObstacle(
            string name,
            List<Vector2> vertices)
        {
            this.name = name;
            this.vertices = vertices;
        }
    }


    public enum TaskType
    {
        Reef,
        CoralStation,
        Processor,
        Net,
        Cage
    }


    public enum ReefAlliance
    {
        Red,
        Blue
    }


    public enum ReefBranchSide
    {
        Left,
        Right
    }


    [System.Serializable]
    public class TaskLocation
    {
        public string name;
        public TaskType type;

        // Planner X-Z position.
        public Vector2 positionXZ;

        // Desired robot rotation about Unity Y.
        public float headingDegrees;

        public TaskLocation(
            string name,
            TaskType type,
            Vector2 positionXZ,
            float headingDegrees = 0.0f)
        {
            this.name = name;
            this.type = type;
            this.positionXZ = positionXZ;
            this.headingDegrees = headingDegrees;
        }
    }


    [System.Serializable]
    public class TaskRegion
    {
        public string name;
        public TaskType type;

        // Polygon describing an X-Z task region.
        public List<Vector2> vertices;

        public TaskRegion(
            string name,
            TaskType type,
            List<Vector2> vertices)
        {
            this.name = name;
            this.type = type;
            this.vertices = vertices;
        }
    }


    [System.Serializable]
    public class ReefScoringNode
    {
        public string name;

        public ReefAlliance alliance;

        // Our own planner face numbering: 1 through 6.
        public int faceNumber;

        public ReefBranchSide branchSide;

        // Actual scoring/alignment pose extracted from MoSim.
        public Vector2 positionXZ;

        // Robot heading about Unity Y at this scoring pose.
        public float headingDegrees;


        public ReefScoringNode(
            string name,
            ReefAlliance alliance,
            int faceNumber,
            ReefBranchSide branchSide,
            Vector2 positionXZ,
            float headingDegrees)
        {
            this.name = name;
            this.alliance = alliance;
            this.faceNumber = faceNumber;
            this.branchSide = branchSide;
            this.positionXZ = positionXZ;
            this.headingDegrees = headingDegrees;
        }
    }


    // ============================================================
    // REEF L1 SCORING NODE
    // ============================================================

    [System.Serializable]
    public class ReefL1ScoringNode
    {
        public string name;
        public ReefAlliance alliance;
        public int faceNumber;
        public Vector2 positionXZ;
        public float headingDegrees;

        public ReefL1ScoringNode(
            string name,
            ReefAlliance alliance,
            int faceNumber,
            Vector2 positionXZ,
            float headingDegrees)
        {
            this.name = name;
            this.alliance = alliance;
            this.faceNumber = faceNumber;
            this.positionXZ = positionXZ;
            this.headingDegrees = headingDegrees;
        }
    }


    // ============================================================
    // FIELD BOUNDARY
    // ============================================================

    public List<Vector2> fieldBoundary =
        new List<Vector2>();


    // ============================================================
    // CAGE SETTINGS
    // ============================================================

    public float cagePhysicalSize = 0.20f;

    // Clearance on EACH side of cage.
    public float cageSafetyMargin = 0.0125f;


    // ============================================================
    // FIELD MAP LAYERS
    // ============================================================

    // Physical obstacles.
    public List<NavigationObstacle> navigationObstacles =
        new List<NavigationObstacle>();


    // Generic point-based tasks.
    public List<TaskLocation> taskLocations =
        new List<TaskLocation>();


    // Area-based tasks such as the Barge scoring regions.
    public List<TaskRegion> taskRegions =
        new List<TaskRegion>();


    // Exact left/right Reef scoring poses.
    public List<ReefScoringNode> reefScoringNodes =
        new List<ReefScoringNode>();


    // Face-level L1 Reef scoring poses.
    public List<ReefL1ScoringNode> reefL1ScoringNodes =
        new List<ReefL1ScoringNode>();


    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void OnEnable()
    {
        BuildFieldMap();
    }


    private void OnValidate()
    {
        BuildFieldMap();
    }


    private void BuildFieldMap()
    {
        BuildFieldBoundary();
        BuildNavigationLayer();
        BuildTaskLayer();
    }


    // ============================================================
    // FIELD BOUNDARY
    // ============================================================

    private void BuildFieldBoundary()
    {
        fieldBoundary.Clear();


        fieldBoundary.Add(
            new Vector2(-7.12f, 4.03f)
        );

        fieldBoundary.Add(
            new Vector2(7.07f, 4.03f)
        );

        fieldBoundary.Add(
            new Vector2(8.76f, 2.77f)
        );

        fieldBoundary.Add(
            new Vector2(8.76f, -2.76f)
        );

        fieldBoundary.Add(
            new Vector2(7.10f, -4.03f)
        );

        fieldBoundary.Add(
            new Vector2(-7.13f, -4.03f)
        );

        fieldBoundary.Add(
            new Vector2(-8.77f, -2.76f)
        );

        fieldBoundary.Add(
            new Vector2(-8.77f, 2.77f)
        );
    }


    // ============================================================
    // NAVIGATION LAYER
    // ============================================================

    private void BuildNavigationLayer()
    {
        navigationObstacles.Clear();

        AddReefs();
        AddCages();

        // No additional Barge obstacle is added.
        //
        // Our Barge geometry inspection found no additional
        // low structural colliders within the normal driving
        // height after the cages were excluded.
    }


    // ============================================================
    // REEF NAVIGATION GEOMETRY
    // ============================================================

    private void AddReefs()
    {
        Vector2[] reefFootprint =
        {
            new Vector2(-0.835f, -0.476f),
            new Vector2( 0.000f, -0.955f),
            new Vector2( 0.835f, -0.476f),
            new Vector2( 0.835f,  0.476f),
            new Vector2( 0.000f,  0.955f),
            new Vector2(-0.835f,  0.476f)
        };


        AddTranslatedObstacle(
            "Red Reef",
            reefFootprint,
            new Vector2(
                -4.298872f,
                0.0f
            )
        );


        AddTranslatedObstacle(
            "Blue Reef",
            reefFootprint,
            new Vector2(
                4.298872f,
                0.0f
            )
        );
    }


    // ============================================================
    // CAGE NAVIGATION GEOMETRY
    // ============================================================

    private void AddCages()
    {
        Vector2[] cageCenters =
        {
            new Vector2(0.009f, -3.246f),
            new Vector2(0.009f, -2.102f),
            new Vector2(0.009f, -1.050f),

            new Vector2(0.009f,  1.064f),
            new Vector2(0.009f,  2.123f),
            new Vector2(0.009f,  3.218f)
        };


        float halfBlockedSize =
            cagePhysicalSize * 0.5f +
            cageSafetyMargin;


        for (int i = 0; i < cageCenters.Length; i++)
        {
            Vector2 center =
                cageCenters[i];


            List<Vector2> footprint =
                new List<Vector2>
                {
                    new Vector2(
                        center.x - halfBlockedSize,
                        center.y - halfBlockedSize
                    ),

                    new Vector2(
                        center.x + halfBlockedSize,
                        center.y - halfBlockedSize
                    ),

                    new Vector2(
                        center.x + halfBlockedSize,
                        center.y + halfBlockedSize
                    ),

                    new Vector2(
                        center.x - halfBlockedSize,
                        center.y + halfBlockedSize
                    )
                };


            navigationObstacles.Add(
                new NavigationObstacle(
                    $"Cage {i + 1}",
                    footprint
                )
            );
        }
    }


    // ============================================================
    // TASK LAYER
    // ============================================================

    private void BuildTaskLayer()
    {
        taskLocations.Clear();
        taskRegions.Clear();
        reefScoringNodes.Clear();
        reefL1ScoringNodes.Clear();

        AddReefScoringNodes();
        AddReefL1ScoringNodes();
        AddCoralStationTasks();
        AddProcessorTasks();
        AddBargeTaskRegions();
        AddCageTasks();
    }


    // ============================================================
    // REEF SCORING NODES
    // ============================================================

    private void AddReefScoringNodes()
    {
        // ========================================================
        // BLUE REEF
        // ========================================================
        //
        // Face numbering is OUR planner numbering.
        //
        // Face 1: +X side
        // Face 2: +X, +Z diagonal
        // Face 3: -X, +Z diagonal
        // Face 4: -X side
        // Face 5: -X, -Z diagonal
        // Face 6: +X, -Z diagonal
        //
        // Left / Right are preserved exactly from MoSim's
        // AlignNode LeftNode and RightNode references.


        // ---------------- FACE 1 ----------------
        // MoSim AutoAlignFace (3)

        AddReefNode(
            ReefAlliance.Blue,
            1,
            ReefBranchSide.Left,
            5.8239f,
            -0.1700f,
            -90.0f
        );

        AddReefNode(
            ReefAlliance.Blue,
            1,
            ReefBranchSide.Right,
            5.8239f,
            0.1800f,
            -90.0f
        );


        // ---------------- FACE 2 ----------------
        // MoSim AutoAlignFace (4)

        AddReefNode(
            ReefAlliance.Blue,
            2,
            ReefBranchSide.Left,
            5.2086f,
            1.2357f,
            -150.0f
        );

        AddReefNode(
            ReefAlliance.Blue,
            2,
            ReefBranchSide.Right,
            4.9055f,
            1.4107f,
            -150.0f
        );


        // ---------------- FACE 3 ----------------
        // MoSim AutoAlignFace (5)

        AddReefNode(
            ReefAlliance.Blue,
            3,
            ReefBranchSide.Left,
            3.6836f,
            1.4057f,
            150.0f
        );

        AddReefNode(
            ReefAlliance.Blue,
            3,
            ReefBranchSide.Right,
            3.3805f,
            1.2307f,
            150.0f
        );


        // ---------------- FACE 4 ----------------
        // MoSim AutoAlignFace

        AddReefNode(
            ReefAlliance.Blue,
            4,
            ReefBranchSide.Left,
            2.7739f,
            0.1700f,
            90.0f
        );

        AddReefNode(
            ReefAlliance.Blue,
            4,
            ReefBranchSide.Right,
            2.7739f,
            -0.1800f,
            90.0f
        );


        // ---------------- FACE 5 ----------------
        // MoSim AutoAlignFace (1)

        AddReefNode(
            ReefAlliance.Blue,
            5,
            ReefBranchSide.Left,
            3.3891f,
            -1.2357f,
            30.0f
        );

        AddReefNode(
            ReefAlliance.Blue,
            5,
            ReefBranchSide.Right,
            3.6923f,
            -1.4107f,
            30.0f
        );


        // ---------------- FACE 6 ----------------
        // MoSim AutoAlignFace (2)

        AddReefNode(
            ReefAlliance.Blue,
            6,
            ReefBranchSide.Left,
            4.9141f,
            -1.4057f,
            -30.0f
        );

        AddReefNode(
            ReefAlliance.Blue,
            6,
            ReefBranchSide.Right,
            5.2173f,
            -1.2307f,
            -30.0f
        );


        // ========================================================
        // RED REEF
        // ========================================================
        //
        // Same geometric numbering convention.


        // ---------------- FACE 1 ----------------
        // +X side
        // MoSim AutoAlignFace

        AddReefNode(
            ReefAlliance.Red,
            1,
            ReefBranchSide.Left,
            -2.7739f,
            -0.1700f,
            -90.0f
        );

        AddReefNode(
            ReefAlliance.Red,
            1,
            ReefBranchSide.Right,
            -2.7739f,
            0.1800f,
            -90.0f
        );


        // ---------------- FACE 2 ----------------
        // +X, +Z diagonal
        // MoSim AutoAlignFace (1)

        AddReefNode(
            ReefAlliance.Red,
            2,
            ReefBranchSide.Left,
            -3.3891f,
            1.2357f,
            -150.0f
        );

        AddReefNode(
            ReefAlliance.Red,
            2,
            ReefBranchSide.Right,
            -3.6923f,
            1.4107f,
            -150.0f
        );


        // ---------------- FACE 3 ----------------
        // -X, +Z diagonal
        // MoSim AutoAlignFace (2)

        AddReefNode(
            ReefAlliance.Red,
            3,
            ReefBranchSide.Left,
            -4.9141f,
            1.4057f,
            150.0f
        );

        AddReefNode(
            ReefAlliance.Red,
            3,
            ReefBranchSide.Right,
            -5.2173f,
            1.2307f,
            150.0f
        );


        // ---------------- FACE 4 ----------------
        // -X side
        // MoSim AutoAlignFace (3)

        AddReefNode(
            ReefAlliance.Red,
            4,
            ReefBranchSide.Left,
            -5.8239f,
            0.1700f,
            90.0f
        );

        AddReefNode(
            ReefAlliance.Red,
            4,
            ReefBranchSide.Right,
            -5.8239f,
            -0.1800f,
            90.0f
        );


        // ---------------- FACE 5 ----------------
        // -X, -Z diagonal
        // MoSim AutoAlignFace (4)

        AddReefNode(
            ReefAlliance.Red,
            5,
            ReefBranchSide.Left,
            -5.2086f,
            -1.2357f,
            30.0f
        );

        AddReefNode(
            ReefAlliance.Red,
            5,
            ReefBranchSide.Right,
            -4.9055f,
            -1.4107f,
            30.0f
        );


        // ---------------- FACE 6 ----------------
        // +X, -Z diagonal
        // MoSim AutoAlignFace (5)

        AddReefNode(
            ReefAlliance.Red,
            6,
            ReefBranchSide.Left,
            -3.6836f,
            -1.4057f,
            -30.0f
        );

        AddReefNode(
            ReefAlliance.Red,
            6,
            ReefBranchSide.Right,
            -3.3805f,
            -1.2307f,
            -30.0f
        );
    }


    // ============================================================
    // REEF L1 SCORING NODES
    // ============================================================

    private void AddReefL1ScoringNodes()
    {
        ReefAlliance[] alliances =
        {
            ReefAlliance.Blue,
            ReefAlliance.Red
        };

        foreach (ReefAlliance alliance in alliances)
        {
            for (int face = 1; face <= 6; face++)
            {
                ReefScoringNode leftNode =
                    GetReefScoringNode(alliance, face, ReefBranchSide.Left);

                ReefScoringNode rightNode =
                    GetReefScoringNode(alliance, face, ReefBranchSide.Right);

                if (leftNode == null || rightNode == null)
                {
                    Debug.LogWarning(
                        $"Could not create L1 node for {alliance} Reef Face {face}: Left or Right node is missing."
                    );
                    continue;
                }

                Vector2 l1Position =
                    (leftNode.positionXZ + rightNode.positionXZ) * 0.5f;

                float l1Heading =
                    leftNode.headingDegrees;

                string nodeName =
                    $"{alliance} Reef Face {face} L1";

                reefL1ScoringNodes.Add(
                    new ReefL1ScoringNode(
                        nodeName,
                        alliance,
                        face,
                        l1Position,
                        l1Heading
                    )
                );
            }
        }
    }


    // ============================================================
    // ADD ONE REEF NODE
    // ============================================================

    private void AddReefNode(
        ReefAlliance alliance,
        int faceNumber,
        ReefBranchSide branchSide,
        float x,
        float z,
        float headingDegrees)
    {
        string nodeName =
            $"{alliance} Reef Face {faceNumber} {branchSide}";


        reefScoringNodes.Add(
            new ReefScoringNode(
                nodeName,
                alliance,
                faceNumber,
                branchSide,
                new Vector2(x, z),
                headingDegrees
            )
        );
    }


    // ============================================================
    // GET A REEF SCORING NODE
    // ============================================================

    public ReefScoringNode GetReefScoringNode(
        ReefAlliance alliance,
        int faceNumber,
        ReefBranchSide branchSide)
    {
        foreach (ReefScoringNode node in reefScoringNodes)
        {
            if (node.alliance == alliance &&
                node.faceNumber == faceNumber &&
                node.branchSide == branchSide)
            {
                return node;
            }
        }


        return null;
    }


    // ============================================================
    // GET A REEF L1 SCORING NODE
    // ============================================================

    public ReefL1ScoringNode GetReefL1ScoringNode(
        ReefAlliance alliance,
        int faceNumber)
    {
        foreach (ReefL1ScoringNode node in reefL1ScoringNodes)
        {
            if (node.alliance == alliance &&
                node.faceNumber == faceNumber)
            {
                return node;
            }
        }

        return null;
    }


    // ============================================================
    // CORAL STATION TASKS
    // ============================================================

    private void AddCoralStationTasks()
    {
        taskLocations.Add(
            new TaskLocation(
                "Blue Coral Station +Z",
                TaskType.CoralStation,
                new Vector2(
                    7.927174f,
                    3.376354f
                ),
                36.0f
            )
        );


        taskLocations.Add(
            new TaskLocation(
                "Blue Coral Station -Z",
                TaskType.CoralStation,
                new Vector2(
                    7.927174f,
                    -3.376354f
                ),
                144.0f
            )
        );


        taskLocations.Add(
            new TaskLocation(
                "Red Coral Station +Z",
                TaskType.CoralStation,
                new Vector2(
                    -7.927174f,
                    3.376354f
                ),
                -36.0f
            )
        );


        taskLocations.Add(
            new TaskLocation(
                "Red Coral Station -Z",
                TaskType.CoralStation,
                new Vector2(
                    -7.927174f,
                    -3.376354f
                ),
                -144.0f
            )
        );
    }


    // ============================================================
    // PROCESSOR TASKS
    // ============================================================

    private void AddProcessorTasks()
    {
        taskLocations.Add(
            new TaskLocation(
                "Processor -X -Z",
                TaskType.Processor,
                new Vector2(
                    -2.7865f,
                    -4.037f
                )
            )
        );


        taskLocations.Add(
            new TaskLocation(
                "Processor +X +Z",
                TaskType.Processor,
                new Vector2(
                    2.7865f,
                    4.037f
                )
            )
        );
    }


    // ============================================================
    // BARGE TASK REGIONS
    // ============================================================

    private void AddBargeTaskRegions()
    {
        // BlueScoring collider:
        //
        // X: -0.5000 -> +0.5000
        // Z: -3.8210 -> -0.1530
        // Y: 1.9008 -> 2.4655

        taskRegions.Add(
            new TaskRegion(
                "Blue Barge Scoring Region",
                TaskType.Net,
                new List<Vector2>
                {
                    new Vector2(-0.5000f, -3.8210f),
                    new Vector2( 0.5000f, -3.8210f),
                    new Vector2( 0.5000f, -0.1530f),
                    new Vector2(-0.5000f, -0.1530f)
                }
            )
        );


        // RedScoring collider:
        //
        // X: -0.5000 -> +0.5000
        // Z: +0.1660 -> +3.8340
        // Y: 1.9008 -> 2.4655

        taskRegions.Add(
            new TaskRegion(
                "Red Barge Scoring Region",
                TaskType.Net,
                new List<Vector2>
                {
                    new Vector2(-0.5000f, 0.1660f),
                    new Vector2( 0.5000f, 0.1660f),
                    new Vector2( 0.5000f, 3.8340f),
                    new Vector2(-0.5000f, 3.8340f)
                }
            )
        );
    }


    // ============================================================
    // CAGE TASKS
    // ============================================================

    private void AddCageTasks()
    {
        Vector2[] cageCenters =
        {
            new Vector2(0.009f, -3.246f),
            new Vector2(0.009f, -2.102f),
            new Vector2(0.009f, -1.050f),

            new Vector2(0.009f,  1.064f),
            new Vector2(0.009f,  2.123f),
            new Vector2(0.009f,  3.218f)
        };


        for (int i = 0; i < cageCenters.Length; i++)
        {
            taskLocations.Add(
                new TaskLocation(
                    $"Cage {i + 1}",
                    TaskType.Cage,
                    cageCenters[i]
                )
            );
        }
    }


    // ============================================================
    // NAVIGATION HELPERS
    // ============================================================

    private void AddTranslatedObstacle(
        string obstacleName,
        Vector2[] localVertices,
        Vector2 positionXZ)
    {
        List<Vector2> worldVertices =
            new List<Vector2>();


        foreach (Vector2 vertex in localVertices)
        {
            worldVertices.Add(
                vertex + positionXZ
            );
        }


        navigationObstacles.Add(
            new NavigationObstacle(
                obstacleName,
                worldVertices
            )
        );
    }


    // ============================================================
    // FIELD BOUNDARY CHECK
    // ============================================================

    public bool IsInsideField(Vector2 point)
    {
        if (fieldBoundary == null ||
            fieldBoundary.Count < 3)
        {
            return false;
        }


        bool inside = false;


        for (
            int i = 0,
            j = fieldBoundary.Count - 1;

            i < fieldBoundary.Count;

            j = i++)
        {
            Vector2 pi =
                fieldBoundary[i];

            Vector2 pj =
                fieldBoundary[j];


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
    // GIZMO VISUALIZATION
    // ============================================================

    private void OnDrawGizmos()
    {
        DrawFieldBoundary();
        DrawNavigationLayer();
        DrawTaskLocations();
        DrawTaskRegions();
        DrawReefScoringNodes();
        DrawReefL1ScoringNodes();
    }


    // ============================================================
    // FIELD BOUNDARY
    // ============================================================

    private void DrawFieldBoundary()
    {
        if (fieldBoundary == null ||
            fieldBoundary.Count < 2)
        {
            return;
        }


        Gizmos.color =
            Color.cyan;


        DrawPolygon(
            fieldBoundary,
            0.17f
        );
    }


    // ============================================================
    // NAVIGATION OBSTACLES
    // ============================================================

    private void DrawNavigationLayer()
    {
        if (navigationObstacles == null)
        {
            return;
        }


        Gizmos.color =
            Color.yellow;


        foreach (
            NavigationObstacle obstacle
            in navigationObstacles)
        {
            if (obstacle.vertices == null ||
                obstacle.vertices.Count < 2)
            {
                continue;
            }


            DrawPolygon(
                obstacle.vertices,
                0.15f
            );
        }
    }


    // ============================================================
    // GENERIC TASK LOCATIONS
    // ============================================================

    private void DrawTaskLocations()
    {
        if (taskLocations == null)
        {
            return;
        }


        Gizmos.color =
            Color.green;


        foreach (
            TaskLocation task
            in taskLocations)
        {
            Vector3 position =
                new Vector3(
                    task.positionXZ.x,
                    0.20f,
                    task.positionXZ.y
                );


            Gizmos.DrawWireSphere(
                position,
                0.12f
            );


            if (Mathf.Abs(task.headingDegrees) >
                0.001f)
            {
                DrawHeading(
                    position,
                    task.headingDegrees,
                    0.50f
                );
            }
        }
    }


    // ============================================================
    // TASK REGIONS
    // ============================================================

    private void DrawTaskRegions()
    {
        if (taskRegions == null)
        {
            return;
        }


        Gizmos.color =
            Color.magenta;


        foreach (
            TaskRegion region
            in taskRegions)
        {
            if (region.vertices == null ||
                region.vertices.Count < 2)
            {
                continue;
            }


            DrawPolygon(
                region.vertices,
                0.25f
            );
        }
    }


    // ============================================================
    // REEF SCORING NODES
    // ============================================================

    private void DrawReefScoringNodes()
    {
        if (reefScoringNodes == null)
        {
            return;
        }


        foreach (
            ReefScoringNode node
            in reefScoringNodes)
        {
            // Left branch = green
            // Right branch = blue

            if (node.branchSide ==
                ReefBranchSide.Left)
            {
                Gizmos.color =
                    Color.green;
            }
            else
            {
                Gizmos.color =
                    Color.blue;
            }


            Vector3 position =
                new Vector3(
                    node.positionXZ.x,
                    0.32f,
                    node.positionXZ.y
                );


            Gizmos.DrawWireSphere(
                position,
                0.10f
            );


            DrawHeading(
                position,
                node.headingDegrees,
                0.40f
            );
        }
    }


    // ============================================================
    // REEF L1 SCORING NODES
    // ============================================================

    private void DrawReefL1ScoringNodes()
    {
        if (reefL1ScoringNodes == null)
        {
            return;
        }

        Gizmos.color =
            Color.white;

        foreach (
            ReefL1ScoringNode node
            in reefL1ScoringNodes)
        {
            Vector3 position =
                new Vector3(
                    node.positionXZ.x,
                    0.36f,
                    node.positionXZ.y
                );

            Gizmos.DrawWireSphere(
                position,
                0.13f
            );

            DrawHeading(
                position,
                node.headingDegrees,
                0.45f
            );
        }
    }


    // ============================================================
    // DRAW HEADING
    // ============================================================

    private void DrawHeading(
        Vector3 position,
        float headingDegrees,
        float length)
    {
        float radians =
            headingDegrees *
            Mathf.Deg2Rad;


        Vector3 direction =
            new Vector3(
                Mathf.Sin(radians),
                0.0f,
                Mathf.Cos(radians)
            );


        Gizmos.DrawLine(
            position,
            position +
            direction * length
        );
    }


    // ============================================================
    // POLYGON DRAWING
    // ============================================================

    private void DrawPolygon(
        List<Vector2> vertices,
        float height)
    {
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


            Vector3 start3D =
                new Vector3(
                    start.x,
                    height,
                    start.y
                );


            Vector3 end3D =
                new Vector3(
                    end.x,
                    height,
                    end.y
                );


            Gizmos.DrawLine(
                start3D,
                end3D
            );
        }
    }
}