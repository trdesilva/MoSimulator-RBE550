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
        CoralRetreat,
        CoralPostStow,

        Done
    }

    private enum OperatingMode { ScoreCoral, ReefAlgaeToProcessor }
    [Header("Algae Settings")]
    [SerializeField] private OperatingMode operatingMode;
    [SerializeField] private float algaeApproachOffset = 0f;
    [SerializeField] private float processorStandOff = 0.65f;
    [SerializeField] private float algaeTimeout = 12f;
    [SerializeField] private float algaeReleaseForce = 2f;
    [SerializeField] private float algaePositionTolerance = 0.075f;
    [SerializeField] private float algaeHeadingTolerance = 3f;
    [SerializeField] private float processorPositionTolerance = 0.05f;
    [SerializeField] private float processorHeadingTolerance = 3f;
    [SerializeField] private float algaePrepStandOff = 0.15f;
    [SerializeField] private float algaePrepTime = 0.75f;
    [SerializeField] private float highAlgaePrepStandOff = 0.65f;
    [SerializeField] private float highAlgaePrepTime = 3.0f;
    private float ActiveAlgaePrepStandOff => algaeSetpoint == ReefscapeSetpoints.HighAlgae ? Mathf.Max(algaePrepStandOff, highAlgaePrepStandOff) : algaePrepStandOff;
    private float ActiveAlgaePrepTime => algaeSetpoint == ReefscapeSetpoints.HighAlgae ? Mathf.Max(algaePrepTime, highAlgaePrepTime) : algaePrepTime;
    [SerializeField] private float algaeCreepSpeed = 0.10f;
    [SerializeField] private float highAlgaeFastApproachSpeed = 0.65f;
    [SerializeField] private float highAlgaeSlowApproachDistance = 0.25f;
    [SerializeField] private float algaeCreepDistance = 0.15f;
    [SerializeField] private float algaeCreepTimeout = 3f;
    [SerializeField] private float algaeRetreatDistance = 0.20f;
    [SerializeField] private float algaeRetreatSpeed = 0.40f;
    [SerializeField] private float algaeHoldClearance = 0.25f;
    [Header("Reef Interaction Improvements")]
    [SerializeField] private float coralRetreatDistance = 0.30f;
    [SerializeField] private float coralRetreatSpeed = 0.40f;
    [SerializeField] private float algaeCaptureWindow = 0.6f;
    [SerializeField] private bool processorPoseForTransport = true;
    private Vector2 coralRetreatStart, coralOutward;
    private bool lastCoralScoreSuccess;
    private Vector2 algaeInward, algaeCreepStart, algaeRetreatStart;
    private enum AlgaePhase { Search, Drive, Align, Prepare, Intake, CaptureWait, Retreat, FailedRetreat, FailedStow, ProcessorPrepare, ProcessorDrive, ProcessorAlign, Release, Verify, Done }
    private AlgaePhase algaePhase;
    private bool algaeRunning;
    private bool scoringPreloadedCoral;
    private float algaeSince;
    private Vector2 algaeGoal;
    private float algaeHeading;
    private GameObject algaeTarget;
    private int algaeFace;
    private readonly HashSet<int> failedAlgaeFaces = new HashSet<int>();
    private ReefscapeSetpoints algaeSetpoint;
    private ProcessorScorer processor;
    private int processorBefore;
    private RobotGamePieceController<ReefscapeGamePiece, ReefscapeGamePieceData>.GamePieceControllerNode algaeNode;
    private MatchState sharedMatch;
    private enum CoralLevel { L2, L3, L4 }

    [Header("Coral Scoring Settings")]
    [SerializeField] private CoralLevel coralScoringLevel = CoralLevel.L4;

    // Optional radial offsets (positive = farther from reef).
    [SerializeField] private float l2ApproachOffset = 0f;
    [SerializeField] private float l3ApproachOffset = 0f;

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

    private class ReefTarget
    {
        public FieldMap2D.ReefScoringNode node;

        public BoxCollider scoringTrigger;
        public CoralLevel level;
        public Vector2 positionXZ;
    }

    private readonly List<ReefTarget> reefTargets =
        new List<ReefTarget>();

    private ReefTarget currentReefTarget;

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
        if (operatingMode == OperatingMode.ReefAlgaeToProcessor)
        {
            if (!coralNode.HasPiece()) { BeginAlgae(); return; }
            scoringPreloadedCoral = true;
            Debug.Log("ALGAE: Scoring preloaded Coral before Algae cycle.");
        }
        BuildReefTargets();


        if (!reefTargets.Exists(t => t.level == coralScoringLevel))
        {
            Debug.LogError(
                $"AUTODRIVER: no {coralScoringLevel} scoring targets found."
            );

            state =
                AutoState.Done;

            return;
        }


        Debug.LogWarning(
            $"AUTODRIVER: mapped " +
            $"{reefTargets.Count} total branch targets; selected {coralScoringLevel}."
        );


        if (coralNode.HasPiece())
        {
            Debug.Log(
                "AUTODRIVER: starting with Coral."
            );

            StartDriveToClosestFreeBranch();

            return;
        }


        state =
            AutoState.SearchingCoral;
    }

    // main state

    private void Update()
    {
        if (algaeRunning) { UpdateAlgae(); return; }
        if (scoringPreloadedCoral && state == AutoState.SearchingCoral)
        {
            drivebase.Stop();
            if (coralNode.HasPiece())
            {
                Debug.LogWarning("ALGAE: Preloaded Coral path failed; stopping while carrying Coral.");
                state = AutoState.Done;
            }
            else
            {
                Debug.LogWarning("ALGAE: Preloaded Coral lost; beginning Algae cycle.");
                scoringPreloadedCoral = false;
                BeginAlgae();
            }
            return;
        }
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
            case AutoState.CoralRetreat:
                if (Vector2.Dot(CurrentPosition() - coralRetreatStart, coralOutward) >= coralRetreatDistance)
                {
                    drivebase.Stop(); ForceRobotState(ReefscapeSetpoints.Stow);
                    state = AutoState.CoralPostStow; stateStartTime = Time.time;
                }
                else if (Time.time - stateStartTime > algaeTimeout)
                { drivebase.Stop(); state = AutoState.Done; Debug.LogWarning("AUTODRIVER: Coral retreat timeout; mechanism held."); }
                break;
            case AutoState.CoralPostStow:
                if (Time.time - stateStartTime >= scorePrepSeconds) FinishCoralCycle();
                break;
        }
    }


    private void FixedUpdate()
    {
        if (!algaeRunning && state == AutoState.CoralRetreat)
        {
            driveController.overideInput(coralOutward * coralRetreatSpeed, 0f, DriveController.DriveMode.FieldOriented);
            return;
        }
        if (algaeRunning)
        {
            if (algaePhase == AlgaePhase.Intake && !algaeNode.HasPiece())
            {
                float travel = Vector2.Dot(CurrentPosition() - algaeCreepStart, algaeInward);
                float remaining = Vector2.Dot(algaeGoal + algaeInward * ActiveAlgaePrepStandOff - CurrentPosition(), algaeInward);
                if (travel < algaeCreepDistance + (ActiveAlgaePrepStandOff - algaePrepStandOff) && remaining > 0.01f)
                {
                    float speed = algaeCreepSpeed;
                    if (algaeSetpoint == ReefscapeSetpoints.HighAlgae &&
                        remaining > Mathf.Max(0f, highAlgaeSlowApproachDistance))
                        speed = Mathf.Max(algaeCreepSpeed, highAlgaeFastApproachSpeed);
                    LocalAlgaeDrive(algaeInward * Mathf.Clamp01(speed));
                }
                else LocalAlgaeDrive(Vector2.zero);
            }
            else if (algaePhase == AlgaePhase.Retreat || algaePhase == AlgaePhase.FailedRetreat)
                LocalAlgaeDrive(Vector2.Dot(algaeRetreatStart - CurrentPosition(), algaeInward) < (algaePhase == AlgaePhase.Retreat ? Mathf.Max(algaeRetreatDistance, algaeHoldClearance) : algaeRetreatDistance) ? -algaeInward * algaeRetreatSpeed : Vector2.zero);
            else if (algaePhase == AlgaePhase.Prepare || algaePhase == AlgaePhase.CaptureWait || algaePhase == AlgaePhase.FailedStow || algaePhase == AlgaePhase.ProcessorPrepare || algaePhase == AlgaePhase.Intake)
                driveController.overideInput(Vector2.zero, 0f, DriveController.DriveMode.FieldOriented);
            return;
        }
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

            if (IsCoralScoredOnReef(coral))
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


        StartDriveToClosestFreeBranch();
    }

    // scoring targets

    private void BuildReefTargets()
    {
        reefTargets.Clear();


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


            foreach (CoralLevel level in Enum.GetValues(typeof(CoralLevel)))
            {
                BoxCollider trigger = FindBranchTrigger(closestScorer, level);
                if (trigger == null)
                    continue;

                float offset = level == CoralLevel.L2 ? l2ApproachOffset :
                               level == CoralLevel.L3 ? l3ApproachOffset : 0f;
                Vector2 outward = (node.positionXZ - GetAllianceReefCenter()).normalized;
                reefTargets.Add(new ReefTarget
                {
                    node = node,
                    level = level,
                    scoringTrigger = trigger,
                    positionXZ = node.positionXZ + outward * offset
                });
            }
        }
    }


    private BoxCollider FindBranchTrigger(
        ReefPoleScorer scorer, CoralLevel level)
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
            if (child.name != level.ToString())
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

    private bool IsFaceBlocked(FieldMap2D.ReefScoringNode node)
    {
        var match = FindFirstObjectByType<MatchState>();
        if (match != null && match.alliance == node.alliance && match.allianceState.reefAlgaeInitialized)
            return match.IsReefAlgaePresent(node.faceNumber);
        return AlgaeMatchStateSync.FindFaceAlgae(fieldMap.GetReefL1ScoringNode(node.alliance, node.faceNumber)) != null;
    }

    private ReefTarget FindClosestFreeBranch()
    {
        ReefTarget best =
            null;


        float bestDistance =
            float.PositiveInfinity;


        Vector2 robotPosition =
            CurrentPosition();


        foreach (
            ReefTarget target
            in reefTargets)
        {
            if (target.level != coralScoringLevel)
                continue;
            if (target.level != CoralLevel.L4 && IsFaceBlocked(target.node))
                continue;

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
                    target.positionXZ
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

    private void StartDriveToClosestFreeBranch()
    {
        currentReefTarget = FindClosestFreeBranch();
        if (scoringPreloadedCoral && currentReefTarget == null)
        {
            CoralLevel original = coralScoringLevel;
            coralScoringLevel = CoralLevel.L4;
            currentReefTarget = FindClosestFreeBranch();
            coralScoringLevel = original;
        }


        if (currentReefTarget == null)
        {
            Debug.LogWarning(
                $"AUTODRIVER: no free {coralScoringLevel} branches remain."
            );

            state =
                AutoState.Done;

            return;
        }


        FieldMap2D.ReefScoringNode node =
            currentReefTarget.node;


        Vector2 reefCenter =
            GetAllianceReefCenter();


        Vector2 outward =
            (
                currentReefTarget.positionXZ -
                reefCenter
            ).normalized;


        reefStagePosition =
            currentReefTarget.positionXZ +
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
                currentReefTarget.positionXZ,
                node.headingDegrees, precisePositionTolerance, preciseHeadingTolerance, true
            );


        if (!started)
        {
            Debug.LogError(
                "AUTODRIVER: could not plan " +
                "to exact Reef scoring pose."
            );

            state =
                AutoState.SearchingCoral;

            return;
        }


        state =
            AutoState.DrivingToReefFinal;


        Debug.LogWarning(
            $"AUTODRIVER: nearest free {currentReefTarget.level} = " +
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
            currentReefTarget.node;


        bool started =
            drivebase.GoTo(
                currentReefTarget.positionXZ,
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


        BeginScorePreparation();
    }

    // better alignment

    private void ApplyPreciseReefAlignment()
    {
        if (currentReefTarget == null)
            return;


        Vector2 target =
            currentReefTarget
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
                currentReefTarget
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
            currentReefTarget.level == CoralLevel.L2 ? ReefscapeSetpoints.L2 :
            currentReefTarget.level == CoralLevel.L3 ? ReefscapeSetpoints.L3 :
            ReefscapeSetpoints.L4
        );


        GamePieceState frontState =
            FindCoralState(
                currentReefTarget.level == CoralLevel.L4 || robot.GetFacingReef()
                    ? "coralFrontStowState" : "coralBackStowState"
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
            $"AUTODRIVER: extending to {currentReefTarget.level} at " +
            $"{currentReefTarget.node.name} {currentReefTarget.level}"
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
            $"{currentReefTarget.node.name} {currentReefTarget.level}"
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
            currentReefTarget != null &&
            IsSpecificCoralInBranch(
                releasedCoral, currentReefTarget
            );


        if (success)
        {
            successfulScores++;


            Debug.LogWarning(
                $"AUTODRIVER: SCORE SUCCESS at " +
                $"{currentReefTarget.node.name} {currentReefTarget.level}. " +
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
                $"{currentReefTarget?.node.name} {currentReefTarget?.level}. " +
                $"Coral position={droppedPosition}. " +
                $"Successes={successfulScores}, " +
                $"Failures={failedScores}"
            );
        }


        lastCoralScoreSuccess = success;
        float heading = currentReefTarget.node.headingDegrees * Mathf.Deg2Rad;
        coralOutward = -new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
        coralRetreatStart = CurrentPosition();
        drivebase.Stop();
        state = AutoState.CoralRetreat;
        stateStartTime = Time.time;
        Debug.Log("AUTODRIVER: Retreat before Coral stow");
    }

    private void FinishCoralCycle()
    {
        bool success = lastCoralScoreSuccess;
        ForceRobotState(
            ReefscapeSetpoints.Stow
        );


        targetCoral =
            null;


        releasedCoral =
            null;


        currentReefTarget =
            null;


        if (scoringPreloadedCoral)
        {
            scoringPreloadedCoral = false;
            Debug.Log(success ? "ALGAE: Preloaded Coral scored; beginning Algae cycle." :
                "ALGAE: Preloaded Coral released but score not verified; beginning Algae cycle.");
            BeginAlgae();
            return;
        }

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
        ReefTarget target)
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


    private bool IsCoralScoredOnReef(
        GameObject coral)
    {
        if (coral == null)
            return false;


        foreach (
            ReefTarget target
            in reefTargets)
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
        ReefTarget target)
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

    private void BeginAlgae()
    {
        algaeRunning = true;
        algaeNode = robotGamePieceController.GetPieceByName("Algae");
        sharedMatch = FindFirstObjectByType<MatchState>();
        if (sharedMatch != null && sharedMatch.alliance != MapAlliance()) sharedMatch = null;
        foreach (var scorer in FindObjectsByType<ProcessorScorer>(FindObjectsSortMode.None))
            if (scorer.Alliance == robot.Alliance) { processor = scorer; break; }
        if (algaeNode == null) { StopAlgae("Algae controller not found."); return; }
        if (processor == null) { StopAlgae("Alliance Processor not found."); return; }
        if (coralNode.HasPiece()) { StopAlgae("Robot is still carrying Coral."); return; }
        if (sharedMatch == null) { StopAlgae("No MatchState for robot alliance."); return; }
        failedAlgaeFaces.Clear();
        ForceRobotMode(ReefscapeRobotMode.Algae);
        if (algaeNode.HasPiece()) BeginProcessorPreparation(); else SetAlgaePhase(AlgaePhase.Search);
    }
    private FieldMap2D.ReefAlliance MapAlliance() => robot.Alliance == Alliance.Blue ? FieldMap2D.ReefAlliance.Blue : FieldMap2D.ReefAlliance.Red;
    private void SetAlgaePhase(AlgaePhase phase) { algaePhase = phase; algaeSince = Time.time; Debug.Log("ALGAE: " + phase); }
    private void StopAlgae(string reason)
    {
        robot.AutonomousDelayAlgaeHome = false;
        robot.AutonomousReefAlgaeIntake = false;
        robot.AutonomousLowAlgaeCarry = false;
        drivebase.Stop(); ForceRobotState(ReefscapeSetpoints.Stow);
        SetAlgaePhase(AlgaePhase.Done);
        if (sharedMatch != null) sharedMatch.ClearCurrentTask();
        Debug.LogWarning("ALGAE: " + reason);
    }
    private void BeginProcessorPreparation()
    {
        drivebase.Stop();
        robot.AutonomousDelayAlgaeHome = false;
        robot.AutonomousLowAlgaeCarry = false;
        ForceRobotState(processorPoseForTransport ? ReefscapeSetpoints.Processor : ReefscapeSetpoints.Stow);
        SetAlgaePhase(AlgaePhase.ProcessorPrepare);
    }
    private void DriveProcessor()
    {
        // Use actual alliance scorer position to select the correct mapped opening.
        FieldMap2D.TaskLocation best = null;
        float distance = float.PositiveInfinity;
        foreach (var task in fieldMap.taskLocations)
        {
            if (task.type != FieldMap2D.TaskType.Processor) continue;
            float d = Vector2.Distance(task.positionXZ, ToXZ(processor.transform.position));
            if (d < distance) { distance = d; best = task; }
        }
        if (best == null) { StopAlgae("No Processor map location"); return; }
        float sign = Mathf.Sign(best.positionXZ.y);
        algaeGoal = best.positionXZ - new Vector2(0, sign * processorStandOff);
        algaeHeading = sign > 0 ? 0 : 180;
        if (sharedMatch != null) sharedMatch.SetCurrentTask(MatchState.RobotTask.ScoreAlgaeProcessor, best.name);
        if (!drivebase.GoTo(algaeGoal, algaeHeading, processorPositionTolerance, processorHeadingTolerance, true)) { StopAlgae("Processor path failed; check exact goal clearance"); return; }
        SetAlgaePhase(AlgaePhase.ProcessorDrive);
    }
    private bool AlignAlgae()
    {
        Vector2 error = algaeGoal - CurrentPosition();
        float angle = Mathf.DeltaAngle(transform.eulerAngles.y, algaeHeading);
        bool pos = error.magnitude <= precisePositionTolerance;
        bool heading = Mathf.Abs(angle) <= preciseHeadingTolerance;
        driveController.overideInput(pos ? Vector2.zero : error.normalized * Mathf.Clamp(error.magnitude * preciseDriveKp, 0.025f, preciseDriveMax), heading ? 0f : -Mathf.Clamp(angle * preciseRotationKp, -preciseRotationMax, preciseRotationMax), DriveController.DriveMode.FieldOriented);
        return pos && heading;
    }
    private void ReselectAlgae(string reason, bool failed)
    {
        robot.AutonomousReefAlgaeIntake = false;
        if (failed && algaeFace > 0) failedAlgaeFaces.Add(algaeFace);
        Debug.LogWarning($"ALGAE: Face {algaeFace}: {reason}; selecting another target.");
        drivebase.Stop();
        if (algaePhase == AlgaePhase.Prepare || algaePhase == AlgaePhase.Intake || algaePhase == AlgaePhase.CaptureWait || algaePhase == AlgaePhase.Retreat)
        {
            algaeRetreatStart = CurrentPosition();
            SetAlgaePhase(AlgaePhase.FailedRetreat);
            return;
        }
        FinishFailedAlgae();
    }
    private void FinishFailedAlgae()
    {
        robot.AutonomousDelayAlgaeHome = false;
        ForceRobotState(ReefscapeSetpoints.Stow);
        sharedMatch.ClearCurrentTask();
        algaeTarget = null;
        algaeFace = 0;
        SetAlgaePhase(AlgaePhase.Search);
    }

    private void BeginReefIntake()
    {
        drivebase.Stop();
        robot.AutonomousDelayAlgaeHome = true;
        CheckFacingReef();
        robot.AutonomousReefAlgaeIntake = false;
        ForceRobotState(algaeSetpoint);
        Debug.Log($"ALGAE: Preparing {algaeSetpoint} at stand-off {ActiveAlgaePrepStandOff:F2} m for {ActiveAlgaePrepTime:F1} s");
        SetAlgaePhase(AlgaePhase.Prepare);
    }

    private void LocalAlgaeDrive(Vector2 input)
    {
        float angle = Mathf.DeltaAngle(transform.eulerAngles.y, algaeHeading);
        driveController.overideInput(input, -Mathf.Clamp(angle * preciseRotationKp, -preciseRotationMax, preciseRotationMax), DriveController.DriveMode.FieldOriented);
    }

    private void OnDisable()
    {
        if (robot != null) { robot.AutonomousReefAlgaeIntake = false; robot.AutonomousLowAlgaeCarry = false; robot.AutonomousDelayAlgaeHome = false; }
        if (drivebase != null) drivebase.Stop();
    }

    private void UpdateAlgae()
    {
        if (algaePhase == AlgaePhase.Done) return;
        if (sharedMatch != null) sharedMatch.SetCarriedGamePiece(algaeNode.HasPiece() ? MatchState.CarriedGamePiece.Algae : MatchState.CarriedGamePiece.None);
        bool acquiring = algaePhase == AlgaePhase.Drive || algaePhase == AlgaePhase.Align || algaePhase == AlgaePhase.Prepare || algaePhase == AlgaePhase.Intake || algaePhase == AlgaePhase.CaptureWait;
        if (acquiring)
        {
            // Acquisition takes priority over disappearance from the Reef.
            if (algaeNode.HasPiece())
            {
                drivebase.Stop(); robot.AutonomousReefAlgaeIntake = false;
                algaeRetreatStart = CurrentPosition();
                SetAlgaePhase(AlgaePhase.Retreat); return;
            }
            var face = sharedMatch.GetReefFace(algaeFace);
            if (face == null || !face.algaePresent || algaeTarget == null || face.algaePiece != algaeTarget)
            {
                if (algaePhase == AlgaePhase.Intake)
                { drivebase.Stop(); SetAlgaePhase(AlgaePhase.CaptureWait); return; }
                if (algaePhase != AlgaePhase.CaptureWait)
                { ReselectAlgae("Target no longer on Reef", false); return; }
            }
        }
        float elapsed = Time.time - algaeSince;
        if (acquiring && elapsed > (algaePhase == AlgaePhase.Drive ? 60f : algaeTimeout))
        { ReselectAlgae($"Timeout in {algaePhase}; position error={Vector2.Distance(CurrentPosition(), algaeGoal):F3} m", true); return; }
        if (algaePhase == AlgaePhase.FailedRetreat && elapsed > algaeTimeout)
        { drivebase.Stop(); SetAlgaePhase(AlgaePhase.Done); sharedMatch.ClearCurrentTask(); Debug.LogWarning("ALGAE: Recovery retreat timeout; mechanism held."); return; }
        if (elapsed > (algaePhase == AlgaePhase.Drive || algaePhase == AlgaePhase.ProcessorDrive ? 60f : algaeTimeout)) { StopAlgae($"Timeout in {algaePhase}; position error={Vector2.Distance(CurrentPosition(), algaeGoal):F3} m, heading error={Mathf.DeltaAngle(transform.eulerAngles.y, algaeHeading):F2} deg"); return; }
        switch (algaePhase)
        {
            case AlgaePhase.Search:
                if (!sharedMatch.allianceState.reefAlgaeInitialized) break;
                FieldMap2D.ReefL1ScoringNode best = null;
                float distance = float.PositiveInfinity;
                foreach (var node in fieldMap.reefL1ScoringNodes)
                {
                    if (node.alliance != MapAlliance()) continue;
                    var face = sharedMatch.GetReefFace(node.faceNumber);
                    if (face == null || !face.algaePresent || failedAlgaeFaces.Contains(node.faceNumber)) continue;
                    var piece = face.algaePiece;
                    if (piece == null) continue;
                    float d = Vector2.Distance(CurrentPosition(), node.positionXZ);
                    if (d < distance) { distance = d; best = node; algaeTarget = piece; }
                }
                if (best == null) { StopAlgae("No Reef Algae detected"); return; }
                algaeFace = best.faceNumber;
                algaeSetpoint = sharedMatch.GetReefFace(algaeFace).algaeHigh ? ReefscapeSetpoints.HighAlgae : ReefscapeSetpoints.LowAlgae;
                algaeHeading = best.headingDegrees;
                float rad = algaeHeading * Mathf.Deg2Rad;
                algaeInward = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                algaeGoal = best.positionXZ - algaeInward * (algaeApproachOffset + ActiveAlgaePrepStandOff);
                if (sharedMatch != null) sharedMatch.SetCurrentTask(MatchState.RobotTask.AcquireAlgae, best.name + " " + algaeSetpoint);
                ForceRobotState(ReefscapeSetpoints.Stow);
                if (!drivebase.GoTo(algaeGoal, algaeHeading, algaePositionTolerance, algaeHeadingTolerance, true)) { ReselectAlgae("Reef path failed; check exact goal clearance", true); return; }
                SetAlgaePhase(AlgaePhase.Drive); break;
            case AlgaePhase.Drive:
                if (!drivebase.IsDriving()) { BeginReefIntake(); } break;
            case AlgaePhase.Align:
                if (AlignAlgae()) { BeginReefIntake(); } break;
            case AlgaePhase.Prepare:
                if (elapsed >= ActiveAlgaePrepTime) { algaeCreepStart = CurrentPosition(); robot.AutonomousReefAlgaeIntake = true; SetAlgaePhase(AlgaePhase.Intake); } break;
            case AlgaePhase.Intake:
                if (elapsed >= algaeCreepTimeout) ReselectAlgae("Creep ended without capture", true);
                break;
            case AlgaePhase.CaptureWait:
                if (elapsed >= algaeCaptureWindow) ReselectAlgae("Capture window expired", true);
                break;
            case AlgaePhase.FailedRetreat:
                if (Vector2.Dot(algaeRetreatStart - CurrentPosition(), algaeInward) >= algaeRetreatDistance)
                { drivebase.Stop(); ForceRobotState(ReefscapeSetpoints.Stow); SetAlgaePhase(AlgaePhase.FailedStow); }
                break;
            case AlgaePhase.FailedStow:
                if (elapsed >= scorePrepSeconds) FinishFailedAlgae();
                break;
            case AlgaePhase.Retreat:
                if (!algaeNode.HasPiece()) { ReselectAlgae("Lost Algae during retreat", true); return; }
                float backedAway = Vector2.Dot(algaeRetreatStart - CurrentPosition(), algaeInward);
                if (robot.AutonomousDelayAlgaeHome && backedAway >= Mathf.Max(0f, algaeHoldClearance))
                {
                    robot.AutonomousDelayAlgaeHome = false;
                    Debug.Log("ALGAE: Clearance reached; enabling held position");
                }
                if (backedAway >= Mathf.Max(algaeRetreatDistance, algaeHoldClearance))
                { BeginProcessorPreparation(); }
                break;
            case AlgaePhase.ProcessorPrepare:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae"); return; }
                if (elapsed >= scorePrepSeconds && algaeNode.atTarget) DriveProcessor(); break;
            case AlgaePhase.ProcessorDrive:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae"); return; }
                if (!drivebase.IsDriving()) { robot.AutonomousLowAlgaeCarry = false; ForceRobotState(ReefscapeSetpoints.Processor); SetAlgaePhase(AlgaePhase.Release); } break;
            case AlgaePhase.ProcessorAlign:
                if (AlignAlgae()) { robot.AutonomousLowAlgaeCarry = false; ForceRobotState(ReefscapeSetpoints.Processor); SetAlgaePhase(AlgaePhase.Release); } break;
            case AlgaePhase.Release:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae before release"); return; }
                if (elapsed < scorePrepSeconds || !algaeNode.atTarget) break;
                processorBefore = processor.GetScoredAlgaeCount();
                if (algaeNode.ReleaseGamePieceWithForce(new Vector3(0, algaeReleaseForce, 0))) SetAlgaePhase(AlgaePhase.Verify);
                break;
            case AlgaePhase.Verify:
                if (processor.GetScoredAlgaeCount() > processorBefore)
                {
                    Debug.Log("ALGAE: Processor count increased");
                    ForceRobotState(ReefscapeSetpoints.Stow);
                    if (repeatCycles) { failedAlgaeFaces.Clear(); algaeTarget = null; algaeFace = 0; SetAlgaePhase(AlgaePhase.Search); } else StopAlgae("Cycle complete");
                }
                break;
        }
    }
}
