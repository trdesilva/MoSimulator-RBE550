using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class ReefPoleDebug : MonoBehaviour
{
    // ============================================================
    // SETTINGS
    // ============================================================

    [Header("Debug Settings")]
    public bool printOnStart = true;

    public bool drawGizmos = true;


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        if (printOnStart)
        {
            PrintReefPoles();
        }
    }


    // ============================================================
    // PRINT REEF POLES
    // ============================================================

    [ContextMenu("Print Reef Pole Positions")]
    public void PrintReefPoles()
    {
        GameObject[] allObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );


        List<GameObject> reefPoles =
            new List<GameObject>();


        foreach (GameObject obj in allObjects)
        {
            if (obj == null)
            {
                continue;
            }


            if (!obj.name.StartsWith("ReefPole"))
            {
                continue;
            }


            if (obj.name == "ReefPoles")
            {
                continue;
            }


            reefPoles.Add(obj);
        }


        // Sort first by Reef, then by object name.
        reefPoles.Sort(
            (a, b) =>
            {
                string reefA =
                    FindReefName(a.transform);

                string reefB =
                    FindReefName(b.transform);


                int reefComparison =
                    reefA.CompareTo(reefB);


                if (reefComparison != 0)
                {
                    return reefComparison;
                }


                return a.name.CompareTo(b.name);
            }
        );


        // Build ONE Console message.
        StringBuilder output =
            new StringBuilder();


        output.AppendLine(
            "=================================================="
        );

        output.AppendLine(
            "REEF POLE DEBUG"
        );

        output.AppendLine(
            $"Found {reefPoles.Count} ReefPole objects."
        );

        output.AppendLine(
            "=================================================="
        );


        string previousReef = "";


        foreach (GameObject pole in reefPoles)
        {
            Vector3 position =
                pole.transform.position;


            string reefName =
                FindReefName(pole.transform);


            // Add a heading whenever we switch Reef.
            if (reefName != previousReef)
            {
                output.AppendLine();
                output.AppendLine(
                    $"--- {reefName} ---"
                );

                previousReef =
                    reefName;
            }


            output.AppendLine(
                $"{pole.name,-15} | " +
                $"XZ = ({position.x:F4}, {position.z:F4}) | " +
                $"Y = {position.y:F4}"
            );
        }


        output.AppendLine();
        output.AppendLine(
            "=================================================="
        );


        // ONE Debug.Log = ONE Unity Console entry.
        Debug.Log(
            output.ToString()
        );
    }


    // ============================================================
    // FIND WHICH REEF THIS POLE BELONGS TO
    // ============================================================

    private string FindReefName(
        Transform current)
    {
        Transform parent =
            current;


        while (parent != null)
        {
            if (parent.name == "RedReef")
            {
                return "RedReef";
            }


            if (parent.name == "BlueReef")
            {
                return "BlueReef";
            }


            parent =
                parent.parent;
        }


        return "UnknownReef";
    }


    // ============================================================
    // GIZMO VISUALIZATION
    // ============================================================

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }


        GameObject[] allObjects =
            FindObjectsByType<GameObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );


        foreach (GameObject obj in allObjects)
        {
            if (obj == null)
            {
                continue;
            }


            if (!obj.name.StartsWith("ReefPole"))
            {
                continue;
            }


            if (obj.name == "ReefPoles")
            {
                continue;
            }


            Vector3 position =
                obj.transform.position;


            Vector3 markerPosition =
                new Vector3(
                    position.x,
                    position.y + 0.20f,
                    position.z
                );


            Gizmos.color =
                Color.red;


            Gizmos.DrawWireSphere(
                markerPosition,
                0.08f
            );
        }
    }
}