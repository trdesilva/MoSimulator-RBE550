using System.Reflection;
using System.Text;
using UnityEngine;

[ExecuteAlways]
public class ReefAlignNodeDebug : MonoBehaviour
{
    // ============================================================
    // INSPECTOR CONTROL
    // ============================================================

    [Header("Debug Controls")]
    [Tooltip("Check this box to print all Reef AlignNode information.")]
    public bool printReport = false;


    // ============================================================
    // UPDATE
    // ============================================================

    private void Update()
    {
        if (printReport)
        {
            printReport = false;
            PrintAllReefAlignNodes();
        }
    }


    // ============================================================
    // PRINT ALL REEF ALIGN NODES
    // ============================================================

    public void PrintAllReefAlignNodes()
    {
        Component[] alignNodes =
            FindAlignNodeComponents();


        StringBuilder report =
            new StringBuilder();


        report.AppendLine(
            "============================================================"
        );

        report.AppendLine(
            "REEF ALIGN NODE REPORT"
        );

        report.AppendLine(
            $"AlignNodes found: {alignNodes.Length}"
        );

        report.AppendLine(
            "============================================================"
        );


        for (int i = 0; i < alignNodes.Length; i++)
        {
            Component alignNode =
                alignNodes[i];


            report.AppendLine();
            report.AppendLine(
                "------------------------------------------------------------"
            );

            report.AppendLine(
                $"ALIGN NODE {i + 1}"
            );

            report.AppendLine(
                $"OBJECT: {alignNode.gameObject.name}"
            );

            report.AppendLine(
                $"PATH: {GetHierarchyPath(alignNode.transform)}"
            );

            report.AppendLine(
                $"FACE POSITION XZ: " +
                $"({alignNode.transform.position.x:F4}, " +
                $"{alignNode.transform.position.z:F4})"
            );

            report.AppendLine(
                $"FACE HEADING: " +
                $"{NormalizeAngle(alignNode.transform.eulerAngles.y):F2} deg"
            );


            // ====================================================
            // GET LEFT / RIGHT NODE REFERENCES
            // ====================================================

            GameObject leftNode =
                GetGameObjectField(
                    alignNode,
                    "LeftNode"
                );


            GameObject rightNode =
                GetGameObjectField(
                    alignNode,
                    "RightNode"
                );


            // ====================================================
            // LEFT NODE
            // ====================================================

            if (leftNode != null)
            {
                AppendNodeInformation(
                    report,
                    "LEFT NODE",
                    leftNode.transform
                );
            }
            else
            {
                report.AppendLine();
                report.AppendLine(
                    "LEFT NODE: NULL"
                );
            }


            // ====================================================
            // RIGHT NODE
            // ====================================================

            if (rightNode != null)
            {
                AppendNodeInformation(
                    report,
                    "RIGHT NODE",
                    rightNode.transform
                );
            }
            else
            {
                report.AppendLine();
                report.AppendLine(
                    "RIGHT NODE: NULL"
                );
            }


            report.AppendLine(
                "------------------------------------------------------------"
            );
        }


        report.AppendLine();

        report.AppendLine(
            "============================================================"
        );

        report.AppendLine(
            "END REEF ALIGN NODE REPORT"
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
    // FIND ALIGN NODE COMPONENTS
    // ============================================================

    private Component[] FindAlignNodeComponents()
    {
        Component[] allComponents =
            FindObjectsOfType<Component>();


        System.Collections.Generic.List<Component> alignNodes =
            new System.Collections.Generic.List<Component>();


        foreach (Component component in allComponents)
        {
            if (component == null)
            {
                continue;
            }


            System.Type componentType =
                component.GetType();


            if (componentType.Name == "AlignNode")
            {
                alignNodes.Add(
                    component
                );
            }
        }


        return alignNodes.ToArray();
    }


    // ============================================================
    // READ GAMEOBJECT FIELD USING REFLECTION
    // ============================================================

    private GameObject GetGameObjectField(
        Component component,
        string fieldName)
    {
        System.Type componentType =
            component.GetType();


        FieldInfo field =
            componentType.GetField(
                fieldName,
                BindingFlags.Public |
                BindingFlags.Instance
            );


        if (field == null)
        {
            return null;
        }


        object value =
            field.GetValue(component);


        return value as GameObject;
    }


    // ============================================================
    // APPEND NODE INFORMATION
    // ============================================================

    private void AppendNodeInformation(
        StringBuilder report,
        string label,
        Transform node)
    {
        report.AppendLine();

        report.AppendLine(
            $"{label}:"
        );

        report.AppendLine(
            $"  Object: {node.gameObject.name}"
        );

        report.AppendLine(
            $"  Path: {GetHierarchyPath(node)}"
        );

        report.AppendLine(
            $"  XZ: ({node.position.x:F4}, {node.position.z:F4})"
        );

        report.AppendLine(
            $"  Y: {node.position.y:F4}"
        );

        report.AppendLine(
            $"  Heading: " +
            $"{NormalizeAngle(node.eulerAngles.y):F2} deg"
        );
    }


    // ============================================================
    // GIZMO VISUALIZATION
    // ============================================================

    private void OnDrawGizmos()
    {
        Component[] alignNodes =
            FindAlignNodeComponents();


        foreach (Component alignNode in alignNodes)
        {
            GameObject leftNode =
                GetGameObjectField(
                    alignNode,
                    "LeftNode"
                );


            GameObject rightNode =
                GetGameObjectField(
                    alignNode,
                    "RightNode"
                );


            if (leftNode != null)
            {
                DrawScoringNode(
                    leftNode.transform,
                    Color.green
                );
            }


            if (rightNode != null)
            {
                DrawScoringNode(
                    rightNode.transform,
                    Color.blue
                );
            }
        }
    }


    // ============================================================
    // DRAW ONE SCORING NODE
    // ============================================================

    private void DrawScoringNode(
        Transform node,
        Color color)
    {
        Vector3 position =
            new Vector3(
                node.position.x,
                0.30f,
                node.position.z
            );


        Gizmos.color =
            color;


        Gizmos.DrawWireSphere(
            position,
            0.08f
        );


        float headingRadians =
            node.eulerAngles.y *
            Mathf.Deg2Rad;


        Vector3 headingDirection =
            new Vector3(
                Mathf.Sin(headingRadians),
                0.0f,
                Mathf.Cos(headingRadians)
            );


        Gizmos.DrawLine(
            position,
            position +
            headingDirection * 0.35f
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
}