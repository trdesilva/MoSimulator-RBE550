using System.Collections.Generic;
using System.Text;
using Games.Reefscape.Enums;
using Games.Reefscape.Scoring.Scorers;
using UnityEngine;

public class ReefScoringStateDebug : MonoBehaviour
{
    [Header("Debug Settings")]
    public float printIntervalSeconds = 1.0f;

    private List<ReefPoleScorer> reefPoleScorers =
        new List<ReefPoleScorer>();

    private float nextPrintTime;


    private void Start()
    {
        FindReefPoleScorers();

        PrintScoringState();
    }


    private void Update()
    {
        if (Time.time < nextPrintTime)
        {
            return;
        }

        nextPrintTime =
            Time.time + printIntervalSeconds;

        PrintScoringState();
    }


    // ============================================================
    // FIND REEF POLE SCORERS
    // ============================================================

    private void FindReefPoleScorers()
    {
        reefPoleScorers.Clear();

        GameObject[] allObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        int reefPoleObjectsFound = 0;
        int scorerComponentsFound = 0;


        foreach (GameObject obj in allObjects)
        {
            if (obj == null)
            {
                continue;
            }

            // Ignore the parent container called "ReefPoles".
            if (!obj.name.StartsWith("ReefPole") ||
                obj.name == "ReefPoles")
            {
                continue;
            }

            reefPoleObjectsFound++;


            ReefPoleScorer scorer =
                obj.GetComponent<ReefPoleScorer>();


            if (scorer == null)
            {
                continue;
            }


            reefPoleScorers.Add(scorer);

            scorerComponentsFound++;
        }


        Debug.Log(
            "==================================================\n" +
            "REEF SCORING DEBUG INITIALIZATION\n" +
            "==================================================\n" +
            $"ReefPole GameObjects found: {reefPoleObjectsFound}\n" +
            $"ReefPoleScorer components found: {scorerComponentsFound}\n" +
            "=================================================="
        );
    }


    // ============================================================
    // PRINT SCORING STATE
    // ============================================================

    [ContextMenu("Print Reef Scoring State")]
    public void PrintScoringState()
    {
        if (reefPoleScorers == null ||
            reefPoleScorers.Count == 0)
        {
            FindReefPoleScorers();
        }


        StringBuilder output =
            new StringBuilder();


        output.AppendLine(
            "=================================================="
        );

        output.AppendLine(
            "REEF SCORING STATE"
        );

        output.AppendLine(
            "=================================================="
        );


        if (reefPoleScorers.Count == 0)
        {
            output.AppendLine(
                "No ReefPoleScorer components found."
            );
        }


        foreach (ReefPoleScorer pole in reefPoleScorers)
        {
            if (pole == null)
            {
                continue;
            }


            Vector3 position =
                pole.transform.position;


            bool l2Scored =
                pole.IsLevelScored(
                    ReefscapeBranchHeight.L2
                );


            bool l3Scored =
                pole.IsLevelScored(
                    ReefscapeBranchHeight.L3
                );


            bool l4Scored =
                pole.IsLevelScored(
                    ReefscapeBranchHeight.L4
                );


            output.AppendLine(
                $"{pole.Alliance,-5} | " +
                $"{pole.gameObject.name,-15} | " +
                $"XZ = ({position.x:F4}, {position.z:F4}) | " +
                $"L2: {l2Scored,-5} | " +
                $"L3: {l3Scored,-5} | " +
                $"L4: {l4Scored,-5}"
            );
        }


        output.AppendLine(
            "=================================================="
        );


        Debug.Log(
            output.ToString()
        );
    }
}