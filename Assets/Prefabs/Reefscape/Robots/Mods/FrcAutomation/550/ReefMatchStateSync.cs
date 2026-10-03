using System.Collections.Generic;
using Games.Reefscape.Enums;
using Games.Reefscape.Scoring;
using Games.Reefscape.Scoring.Scorers;
using MoSimCore.Enums;
using UnityEngine;

public class ReefMatchStateSync : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField]
    private FieldMap2D fieldMap;

    [SerializeField]
    private MatchState matchState;


    // ============================================================
    // SETTINGS
    // ============================================================

    [Header("Synchronization")]
    [SerializeField]
    private float updateIntervalSeconds = 0.10f;


    [Header("Debug")]
    [SerializeField]
    private bool logMappingOnStart = true;


    // ============================================================
    // INTERNAL MAPPING
    // ============================================================

    private class PoleMapping
    {
        public ReefPoleScorer scorer;

        public int faceNumber;

        public MatchState.ReefScoringTarget target;

        public FieldMap2D.ReefScoringNode scoringNode;

        public float mappingDistance;
    }


    private readonly List<PoleMapping> poleMappings =
        new List<PoleMapping>();


    private TroughScorer troughScorer;

    private float nextUpdateTime;


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        FindReferences();

        BuildPoleMappings();

        FindTroughScorer();

        SyncReefState();
    }


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (Time.time < nextUpdateTime)
        {
            return;
        }


        nextUpdateTime =
            Time.time + updateIntervalSeconds;


        SyncReefState();
    }


    // ============================================================
    // FIND REFERENCES
    // ============================================================

    private void FindReferences()
    {
        if (fieldMap == null)
        {
            fieldMap =
                FindFirstObjectByType<FieldMap2D>();
        }


        if (matchState == null)
        {
            matchState =
                FindFirstObjectByType<MatchState>();
        }


        if (fieldMap == null)
        {
            Debug.LogError(
                "ReefMatchStateSync could not find FieldMap2D."
            );
        }


        if (matchState == null)
        {
            Debug.LogError(
                "ReefMatchStateSync could not find MatchState."
            );
        }
    }


    // ============================================================
    // BUILD POLE MAPPINGS
    // ============================================================

    private void BuildPoleMappings()
    {
        poleMappings.Clear();


        if (fieldMap == null ||
            matchState == null)
        {
            return;
        }


        ReefPoleScorer[] allScorers =
            FindObjectsByType<ReefPoleScorer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );


        FieldMap2D.ReefAlliance desiredAlliance =
            matchState.alliance;


        foreach (ReefPoleScorer scorer in allScorers)
        {
            if (scorer == null)
            {
                continue;
            }


            if (!IsCorrectAlliance(
                    scorer,
                    desiredAlliance))
            {
                continue;
            }


            FieldMap2D.ReefScoringNode nearestNode =
                FindNearestScoringNode(
                    scorer,
                    desiredAlliance,
                    out float distance
                );


            if (nearestNode == null)
            {
                Debug.LogWarning(
                    $"Could not map " +
                    $"{scorer.gameObject.name} " +
                    "to a Reef scoring node."
                );

                continue;
            }


            MatchState.ReefScoringTarget target =
                nearestNode.branchSide ==
                FieldMap2D.ReefBranchSide.Left

                    ? MatchState.ReefScoringTarget.Left

                    : MatchState.ReefScoringTarget.Right;


            PoleMapping mapping =
                new PoleMapping
                {
                    scorer = scorer,

                    faceNumber =
                        nearestNode.faceNumber,

                    target = target,

                    scoringNode =
                        nearestNode,

                    mappingDistance =
                        distance
                };


            poleMappings.Add(mapping);
        }


        if (logMappingOnStart)
        {
            PrintMappings();
        }


        if (poleMappings.Count != 12)
        {
            Debug.LogWarning(
                $"ReefMatchStateSync mapped " +
                $"{poleMappings.Count} poles. " +
                "Expected 12 for one alliance Reef."
            );
        }
    }


    // ============================================================
    // FIND L1 TROUGH SCORER
    // ============================================================

    private void FindTroughScorer()
    {
        troughScorer = null;


        if (matchState == null)
        {
            return;
        }


        TroughScorer[] allTroughScorers =
            FindObjectsByType<TroughScorer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );


        foreach (
            TroughScorer scorer
            in allTroughScorers)
        {
            if (scorer == null)
            {
                continue;
            }


            bool correctAlliance = false;


            if (matchState.alliance ==
                FieldMap2D.ReefAlliance.Blue)
            {
                correctAlliance =
                    scorer.Alliance ==
                    Alliance.Blue;
            }
            else
            {
                correctAlliance =
                    scorer.Alliance ==
                    Alliance.Red;
            }


            if (!correctAlliance)
            {
                continue;
            }


            troughScorer =
                scorer;

            break;
        }


        if (troughScorer == null)
        {
            Debug.LogWarning(
                "ReefMatchStateSync could not find " +
                "the alliance TroughScorer."
            );
        }
        else if (logMappingOnStart)
        {
            Debug.Log(
                $"ReefMatchStateSync found " +
                $"{matchState.alliance} " +
                $"TroughScorer: " +
                $"{troughScorer.gameObject.name}"
            );
        }
    }


    // ============================================================
    // ALLIANCE CHECK
    // ============================================================

    private bool IsCorrectAlliance(
        ReefPoleScorer scorer,
        FieldMap2D.ReefAlliance desiredAlliance)
    {
        if (desiredAlliance ==
            FieldMap2D.ReefAlliance.Blue)
        {
            return scorer.Alliance ==
                   Alliance.Blue;
        }


        return scorer.Alliance ==
               Alliance.Red;
    }


    // ============================================================
    // FIND NEAREST FIELD MAP NODE
    // ============================================================

    private FieldMap2D.ReefScoringNode
        FindNearestScoringNode(
            ReefPoleScorer scorer,
            FieldMap2D.ReefAlliance alliance,
            out float bestDistance)
    {
        FieldMap2D.ReefScoringNode bestNode =
            null;


        bestDistance =
            float.PositiveInfinity;


        Vector2 polePosition =
            new Vector2(
                scorer.transform.position.x,
                scorer.transform.position.z
            );


        foreach (
            FieldMap2D.ReefScoringNode node
            in fieldMap.reefScoringNodes)
        {
            if (node == null)
            {
                continue;
            }


            if (node.alliance != alliance)
            {
                continue;
            }


            float distance =
                Vector2.Distance(
                    polePosition,
                    node.positionXZ
                );


            if (distance < bestDistance)
            {
                bestDistance =
                    distance;

                bestNode =
                    node;
            }
        }


        return bestNode;
    }


    // ============================================================
    // SYNCHRONIZE MATCH STATE
    // ============================================================

    private void SyncReefState()
    {
        if (matchState == null)
        {
            return;
        }


        // --------------------------------------------------------
        // L1
        // --------------------------------------------------------

        SyncL1();


        // --------------------------------------------------------
        // L2 / L3 / L4
        // --------------------------------------------------------

        foreach (PoleMapping mapping in poleMappings)
        {
            if (mapping == null ||
                mapping.scorer == null)
            {
                continue;
            }


            SyncLevel(
                mapping,
                ReefscapeBranchHeight.L2,
                MatchState.ReefLevel.L2
            );


            SyncLevel(
                mapping,
                ReefscapeBranchHeight.L3,
                MatchState.ReefLevel.L3
            );


            SyncLevel(
                mapping,
                ReefscapeBranchHeight.L4,
                MatchState.ReefLevel.L4
            );
        }
    }


    // ============================================================
    // SYNCHRONIZE L1
    // ============================================================

    private void SyncL1()
    {
        if (troughScorer == null)
        {
            return;
        }


        int currentCount =
            troughScorer.GetScoredCoralCount();


        int previousCount =
            matchState.GetL1CoralCount();


        if (currentCount ==
            previousCount)
        {
            return;
        }


        matchState.SetL1CoralCount(
            currentCount
        );


        Debug.Log(
            $"MatchState updated: " +
            $"L1 Coral Count " +
            $"{previousCount} -> " +
            $"{currentCount}"
        );
    }


    // ============================================================
    // SYNCHRONIZE ONE BRANCH LEVEL
    // ============================================================

    private void SyncLevel(
        PoleMapping mapping,
        ReefscapeBranchHeight scorerLevel,
        MatchState.ReefLevel matchLevel)
    {
        bool scored =
            mapping.scorer.IsLevelScored(
                scorerLevel
            );


        MatchState.ReefSlotState slot =
            matchState.GetReefSlot(
                mapping.faceNumber,
                mapping.target,
                matchLevel
            );


        if (slot == null)
        {
            Debug.LogWarning(
                $"Could not find MatchState slot: " +
                $"Face {mapping.faceNumber}, " +
                $"{mapping.target}, " +
                $"{matchLevel}"
            );

            return;
        }


        if (slot.occupied ==
            scored)
        {
            return;
        }


        if (scored)
        {
            matchState.MarkReefSlotOccupied(
                mapping.faceNumber,
                mapping.target,
                matchLevel
            );
        }
        else
        {
            matchState.MarkReefSlotEmpty(
                mapping.faceNumber,
                mapping.target,
                matchLevel
            );
        }


        Debug.Log(
            $"MatchState updated: " +
            $"Face {mapping.faceNumber} " +
            $"{mapping.target} " +
            $"{matchLevel} -> " +
            $"{(scored ? "OCCUPIED" : "EMPTY")}"
        );
    }


    // ============================================================
    // DEBUG MAPPING
    // ============================================================

    [ContextMenu("Print Reef Pole Mapping")]
    public void PrintMappings()
    {
        Debug.Log(
            "==================================================\n" +
            "REEF MATCH STATE MAPPING\n" +
            "=================================================="
        );


        foreach (
            PoleMapping mapping
            in poleMappings)
        {
            if (mapping == null ||
                mapping.scorer == null ||
                mapping.scoringNode == null)
            {
                continue;
            }


            Vector3 polePosition =
                mapping.scorer.transform.position;


            Debug.Log(
                $"{mapping.scorer.gameObject.name} | " +
                $"XZ = " +
                $"({polePosition.x:F4}, " +
                $"{polePosition.z:F4}) | " +
                $"Face {mapping.faceNumber} | " +
                $"{mapping.target} | " +
                $"Node = " +
                $"{mapping.scoringNode.name} | " +
                $"Distance = " +
                $"{mapping.mappingDistance:F4} m"
            );
        }


        Debug.Log(
            "=================================================="
        );
    }
}