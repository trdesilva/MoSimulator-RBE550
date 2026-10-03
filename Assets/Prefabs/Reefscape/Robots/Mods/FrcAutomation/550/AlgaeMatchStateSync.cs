using Games.Reefscape.Scoring.Scorers;
using MoSimCore.Enums;
using UnityEngine;

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

        SyncProcessor();
        SyncNet();
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