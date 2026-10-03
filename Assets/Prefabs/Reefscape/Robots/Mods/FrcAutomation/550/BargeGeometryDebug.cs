using System.Text;
using UnityEngine;

[ExecuteAlways]
public class BargeGeometryDebug : MonoBehaviour
{
    private const float RobotDrivingHeight = 1.06f;


    // ============================================================
    // PRINT USEFUL BARGE GEOMETRY
    // ============================================================

    [ContextMenu("Print Useful Barge Geometry")]
    public void PrintUsefulBargeGeometry()
    {
        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);

        StringBuilder report =
            new StringBuilder();


        report.AppendLine(
            "============================================================"
        );

        report.AppendLine(
            "USEFUL BARGE GEOMETRY"
        );

        report.AppendLine(
            $"Total Barge colliders: {colliders.Length}"
        );

        report.AppendLine(
            $"Robot driving height: {RobotDrivingHeight:F3} m"
        );

        report.AppendLine(
            "Cage geometry excluded because cages are already represented separately."
        );

        report.AppendLine(
            "============================================================"
        );


        // ========================================================
        // LOW STRUCTURAL GEOMETRY
        // ========================================================

        report.AppendLine();
        report.AppendLine(
            "################ LOW STRUCTURAL GEOMETRY ################"
        );

        report.AppendLine(
            "Possible navigation obstacles:"
        );

        report.AppendLine();


        int lowCount = 0;


        foreach (Collider collider in colliders)
        {
            string path =
                GetHierarchyPath(collider.transform);

            Bounds bounds =
                collider.bounds;


            // Skip cages completely.
            if (path.Contains("/Cages/"))
            {
                continue;
            }


            // Skip scoring geometry here.
            if (IsScoringGeometry(collider))
            {
                continue;
            }


            bool overlapsRobotHeight =
                bounds.min.y <= RobotDrivingHeight &&
                bounds.max.y >= 0.0f;


            if (!overlapsRobotHeight)
            {
                continue;
            }


            lowCount++;

            AppendCompactCollider(
                report,
                collider,
                path
            );
        }


        report.AppendLine();

        report.AppendLine(
            $"LOW STRUCTURAL COLLIDERS FOUND: {lowCount}"
        );


        // ========================================================
        // SCORING GEOMETRY
        // ========================================================

        report.AppendLine();
        report.AppendLine(
            "################ SCORING GEOMETRY ################"
        );

        report.AppendLine(
            "These will be used for the Barge task layer:"
        );

        report.AppendLine();


        int scoringCount = 0;


        foreach (Collider collider in colliders)
        {
            if (!IsScoringGeometry(collider))
            {
                continue;
            }


            scoringCount++;

            AppendCompactCollider(
                report,
                collider,
                GetHierarchyPath(collider.transform)
            );
        }


        report.AppendLine();

        report.AppendLine(
            $"SCORING COLLIDERS FOUND: {scoringCount}"
        );


        report.AppendLine();
        report.AppendLine(
            "============================================================"
        );

        report.AppendLine(
            "END USEFUL BARGE GEOMETRY"
        );

        report.AppendLine(
            "============================================================"
        );


        Debug.Log(
            report.ToString(),
            gameObject
        );
    }


    // ============================================================
    // COMPACT COLLIDER OUTPUT
    // ============================================================

    private void AppendCompactCollider(
        StringBuilder report,
        Collider collider,
        string path)
    {
        Bounds bounds =
            collider.bounds;


        report.AppendLine(
            "------------------------------------------------------------"
        );

        report.AppendLine(
            $"OBJECT: {collider.gameObject.name}"
        );

        report.AppendLine(
            $"TYPE: {collider.GetType().Name}"
        );

        report.AppendLine(
            $"PATH: {path}"
        );

        report.AppendLine(
            $"Y: {bounds.min.y:F4} -> {bounds.max.y:F4}"
        );

        report.AppendLine(
            $"X: {bounds.min.x:F4} -> {bounds.max.x:F4}"
        );

        report.AppendLine(
            $"Z: {bounds.min.z:F4} -> {bounds.max.z:F4}"
        );

        report.AppendLine(
            $"XZ CORNERS: " +
            $"({bounds.min.x:F4}, {bounds.min.z:F4}), " +
            $"({bounds.max.x:F4}, {bounds.min.z:F4}), " +
            $"({bounds.max.x:F4}, {bounds.max.z:F4}), " +
            $"({bounds.min.x:F4}, {bounds.max.z:F4})"
        );
    }


    // ============================================================
    // SCORING CHECK
    // ============================================================

    private bool IsScoringGeometry(
        Collider collider)
    {
        string objectName =
            collider.gameObject.name;


        return
            objectName.Contains("RedScoring") ||
            objectName.Contains("BlueScoring");
    }


    // ============================================================
    // HIERARCHY PATH
    // ============================================================

    private string GetHierarchyPath(
        Transform current)
    {
        string path =
            current.name;


        while (current.parent != null)
        {
            current =
                current.parent;

            path =
                current.name +
                "/" +
                path;
        }


        return path;
    }


    // ============================================================
    // GIZMO VISUALIZATION
    // ============================================================

    private void OnDrawGizmos()
    {
        Collider[] colliders =
            GetComponentsInChildren<Collider>(true);


        foreach (Collider collider in colliders)
        {
            string path =
                GetHierarchyPath(collider.transform);

            Bounds bounds =
                collider.bounds;


            // Cages already have their own navigation obstacles.
            if (path.Contains("/Cages/"))
            {
                continue;
            }


            if (IsScoringGeometry(collider))
            {
                // MAGENTA = Barge task/scoring geometry

                Gizmos.color =
                    Color.magenta;

                DrawBoundsXZ(
                    bounds,
                    0.24f
                );

                continue;
            }


            bool overlapsRobotHeight =
                bounds.min.y <= RobotDrivingHeight &&
                bounds.max.y >= 0.0f;


            if (overlapsRobotHeight)
            {
                // RED = possible navigation obstacle

                Gizmos.color =
                    Color.red;

                DrawBoundsXZ(
                    bounds,
                    0.22f
                );
            }
        }
    }


    // ============================================================
    // DRAW X-Z BOUNDS
    // ============================================================

    private void DrawBoundsXZ(
        Bounds bounds,
        float height)
    {
        Vector3 c0 =
            new Vector3(
                bounds.min.x,
                height,
                bounds.min.z
            );

        Vector3 c1 =
            new Vector3(
                bounds.max.x,
                height,
                bounds.min.z
            );

        Vector3 c2 =
            new Vector3(
                bounds.max.x,
                height,
                bounds.max.z
            );

        Vector3 c3 =
            new Vector3(
                bounds.min.x,
                height,
                bounds.max.z
            );


        Gizmos.DrawLine(c0, c1);
        Gizmos.DrawLine(c1, c2);
        Gizmos.DrawLine(c2, c3);
        Gizmos.DrawLine(c3, c0);
    }
}