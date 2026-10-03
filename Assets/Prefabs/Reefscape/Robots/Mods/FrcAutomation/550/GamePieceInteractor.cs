using Games.Reefscape.GamePieceSystem;
using UnityEngine;

public class GamePieceInteractor : MonoBehaviour
{
    private ReefscapeGamePieceIntake[] intakes;


    private void Awake()
    {
        intakes =
            GetComponentsInChildren<ReefscapeGamePieceIntake>(
                true
            );

        Debug.Log(
            $"GamePieceInteractor found {intakes.Length} intakes."
        );

        foreach (ReefscapeGamePieceIntake intake in intakes)
        {
            Debug.Log(
                $"Found intake for {intake.pieceName}"
            );
        }
    }


    public bool StartIntake(string pieceName)
    {
        ReefscapeGamePieceIntake intake =
            FindIntake(pieceName);

        if (intake == null)
        {
            Debug.LogWarning(
                $"No intake found for {pieceName}"
            );

            return false;
        }

        intake.requestIntake = true;

        Debug.Log(
            $"Started intake for {pieceName}"
        );

        return true;
    }


    public void StopIntake(string pieceName)
    {
        ReefscapeGamePieceIntake intake =
            FindIntake(pieceName);

        if (intake == null)
            return;

        intake.requestIntake = false;
    }


    public bool HasPiece(string pieceName)
    {
        ReefscapeGamePieceIntake intake =
            FindIntake(pieceName);

        return
            intake != null &&
            intake.hasGamePiece;
    }


    private ReefscapeGamePieceIntake FindIntake(
        string pieceName)
    {
        foreach (ReefscapeGamePieceIntake intake in intakes)
        {
            if (intake.pieceName == pieceName)
                return intake;
        }

        return null;
    }
}
