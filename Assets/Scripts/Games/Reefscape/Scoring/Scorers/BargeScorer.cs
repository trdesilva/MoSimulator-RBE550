using System.Collections.Generic;
using MoSimCore.Enums;
using MoSimCore.Interfaces;
using MoSimLib;
using UnityEngine;

namespace Games.Reefscape.Scoring.Scorers
{
    public class BargeScorer : MonoBehaviour, IScorer
    {
        [field: SerializeField]
        public Alliance Alliance { get; private set; }

        private BoxCollider _scoringCollider;
        private Vector3 boxSize;
        private Vector3 boxPosition;
        private Quaternion boxRotation;

        private HashSet<Collider> _scoredAlgae =
            new HashSet<Collider>();

        private int _algaeCount = 0;
        private bool _gameEnded = false;

        // ============================================================
        // START
        // ============================================================

        private void Start()
        {
            _scoringCollider = GetComponent<BoxCollider>();

            if (_scoringCollider == null)
            {
                Debug.LogError(
                    "BargeScorer could not find a BoxCollider component."
                );

                enabled = false;
                return;
            }

            boxSize =
                Utils.MultiplyVectors(
                    _scoringCollider.size,
                    _scoringCollider.transform.lossyScale
                ) / 2;

            boxPosition =
                _scoringCollider.bounds.center;

            boxRotation =
                _scoringCollider.transform.rotation;
        }

        // ============================================================
        // UPDATE
        // ============================================================

        private void Update()
        {
            // Stop accepting new algae once the game has ended.
            if (_gameEnded)
            {
                return;
            }

            Collider[] results =
                Physics.OverlapBox(
                    boxPosition,
                    boxSize,
                    boxRotation
                );

            // Build the current set of Algae inside the scoring zone.
            HashSet<Collider> currentAlgae =
                new HashSet<Collider>();

            foreach (Collider result in results)
            {
                if (result == null)
                {
                    continue;
                }

                if (!result.CompareTag("Algae"))
                {
                    continue;
                }

                currentAlgae.Add(result);

                // New Algae entered the Net.
                if (!_scoredAlgae.Contains(result))
                {
                    _scoredAlgae.Add(result);
                    _algaeCount++;
                }
            }

            // Find Algae that have left the scoring zone.
            List<Collider> toRemove =
                new List<Collider>();

            foreach (Collider scored in _scoredAlgae)
            {
                if (scored == null)
                {
                    toRemove.Add(scored);
                    continue;
                }

                if (!currentAlgae.Contains(scored))
                {
                    toRemove.Add(scored);
                }
            }

            // Remove Algae that are no longer scored.
            foreach (Collider rem in toRemove)
            {
                if (_scoredAlgae.Remove(rem))
                {
                    _algaeCount =
                        Mathf.Max(
                            0,
                            _algaeCount - 1
                        );
                }
            }
        }

        // ============================================================
        // SCORE
        // ============================================================

        public void AddScore(
            IScoreData scoreData,
            GameState gameState)
        {
            // Stop accepting new Algae after the match ends.
            if (gameState == GameState.End)
            {
                _gameEnded = true;
            }

            if (scoreData is ReefscapeScoreData reefscapeScoreData)
            {
                reefscapeScoreData.NetPoints +=
                    _algaeCount * 4;

                reefscapeScoreData.AlgaeScored +=
                    _algaeCount;
            }
            else
            {
                Debug.LogError(
                    "Invalid score data type passed to BargeScorer."
                );
            }
        }

        // ============================================================
        // MATCH STATE ACCESSOR
        // ============================================================

        public int GetScoredAlgaeCount()
        {
            return _algaeCount;
        }
    }
}