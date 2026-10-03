using UnityEngine;

[ExecuteAlways]
public class WallGeometryDebug : MonoBehaviour
{
    [ContextMenu("Print Wall Geometry")]
    public void PrintWallGeometry()
    {
        BoxCollider[] colliders =
            GetComponentsInChildren<BoxCollider>(true);

        Debug.Log(
            "============================================================\n" +
            "WALL GEOMETRY DEBUG\n" +
            $"Found {colliders.Length} BoxColliders under: {gameObject.name}\n" +
            "============================================================"
        );

        foreach (BoxCollider box in colliders)
        {
            PrintBoxCollider(box);
        }

        Debug.Log(
            "============================================================\n" +
            "END WALL GEOMETRY DEBUG\n" +
            "============================================================"
        );
    }


    private void PrintBoxCollider(BoxCollider box)
    {
        Transform t = box.transform;

        Vector3 center = box.center;
        Vector3 halfSize = box.size * 0.5f;

        // --------------------------------------------------------
        // LOCAL X-Z CORNERS OF THE BOX COLLIDER
        // --------------------------------------------------------
        //
        // We only care about the planning plane:
        //
        // Unity X
        // Unity Z
        //
        // Y is ignored for the field-boundary calculation.
        //
        // TransformPoint automatically handles:
        // position
        // rotation
        // scale
        // parent transforms
        // --------------------------------------------------------

        Vector3 localCorner0 =
            center +
            new Vector3(
                -halfSize.x,
                0.0f,
                -halfSize.z
            );

        Vector3 localCorner1 =
            center +
            new Vector3(
                 halfSize.x,
                 0.0f,
                -halfSize.z
            );

        Vector3 localCorner2 =
            center +
            new Vector3(
                 halfSize.x,
                 0.0f,
                 halfSize.z
            );

        Vector3 localCorner3 =
            center +
            new Vector3(
                -halfSize.x,
                 0.0f,
                 halfSize.z
            );


        // --------------------------------------------------------
        // CONVERT LOCAL CORNERS TO WORLD SPACE
        // --------------------------------------------------------

        Vector3 worldCorner0 =
            t.TransformPoint(localCorner0);

        Vector3 worldCorner1 =
            t.TransformPoint(localCorner1);

        Vector3 worldCorner2 =
            t.TransformPoint(localCorner2);

        Vector3 worldCorner3 =
            t.TransformPoint(localCorner3);


        // --------------------------------------------------------
        // PRINT RESULTS
        // --------------------------------------------------------

        string output =
            "\n------------------------------------------------------------\n" +
            $"OBJECT: {box.gameObject.name}\n" +
            $"PATH: {GetHierarchyPath(box.transform)}\n" +
            $"Trigger: {box.isTrigger}\n" +
            "\n" +
            "WORLD X-Z CORNERS:\n" +
            $"  C0 = ({worldCorner0.x:F4}, {worldCorner0.z:F4})\n" +
            $"  C1 = ({worldCorner1.x:F4}, {worldCorner1.z:F4})\n" +
            $"  C2 = ({worldCorner2.x:F4}, {worldCorner2.z:F4})\n" +
            $"  C3 = ({worldCorner3.x:F4}, {worldCorner3.z:F4})\n" +
            "\n" +
            "WORLD AABB:\n" +
            $"  Min X = {box.bounds.min.x:F4}\n" +
            $"  Max X = {box.bounds.max.x:F4}\n" +
            $"  Min Z = {box.bounds.min.z:F4}\n" +
            $"  Max Z = {box.bounds.max.z:F4}\n" +
            "------------------------------------------------------------";

        Debug.Log(output, box.gameObject);
    }


    private string GetHierarchyPath(Transform current)
    {
        string path = current.name;

        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }


    // ============================================================
    // OPTIONAL GIZMO VISUALIZATION
    // ============================================================

    private void OnDrawGizmos()
    {
        BoxCollider[] colliders =
            GetComponentsInChildren<BoxCollider>(true);

        Gizmos.color = Color.magenta;

        foreach (BoxCollider box in colliders)
        {
            DrawColliderXZ(box);
        }
    }


    private void DrawColliderXZ(BoxCollider box)
    {
        Transform t = box.transform;

        Vector3 center = box.center;
        Vector3 halfSize = box.size * 0.5f;


        Vector3 localCorner0 =
            center +
            new Vector3(
                -halfSize.x,
                0.0f,
                -halfSize.z
            );

        Vector3 localCorner1 =
            center +
            new Vector3(
                 halfSize.x,
                 0.0f,
                -halfSize.z
            );

        Vector3 localCorner2 =
            center +
            new Vector3(
                 halfSize.x,
                 0.0f,
                 halfSize.z
            );

        Vector3 localCorner3 =
            center +
            new Vector3(
                -halfSize.x,
                 0.0f,
                 halfSize.z
            );


        Vector3 worldCorner0 =
            t.TransformPoint(localCorner0);

        Vector3 worldCorner1 =
            t.TransformPoint(localCorner1);

        Vector3 worldCorner2 =
            t.TransformPoint(localCorner2);

        Vector3 worldCorner3 =
            t.TransformPoint(localCorner3);


        // Raise the visualization slightly above the floor.

        const float gizmoHeight = 0.25f;

        worldCorner0.y = gizmoHeight;
        worldCorner1.y = gizmoHeight;
        worldCorner2.y = gizmoHeight;
        worldCorner3.y = gizmoHeight;


        Gizmos.DrawLine(
            worldCorner0,
            worldCorner1
        );

        Gizmos.DrawLine(
            worldCorner1,
            worldCorner2
        );

        Gizmos.DrawLine(
            worldCorner2,
            worldCorner3
        );

        Gizmos.DrawLine(
            worldCorner3,
            worldCorner0
        );
    }
}