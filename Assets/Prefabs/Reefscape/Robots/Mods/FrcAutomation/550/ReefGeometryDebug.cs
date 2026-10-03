using System.Text;
using UnityEngine;

[ExecuteAlways]
public class ReefGeometryDebug : MonoBehaviour
{
    // ============================================================
    // PRINT REEF HIERARCHY / SCORING NODE DATA
    // ============================================================

    [ContextMenu("Print Reef Scoring Geometry")]
    public void PrintReefScoringGeometry()
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "============================================================"
        );

        report.AppendLine(
            "REEF SCORING GEOMETRY DEBUG"
        );

        report.AppendLine(
            $"Selected Object: {gameObject.name}"
        );

        report.AppendLine(
            $"Selected Path: {GetHierarchyPath(transform)}"
        );

        report.AppendLine(
            "============================================================"
        );

        Transform[] allTransforms =
            GetComponentsInChildren<Transform>(true);

        report.AppendLine();
        report.AppendLine(
            $"TOTAL CHILD TRANSFORMS: {allTransforms.Length}"
        );

        report.AppendLine();


        // ========================================================
        // FIND POSSIBLE REEF FACE / ALIGN NODE OBJECTS
        // ========================================================

        report.AppendLine(
            "################ POSSIBLE SCORING OBJECTS ################"
        );

        report.AppendLine(
            "Objects containing Face, Node, Left, Right, Align, or Reef:"
        );

        report.AppendLine();


        int relevantCount = 0;


        foreach (Transform child in allTransforms)
        {
            string objectName =
                child.name.ToLower();

            bool looksRelevant =
                objectName.Contains("face") ||
                objectName.Contains("node") ||
                objectName.Contains("left") ||
                objectName.Contains("right") ||
                objectName.Contains("align") ||
                objectName.Contains("reef");


            if (!looksRelevant)
            {
                continue;
            }


            relevantCount++;


            AppendTransformInformation(
                report,
                child
            );
        }


        report.AppendLine();

        report.AppendLine(
            $"POSSIBLE SCORING OBJECTS FOUND: {relevantCount}"
        );


        // ========================================================
        // COMPONENT SEARCH
        // ========================================================

        report.AppendLine();
        report.AppendLine(
            "################ COMPONENT SEARCH ################"
        );

        report.AppendLine(
            "Components with names containing Reef, Align, Face, or Node:"
        );

        report.AppendLine();


        Component[] components =
            GetComponentsInChildren<Component>(true);


        int componentCount = 0;


        foreach (Component component in components)
        {
            if (component == null)
            {
                continue;
            }


            string typeName =
                component.GetType().Name;

            string lowerTypeName =
                typeName.ToLower();


            bool looksRelevant =
                lowerTypeName.Contains("reef") ||
                lowerTypeName.Contains("align") ||
                lowerTypeName.Contains("face") ||
                lowerTypeName.Contains("node");


            if (!looksRelevant)
            {
                continue;
            }


            componentCount++;


            report.AppendLine(
                "------------------------------------------------------------"
            );

            report.AppendLine(
                $"COMPONENT: {typeName}"
            );

            report.AppendLine(
                $"OBJECT: {component.gameObject.name}"
            );

            report.AppendLine(
                $"PATH: {GetHierarchyPath(component.transform)}"
            );
        }


        report.AppendLine();

        report.AppendLine(
            $"RELEVANT COMPONENTS FOUND: {componentCount}"
        );


        // ========================================================
        // FULL DIRECT CHILD SUMMARY
        // ========================================================

        report.AppendLine();
        report.AppendLine(
            "################ DIRECT CHILDREN ################"
        );

        report.AppendLine(
            "Useful for understanding how this Reef is organized:"
        );

        report.AppendLine();


        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child =
                transform.GetChild(i);


            report.AppendLine(
                $"[{i}] {child.name}"
            );

            report.AppendLine(
                $"    Path: {GetHierarchyPath(child)}"
            );

            report.AppendLine(
                $"    Position XZ: ({child.position.x:F4}, {child.position.z:F4})"
            );

            report.AppendLine(
                $"    Rotation Y: {NormalizeAngle(child.eulerAngles.y):F2} deg"
            );

            report.AppendLine();
        }


        report.AppendLine(
            "============================================================"
        );

        report.AppendLine(
            "END REEF SCORING GEOMETRY DEBUG"
        );

        report.AppendLine(
            "============================================================"
        );


        // ONE console message so it is easy to copy.

        Debug.Log(
            report.ToString(),
            gameObject
        );
    }


    // ============================================================
    // TRANSFORM INFORMATION
    // ============================================================

    private void AppendTransformInformation(
        StringBuilder report,
        Transform target)
    {
        Vector3 worldPosition =
            target.position;


        float worldHeading =
            NormalizeAngle(
                target.eulerAngles.y
            );


        report.AppendLine(
            "------------------------------------------------------------"
        );

        report.AppendLine(
            $"OBJECT: {target.name}"
        );

        report.AppendLine(
            $"PATH: {GetHierarchyPath(target)}"
        );


        report.AppendLine(
            $"WORLD XZ: ({worldPosition.x:F4}, {worldPosition.z:F4})"
        );

        report.AppendLine(
            $"WORLD Y: {worldPosition.y:F4}"
        );

        report.AppendLine(
            $"WORLD HEADING: {worldHeading:F2} deg"
        );


        report.AppendLine(
            $"LOCAL POSITION: " +
            $"({target.localPosition.x:F4}, " +
            $"{target.localPosition.y:F4}, " +
            $"{target.localPosition.z:F4})"
        );

        report.AppendLine(
            $"LOCAL Y ROTATION: " +
            $"{NormalizeAngle(target.localEulerAngles.y):F2} deg"
        );
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
    // ANGLE NORMALIZATION
    // ============================================================

    private float NormalizeAngle(
        float angle)
    {
        while (angle > 180.0f)
        {
            angle -= 360.0f;
        }


        while (angle <= -180.0f)
        {
            angle += 360.0f;
        }


        return angle;
    }


    // ============================================================
    // GIZMO DEBUGGING
    // ============================================================

    private void OnDrawGizmos()
    {
        Transform[] allTransforms =
            GetComponentsInChildren<Transform>(true);


        foreach (Transform child in allTransforms)
        {
            string objectName =
                child.name.ToLower();


            bool looksLikeNode =
                objectName.Contains("node") ||
                objectName.Contains("left") ||
                objectName.Contains("right") ||
                objectName.Contains("align");


            if (!looksLikeNode)
            {
                continue;
            }


            Vector3 position =
                child.position;


            // Lift debug marker slightly above the floor
            // so it is visible from the top view.

            Vector3 debugPosition =
                new Vector3(
                    position.x,
                    0.30f,
                    position.z
                );


            Gizmos.color =
                Color.green;


            Gizmos.DrawWireSphere(
                debugPosition,
                0.08f
            );


            // Draw the transform's forward direction.
            //
            // Project onto X-Z because our planner is 2D.

            Vector3 forward =
                child.forward;


            forward.y =
                0.0f;


            if (forward.sqrMagnitude >
                0.0001f)
            {
                forward.Normalize();


                Gizmos.DrawLine(
                    debugPosition,
                    debugPosition +
                    forward * 0.35f
                );
            }
        }
    }
}