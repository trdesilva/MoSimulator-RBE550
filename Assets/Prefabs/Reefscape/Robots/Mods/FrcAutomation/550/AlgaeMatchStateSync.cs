using Games.Reefscape.Scoring.Scorers;
using MoSimCore.Enums;
using UnityEngine;

[DefaultExecutionOrder(900)]
public class AlgaeMatchStateSync : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MatchState matchState;

    [Header("Update Settings")]
    [SerializeField] private float updateInterval = 0.10f;

    [Header("Debug")]
    [SerializeField] private bool logScorersOnStart = true;

    private ProcessorScorer processorScorer;
    private BargeScorer bargeScorer;

    private float nextUpdateTime = 0.0f;

    // ============================================================
    // UNITY
    // ============================================================

    private void Start()
    {
        FindMatchState();
        FindProcessorScorer();
        FindBargeScorer();

        SyncAlgaeState();
    }

    private void Update()
    {
        if (Time.time < nextUpdateTime)
        {
            return;
        }

        nextUpdateTime = Time.time + updateInterval;

        SyncAlgaeState();
    }

    // ============================================================
    // REFERENCES
    // ============================================================

    private void FindMatchState()
    {
        if (matchState != null)
        {
            return;
        }

        matchState = GetComponent<MatchState>();

        if (matchState == null)
        {
            matchState = FindFirstObjectByType<MatchState>();
        }

        if (matchState == null)
        {
            Debug.LogError(
                "AlgaeMatchStateSync could not find MatchState."
            );
        }
    }

    // ============================================================
    // PROCESSOR SCORER
    // ============================================================

    private void FindProcessorScorer()
    {
        if (matchState == null)
        {
            return;
        }

        ProcessorScorer[] scorers =
            FindObjectsByType<ProcessorScorer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        Alliance targetAlliance = GetMoSimAlliance();

        foreach (ProcessorScorer scorer in scorers)
        {
            if (scorer == null)
            {
                continue;
            }

            if (scorer.Alliance != targetAlliance)
            {
                continue;
            }

            processorScorer = scorer;

            if (logScorersOnStart)
            {
                Debug.Log(
                    $"AlgaeMatchStateSync: Found " +
                    $"{targetAlliance} ProcessorScorer " +
                    $"on {scorer.gameObject.name}"
                );
            }

            return;
        }

        Debug.LogWarning(
            $"AlgaeMatchStateSync could not find a " +
            $"{targetAlliance} ProcessorScorer."
        );
    }

    // ============================================================
    // BARGE / NET SCORER
    // ============================================================

    private void FindBargeScorer()
    {
        if (matchState == null)
        {
            return;
        }

        BargeScorer[] scorers =
            FindObjectsByType<BargeScorer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        Alliance targetAlliance = GetMoSimAlliance();

        foreach (BargeScorer scorer in scorers)
        {
            if (scorer == null)
            {
                continue;
            }

            if (scorer.Alliance != targetAlliance)
            {
                continue;
            }

            bargeScorer = scorer;

            if (logScorersOnStart)
            {
                Debug.Log(
                    $"AlgaeMatchStateSync: Found " +
                    $"{targetAlliance} BargeScorer " +
                    $"on {scorer.gameObject.name}"
                );
            }

            return;
        }

        Debug.LogWarning(
            $"AlgaeMatchStateSync could not find a " +
            $"{targetAlliance} BargeScorer."
        );
    }

    // ============================================================
    // ALLIANCE CONVERSION
    // ============================================================

    private Alliance GetMoSimAlliance()
    {
        if (matchState.alliance ==
            FieldMap2D.ReefAlliance.Blue)
        {
            return Alliance.Blue;
        }

        return Alliance.Red;
    }

    // ============================================================
    // SYNCHRONIZATION
    // ============================================================

    private void SyncAlgaeState()
    {
        if (matchState == null)
        {
            return;
        }

        SyncReefAlgae();
        SyncProcessor();
        SyncNet();
    }

    // Detect elevated Algae near the original Reef face centers.
    public static GameObject FindFaceAlgae(FieldMap2D.ReefL1ScoringNode node)
    {
        if (node == null) return null;
        return FindFaceAlgae(node, GameObject.FindGameObjectsWithTag("Algae"));
    }

    private static GameObject FindFaceAlgae(FieldMap2D.ReefL1ScoringNode node, GameObject[] pieces)
    {
        float radians = node.headingDegrees * Mathf.Deg2Rad;
        Vector2 inward = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        Vector2 expected = node.positionXZ + inward * 0.84f;
        GameObject best = null;
        float distance = 0.18f;
        foreach (var piece in pieces)
        {
            if (piece.GetComponentInParent<RobotFramework.RobotBase>() != null) continue;
            Vector3 pos = piece.transform.position;
            if (Mathf.Abs(pos.y - 0.933f) > 0.12f && Mathf.Abs(pos.y - 1.319f) > 0.12f) continue;
            float d = Vector2.Distance(expected, new Vector2(pos.x, pos.z));
            if (d < distance) { best = piece; distance = d; }
        }
        return best;
    }

    private void SyncReefAlgae()
    {
        var map = matchState.GetComponent<FieldMap2D>();
        if (map == null) return;
        var pieces = GameObject.FindGameObjectsWithTag("Algae");
        int mapped = 0;
        foreach (var node in map.reefL1ScoringNodes)
        {
            if (node.alliance != matchState.alliance) continue;
            var piece = FindFaceAlgae(node, pieces);
            var face = matchState.GetReefFace(node.faceNumber);
            if (face == null) continue;
            bool changed = face.algaePresent != (piece != null);
            face.algaePiece = piece;
            face.algaeHigh = piece != null && piece.transform.position.y > 1.126f;
            matchState.SetReefAlgaePresent(node.faceNumber, piece != null);
            mapped++;
            if (changed) Debug.Log($"MatchState: {node.alliance} Reef Face {node.faceNumber} Algae {(piece != null ? "present" : "removed")}; remaining={matchState.GetReefAlgaeRemaining()}");
        }
        matchState.allianceState.reefAlgaeInitialized = mapped == 6;
    }

    private void SyncProcessor()
    {
        if (processorScorer == null)
        {
            return;
        }

        int currentCount =
            processorScorer.GetScoredAlgaeCount();

        int previousCount =
            matchState.GetProcessorAlgaeScored();

        if (currentCount == previousCount)
        {
            return;
        }

        matchState.SetProcessorAlgaeScored(
            currentCount
        );

        Debug.Log(
            $"MatchState updated: Processor Algae " +
            $"{previousCount} -> {currentCount}"
        );
    }

    private void SyncNet()
    {
        if (bargeScorer == null)
        {
            return;
        }

        int currentCount =
            bargeScorer.GetScoredAlgaeCount();

        int previousCount =
            matchState.GetNetAlgaeScored();

        if (currentCount == previousCount)
        {
            return;
        }

        matchState.SetNetAlgaeScored(
            currentCount
        );

        Debug.Log(
            $"MatchState updated: Net Algae " +
            $"{previousCount} -> {currentCount}"
        );
    }
}