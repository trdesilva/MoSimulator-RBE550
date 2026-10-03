using System.Collections.Generic;
using UnityEngine;

public class MatchState : MonoBehaviour
{
    // ============================================================
    // ENUMS
    // ============================================================

    public enum CarriedGamePiece
    {
        None,
        Coral,
        Algae
    }

    public enum RobotTask
    {
        Idle,
        AcquireCoral,
        ScoreCoral,
        AcquireAlgae,
        ScoreAlgaeProcessor,
        ScoreAlgaeNet,
        Climb
    }

    public enum ReefScoringTarget
    {
        Left,
        Right
    }

    public enum ReefLevel
    {
        L2,
        L3,
        L4
    }

    // ============================================================
    // REEF SLOT STATE
    // ============================================================

    [System.Serializable]
    public class ReefSlotState
    {
        public int faceNumber;
        public ReefScoringTarget target;
        public ReefLevel level;
        public bool occupied;

        public ReefSlotState(
            int faceNumber,
            ReefScoringTarget target,
            ReefLevel level)
        {
            this.faceNumber = faceNumber;
            this.target = target;
            this.level = level;
            occupied = false;
        }
    }

    // ============================================================
    // REEF FACE STATE
    // ============================================================

    [System.Serializable]
    public class ReefFaceState
    {
        public int faceNumber;

        public ReefSlotState leftL2;
        public ReefSlotState leftL3;
        public ReefSlotState leftL4;

        public ReefSlotState rightL2;
        public ReefSlotState rightL3;
        public ReefSlotState rightL4;

        // Can be removed later if we decide Reef-face Algae
        // tracking is not needed by the planner.
        public bool algaePresent;

        public ReefFaceState(int faceNumber)
        {
            this.faceNumber = faceNumber;

            leftL2 = new ReefSlotState(
                faceNumber,
                ReefScoringTarget.Left,
                ReefLevel.L2
            );

            leftL3 = new ReefSlotState(
                faceNumber,
                ReefScoringTarget.Left,
                ReefLevel.L3
            );

            leftL4 = new ReefSlotState(
                faceNumber,
                ReefScoringTarget.Left,
                ReefLevel.L4
            );

            rightL2 = new ReefSlotState(
                faceNumber,
                ReefScoringTarget.Right,
                ReefLevel.L2
            );

            rightL3 = new ReefSlotState(
                faceNumber,
                ReefScoringTarget.Right,
                ReefLevel.L3
            );

            rightL4 = new ReefSlotState(
                faceNumber,
                ReefScoringTarget.Right,
                ReefLevel.L4
            );

            algaePresent = false;
        }

        public ReefSlotState GetSlot(
            ReefScoringTarget target,
            ReefLevel level)
        {
            if (target == ReefScoringTarget.Left)
            {
                switch (level)
                {
                    case ReefLevel.L2:
                        return leftL2;

                    case ReefLevel.L3:
                        return leftL3;

                    case ReefLevel.L4:
                        return leftL4;
                }
            }

            if (target == ReefScoringTarget.Right)
            {
                switch (level)
                {
                    case ReefLevel.L2:
                        return rightL2;

                    case ReefLevel.L3:
                        return rightL3;

                    case ReefLevel.L4:
                        return rightL4;
                }
            }

            return null;
        }

        public List<ReefSlotState> GetAllSlots()
        {
            return new List<ReefSlotState>
            {
                leftL2,
                leftL3,
                leftL4,
                rightL2,
                rightL3,
                rightL4
            };
        }
    }

    // ============================================================
    // ROBOT STATE
    // ============================================================

    [System.Serializable]
    public class RobotState
    {
        public CarriedGamePiece carriedGamePiece = CarriedGamePiece.None;
        public RobotTask currentTask = RobotTask.Idle;
        public string currentTargetName = "";
    }

    // ============================================================
    // ALLIANCE STATE
    // ============================================================

    [System.Serializable]
    public class AllianceState
    {
        // L1 is a combined trough count rather than a per-face slot.
        public int l1CoralCount = 0;

        public List<ReefFaceState> reefFaces =
            new List<ReefFaceState>();

        // Algae scoring state.
        public int processorAlgaeScored = 0;
        public int netAlgaeScored = 0;

        // Endgame state.
        public int robotsClimbed = 0;
    }

    // ============================================================
    // MATCH STATE
    // ============================================================

    [Header("Robot State")]
    public RobotState robot = new RobotState();

    [Header("Alliance State")]
    public FieldMap2D.ReefAlliance alliance =
        FieldMap2D.ReefAlliance.Blue;

    public AllianceState allianceState = new AllianceState();

    [Header("Match Time")]
    public float matchTimeRemaining = 0.0f;
    public float endgameThresholdSeconds = 30.0f;

    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void Awake()
    {
        InitializeMatchState();
    }

    private void OnValidate()
    {
        if (allianceState == null)
        {
            allianceState = new AllianceState();
        }

        if (allianceState.reefFaces == null ||
            allianceState.reefFaces.Count != 6)
        {
            InitializeReefState();
        }
    }

    public void InitializeMatchState()
    {
        if (robot == null)
        {
            robot = new RobotState();
        }

        if (allianceState == null)
        {
            allianceState = new AllianceState();
        }

        InitializeReefState();
    }

    private void InitializeReefState()
    {
        allianceState.reefFaces =
            new List<ReefFaceState>();

        for (int face = 1; face <= 6; face++)
        {
            allianceState.reefFaces.Add(
                new ReefFaceState(face)
            );
        }
    }

    // ============================================================
    // MATCH TIME
    // ============================================================

    public void SetMatchTimeRemaining(float seconds)
    {
        matchTimeRemaining = Mathf.Max(0.0f, seconds);
    }

    public bool IsEndgameActive()
    {
        return matchTimeRemaining <= endgameThresholdSeconds;
    }

    // ============================================================
    // ROBOT GAME PIECE
    // ============================================================

    public void SetCarriedGamePiece(
        CarriedGamePiece gamePiece)
    {
        robot.carriedGamePiece = gamePiece;
    }

    public bool IsCarryingGamePiece()
    {
        return robot.carriedGamePiece !=
               CarriedGamePiece.None;
    }

    public bool IsCarryingCoral()
    {
        return robot.carriedGamePiece ==
               CarriedGamePiece.Coral;
    }

    public bool IsCarryingAlgae()
    {
        return robot.carriedGamePiece ==
               CarriedGamePiece.Algae;
    }

    public void ClearCarriedGamePiece()
    {
        robot.carriedGamePiece =
            CarriedGamePiece.None;
    }

    // ============================================================
    // ROBOT TASK
    // ============================================================

    public void SetCurrentTask(
        RobotTask task,
        string targetName = "")
    {
        robot.currentTask = task;
        robot.currentTargetName = targetName;
    }

    public void ClearCurrentTask()
    {
        robot.currentTask = RobotTask.Idle;
        robot.currentTargetName = "";
    }

    // ============================================================
    // L1 CORAL STATE
    // ============================================================

    public void SetL1CoralCount(int count)
    {
        allianceState.l1CoralCount =
            Mathf.Max(0, count);
    }

    public int GetL1CoralCount()
    {
        return allianceState.l1CoralCount;
    }

    // ============================================================
    // REEF LOOKUPS
    // ============================================================

    public ReefFaceState GetReefFace(int faceNumber)
    {
        if (allianceState == null ||
            allianceState.reefFaces == null)
        {
            return null;
        }

        foreach (ReefFaceState face
                 in allianceState.reefFaces)
        {
            if (face.faceNumber == faceNumber)
            {
                return face;
            }
        }

        return null;
    }

    public ReefSlotState GetReefSlot(
        int faceNumber,
        ReefScoringTarget target,
        ReefLevel level)
    {
        ReefFaceState face =
            GetReefFace(faceNumber);

        if (face == null)
        {
            return null;
        }

        return face.GetSlot(target, level);
    }

    // ============================================================
    // REEF OCCUPANCY
    // ============================================================

    public bool IsReefSlotAvailable(
        int faceNumber,
        ReefScoringTarget target,
        ReefLevel level)
    {
        ReefSlotState slot =
            GetReefSlot(
                faceNumber,
                target,
                level
            );

        return slot != null && !slot.occupied;
    }

    public bool MarkReefSlotOccupied(
        int faceNumber,
        ReefScoringTarget target,
        ReefLevel level)
    {
        ReefSlotState slot =
            GetReefSlot(
                faceNumber,
                target,
                level
            );

        if (slot == null)
        {
            Debug.LogWarning(
                $"Invalid Reef slot: Face {faceNumber}, " +
                $"{target}, {level}"
            );

            return false;
        }

        slot.occupied = true;
        return true;
    }

    public bool MarkReefSlotEmpty(
        int faceNumber,
        ReefScoringTarget target,
        ReefLevel level)
    {
        ReefSlotState slot =
            GetReefSlot(
                faceNumber,
                target,
                level
            );

        if (slot == null)
        {
            Debug.LogWarning(
                $"Invalid Reef slot: Face {faceNumber}, " +
                $"{target}, {level}"
            );

            return false;
        }

        slot.occupied = false;
        return true;
    }

    public List<ReefSlotState> GetAvailableReefSlots()
    {
        List<ReefSlotState> available =
            new List<ReefSlotState>();

        if (allianceState == null ||
            allianceState.reefFaces == null)
        {
            return available;
        }

        foreach (ReefFaceState face
                 in allianceState.reefFaces)
        {
            foreach (ReefSlotState slot
                     in face.GetAllSlots())
            {
                if (!slot.occupied)
                {
                    available.Add(slot);
                }
            }
        }

        return available;
    }

    // ============================================================
    // REEF ALGAE STATE
    // ============================================================

    public bool IsReefAlgaePresent(int faceNumber)
    {
        ReefFaceState face =
            GetReefFace(faceNumber);

        return face != null &&
               face.algaePresent;
    }

    public void SetReefAlgaePresent(
        int faceNumber,
        bool present)
    {
        ReefFaceState face =
            GetReefFace(faceNumber);

        if (face == null)
        {
            Debug.LogWarning(
                $"Invalid Reef face: {faceNumber}"
            );

            return;
        }

        face.algaePresent = present;
    }

    // ============================================================
    // ALGAE SCORING STATE
    // ============================================================

    public void SetProcessorAlgaeScored(int count)
    {
        allianceState.processorAlgaeScored =
            Mathf.Max(0, count);
    }

    public int GetProcessorAlgaeScored()
    {
        return allianceState.processorAlgaeScored;
    }

    public void SetNetAlgaeScored(int count)
    {
        allianceState.netAlgaeScored =
            Mathf.Max(0, count);
    }

    public int GetNetAlgaeScored()
    {
        return allianceState.netAlgaeScored;
    }

    // ============================================================
    // ENDGAME STATE
    // ============================================================

    public void RecordRobotClimbed()
    {
        allianceState.robotsClimbed++;
    }

    // ============================================================
    // RESET
    // ============================================================

    public void ResetMatchState()
    {
        robot = new RobotState();
        allianceState = new AllianceState();
        matchTimeRemaining = 0.0f;

        InitializeReefState();
    }
}