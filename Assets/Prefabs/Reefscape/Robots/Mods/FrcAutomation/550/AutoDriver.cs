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

    private enum OperatingMode { ScoreCoral, ReefAlgaeToProcessor, ReefAlgaeProcessorThenNet, ElevatedIntakeTest, CombinedAlgaeScoringTest, FloorIntakeTest, ClearAlgaeThenScoreCoral, CompleteReef }
    [Header("Algae Settings")]
    [SerializeField] private OperatingMode operatingMode;
    [SerializeField] private float algaeApproachOffset = 0f;
    [SerializeField] private float processorStandOff = 0.65f;
    [SerializeField] private float processorStagingStandOff = 1.10f;
    [SerializeField] private float processorFinalApproachSpeed = 0.30f;
    private Vector2 processorFinalGoal, processorInward;
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
    [Header("Barge Net Settings")]
    [SerializeField] private float bargeStandOff = 1.0f;
    [SerializeField] private float bargeHeadingOffset = 0f;
    [SerializeField] private float bargePrepTime = 3f;
    [SerializeField] private float bargeVerifyTime = 1f;
    private bool nextScoreNet;
    private int combinedScores;
    private bool IsCombinedTestMode => operatingMode == OperatingMode.CombinedAlgaeScoringTest;
    private int netBefore;
    private float netDetectedSince = -1f;
    private Vector2 coralRetreatStart, coralOutward;
    private bool lastCoralScoreSuccess;
    private Vector2 algaeInward, algaeCreepStart, algaeRetreatStart;
    private enum AlgaePhase { Search, Drive, Align, Prepare, Intake, CaptureWait, Retreat, ReefCarryTransition, FailedRetreat, FailedStow, ProcessorPrepare, ProcessorDrive, ProcessorAlign, ProcessorStagePrepare, ProcessorApproach, Release, Verify, NetDrive, NetPrepare, NetRelease, NetVerify, Done, VerifyClear }
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

    [Header("Clear Algae Then Score Coral")]
    [SerializeField, Range(1, 6)] private int targetReefFace = 3;
    [SerializeField] private FieldMap2D.ReefBranchSide targetBranchSide = FieldMap2D.ReefBranchSide.Right;
    [SerializeField] private CoralLevel targetCoralLevel = CoralLevel.L3;
    private ReefTarget clearTarget;
    private bool clearPreload;
    private float clearStarted;
    private bool IsClearTest => operatingMode == OperatingMode.ClearAlgaeThenScoreCoral;

    [Header("Complete Reef Planner")]
    [SerializeField] private float reefRetryCooldown = 5f;
    [SerializeField] private float reefCoralTaskTimeout = 90f;
    [SerializeField] private float reefProgressInterval = 10f;
    private bool IsCompleteReef => operatingMode == OperatingMode.CompleteReef;
    private bool reefActive, reefFinished, reefClockSeen, reefFloorRecovery;
    private float reefStarted, reefNextPlan, reefNextProgress, reefTaskStarted;
    private int reefFaceGoal, reefCoralAttempts, reefCoralMisses, reefAlgaeAttempts,
        reefAlgaeFailures, reefProcessorFailures, reefOtherFailures, reefRetries;
    private string reefTaskKey, reefLogPath;
    private readonly Dictionary<string,float> reefCooldowns = new Dictionary<string,float>();
    private readonly Dictionary<int,GameObject> reefOriginalAlgae = new Dictionary<int,GameObject>();
    private readonly HashSet<int> reefProcessed = new HashSet<int>();
    private readonly Dictionary<ReefTarget,bool> reefPreviousOccupancy = new Dictionary<ReefTarget,bool>();
    private string BranchKey(ReefTarget t) => t.node.name + " " + t.level;
    private readonly Dictionary<string,int> reefBranchMisses = new Dictionary<string,int>();
    private static readonly float[] reefRetryOffsets = { 0f, -0.02f, 0.02f, -0.04f, 0.04f };
    private float ActiveRetryOffset(ReefTarget t)
    {
        if (!IsCompleteReef || t == null) return 0f;
        reefBranchMisses.TryGetValue(BranchKey(t), out int misses);
        return reefRetryOffsets[misses % reefRetryOffsets.Length];
    }
    private Vector2 ActiveCoralPosition(ReefTarget t)
    {
        float heading = t.node.headingDegrees * Mathf.Deg2Rad;
        Vector2 outward = -new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
        return t.positionXZ + outward * ActiveRetryOffset(t);
    }
    private bool ReefReady(string key) => !reefCooldowns.TryGetValue(key, out float until) || Time.time >= until;
    private bool ReefOccupied(ReefTarget t)
    {
        if (sharedMatch == null || sharedMatch.alliance != MapAlliance()) return IsBranchOccupied(t);
        var slot = sharedMatch.GetReefSlot(t.node.faceNumber,
            t.node.branchSide == FieldMap2D.ReefBranchSide.Left ? MatchState.ReefScoringTarget.Left : MatchState.ReefScoringTarget.Right,
            (MatchState.ReefLevel)Enum.Parse(typeof(MatchState.ReefLevel), t.level.ToString()));
        return slot != null ? slot.occupied : IsBranchOccupied(t);
    }
    private void ReefEvent(string kind, string detail)
    {
        string line = $"REEF PLAN: {kind}: {detail} | coral attempts={reefCoralAttempts}, misses={reefCoralMisses}, algae attempts={reefAlgaeAttempts}, intake failures={reefAlgaeFailures}, processor failures={reefProcessorFailures}, other failures={reefOtherFailures}, retries={reefRetries}";
        Debug.Log(line);
        if (string.IsNullOrEmpty(reefLogPath)) return;
        try { System.IO.File.AppendAllText(reefLogPath, $"{Time.time-reefStarted:F2},\"{kind}\",\"{detail.Replace("\"", "\"\"")}\",{reefCoralAttempts},{reefCoralMisses},{reefAlgaeAttempts},{reefAlgaeFailures},{reefProcessorFailures},{reefOtherFailures},{reefRetries}\n"); }
        catch (Exception e) { Debug.LogWarning("REEF PLAN: CSV write failed: " + e.Message); reefLogPath = null; }
    }
    private void ReefProgress()
    {
        int l2=0,l3=0,l4=0;
        foreach (var t in reefTargets)
        {
            bool occupied = ReefOccupied(t);
            if (reefPreviousOccupancy.TryGetValue(t,out bool old) && old && !occupied) ReefEvent("coral_lost", BranchKey(t));
            reefPreviousOccupancy[t]=occupied;
            if (!occupied) continue;
            if (t.level==CoralLevel.L2) l2++; else if (t.level==CoralLevel.L3) l3++; else l4++;
        }
        ReefEvent("progress", $"L2={l2}/12 L3={l3}/12 L4={l4}/12 processed={reefProcessed.Count}/6");
    }
    private void BeginCompleteReef()
    {
        BuildReefTargets();
        sharedMatch = FindFirstObjectByType<MatchState>();
        algaeNode = robotGamePieceController.GetPieceByName("Algae");
        foreach (var scorer in FindObjectsByType<ProcessorScorer>(FindObjectsSortMode.None))
            if (scorer.Alliance == robot.Alliance) { processor=scorer; break; }
        reefBranchMisses.Clear();
        reefStarted=Time.time;
        reefLogPath=System.IO.Path.Combine(Application.persistentDataPath, "complete_reef_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".csv");
        try { System.IO.File.WriteAllText(reefLogPath,"elapsed_s,event,detail,coral_attempts,coral_misses,algae_attempts,algae_intake_failures,processor_failures,other_failures,retries\n"); }
        catch(Exception e) { Debug.LogWarning(e.Message); reefLogPath=null; }
        reefActive=true; state=AutoState.Waiting;
        ReefEvent("start", "CSV: "+reefLogPath+"; mapped branches="+reefTargets.Count);
    }
    private void ReefReplan(string reason, bool failed)
    {
        drivebase.Stop();
        robot.AutonomousReefAlgaeIntake=false;
        robot.AutonomousGroundAlgaeIntake=false;
        robot.AutonomousHoldGroundIntakePose=false;
        robot.AutonomousDelayAlgaeHome=false;
        robot.AutonomousLowAlgaeCarry=false;
        robot.CancelReefPoseLock();
        ForceRobotState(ReefscapeSetpoints.Stow);
        if (failed) { reefRetries++; if (reefTaskKey!=null) reefCooldowns[reefTaskKey]=Time.time+reefRetryCooldown; }
        ReefEvent(failed ? "retry" : "task_done", reason);
        algaeRunning=false; groundPhase=GroundPhase.Off; reefFloorRecovery=false;
        currentReefTarget=null; targetCoral=null; releasedCoral=null; clearTarget=null;
        state=AutoState.Waiting; reefNextPlan=Time.time+Mathf.Max(1f,scorePrepSeconds);
        if (sharedMatch!=null) sharedMatch.ClearCurrentTask();
    }
    private void ReefPlan()
    {
        if (Time.time<reefNextPlan) return;
        reefNextPlan=Time.time+1f;
        if (sharedMatch==null || sharedMatch.alliance!=MapAlliance() || algaeNode==null || processor==null || reefTargets.Count!=36)
        { ReefEvent("waiting", "Missing alliance state, controller, Processor or 36 mapped branches"); return; }
        if (!sharedMatch.allianceState.reefAlgaeInitialized) return;
        if (reefOriginalAlgae.Count==0)
        {
            for(int f=1;f<=6;f++) { var face=sharedMatch.GetReefFace(f); if(face!=null && face.algaePresent && face.algaePiece!=null) reefOriginalAlgae[f]=face.algaePiece; }
            if (reefOriginalAlgae.Count!=6) { reefOriginalAlgae.Clear(); ReefEvent("waiting", "Requires six original Reef Algae at start"); return; }
        }
        bool all=true; foreach(var t in reefTargets) if(!ReefOccupied(t)) { all=false; break; }
        if(all && reefProcessed.Count==6) { ReefProgress(); ReefEvent("complete","All 36 branches occupied and six original Algae processed"); reefFinished=true; state=AutoState.Done; drivebase.Stop(); return; }
        reefTaskStarted=Time.time;
        if(algaeNode.HasPiece()) { reefTaskKey="algae "+reefFaceGoal; BeginAlgae(); return; }
        ReefTarget best=null; float cost=float.PositiveInfinity;
        foreach(var t in reefTargets)
        {
            if(ReefOccupied(t) || (t.level!=CoralLevel.L4 && IsFaceBlocked(t.node)) || !ReefReady(BranchKey(t))) continue;
            float d=Vector2.Distance(CurrentPosition(),t.positionXZ);
            if(d<cost) { cost=d; best=t; }
        }
        int faceGoal=0; float algaeCost=float.PositiveInfinity;
        foreach(var n in fieldMap.reefL1ScoringNodes)
        {
            if(n.alliance!=MapAlliance() || reefProcessed.Contains(n.faceNumber) || !ReefReady("algae "+n.faceNumber)) continue;
            var face=sharedMatch.GetReefFace(n.faceNumber);
            var piece=reefOriginalAlgae[n.faceNumber];
            if(piece==null) continue;
            bool onReef=face!=null && face.algaePresent && face.algaePiece==piece;
            if(!onReef && (piece.GetComponentInParent<RobotBase>()!=null || piece.transform.position.y>floorMaxHeight)) continue;
            float d=Vector2.Distance(CurrentPosition(),onReef?n.positionXZ:ToXZ(piece.transform.position));
            if(d<algaeCost) { algaeCost=d; faceGoal=n.faceNumber; }
        }
        if(!coralNode.HasPiece() && faceGoal!=0 && (best==null || algaeCost<=cost || FindNearestLooseCoral()==null))
        {
            reefFaceGoal=faceGoal; reefTaskKey="algae "+faceGoal;
            ReefEvent("select",reefTaskKey);
            var face=sharedMatch.GetReefFace(faceGoal);
            if(face!=null && face.algaePresent && face.algaePiece==reefOriginalAlgae[faceGoal]) BeginAlgae();
            else { reefFloorRecovery=true; groundTarget=reefOriginalAlgae[faceGoal]; algaeTarget=groundTarget; reefAlgaeAttempts++; ReefEvent("algae_intake_attempt","floor recovery "+faceGoal); BeginFloorPickup(); }
            return;
        }
        if(best==null || (!coralNode.HasPiece() && FindNearestLooseCoral()==null)) { ReefEvent("waiting", "No accessible branch or loose Coral; rechecking"); return; }
        clearTarget=best; reefTaskKey=BranchKey(best); coralScoringLevel=best.level;
        ReefEvent("select",reefTaskKey);
        ForceRobotMode(ReefscapeRobotMode.Coral); ForceRobotState(ReefscapeSetpoints.Stow);
        if(coralNode.HasPiece()) StartDriveToClosestFreeBranch(); else state=AutoState.SearchingCoral;
    }
    private void StopClear(string reason)
    {
        drivebase.Stop();
        ForceRobotState(ReefscapeSetpoints.Stow);
        state = AutoState.Done;
        if (sharedMatch != null) sharedMatch.ClearCurrentTask();
        Debug.LogWarning("CLEAR CORAL: " + reason);
    }

    private void PlanClearTarget()
    {
        if (IsBranchOccupied(clearTarget)) { StopClear("Requested branch occupied"); return; }
        bool blocked = clearTarget.level != CoralLevel.L4 && IsFaceBlocked(clearTarget.node);
        Debug.Log($"CLEAR CORAL: target {clearTarget.node.name} {clearTarget.level}; blocked={blocked}");
        if (blocked && coralNode.HasPiece())
        {
            clearPreload = true;
            coralScoringLevel = CoralLevel.L4;
            StartDriveToClosestFreeBranch();
            return;
        }
        clearPreload = false;
        if (blocked) { BeginAlgae(); return; }
        StartClearCoral();
    }

    private void StartClearCoral()
    {
        if (IsBranchOccupied(clearTarget) || (clearTarget.level != CoralLevel.L4 && IsFaceBlocked(clearTarget.node)))
        { StopClear("Requested branch is not available"); return; }
        coralScoringLevel = clearTarget.level;
        ForceRobotMode(ReefscapeRobotMode.Coral);
        ForceRobotState(ReefscapeSetpoints.Stow);
        Debug.Log("CLEAR CORAL: branch available; acquiring Coral for original target");
        if (coralNode.HasPiece()) StartDriveToClosestFreeBranch();
        else state = AutoState.SearchingCoral;
    }

    [Header("Coral Scoring Settings")]
    [SerializeField] private CoralLevel coralScoringLevel = CoralLevel.L4;

    // Optional radial offsets (positive = farther from reef).
    [SerializeField] private float l2ApproachOffset = -0.03f;
    [SerializeField] private float l3ApproachOffset = -0.03f;

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
    private float lowerCoralPositionTolerance = 0.010f;

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


    [Header("Ground Algae Test")]
    [SerializeField] private float groundPrepDistance = 1.2f;
    [SerializeField] private float groundFinalDistance = 0.55f;
    [SerializeField] private float groundPrepTime = 3f;
    [SerializeField] private float groundApproachSpeed = 0.20f;
    [SerializeField] private float groundApproachTimeout = 35f;
    [SerializeField] private float groundMinHeight = 0.35f;
    [SerializeField] private float groundMaxHeight = 0.85f;
    [Header("Floor Intake Test")]
    [SerializeField] private float floorDropTravel = 1.2f;
    [SerializeField] private float floorSettleTime = 2f;
    [SerializeField] private float floorMaxHeight = 0.34f;
    [SerializeField] private float floorFinalDistance = 0.45f;
    [SerializeField] private float floorApproachSpeed = 0.55f;
    [SerializeField] private float floorDropReverseDistance = 1f;
    private Vector2 floorDropReverseStart;
    private Vector2 floorDropForward;
    private bool IsFloorTest => operatingMode == OperatingMode.FloorIntakeTest;
    private enum GroundPhase { Off, Drive, Prepare, Approach, Retreat, Done, DropDrive, DropPrepare, DropSettle, DropTurn, DropReverse }
    private GroundPhase groundPhase;
    private float groundSince;
    private GameObject groundTarget;
    private Vector2 groundStart;
    private Vector2 groundDirection;
    private void GroundTransition(GroundPhase phase)
    {
        groundPhase = phase; groundSince = Time.time;
        Debug.Log("GROUND ALGAE: " + phase);
        if (IsFloorTest && (phase == GroundPhase.Prepare || phase == GroundPhase.Approach)) Debug.Log(robot.AutonomousGroundIntakeDiagnostic());
    }
    private void FinishGround(string reason)
    {
        if(IsCompleteReef) { reefAlgaeFailures++; ReefReplan(reason,true); return; }
        drivebase.Stop(); robot.AutonomousGroundAlgaeIntake = false;
        robot.AutonomousHoldGroundIntakePose = false;
        robot.AutonomousDelayAlgaeHome = false;
        ForceRobotState(ReefscapeSetpoints.Stow);
        if (sharedMatch != null) sharedMatch.ClearCurrentTask();
        GroundTransition(GroundPhase.Done); Debug.Log("GROUND ALGAE: " + reason);
    }
    private void BeginFloorDrop()
    {
        drivebase.Stop();
        robot.AutonomousDelayAlgaeHome = false;
        robot.AutonomousReefAlgaeIntake = false;
        algaeRunning = false;
        ForceRobotState(ReefscapeSetpoints.Stow);
        groundTarget = algaeTarget;
        algaeGoal = CurrentPosition() - algaeInward * floorDropTravel;
        algaeHeading = HeadingFromDirection(algaeInward);
        if (!drivebase.GoTo(algaeGoal, algaeHeading, algaePositionTolerance, algaeHeadingTolerance, true))
        { FinishGround("Floor drop path failed"); return; }
        GroundTransition(GroundPhase.DropDrive);
    }
    private void BeginFloorPickup()
    {
        if (algaeNode.HasPiece()) { FinishGround("Invalid test: robot already holding Algae before pickup"); return; }
        if (groundTarget == null || groundTarget.GetComponentInParent<RobotBase>() != null || groundTarget.transform.position.y > floorMaxHeight)
        { FinishGround("Released Algae not detected on floor"); return; }
        Vector2 target = ToXZ(groundTarget.transform.position);
        float separation = Vector2.Distance(target, CurrentPosition());
        if (!reefFloorRecovery && separation < groundPrepDistance) { FinishGround($"Invalid test: floor separation only {separation:F2} m"); return; }
        Debug.Log($"FLOOR TEST: empty robot confirmed; target separation {separation:F2} m; enabling intake");
        groundDirection = (target - CurrentPosition()).normalized;
        if (groundDirection.sqrMagnitude < 0.01f) { FinishGround("Floor target too close to determine heading"); return; }
        algaeHeading = HeadingFromDirection(groundDirection);
        algaeGoal = target - groundDirection * groundPrepDistance;
        ForceRobotMode(ReefscapeRobotMode.Algae);
        robot.AutonomousHoldGroundIntakePose = true;
        ForceRobotState(ReefscapeSetpoints.Intake);
        robot.AutonomousDelayAlgaeHome = true;
        robot.AutonomousGroundAlgaeIntake = true;
        if (!drivebase.GoTo(algaeGoal, algaeHeading, algaePositionTolerance, algaeHeadingTolerance, true))
        { FinishGround("Floor pickup path failed"); return; }
        GroundTransition(GroundPhase.Drive);
    }
    private void BeginGroundTest()
    {
        groundTarget = null;
        algaeRunning = false;
        sharedMatch = FindFirstObjectByType<MatchState>();
        if (sharedMatch == null || sharedMatch.alliance != MapAlliance()) { FinishGround("No MatchState for robot alliance"); return; }
        processor = null;
        foreach (var scorer in FindObjectsByType<ProcessorScorer>(FindObjectsSortMode.None))
            if (scorer.Alliance == robot.Alliance) { processor = scorer; break; }
        if (processor == null) { FinishGround("Alliance Processor not found"); return; }
        algaeNode = robotGamePieceController.GetPieceByName("Algae");
        if (algaeNode == null || coralNode.HasPiece() || algaeNode.HasPiece())
        { FinishGround("Test requires empty robot and Algae controller; disable preloaded Coral."); return; }
        ForceRobotMode(ReefscapeRobotMode.Algae);
        ForceRobotState(ReefscapeSetpoints.Stow);
        float best = float.PositiveInfinity;
        foreach (var piece in GameObject.FindGameObjectsWithTag("Algae"))
        {
            if (piece.GetComponentInParent<RobotBase>() != null) continue;
            float y = piece.transform.position.y;
            if (y < groundMinHeight || y > groundMaxHeight) continue;
            float d = Vector2.Distance(CurrentPosition(), ToXZ(piece.transform.position));
            if (d < best && d > groundFinalDistance) { best = d; groundTarget = piece; }
        }
        if (groundTarget == null) { FinishGround("No elevated Algae found in height range."); return; }
        groundDirection = (ToXZ(groundTarget.transform.position) - CurrentPosition()).normalized;
        algaeHeading = HeadingFromDirection(groundDirection);
        algaeGoal = ToXZ(groundTarget.transform.position) - groundDirection * groundPrepDistance;
        Debug.Log($"GROUND ALGAE: target {groundTarget.name}, height {groundTarget.transform.position.y:F3} m");
        if (!drivebase.GoTo(algaeGoal, algaeHeading, algaePositionTolerance, algaeHeadingTolerance, true))
        { FinishGround("Preparation path failed"); return; }
        sharedMatch.SetCurrentTask(MatchState.RobotTask.AcquireAlgae, groundTarget.name);
        GroundTransition(GroundPhase.Drive);
    }
    private void UpdateGroundTest()
    {
        if (sharedMatch != null && algaeNode != null) sharedMatch.SetCarriedGamePiece(algaeNode.HasPiece() ? MatchState.CarriedGamePiece.Algae : MatchState.CarriedGamePiece.None);
        float elapsed = Time.time - groundSince;
        if ((IsFloorTest || reefFloorRecovery) && (groundPhase == GroundPhase.Drive || groundPhase == GroundPhase.Prepare || groundPhase == GroundPhase.Approach))
        {
            ForceRobotMode(ReefscapeRobotMode.Algae);
            robot.AutonomousHoldGroundIntakePose = true;
            if (robot.CurrentSetpoint != ReefscapeSetpoints.Intake) ForceRobotState(ReefscapeSetpoints.Intake);
            robot.AutonomousGroundAlgaeIntake = !algaeNode.HasPiece();
        }
        switch (groundPhase)
        {
            case GroundPhase.DropDrive:
                if (!algaeNode.HasPiece() || elapsed > 60f) { FinishGround("Drop navigation failed"); return; }
                if (!drivebase.IsDriving())
                {
                    if (Vector2.Distance(CurrentPosition(), algaeGoal) > 0.15f) { FinishGround("Drop goal not reached"); return; }
                    drivebase.Stop();
                    algaeHeading = HeadingFromDirection(-algaeInward);
                    GroundTransition(GroundPhase.DropTurn);
                }
                break;
            case GroundPhase.DropTurn:
                if (!algaeNode.HasPiece() || elapsed > 20f) { FinishGround("Drop turn failed"); return; }
                if (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, algaeHeading)) <= algaeHeadingTolerance)
                {
                    drivebase.Stop(); ForceRobotMode(ReefscapeRobotMode.Algae);
                    robot.AutonomousHoldGroundIntakePose = true;
                    ForceRobotState(ReefscapeSetpoints.Intake);
                    GroundTransition(GroundPhase.DropPrepare);
                }
                break;
            case GroundPhase.DropPrepare:
                if (!algaeNode.HasPiece() || elapsed > 15f) { FinishGround("Drop preparation failed"); return; }
                if (elapsed >= groundPrepTime && algaeNode.atTarget && algaeNode.ReleaseGamePieceWithForce(new Vector3(0, 0.5f, 0)))
                {
                    robot.AutonomousGroundAlgaeIntake = false;
                    robot.AutonomousReefAlgaeIntake = false;
                    floorDropReverseStart = CurrentPosition();
                    float rad = algaeHeading * Mathf.Deg2Rad;
                    floorDropForward = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));
                    GroundTransition(GroundPhase.DropReverse);
                }
                break;
            case GroundPhase.DropReverse:
                if (algaeNode.HasPiece()) { FinishGround("Invalid test: Algae still held after release"); return; }
                if (elapsed > 20f) { FinishGround("Drop reverse timeout"); return; }
                if (Vector2.Dot(floorDropReverseStart - CurrentPosition(), floorDropForward) >= floorDropReverseDistance)
                {
                    drivebase.Stop();
                    robot.AutonomousHoldGroundIntakePose = false;
                    robot.AutonomousDelayAlgaeHome = false;
                    ForceRobotState(ReefscapeSetpoints.Stow);
                    Debug.Log("FLOOR TEST: reverse complete; robot empty; intake disabled");
                    GroundTransition(GroundPhase.DropSettle);
                }
                break;
            case GroundPhase.DropSettle:
                if (elapsed >= floorSettleTime) BeginFloorPickup();
                break;
            case GroundPhase.Drive:
                if ((IsFloorTest || reefFloorRecovery) && algaeNode.HasPiece())
                { drivebase.Stop(); robot.AutonomousGroundAlgaeIntake = false; groundStart = CurrentPosition(); GroundTransition(GroundPhase.Retreat); break; }
                if (elapsed > 60f) { FinishGround("Navigation timeout"); return; }
                if (!drivebase.IsDriving())
                {
                    if (Vector2.Distance(CurrentPosition(), algaeGoal) > 0.15f) { FinishGround("Preparation goal not reached"); return; }
                    drivebase.Stop();
                    ForceRobotState((IsFloorTest || reefFloorRecovery) ? ReefscapeSetpoints.Intake : ReefscapeSetpoints.Stack);
                    GroundTransition(GroundPhase.Prepare);
                }
                break;
            case GroundPhase.Prepare:
                if ((IsFloorTest || reefFloorRecovery) && elapsed > 15f) { Debug.Log(robot.AutonomousGroundIntakeDiagnostic()); FinishGround("Ground intake joints not ready"); return; }
                if (elapsed >= groundPrepTime && (!(IsFloorTest || reefFloorRecovery) || robot.AutonomousGroundIntakeAnglesReady()))
                { robot.AutonomousDelayAlgaeHome = true; robot.AutonomousGroundAlgaeIntake = true; groundStart = CurrentPosition(); GroundTransition(GroundPhase.Approach); }
                break;
            case GroundPhase.Approach:
                if (algaeNode.HasPiece())
                { drivebase.Stop(); robot.AutonomousGroundAlgaeIntake = false; groundStart = CurrentPosition(); GroundTransition(GroundPhase.Retreat); }
                else if (elapsed > groundApproachTimeout || Vector2.Dot(CurrentPosition()-groundStart, groundDirection) >= Mathf.Max(0f, groundPrepDistance-((IsFloorTest || reefFloorRecovery) ? floorFinalDistance : groundFinalDistance)))
                    FinishGround("Acquisition not confirmed");
                break;
            case GroundPhase.Retreat:
                if (!algaeNode.HasPiece()) { FinishGround("Lost Algae during retreat"); return; }
                if (Vector2.Dot(groundStart-CurrentPosition(), groundDirection) >= algaeRetreatDistance)
                {
                    drivebase.Stop();
                    robot.AutonomousGroundAlgaeIntake = false;
                    robot.AutonomousDelayAlgaeHome = false;
                    if (IsFloorTest) { FinishGround("Floor Algae reacquired successfully"); return; }
                    robot.AutonomousHoldGroundIntakePose=false; reefFloorRecovery=false;
                    groundPhase = GroundPhase.Off;
                    algaeRunning = true;
                    Debug.Log("GROUND ALGAE: Acquired; beginning scoring");
                    BeginProcessorPreparation();
                }
                else if (elapsed > 30f) FinishGround("Retreat timeout");
                break;
        }
    }

    private bool IsGroundTestMode => operatingMode == OperatingMode.ElevatedIntakeTest;

    private void BeginAutonomy()
    {
        if (IsCompleteReef) { BeginCompleteReef(); return; }
        if (IsClearTest)
        {
            BuildReefTargets();
            clearTarget = reefTargets.Find(t => t.node.faceNumber == targetReefFace && t.node.branchSide == targetBranchSide && t.level == targetCoralLevel);
            if (clearTarget == null) { StopClear("Requested branch not mapped"); return; }
            clearStarted = Time.time;
            PlanClearTarget();
            return;
        }
        if (operatingMode != OperatingMode.ScoreCoral)
        {
            if (!coralNode.HasPiece()) { if (IsGroundTestMode) { nextScoreNet = false; BeginGroundTest(); } else BeginAlgae(); return; }
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
        if (IsCompleteReef && reefActive)
        {
            if (reefFinished) return;
            if(sharedMatch!=null && sharedMatch.matchTimeRemaining>0f) reefClockSeen=true;
            if(reefClockSeen && sharedMatch.matchTimeRemaining<=0f) { ReefReplan("Match time expired",false); ReefProgress(); reefFinished=true; return; }
            if(Time.time>=reefNextProgress) { reefNextProgress=Time.time+reefProgressInterval; ReefProgress(); }
            if(!algaeRunning && groundPhase==GroundPhase.Off)
            {
                if(state==AutoState.Waiting) { ReefPlan(); return; }
                if(state==AutoState.Done || (state==AutoState.SearchingCoral && coralNode.HasPiece()) || Time.time-reefTaskStarted>reefCoralTaskTimeout)
                { reefOtherFailures++; ReefReplan("Coral task interrupted or timed out: "+state,true); return; }
            }
        }
        if (groundPhase != GroundPhase.Off) { UpdateGroundTest(); return; }
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
                if (IsGroundTestMode || IsCombinedTestMode)
                { state = AutoState.Done; Debug.LogWarning("GROUND ALGAE: Coral lost before scoring; test stopped."); }
                else BeginAlgae();
            }
            return;
        }
        if (IsClearTest && state != AutoState.Done && clearTarget != null)
        {
            if (Time.time - clearStarted > 240f) { StopClear("Test timeout"); return; }
            if (state == AutoState.SearchingCoral && coralNode.HasPiece())
            { StopClear("Coral held but scoring path failed"); return; }
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
        if (groundPhase != GroundPhase.Off)
        {
            if (groundPhase == GroundPhase.Approach) LocalAlgaeDrive(algaeNode.HasPiece() ? Vector2.zero : groundDirection * Mathf.Clamp01((IsFloorTest || reefFloorRecovery) ? floorApproachSpeed : groundApproachSpeed));
            else if (groundPhase == GroundPhase.Retreat) LocalAlgaeDrive(-groundDirection * algaeRetreatSpeed);
            else if (groundPhase == GroundPhase.DropReverse) LocalAlgaeDrive(-floorDropForward * algaeRetreatSpeed);
            else if (groundPhase == GroundPhase.DropTurn) LocalAlgaeDrive(Vector2.zero);
            else if (groundPhase != GroundPhase.Drive && groundPhase != GroundPhase.DropDrive) driveController.overideInput(Vector2.zero, 0f, DriveController.DriveMode.FieldOriented);
            return;
        }
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
                LocalAlgaeDrive(Vector2.Dot(algaeRetreatStart - CurrentPosition(), algaeInward) < (algaePhase == AlgaePhase.Retreat ? Mathf.Max(0.40f, Mathf.Max(algaeRetreatDistance, algaeHoldClearance)) : algaeRetreatDistance) ? -algaeInward * algaeRetreatSpeed : Vector2.zero);
            else if (algaePhase == AlgaePhase.ProcessorApproach)
            {
                Vector2 error = processorFinalGoal - CurrentPosition();
                float remaining = Vector2.Dot(error, processorInward);
                float angle = Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, algaeHeading));
                Vector2 lateral = error - remaining * processorInward;
                if (!algaeNode.HasPiece() || remaining <= Mathf.Sqrt(Mathf.Max(0f, processorPositionTolerance * processorPositionTolerance - lateral.sqrMagnitude)) ||
                    angle > processorHeadingTolerance || lateral.magnitude > processorPositionTolerance)
                    LocalAlgaeDrive(Vector2.zero);
                else
                    LocalAlgaeDrive(processorInward * Mathf.Min(Mathf.Clamp01(processorFinalApproachSpeed), Mathf.Max(0.05f, remaining * preciseDriveKp)));
            }
            else if (algaePhase == AlgaePhase.ProcessorStagePrepare)
                LocalAlgaeDrive(Vector2.zero);
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
            if(IsCompleteReef) { reefOtherFailures++; ReefReplan("Coral pickup path failed",true); return; }
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

            if(IsCompleteReef) { reefOtherFailures++; ReefReplan("Coral intake claim failed",true); return; }
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
                float heading = node.headingDegrees * Mathf.Deg2Rad;
                Vector2 outward = -new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
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
        if ((IsClearTest && !clearPreload) || IsCompleteReef)
            return clearTarget != null && !IsBranchOccupied(clearTarget) && (clearTarget.level == CoralLevel.L4 || !IsFaceBlocked(clearTarget.node)) ? clearTarget : null;
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
                ActiveCoralPosition(currentReefTarget) -
                reefCenter
            ).normalized;


        reefStagePosition =
            ActiveCoralPosition(currentReefTarget) +
            outward *
            reefStageDistance;


        ForceRobotMode(
            ReefscapeRobotMode.Coral
        );


        ForceRobotState(
            ReefscapeSetpoints.Stow
        );


        if (IsCompleteReef)
            ReefEvent("coral_retry_offset", $"{BranchKey(currentReefTarget)}; offset_m={ActiveRetryOffset(currentReefTarget):F3}");
        bool started =
            drivebase.GoTo(
                ActiveCoralPosition(currentReefTarget),
                node.headingDegrees, CoralPositionTolerance(), preciseHeadingTolerance, true
            );


        if (!started)
        {
            Debug.LogError(
                "AUTODRIVER: could not plan " +
                "to exact Reef scoring pose."
            );

            if (IsCompleteReef)
            {
                string key = BranchKey(currentReefTarget);
                reefBranchMisses.TryGetValue(key, out int misses);
                reefBranchMisses[key] = misses + 1;
                reefOtherFailures++;
                ReefReplan("Exact Coral scoring pose unreachable; advancing retry offset", true);
                return;
            }
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
                ActiveCoralPosition(currentReefTarget),
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

    private float CoralPositionTolerance()
    {
        return lowerCoralPositionTolerance;
    }

    private void ApplyPreciseReefAlignment()
    {
        if (currentReefTarget == null)
            return;


        Vector2 target =
            ActiveCoralPosition(currentReefTarget);


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
            CoralPositionTolerance();


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


        if (!robot.AutonomousMechanismAnglesReady())
        {
            if (Time.time - stateStartTime > 10f)
            {
                drivebase.Stop();
                state = AutoState.Done;
                Debug.LogWarning($"AUTODRIVER: {currentReefTarget.level} Coral scoring angles not ready; release cancelled.");
                if (IsCompleteReef) ReefEvent("coral_readiness_timeout", BranchKey(currentReefTarget));
            }
            return;
        }
        if (coralNode.controller != null && currentReefTarget.scoringTrigger != null)
        {
            Vector3 piece = coralNode.controller.transform.position;
            Vector3 branch = currentReefTarget.scoringTrigger.transform.TransformPoint(currentReefTarget.scoringTrigger.center);
            Debug.Log($"CORAL ALIGN: {currentReefTarget.level}, piece={piece:F3}, branch={branch:F3}, world delta={branch-piece:F3}");
        }
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


        if(IsCompleteReef) { reefCoralAttempts++; ReefEvent("coral_release",reefTaskKey); }
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


        if(IsCompleteReef) ReefEvent("coral_initial_check", success ? "occupied" : "not yet confirmed");
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
        if(IsCompleteReef)
        {
            success=IsSpecificCoralInBranch(releasedCoral,currentReefTarget) && !coralNode.HasPiece();
            string key = BranchKey(currentReefTarget);
            if (!success)
            {
                reefCoralMisses++;
                reefBranchMisses.TryGetValue(key, out int misses);
                reefBranchMisses[key] = misses + 1;
            }
            else reefBranchMisses.Remove(key);
            ReefEvent(success?"coral_score_success":"coral_score_failed",reefTaskKey);
            ReefReplan(success?"Coral retained":"Coral missed or fell",!success); return;
        }
        if (IsClearTest)
        {
            success = IsSpecificCoralInBranch(releasedCoral, currentReefTarget);
            if (!success || coralNode.HasPiece()) { StopClear("Coral score not retained"); return; }
            if (clearPreload)
            {
                Debug.Log("CLEAR CORAL: preloaded Coral scored; clearing requested face");
                clearPreload = false;
                currentReefTarget = null;
                PlanClearTarget();
            }
            else StopClear("SUCCESS: requested Coral branch scored and retained after retreat");
            return;
        }
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
            if (IsCombinedTestMode && (!success || coralNode.HasPiece()))
            {
                state = AutoState.Done;
                Debug.LogWarning("ALGAE: Combined test stopped; initial Coral score not confirmed.");
                return;
            }
            if (IsGroundTestMode)
            {
                if (!success || coralNode.HasPiece())
                {
                    state = AutoState.Done;
                    Debug.LogWarning("GROUND ALGAE: Coral score not confirmed or Coral still held; test stopped.");
                    return;
                }
                Debug.Log("GROUND ALGAE: Preloaded Coral scored; beginning ground intake test.");
                BeginGroundTest();
                return;
            }
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
        nextScoreNet = false;
        combinedScores = 0;
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
        if(IsCompleteReef)
        {
            if(algaePhase==AlgaePhase.Release || algaePhase==AlgaePhase.Verify || algaePhase==AlgaePhase.ProcessorApproach || algaePhase==AlgaePhase.ProcessorAlign || algaePhase==AlgaePhase.ProcessorDrive || algaePhase==AlgaePhase.ProcessorPrepare || algaePhase==AlgaePhase.ProcessorStagePrepare) reefProcessorFailures++;
            else reefOtherFailures++;
            ReefReplan(reason,true); return;
        }
        robot.CancelReefPoseLock();
        robot.AutonomousDelayAlgaeHome = false;
        robot.AutonomousReefAlgaeIntake = false;
        robot.AutonomousLowAlgaeCarry = false;
        drivebase.Stop(); ForceRobotState(ReefscapeSetpoints.Stow);
        SetAlgaePhase(AlgaePhase.Done);
        if (sharedMatch != null) sharedMatch.ClearCurrentTask();
        Debug.LogWarning("ALGAE: " + reason);
    }
    private void AdvanceCombinedTest(bool scoredNet)
    {
        // Count only verified scores, in the required Processor/Net order.
        if (combinedScores >= 4 || scoredNet != (combinedScores % 2 == 1))
        { StopAlgae("Combined test: unexpected scoring destination"); return; }
        combinedScores++;
        Debug.Log($"ALGAE: Combined test verified {combinedScores}/4 scores");
        if (combinedScores == 4)
        { StopAlgae("Combined test complete: two Processor and two Net scores"); return; }
        nextScoreNet = combinedScores % 2 == 1;
        drivebase.Stop();
        ForceRobotState(ReefscapeSetpoints.Stow);
        if (combinedScores == 1)
        {
            failedAlgaeFaces.Clear(); algaeTarget = null; algaeFace = 0;
            SetAlgaePhase(AlgaePhase.Search);
        }
        else BeginGroundTest();
    }
    private void BeginProcessorPreparation()
    {
        drivebase.Stop();
        robot.AutonomousDelayAlgaeHome = false;
        robot.AutonomousLowAlgaeCarry = false;
        if (nextScoreNet) { DriveNet(); return; }
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
        processorInward = new Vector2(0, sign);
        processorFinalGoal = best.positionXZ - processorInward * processorStandOff;
        algaeGoal = best.positionXZ - processorInward * Mathf.Max(processorStagingStandOff, processorStandOff + 0.05f);
        algaeHeading = sign > 0 ? 0 : 180;
        if (sharedMatch != null) sharedMatch.SetCurrentTask(MatchState.RobotTask.ScoreAlgaeProcessor, best.name);
        if (!drivebase.GoTo(algaeGoal, algaeHeading, processorPositionTolerance, processorHeadingTolerance, true)) { StopAlgae("Processor path failed; check exact goal clearance"); return; }
        SetAlgaePhase(AlgaePhase.ProcessorDrive);
    }
    private void DriveNet()
    {
        string regionName = robot.Alliance == Alliance.Blue ? "Blue Barge Scoring Region" : "Red Barge Scoring Region";
        var region = fieldMap.taskRegions.Find(r => r.type == FieldMap2D.TaskType.Net && r.name == regionName);
        if (region == null || region.vertices.Count == 0) { StopAlgae("Alliance Net region missing"); return; }
        Vector2 center = Vector2.zero;
        foreach (var vertex in region.vertices) center += vertex;
        center /= region.vertices.Count;
        float side = robot.Alliance == Alliance.Blue ? 1f : -1f;
        algaeGoal = center + new Vector2(side * bargeStandOff, 0f);
        algaeHeading = (side > 0 ? -90f : 90f) + bargeHeadingOffset;
        ForceRobotState(ReefscapeSetpoints.Stow);
        sharedMatch.SetCurrentTask(MatchState.RobotTask.ScoreAlgaeNet, regionName);
        if (!drivebase.GoTo(algaeGoal, algaeHeading, processorPositionTolerance, processorHeadingTolerance, true))
        { StopAlgae("Net path failed; check stand-off clearance"); return; }
        SetAlgaePhase(AlgaePhase.NetDrive);
    }

    private bool AlignProcessor()
    {
        Vector2 error = algaeGoal - CurrentPosition();
        float angle = Mathf.DeltaAngle(transform.eulerAngles.y, algaeHeading);
        bool pos = error.magnitude <= processorPositionTolerance;
        bool heading = Mathf.Abs(angle) <= processorHeadingTolerance;
        Vector2 input = pos ? Vector2.zero : error.normalized * Mathf.Clamp(error.magnitude * preciseDriveKp, 0.08f, preciseDriveMax);
        float rotation = heading ? 0f : -Mathf.Sign(angle) * Mathf.Clamp(Mathf.Abs(angle) * preciseRotationKp, 0.08f, preciseRotationMax);
        driveController.overideInput(input, rotation, DriveController.DriveMode.FieldOriented);
        return pos && heading;
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
        if(IsCompleteReef) { if(algaePhase==AlgaePhase.Intake || algaePhase==AlgaePhase.CaptureWait) reefAlgaeFailures++; else reefOtherFailures++; ReefEvent("algae_failure",reason); }
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
        if(IsCompleteReef) { ReefReplan("Algae acquisition recovery finished",true); return; }
        robot.CancelReefPoseLock();
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
        if (reefActive && !reefFinished) ReefEvent("stopped", "Autonomy disabled");
        if (robot != null) { robot.CancelReefPoseLock(); robot.AutonomousGroundAlgaeIntake = false; robot.AutonomousReefAlgaeIntake = false; robot.AutonomousLowAlgaeCarry = false; robot.AutonomousDelayAlgaeHome = false; }
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
                drivebase.Stop(); robot.LockReefIntakePose(); robot.AutonomousReefAlgaeIntake = false;
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
        { if(IsCompleteReef) { reefOtherFailures++; ReefReplan("Recovery retreat timeout",true); return; } drivebase.Stop(); SetAlgaePhase(AlgaePhase.Done); sharedMatch.ClearCurrentTask(); Debug.LogWarning("ALGAE: Recovery retreat timeout; mechanism held."); return; }
        if (elapsed > (algaePhase == AlgaePhase.Drive || algaePhase == AlgaePhase.ProcessorDrive || algaePhase == AlgaePhase.NetDrive ? 60f : algaeTimeout)) { StopAlgae($"Timeout in {algaePhase}; position error={Vector2.Distance(CurrentPosition(), algaeGoal):F3} m, heading error={Mathf.DeltaAngle(transform.eulerAngles.y, algaeHeading):F2} deg"); return; }
        switch (algaePhase)
        {
            case AlgaePhase.Search:
                if (!sharedMatch.allianceState.reefAlgaeInitialized) break;
                FieldMap2D.ReefL1ScoringNode best = null;
                float distance = float.PositiveInfinity;
                foreach (var node in fieldMap.reefL1ScoringNodes)
                {
                    if (node.alliance != MapAlliance()) continue;
                    if (IsClearTest && node.faceNumber != clearTarget.node.faceNumber) continue;
                    if (IsCompleteReef && node.faceNumber != reefFaceGoal) continue;
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
                if (elapsed >= ActiveAlgaePrepTime) { robot.LockReefIntakePose(); algaeCreepStart = CurrentPosition(); robot.AutonomousReefAlgaeIntake = true; if(IsCompleteReef) { reefAlgaeAttempts++; ReefEvent("algae_intake_attempt",reefTaskKey); } SetAlgaePhase(AlgaePhase.Intake); } break;
            case AlgaePhase.Intake:
                if (elapsed >= algaeCreepTimeout) ReselectAlgae("Creep ended without capture", true);
                break;
            case AlgaePhase.CaptureWait:
                if (elapsed >= algaeCaptureWindow) ReselectAlgae("Capture window expired", true);
                break;
            case AlgaePhase.FailedRetreat:
                if (Vector2.Dot(algaeRetreatStart - CurrentPosition(), algaeInward) >= algaeRetreatDistance)
                { drivebase.Stop(); robot.CancelReefPoseLock(); ForceRobotState(ReefscapeSetpoints.Stow); SetAlgaePhase(AlgaePhase.FailedStow); }
                break;
            case AlgaePhase.FailedStow:
                if (elapsed >= scorePrepSeconds) FinishFailedAlgae();
                break;
            case AlgaePhase.Retreat:
                if (!algaeNode.HasPiece()) { ReselectAlgae("Lost Algae during retreat", true); return; }
                float backedAway = Vector2.Dot(algaeRetreatStart - CurrentPosition(), algaeInward);
                if (backedAway >= Mathf.Max(0.40f, Mathf.Max(algaeRetreatDistance, algaeHoldClearance)))
                {
                    drivebase.Stop();
                    robot.AutonomousDelayAlgaeHome = false;
                    robot.AutonomousLowAlgaeCarry = false;
                    ForceRobotState(IsFloorTest || nextScoreNet || !processorPoseForTransport ? ReefscapeSetpoints.Stow : ReefscapeSetpoints.Processor);
                    robot.ReleaseReefIntakePose();
                    Debug.Log($"ALGAE: {backedAway:F2} m clearance; slowly transitioning arm, wrist and extension");
                    SetAlgaePhase(AlgaePhase.ReefCarryTransition);
                }
                break;
            case AlgaePhase.ReefCarryTransition:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae during carry transition"); return; }
                if (elapsed >= Mathf.Max(2f, scorePrepSeconds) && robot.ReefCarryWristReady && algaeNode.atTarget)
                { if (IsFloorTest) BeginFloorDrop(); else BeginProcessorPreparation(); }
                break;
            case AlgaePhase.ProcessorPrepare:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae"); return; }
                if (elapsed >= scorePrepSeconds && algaeNode.atTarget) DriveProcessor(); break;
            case AlgaePhase.ProcessorDrive:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae"); return; }
                if (!drivebase.IsDriving()) { drivebase.Stop(); SetAlgaePhase(AlgaePhase.ProcessorAlign); } break;
            case AlgaePhase.ProcessorAlign:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae at Processor staging"); return; }
                if (AlignProcessor())
                {
                    Debug.Log($"PROCESSOR: aligned in {elapsed:F2}s; position error={Vector2.Distance(CurrentPosition(), algaeGoal):F3} m; heading error={Mathf.DeltaAngle(transform.eulerAngles.y, algaeHeading):F2} deg");
                    drivebase.Stop();
                    robot.AutonomousLowAlgaeCarry = false;
                    ForceRobotState(ReefscapeSetpoints.Processor);
                    SetAlgaePhase(AlgaePhase.ProcessorStagePrepare);
                }
                break;
            case AlgaePhase.ProcessorStagePrepare:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae during Processor preparation"); return; }
                if (elapsed >= scorePrepSeconds && algaeNode.atTarget)
                {
                    algaeGoal = processorFinalGoal;
                    SetAlgaePhase(AlgaePhase.ProcessorApproach);
                }
                break;
            case AlgaePhase.ProcessorApproach:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae on final Processor approach"); return; }
                Vector2 processorError = processorFinalGoal - CurrentPosition();
                float processorRemaining = Vector2.Dot(processorError, processorInward);
                float processorLateral = (processorError - processorRemaining * processorInward).magnitude;
                if (processorLateral > processorPositionTolerance || processorRemaining < -processorPositionTolerance)
                { StopAlgae("Processor approach drifted outside alignment tolerance"); return; }
                if (processorError.magnitude <= processorPositionTolerance &&
                    Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, algaeHeading)) <= processorHeadingTolerance)
                {
                    drivebase.Stop();
                    SetAlgaePhase(AlgaePhase.Release);
                }
                break;
            case AlgaePhase.Release:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae before release"); return; }
                if (elapsed < scorePrepSeconds || !algaeNode.atTarget) break;
                if(IsCompleteReef && algaeNode.controller!=null) algaeTarget=algaeNode.controller.gameObject;
                processorBefore = processor.GetScoredAlgaeCount();
                if (algaeNode.ReleaseGamePieceWithForce(new Vector3(0, algaeReleaseForce, 0))) SetAlgaePhase(AlgaePhase.Verify);
                break;
            case AlgaePhase.NetDrive:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae on Net approach"); return; }
                if (!drivebase.IsDriving())
                {
                    drivebase.Stop();
                    CheckFacingReef();
                    ForceRobotState(ReefscapeSetpoints.Barge);
                    SetAlgaePhase(AlgaePhase.NetPrepare);
                }
                break;
            case AlgaePhase.NetPrepare:
                if (!algaeNode.HasPiece()) { StopAlgae("Lost Algae during Net preparation"); return; }
                if (elapsed >= bargePrepTime && algaeNode.atTarget)
                {
                    netBefore = sharedMatch.GetNetAlgaeScored();
                    netDetectedSince = -1f;
                    if (robot.TryAutonomousBargePlace()) SetAlgaePhase(AlgaePhase.NetRelease);
                }
                break;
            case AlgaePhase.NetRelease:
                if (!robot.AutonomousBargePlacing)
                {
                    if (algaeNode.HasPiece()) { StopAlgae("Net release failed"); return; }
                    SetAlgaePhase(AlgaePhase.NetVerify);
                }
                break;
            case AlgaePhase.NetVerify:
                if (sharedMatch.GetNetAlgaeScored() > netBefore)
                {
                    if (netDetectedSince < 0f) netDetectedSince = Time.time;
                    if (Time.time - netDetectedSince >= bargeVerifyTime)
                    {
                        if (IsCombinedTestMode) AdvanceCombinedTest(true);
                        else StopAlgae("Net score confirmed; two cycles complete");
                    }
                }
                else netDetectedSince = -1f;
                break;
            case AlgaePhase.VerifyClear:
                if (algaeNode.HasPiece()) { StopAlgae("Clear test: Algae still held"); return; }
                if (elapsed >= scorePrepSeconds && !IsFaceBlocked(clearTarget.node))
                {
                    Debug.Log("CLEAR CORAL: Processor score confirmed; requested face cleared");
                    algaeRunning = false;
                    sharedMatch.ClearCurrentTask();
                    StartClearCoral();
                }
                break;
            case AlgaePhase.Verify:
                if (processor.GetScoredAlgaeCount() > processorBefore)
                {
                    Debug.Log("ALGAE: Processor count increased");
                    ForceRobotState(ReefscapeSetpoints.Stow);
                    if (IsCompleteReef)
                    {
                        foreach(var original in reefOriginalAlgae) if(algaeTarget==original.Value) reefProcessed.Add(original.Key);
                        ReefEvent("processor_score",reefTaskKey); ReefReplan("Processor score confirmed",false);
                    }
                    else if (IsClearTest) SetAlgaePhase(AlgaePhase.VerifyClear);
                    else if (IsCombinedTestMode) AdvanceCombinedTest(false);
                    else if (IsGroundTestMode)
                    {
                        nextScoreNet = true;
                        BeginGroundTest();
                    }
                    else if (operatingMode == OperatingMode.ReefAlgaeProcessorThenNet)
                    {
                        nextScoreNet = true;
                        failedAlgaeFaces.Clear(); algaeTarget = null; algaeFace = 0;
                        SetAlgaePhase(AlgaePhase.Search);
                    }
                    else if (repeatCycles) { failedAlgaeFaces.Clear(); algaeTarget = null; algaeFace = 0; SetAlgaePhase(AlgaePhase.Search); } else StopAlgae("Cycle complete");
                }
                break;
        }
    }
}
