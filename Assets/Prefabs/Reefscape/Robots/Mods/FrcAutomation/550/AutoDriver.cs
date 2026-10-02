using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AutoDriver : MonoBehaviour
{
    [SerializeField] private Perception perception;

    private Drivebase drivebase;


    void Start()
    {
        // Reduce physics simulation rate to 100 Hz.
        Time.fixedDeltaTime = 0.01f;

        Debug.Log(
            $"Fixed timestep set to {Time.fixedDeltaTime}"
        );

        DisableChainPhysics();

        Debug.LogWarning("AUTODRIVER STARTED");

        drivebase = GetComponent<Drivebase>();

        if (drivebase == null)
        {
            Debug.LogError(
                "AUTODRIVER: Drivebase NOT FOUND"
            );

            return;
        }

        Debug.LogWarning(
            "AUTODRIVER: Drivebase found"
        );

        // Start driving after one second.
        Invoke(
            nameof(TestDrive),
            1f
        );

        InvokeRepeating(
            nameof(LogNearestAlgae),
            5f,
            1f
        );
    }


    private void DisableChainPhysics()
    {
        Rigidbody[] bodies =
            FindObjectsByType<Rigidbody>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        int disabledCount = 0;

        foreach (Rigidbody rb in bodies)
        {
            if (!rb.gameObject.name.StartsWith("ChainLink"))
                continue;

            Joint[] joints =
                rb.GetComponents<Joint>();

            foreach (Joint joint in joints)
            {
                Destroy(joint);
            }

            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            rb.useGravity = false;
            rb.isKinematic = true;

            disabledCount++;
        }

        Debug.LogWarning(
            $"Disabled physics on {disabledCount} chain links."
        );
    }


    private void TestDrive()
    {
        Debug.LogWarning(
            "AUTODRIVER: TestDrive called"
        );

        if (drivebase == null)
        {
            Debug.LogError(
                "AUTODRIVER: Drivebase is null"
            );

            return;
        }

        Vector2 target =
            new Vector2(
                2.2f,
                0.0f
            );

        Debug.LogWarning(
            $"AUTODRIVER: Current position = " +
            $"({transform.position.x:F2}, " +
            $"{transform.position.z:F2})"
        );

        bool started =
            drivebase.GoTo(
                target,
                transform.eulerAngles.y
            );

        if (started)
        {
            Debug.LogWarning(
                $"AUTODRIVER: Drive started toward {target}"
            );
        }
        else
        {
            Debug.LogError(
                $"AUTODRIVER: Drivebase rejected target {target}"
            );
        }
    }


    private void LogNearestAlgae()
    {
        if (perception == null)
            return;

        ICollection<GameObject> algae =
            perception.GetNearestWithTag(
                "Algae",
                transform.position
            );

        if (
            algae != null &&
            algae.Count > 0)
        {
            Debug.Log(
                $"Nearest algae: " +
                $"{algae.First().transform.position}"
            );
        }
    }
}
