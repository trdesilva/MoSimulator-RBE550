using System;
using System.Collections.Generic;
using System.Reflection;

using Games.Reefscape.Enums;
using Games.Reefscape.GamePieceSystem;
using Games.Reefscape.Robots;
using Games.Reefscape.Scoring.Scorers;

using MoSimCore.Enums;

using RobotFramework;
using RobotFramework.Controllers.Drivetrain;
using RobotFramework.Controllers.GamePieceSystem;
using RobotFramework.GamePieceSystem;

using UnityEngine;


[DefaultExecutionOrder(1000)]
public class AutoDriver : MonoBehaviour
{
    private enum AutoState
    {
        Waiting,

        SearchingCoral,
        DrivingToCoral,
        PreparingIntake,
        Intaking,
        StowingCoral,

        DrivingToReefStage,
        DrivingToReefFinal,
        FinalAlign,

        PreparingScore,
        Scoring,

        Done
    }

    // References

    [SerializeField]
    private Perception perception;

    [SerializeField]
    private FieldMap2D fieldMap;

    [SerializeField]
    private float startupDelay = 1.0f;

    [SerializeField]
    private float pickupStandOff = 0.60f;

    [SerializeField]
    private float pickupClaimDistance = 0.85f;

    [SerializeField]
    private float intakePrepSeconds = 0.65f;

    [SerializeField]
    private float coralReplanDistance = 0.20f;

    [SerializeField]
    private float coralReplanPeriod = 0.35f;

    [SerializeField]
    private float maxLooseCoralHeight = 0.55f;

    [SerializeField]
    private float reefStageDistance = 0.50f;

    [SerializeField]
    private float precisePositionTolerance = 0.025f;

    [SerializeField]
    private float preciseHeadingTolerance = 0.75f;

    [SerializeField]
    private float preciseDriveMax = 0.22f;

    [SerializeField]
    private float preciseDriveKp = 1.25f;

    [SerializeField]
    private float preciseRotationKp = 0.020f;

    [SerializeField]
    private float preciseRotationMax = 0.35f;

    [SerializeField]
    private float scorePrepSeconds = 1.75f;

    [SerializeField]
    private float scoreCheckDelay = 1.00f;

    [SerializeField]
    private bool repeatCycles = true;

    private Drivebase drivebase;

    private DriveController driveController;

    private JackInTheBot robot;

    private ReefscapeRobotGamePieceController
        robotGamePieceController;

    private RobotGamePieceController<
        ReefscapeGamePiece,
        ReefscapeGamePieceData
    >.GamePieceControllerNode coralNode;

    private AutoState state =
        AutoState.Waiting;

    private GameObject targetCoral;

    private Vector2 lastPlannedCoralPosition;

    private float nextCoralReplanTime;

    private float stateStartTime;

    private class L4Target
    {
        public FieldMap2D.ReefScoringNode node;

        public BoxCollider scoringTrigger;
    }

    private readonly List<L4Target> l4Targets =
        new List<L4Target>();

    private L4Target currentL4Target;

    private Vector2 reefStagePosition;

    private GameObject releasedCoral;

    private int successfulScores;

    private int failedScores;

    private MethodInfo setRobotStateMethod;

    private MethodInfo setRobotModeMethod;

    private MethodInfo checkFacingReefMethod;

    private MethodInfo setJackSetpointMethod;

    private JackInTheBotSetpoint
        groundCoralIntakeSetpoint;


    // start

    private void Start()
    {
        // Reduce simulation physics load.
        Time.fixedDeltaTime = 0.01f;

        Debug.Log(
            $"Fixed timestep set to " +
            $"{Time.fixedDeltaTime}"
        );


        DisableChainPhysics();


        drivebase =
            GetComponent<Drivebase>();

        driveController =
            GetComponent<DriveController>();

        robot =
            GetComponent<JackInTheBot>();

        robotGamePieceController =
            GetComponent<
                ReefscapeRobotGamePieceController
            >();


        if (fieldMap == null)
        {
            fieldMap =
                GetComponent<FieldMap2D>();
        }


        if (
            drivebase == null ||
            driveController == null ||
            robot == null ||
            robotGamePieceController == null ||
            perception == null ||
            fieldMap == null)
        {
            Debug.LogError(
                "AUTODRIVER: missing a required component."
            );

            return;
        }


        coralNode =
            robotGamePieceController
            .GetPieceByName("Coral");


        if (coralNode == null)
        {
            Debug.LogError(
                "AUTODRIVER: Coral node not found."
            );

            return;
        }


        if (!InitializeRobotBridge())
            return;


        Debug.LogWarning(
            "AUTODRIVER: initialized"
        );


        Invoke(
            nameof(BeginAutonomy),
            startupDelay
        );
    }


    private void BeginAutonomy()
    {
        BuildL4Targets();


        if (l4Targets.Count == 0)
        {
            Debug.LogError(
                "AUTODRIVER: no L4 scoring targets found."
            );

            state =
                AutoState.Done;

            return;
        }


        Debug.LogWarning(
            $"AUTODRIVER: mapped " +
            $"{l4Targets.Count} L4 targets."
        );


        if (coralNode.HasPiece())
        {
            Debug.Log(
                "AUTODRIVER: starting with Coral."
            );

            StartDriveToClosestFreeL4();

            return;
        }


        state =
            AutoState.SearchingCoral;
    }

    // main state

    private void Update()
    {
        switch (state)
        {
            case AutoState.SearchingCoral:
                SearchForCoral();
                break;


            case AutoState.DrivingToCoral:
                UpdateDriveToCoral();
                break;


            case AutoState.PreparingIntake:
                UpdatePreparingIntake();
                break;


            case AutoState.Intaking:
                UpdateIntaking();
                break;


            case AutoState.StowingCoral:
                UpdateStowingCoral();
                break;


            case AutoState.DrivingToReefStage:
                UpdateDrivingToReefStage();
                break;


            case AutoState.DrivingToReefFinal:
                UpdateDrivingToReefFinal();
                break;


            case AutoState.PreparingScore:
                UpdatePreparingScore();
                break;


            case AutoState.Scoring:
                UpdateScoring();
                break;
        }
    }


    private void FixedUpdate()
    {
        if (
            state == AutoState.PreparingIntake ||
            state == AutoState.Intaking)
        {
            ApplyGroundCoralIntakePose();
        }


        if (state == AutoState.FinalAlign)
        {
            ApplyPreciseReefAlignment();
        }
    }

    // near coral

    private void SearchForCoral()
    {
        targetCoral =
            FindNearestLooseCoral();


        if (targetCoral == null)
        {
            return;
        }


        Debug.Log(
            $"AUTODRIVER: closest Coral = " +
            $"{targetCoral.transform.position}"
        );


        if (!PlanToTargetCoral())
        {
            targetCoral = null;

            return;
        }


        state =
            AutoState.DrivingToCoral;
    }


    private GameObject FindNearestLooseCoral()
    {
        ICollection<GameObject> corals =
            perception.GetAllWithTag("Coral");


        if (corals == null)
            return null;


        GameObject best = null;

        float bestDistance =
            float.PositiveInfinity;


        foreach (GameObject coral in corals)
        {
            if (coral == null)
                continue;

            if (!coral.activeInHierarchy)
                continue;

            if (
                coral.GetComponentInParent<
                    RobotBase
                >() != null)
            {
                continue;
            }

            if (IsCoralOnL4(coral))
                continue;

            if (
                coral.transform.position.y >
                maxLooseCoralHeight)
            {
                continue;
            }


            ReefscapeGamePieceController
                controller =
                    coral.GetComponent<
                        ReefscapeGamePieceController
                    >();


            if (controller == null)
                continue;


            float distance =
                (
                    ToXZ(
                        coral.transform.position
                    ) -
                    CurrentPosition()
                ).sqrMagnitude;


            if (distance < bestDistance)
            {
                bestDistance =
                    distance;

                best =
                    coral;
            }
        }


        return best;
    }

    // get to coral

    private bool PlanToTargetCoral()
    {
        if (targetCoral == null)
            return false;


        Vector2 robotPosition =
            CurrentPosition();


        Vector2 coralPosition =
            ToXZ(
                targetCoral.transform.position
            );


        Vector2 towardCoral =
            coralPosition -
            robotPosition;


        if (
            towardCoral.sqrMagnitude <
            0.0001f)
        {
            return false;
        }


        Vector2 direction =
            towardCoral.normalized;


        Vector2 approachPosition =
            coralPosition -
            direction *
            pickupStandOff;


        float heading =
            HeadingFromDirection(
                direction
            );


        bool started =
            drivebase.GoTo(
                approachPosition,
                heading
            );


        if (!started)
        {
            Debug.LogWarning(
                "AUTODRIVER: could not plan to Coral."
            );

            return false;
        }


        lastPlannedCoralPosition =
            coralPosition;


        nextCoralReplanTime =
            Time.time +
            coralReplanPeriod;


        return true;
    }


    private void UpdateDriveToCoral()
    {
        if (targetCoral == null)
        {
            state =
                AutoState.SearchingCoral;

            return;
        }


        Vector2 coralPosition =
            ToXZ(
                targetCoral.transform.position
            );


        float distance =
            Vector2.Distance(
                CurrentPosition(),
                coralPosition
            );


        // Dynamically chase a CORAL that rolls.
        if (
            Time.time >=
            nextCoralReplanTime)
        {
            if (
                Vector2.Distance(
                    coralPosition,
                    lastPlannedCoralPosition
                ) >
                coralReplanDistance)
            {
                PlanToTargetCoral();
            }


            nextCoralReplanTime =
                Time.time +
                coralReplanPeriod;
        }


        if (
            distance <= pickupClaimDistance ||
            !drivebase.IsDriving())
        {
            drivebase.Stop();


            state =
                AutoState.PreparingIntake;


            stateStartTime =
                Time.time;


            Debug.Log(
                "AUTODRIVER: preparing intake"
            );
        }
    }

    // intake

    private void UpdatePreparingIntake()
    {
        if (targetCoral == null)
        {
            state =
                AutoState.SearchingCoral;

            return;
        }


        float distance =
            Vector2.Distance(
                CurrentPosition(),
                ToXZ(
                    targetCoral.transform.position
                )
            );



        if (
            distance >
            pickupClaimDistance +
            0.25f)
        {
            if (PlanToTargetCoral())
            {
                state =
                    AutoState.DrivingToCoral;
            }
            else
            {
                state =
                    AutoState.SearchingCoral;
            }

            return;
        }


        if (
            Time.time -
            stateStartTime <
            intakePrepSeconds)
        {
            return;
        }


        if (!ClaimTargetCoral())
        {
            Debug.LogWarning(
                "AUTODRIVER: failed to claim Coral."
            );

            targetCoral = null;

            state =
                AutoState.SearchingCoral;

            return;
        }


        state =
            AutoState.Intaking;


        Debug.Log(
            "AUTODRIVER: Coral entering intake"
        );
    }


    private bool ClaimTargetCoral()
    {
        if (targetCoral == null)
            return false;


        ReefscapeGamePieceController
            pieceController =
                targetCoral.GetComponent<
                    ReefscapeGamePieceController
                >();


        if (pieceController == null)
            return false;


        GamePieceState intakeState =
            FindCoralState(
                "coralIntakeState"
            );


        if (intakeState == null)
        {
            Debug.LogError(
                "AUTODRIVER: coralIntakeState not found."
            );

            return false;
        }


        coralNode.controller =
            pieceController;


        coralNode.currentStateNum =
            intakeState.stateNum;


        coralNode.atTarget =
            false;


        coralNode.movingTo =
            intakeState.name
            .Trim()
            .ToLowerInvariant();


        coralNode.wasMovingTo =
            coralNode.movingTo;


        return true;
    }


    private void UpdateIntaking()
    {
        if (!coralNode.HasPiece())
            return;


        if (!coralNode.atTarget)
            return;


        GamePieceState stowState =
            FindCoralState(
                "coralStowState"
            );


        if (stowState == null)
        {
            Debug.LogError(
                "AUTODRIVER: coralStowState not found."
            );

            state =
                AutoState.Done;

            return;
        }


        coralNode.SetTargetState(
            stowState
        );


        ForceRobotState(
            ReefscapeSetpoints.Stow
        );


        state =
            AutoState.StowingCoral;


        Debug.Log(
            "AUTODRIVER: Coral acquired"
        );
    }


    private void UpdateStowingCoral()
    {
        if (!coralNode.HasPiece())
        {
            state =
                AutoState.SearchingCoral;

            return;
        }


        if (!coralNode.atTarget)
            return;


        StartDriveToClosestFreeL4();
    }

    // scoring targets

    private void BuildL4Targets()
    {
        l4Targets.Clear();


        FieldMap2D.ReefAlliance
            mapAlliance =
                robot.Alliance ==
                Alliance.Blue
                ? FieldMap2D.ReefAlliance.Blue
                : FieldMap2D.ReefAlliance.Red;


        ReefPoleScorer[] scorers =
            FindObjectsByType<
                ReefPoleScorer
            >(
                FindObjectsSortMode.None
            );


        List<ReefPoleScorer>
            allianceScorers =
                new List<
                    ReefPoleScorer
                >();


        foreach (
            ReefPoleScorer scorer
            in scorers)
        {
            if (
                scorer.Alliance ==
                robot.Alliance)
            {
                allianceScorers.Add(
                    scorer
                );
            }
        }


        foreach (
            FieldMap2D.ReefScoringNode node
            in fieldMap.reefScoringNodes)
        {
            if (
                node.alliance !=
                mapAlliance)
            {
                continue;
            }


            ReefPoleScorer
                closestScorer =
                    null;


            float closestDistance =
                float.PositiveInfinity;


            foreach (
                ReefPoleScorer scorer
                in allianceScorers)
            {
                Vector2 scorerPosition =
                    new Vector2(
                        scorer.transform.position.x,
                        scorer.transform.position.z
                    );


                float distance =
                    Vector2.Distance(
                        node.positionXZ,
                        scorerPosition
                    );


                if (
                    distance <
                    closestDistance)
                {
                    closestDistance =
                        distance;

                    closestScorer =
                        scorer;
                }
            }


            if (closestScorer == null)
                continue;


            BoxCollider l4 =
                FindL4Trigger(
                    closestScorer
                );


            if (l4 == null)
                continue;


            l4Targets.Add(
                new L4Target
                {
                    node = node,
                    scoringTrigger = l4
                }
            );
        }
    }


    private BoxCollider FindL4Trigger(
        ReefPoleScorer scorer)
    {
        Transform[] children =
            scorer
            .GetComponentsInChildren<
                Transform
            >(
                true
            );


        foreach (
            Transform child
            in children)
        {
            if (child.name != "L4")
                continue;


            BoxCollider trigger =
                child.GetComponent<
                    BoxCollider
                >();


            if (trigger != null)
                return trigger;
        }


        return null;
    }

    // free branch

    private L4Target FindClosestFreeL4()
    {
        L4Target best =
            null;


        float bestDistance =
            float.PositiveInfinity;


        Vector2 robotPosition =
            CurrentPosition();


        foreach (
            L4Target target
            in l4Targets)
        {
            if (
                IsBranchOccupied(
                    target
                ))
            {
                continue;
            }


            float distance =
                Vector2.Distance(
                    robotPosition,
                    target.node.positionXZ
                );


            if (
                distance <
                bestDistance)
            {
                bestDistance =
                    distance;

                best =
                    target;
            }
        }


        return best;
    }


    // Drive to branches

    private void StartDriveToClosestFreeL4()
    {
        currentL4Target =
            FindClosestFreeL4();


        if (currentL4Target == null)
        {
            Debug.LogWarning(
                "AUTODRIVER: no free L4 branches remain."
            );

            state =
                AutoState.Done;

            return;
        }


        FieldMap2D.ReefScoringNode node =
            currentL4Target.node;


        Vector2 reefCenter =
            GetAllianceReefCenter();


        Vector2 outward =
            (
                node.positionXZ -
                reefCenter
            ).normalized;


        reefStagePosition =
            node.positionXZ +
            outward *
            reefStageDistance;


        ForceRobotMode(
            ReefscapeRobotMode.Coral
        );


        ForceRobotState(
            ReefscapeSetpoints.Stow
        );


        bool started =
            drivebase.GoTo(
                reefStagePosition,
                node.headingDegrees
            );


        if (!started)
        {
            Debug.LogError(
                "AUTODRIVER: could not plan " +
                "to Reef staging position."
            );

            state =
                AutoState.SearchingCoral;

            return;
        }


        state =
            AutoState.DrivingToReefStage;


        Debug.LogWarning(
            $"AUTODRIVER: nearest free L4 = " +
            $"{node.name}"
        );
    }


    private void UpdateDrivingToReefStage()
    {
        if (!coralNode.HasPiece())
        {
            state =
                AutoState.SearchingCoral;

            return;
        }


        if (drivebase.IsDriving())
            return;


        FieldMap2D.ReefScoringNode node =
            currentL4Target.node;


        bool started =
            drivebase.GoTo(
                node.positionXZ,
                node.headingDegrees
            );


        if (!started)
        {
            Debug.LogError(
                "AUTODRIVER: final Reef path failed."
            );

            state =
                AutoState.Done;

            return;
        }


        state =
            AutoState.DrivingToReefFinal;
    }


    private void UpdateDrivingToReefFinal()
    {
        if (!coralNode.HasPiece())
        {
            state =
                AutoState.SearchingCoral;

            return;
        }


        if (drivebase.IsDriving())
            return;


        state =
            AutoState.FinalAlign;


        Debug.Log(
            "AUTODRIVER: beginning precision alignment"
        );
    }

    // better alignment

    private void ApplyPreciseReefAlignment()
    {
        if (currentL4Target == null)
            return;


        Vector2 target =
            currentL4Target
            .node
            .positionXZ;


        Vector2 current =
            CurrentPosition();


        Vector2 error =
            target -
            current;


        float distance =
            error.magnitude;


        float headingError =
            Mathf.DeltaAngle(
                transform.eulerAngles.y,
                currentL4Target
                .node
                .headingDegrees
            );


        bool positionGood =
            distance <=
            precisePositionTolerance;


        bool headingGood =
            Mathf.Abs(
                headingError
            ) <=
            preciseHeadingTolerance;


        if (
            positionGood &&
            headingGood)
        {
            driveController.overideInput(
                Vector2.zero,
                0f,
                DriveController
                .DriveMode
                .FieldOriented
            );


            BeginScorePreparation();

            return;
        }


        Vector2 input =
            Vector2.zero;


        if (!positionGood)
        {
            float speed =
                Mathf.Clamp(
                    distance *
                    preciseDriveKp,
                    0.025f,
                    preciseDriveMax
                );


            input =
                error.normalized *
                speed;
        }


        float rotation =
            0f;


        if (!headingGood)
        {
            rotation =
                -Mathf.Clamp(
                    headingError *
                    preciseRotationKp,
                    -preciseRotationMax,
                    preciseRotationMax
                );
        }


        driveController.overideInput(
            input,
            rotation,
            DriveController
            .DriveMode
            .FieldOriented
        );
    }

    // Lifting the arm

    private void BeginScorePreparation()
    {
        CheckFacingReef();


        ForceRobotMode(
            ReefscapeRobotMode.Coral
        );


        // full extension
        ForceRobotState(
            ReefscapeSetpoints.L4
        );


        GamePieceState frontState =
            FindCoralState(
                "coralFrontStowState"
            );


        if (frontState != null)
        {
            coralNode.SetTargetState(
                frontState
            );
        }


        state =
            AutoState.PreparingScore;


        stateStartTime =
            Time.time;


        Debug.LogWarning(
            $"AUTODRIVER: extending to L4 at " +
            $"{currentL4Target.node.name}"
        );
    }


    private void UpdatePreparingScore()
    {
        if (
            Time.time -
            stateStartTime <
            scorePrepSeconds)
        {
            return;
        }


        if (!coralNode.HasPiece())
        {
            state =
                AutoState.SearchingCoral;

            return;
        }


        if (!coralNode.atTarget)
            return;


        ReleaseCoral();
    }

    // Releaseing the coral

    private void ReleaseCoral()
    {
        if (!coralNode.HasPiece())
        {
            state =
                AutoState.SearchingCoral;

            return;
        }


        if (
            coralNode.controller != null)
        {
            releasedCoral =
                coralNode
                .controller
                .gameObject;
        }


        Vector3 releaseForce =
            robot.GetFacingReef()
            ? new Vector3(
                0f,
                0f,
                -5f
            )
            : new Vector3(
                0f,
                0f,
                5f
            );


        bool released =
            coralNode
            .ReleaseGamePieceWithForce(
                releaseForce
            );


        if (!released)
        {
            Debug.LogWarning(
                "AUTODRIVER: Coral not ready " +
                "to release."
            );

            return;
        }


        Debug.Log(
            $"AUTODRIVER: released Coral at " +
            $"{currentL4Target.node.name}"
        );


        state =
            AutoState.Scoring;


        stateStartTime =
            Time.time;
    }


    // scoring

    private void UpdateScoring()
    {
        if (
            Time.time -
            stateStartTime <
            scoreCheckDelay)
        {
            return;
        }


        bool success =
            currentL4Target != null &&
            IsBranchOccupied(
                currentL4Target
            );


        if (success)
        {
            successfulScores++;


            Debug.LogWarning(
                $"AUTODRIVER: SCORE SUCCESS at " +
                $"{currentL4Target.node.name}. " +
                $"Successes={successfulScores}, " +
                $"Failures={failedScores}"
            );
        }
        else
        {
            failedScores++;


            string droppedPosition =
                releasedCoral != null
                ? releasedCoral
                    .transform
                    .position
                    .ToString()
                : "unknown";


            Debug.LogWarning(
                $"AUTODRIVER: SCORE FAILED at " +
                $"{currentL4Target?.node.name}. " +
                $"Coral position={droppedPosition}. " +
                $"Successes={successfulScores}, " +
                $"Failures={failedScores}"
            );
        }


        ForceRobotState(
            ReefscapeSetpoints.Stow
        );


        targetCoral =
            null;


        releasedCoral =
            null;


        currentL4Target =
            null;


        if (!repeatCycles)
        {
            state =
                AutoState.Done;

            return;
        }

        state =
            AutoState.SearchingCoral;
    }

    // On branch?

    private bool IsBranchOccupied(
        L4Target target)
    {
        if (
            target == null ||
            target.scoringTrigger == null)
        {
            return false;
        }


        BoxCollider box =
            target.scoringTrigger;


        Vector3 center =
            box.transform.TransformPoint(
                box.center
            );


        Vector3 scale =
            box.transform.lossyScale;


        Vector3 halfExtents =
            new Vector3(
                Mathf.Abs(
                    box.size.x *
                    scale.x
                ),
                Mathf.Abs(
                    box.size.y *
                    scale.y
                ),
                Mathf.Abs(
                    box.size.z *
                    scale.z
                )
            ) * 0.5f;


        Collider[] hits =
            Physics.OverlapBox(
                center,
                halfExtents,
                box.transform.rotation
            );


        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Coral"))
            {
                return true;
            }
        }


        return false;
    }


    private bool IsCoralOnL4(
        GameObject coral)
    {
        if (coral == null)
            return false;


        foreach (
            L4Target target
            in l4Targets)
        {
            if (
                IsSpecificCoralInBranch(
                    coral,
                    target
                ))
            {
                return true;
            }
        }


        return false;
    }


    private bool IsSpecificCoralInBranch(
        GameObject coral,
        L4Target target)
    {
        if (
            coral == null ||
            target == null ||
            target.scoringTrigger == null)
        {
            return false;
        }


        BoxCollider box =
            target.scoringTrigger;


        Vector3 center =
            box.transform.TransformPoint(
                box.center
            );


        Vector3 scale =
            box.transform.lossyScale;


        Vector3 halfExtents =
            new Vector3(
                Mathf.Abs(
                    box.size.x *
                    scale.x
                ),
                Mathf.Abs(
                    box.size.y *
                    scale.y
                ),
                Mathf.Abs(
                    box.size.z *
                    scale.z
                )
            ) * 0.5f;


        Collider[] hits =
            Physics.OverlapBox(
                center,
                halfExtents,
                box.transform.rotation
            );


        foreach (Collider hit in hits)
        {
            if (
                hit.CompareTag("Coral") &&
                hit.gameObject == coral)
            {
                return true;
            }
        }


        return false;
    }

    // game pieces

    private GamePieceState FindCoralState(
        string stateName)
    {
        if (
            coralNode == null ||
            coralNode.gamePieceStates == null)
        {
            return null;
        }


        foreach (
            GamePieceState gamePieceState
            in coralNode.gamePieceStates)
        {
            if (gamePieceState == null)
                continue;


            if (
                string.Equals(
                    gamePieceState.name,
                    stateName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return gamePieceState;
            }
        }


        return null;
    }

    // bridges

    private bool InitializeRobotBridge()
    {
        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.NonPublic;


        setRobotStateMethod =
            typeof(ReefscapeRobotBase)
            .GetMethod(
                "SetState",
                flags
            );


        setRobotModeMethod =
            typeof(ReefscapeRobotBase)
            .GetMethod(
                "SetRobotMode",
                flags
            );


        checkFacingReefMethod =
            typeof(ReefscapeRobotBase)
            .GetMethod(
                "CheckFacingReef",
                flags
            );


        setJackSetpointMethod =
            typeof(JackInTheBot)
            .GetMethod(
                "SetSetpoint",
                flags
            );


        FieldInfo groundIntakeField =
            typeof(JackInTheBot)
            .GetField(
                "groundCoralIntakeSetpoint",
                flags
            );


        if (
            setRobotStateMethod == null ||
            setRobotModeMethod == null ||
            checkFacingReefMethod == null ||
            setJackSetpointMethod == null ||
            groundIntakeField == null)
        {
            Debug.LogError(
                "AUTODRIVER: could not initialize " +
                "JackInTheBot automation bridge."
            );

            return false;
        }


        groundCoralIntakeSetpoint =
            (JackInTheBotSetpoint)
            groundIntakeField
            .GetValue(robot);


        return true;
    }


    private void ApplyGroundCoralIntakePose()
    {
        setJackSetpointMethod.Invoke(
            robot,
            new object[]
            {
                groundCoralIntakeSetpoint
            }
        );
    }


    private void ForceRobotState(
        ReefscapeSetpoints robotState)
    {
        setRobotStateMethod.Invoke(
            robot,
            new object[]
            {
                robotState
            }
        );
    }


    private void ForceRobotMode(
        ReefscapeRobotMode mode)
    {
        setRobotModeMethod.Invoke(
            robot,
            new object[]
            {
                mode
            }
        );
    }


    private void CheckFacingReef()
    {
        checkFacingReefMethod.Invoke(
            robot,
            null
        );
    }

    // running better for the sim

    private void DisableChainPhysics()
    {
        Rigidbody[] bodies =
            FindObjectsByType<Rigidbody>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );


        int disabledCount =
            0;


        foreach (Rigidbody rb in bodies)
        {
            if (
                !rb.gameObject.name
                .StartsWith("ChainLink"))
            {
                continue;
            }


            Joint[] joints =
                rb.GetComponents<Joint>();


            foreach (Joint joint in joints)
            {
                Destroy(joint);
            }


            rb.velocity =
                Vector3.zero;


            rb.angularVelocity =
                Vector3.zero;


            rb.useGravity =
                false;


            rb.isKinematic =
                true;


            disabledCount++;
        }


        Debug.Log(
            $"Disabled physics on " +
            $"{disabledCount} chain links."
        );
    }

    // positions

    private Vector2 CurrentPosition()
    {
        return new Vector2(
            transform.position.x,
            transform.position.z
        );
    }


    private Vector2 ToXZ(
        Vector3 position)
    {
        return new Vector2(
            position.x,
            position.z
        );
    }


    private float HeadingFromDirection(
        Vector2 direction)
    {
        return Mathf.Atan2(
            direction.x,
            direction.y
        ) * Mathf.Rad2Deg;
    }


    private Vector2 GetAllianceReefCenter()
    {
        if (
            robot.Alliance ==
            Alliance.Blue)
        {
            return new Vector2(
                4.298872f,
                0f
            );
        }


        return new Vector2(
            -4.298872f,
            0f
        );
    }
}
