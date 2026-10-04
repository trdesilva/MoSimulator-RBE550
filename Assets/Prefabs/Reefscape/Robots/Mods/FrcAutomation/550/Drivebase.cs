using System.Collections.Generic;
using UnityEngine;
using RobotFramework.Controllers.Drivetrain;

public class Drivebase : MonoBehaviour
{
    [SerializeField] private GridPlanner planner;

    [SerializeField] private float driveSpeedKf = .5f;
    [SerializeField] private float driveSpeedKp = 0.25f;
    [SerializeField] private float driveSpeedMax = 1.0f;
    [SerializeField] private float waypointTolerance = 0.25f;
    [SerializeField] private float goalTolerance = 0.12f;
    [SerializeField] private float rotationKp = 0.02f;
    [SerializeField] private float rotationKf = 0.03f;

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

        bool finalWaypoint = waypointIndex == path.Count - 1;
        while (
            waypointIndex < path.Count - 1 &&
            Vector2.Distance(
                currentPosition,
                path[waypointIndex]
            ) < waypointTolerance)
        {
            Debug.Log($"Reached waypoint at {path[waypointIndex]}. (Current: {currentPosition})");
            waypointIndex++;
            finalWaypoint = waypointIndex == path.Count - 1;
            Debug.Log($"Next waypoint at {path[waypointIndex]}, finalWaypoint: {finalWaypoint}");
        }

        Vector2 target =
            path[waypointIndex];

        Vector2 toTarget =
            target - currentPosition;

        float distance =
            toTarget.magnitude;

        if (
            finalWaypoint &&
            distance < goalTolerance)
        {
            Finish();
            return;
        }

        float speed =
            driveSpeedMax;

        if (finalWaypoint)
        {
            speed = Mathf.Clamp(distance * driveSpeedKp + driveSpeedKf, 0.05f, driveSpeedMax);
            Debug.Log($"Approach speed: {speed}");
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

        if (Mathf.Abs(headingError) > 1.5f)
        {
            float rotation =
                -Mathf.Clamp(
                    headingError * rotationKp + rotationKf * Mathf.Sign(headingError),
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
