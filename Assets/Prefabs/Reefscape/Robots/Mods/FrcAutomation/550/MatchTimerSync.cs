using Games.Reefscape.GameManagement;
using UnityEngine;

public class MatchTimerSync : MonoBehaviour
{
    // ============================================================
    // REFERENCES
    // ============================================================

    [Header("References")]
    [SerializeField]
    private MatchState matchState;

    private ReefscapeTimerManager timerManager;


    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void Start()
    {
        // Find MatchState automatically if it was not assigned.
        if (matchState == null)
        {
            matchState = FindAnyObjectByType<MatchState>();
        }

        if (matchState == null)
        {
            Debug.LogError(
                "MatchTimerSync could not find MatchState."
            );

            enabled = false;
            return;
        }


        // Find MoSim's Reefscape timer.
        timerManager =
            FindAnyObjectByType<ReefscapeTimerManager>();

        if (timerManager == null)
        {
            Debug.LogError(
                "MatchTimerSync could not find ReefscapeTimerManager."
            );

            enabled = false;
            return;
        }


        // Synchronize immediately so MatchState does not have to
        // wait for the first timer event.
        matchState.SetMatchTimeRemaining(
            timerManager.Timer
        );


        // Listen for MoSim's timer updates.
        timerManager.OnTimerUpdated += HandleTimerUpdated;

        Debug.Log(
            $"MatchTimerSync connected. Initial time: " +
            $"{timerManager.Timer:F1} seconds"
        );
    }


    // ============================================================
    // TIMER UPDATE
    // ============================================================

    private void HandleTimerUpdated(float timeRemaining)
    {
        matchState.SetMatchTimeRemaining(
            timeRemaining
        );
    }


    // ============================================================
    // CLEANUP
    // ============================================================

    private void OnDestroy()
    {
        if (timerManager != null)
        {
            timerManager.OnTimerUpdated -= HandleTimerUpdated;
        }
    }
}