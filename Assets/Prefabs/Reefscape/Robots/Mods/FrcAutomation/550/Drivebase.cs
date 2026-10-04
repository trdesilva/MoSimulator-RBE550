using System.Collections.Generic;
using UnityEngine;
using RobotFramework.Controllers.Drivetrain;

public class Drivebase : MonoBehaviour
{
    [SerializeField] private GridPlanner planner;

    [SerializeField] private float driveSpeed = 0.6f;
    [SerializeField] private float waypointTolerance = 0.25f;
    [SerializeField] private float goalTolerance = 0.12f;
    [SerializeField] private float rotationKp = 0.015f;

    private DriveController driveController;

    private List<Vector2> path;
    private int waypointIndex;

    private Vector2 goal;
    private float goalHeading;

    private bool driving;


    private void Awake()
    {
        driveController = GetComponent<DriveController>();

        if (planner == null)
            planner = GetComponent<GridPlanner>();

        if (driveController == null)
            Debug.LogError("Drivebase could not find DriveController.");

        if (planner == null)
            Debug.LogError("Drivebase could not find GridPlanner.");
    }


    public bool GoTo(
        Vector2 target,
        float headingDegrees = 0f)
    {
        if (planner == null || driveController == null)
            return false;

        Vector2 start = new(
            transform.position.x,
            transform.position.z
        );

        path = planner.Plan(
            start,
            target
        );

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("Drivebase could not find a path.");
            return false;
        }

        goal = target;
        goalHeading = headingDegrees;

        waypointIndex = 0;
        driving = true;

        Debug.Log(
            $"Drivebase path created with {path.Count} waypoints."
        );

        return true;
    }


    private void FixedUpdate()
    {
        if (!driving || path == null)
            return;

        FollowPath();
    }


    private void FollowPath()
    {
        Vector2 currentPosition = new(
            transform.position.x,
            transform.position.z
        );

        while (
            waypointIndex < path.Count - 1 &&
            Vector2.Distance(
                currentPosition,
                path[waypointIndex]
            ) < waypointTolerance)
        {
            waypointIndex++;
        }

        Vector2 target =
            path[waypointIndex];

        Vector2 toTarget =
            target - currentPosition;

        float distance =
            toTarget.magnitude;

        bool finalWaypoint =
            waypointIndex == path.Count - 1;

        if (
            finalWaypoint &&
            distance < goalTolerance)
        {
            Finish();
            return;
        }

        float speed =
            driveSpeed;

        if (finalWaypoint)
        {
            speed = Mathf.Min(
                driveSpeed,
                Mathf.Max(
                    0.15f,
                    distance
                )
            );
        }

        Vector2 input =
            toTarget.normalized *
            speed;

        float headingError =
            Mathf.DeltaAngle(
                transform.eulerAngles.y,
                goalHeading
            );

        float rotation =
            -Mathf.Clamp(
                headingError * rotationKp,
                -1f,
                1f
            );

        driveController.overideInput(
            input,
            rotation,
            DriveController.DriveMode.FieldOriented
        );
    }


    private void Finish()
    {
        float headingError =
            Mathf.DeltaAngle(
                transform.eulerAngles.y,
                goalHeading
            );

        if (Mathf.Abs(headingError) > 3f)
        {
            float rotation =
                -Mathf.Clamp(
                    headingError * rotationKp,
                    -1f,
                    1f
                );

            driveController.overideInput(
                Vector2.zero,
                rotation,
                DriveController.DriveMode.FieldOriented
            );

            return;
        }

        Stop();
    }


    public void Stop()
    {
        driving = false;

        if (driveController != null)
        {
            driveController.overideInput(
                Vector2.zero,
                0f,
                DriveController.DriveMode.FieldOriented
            );
        }

        Debug.Log("Drivebase reached destination.");
    }


    public bool IsDriving()
    {
        return driving;
    }
}
