using System.Collections.Generic;
using Games.Reefscape.FieldScripts;
using MoSimCore.BaseClasses.GameManagement;
using MoSimCore.Enums;
using MoSimCore.Interfaces;
using MoSimLib;
using UnityEngine;

namespace Games.Reefscape.Scoring
{
    public class TroughScorer : MonoBehaviour, IScorer
    {
        public Alliance Alliance { get; private set; }

        private Reef _reef;

        [SerializeField]
        private BoxCollider[] troughTriggers;

        private Vector3[] troughBounds;
        private Vector3[] troughCenters;
        private Quaternion[] troughNormals;

        [SerializeField]
        private List<GameObject> troughObjects =
            new List<GameObject>();

        [SerializeField]
        private List<GameObject> scoredInAuto =
            new List<GameObject>();

        [SerializeField]
        private List<GameObject> stillScored =
            new List<GameObject>();


        private void Awake()
        {
            _reef =
                GetComponentInParent<Reef>();

            if (_reef == null)
            {
                Debug.LogError(
                    "Trough could not find a Reef component " +
                    "in parent objects."
                );

                enabled = false;
                return;
            }

            Alliance =
                _reef.Alliance;
        }


        private void Start()
        {
            troughBounds =
                new Vector3[troughTriggers.Length];

            troughCenters =
                new Vector3[troughTriggers.Length];

            troughNormals =
                new Quaternion[troughTriggers.Length];


            for (int i = 0;
                 i < troughTriggers.Length;
                 i++)
            {
                troughBounds[i] =
                    Utils.MultiplyVectors(
                        troughTriggers[i].size,
                        troughTriggers[i]
                            .transform
                            .lossyScale
                    ) / 2;


                troughCenters[i] =
                    troughTriggers[i]
                        .bounds
                        .center;


                troughNormals[i] =
                    troughTriggers[i]
                        .transform
                        .rotation;
            }
        }


        private void Update()
        {
            // Safety check for GameManager.
            if (BaseGameManager.Instance == null)
            {
                return;
            }


            // Keep final scoring state frozen after match end.
            if (BaseGameManager.Instance.GameState ==
                GameState.End)
            {
                return;
            }


            // Rebuild the currently scored Coral list.
            troughObjects.Clear();

            scoredInAuto.Clear();
            scoredInAuto.AddRange(stillScored);

            stillScored.Clear();


            for (int i = 0;
                 i < troughTriggers.Length;
                 i++)
            {
                CheckCoral(i);
            }
        }


        // ========================================================
        // L1 STATE ACCESS
        // ========================================================

        public int GetScoredCoralCount()
        {
            return troughObjects.Count;
        }


        // ========================================================
        // SCORING
        // ========================================================

        public void AddScore(
            IScoreData scoreData,
            GameState gameState)
        {
            if (scoreData is not
                ReefscapeScoreData reefscapeData)
            {
                return;
            }


            // Calculate Auto bonuses.
            if (BaseGameManager.Instance != null &&
                BaseGameManager.Instance.GameState ==
                GameState.Auto)
            {
                foreach (var col in troughObjects)
                {
                    if (scoredInAuto.Contains(col))
                    {
                        continue;
                    }

                    scoredInAuto.Add(col);
                }
            }


            // Apply points for every Coral currently
            // detected in the trough.
            foreach (GameObject go in troughObjects)
            {
                if (scoredInAuto.Contains(go))
                {
                    stillScored.Add(go);

                    reefscapeData.CoralPoints += 3;
                    reefscapeData.CoralScored += 1;
                }
                else
                {
                    reefscapeData.CoralPoints += 2;
                    reefscapeData.CoralScored += 1;
                }
            }
        }


        // ========================================================
        // CORAL DETECTION
        // ========================================================

        private void CheckCoral(int index)
        {
            foreach (
                var result in
                Physics.OverlapBox(
                    troughCenters[index],
                    troughBounds[index],
                    troughNormals[index]))
            {
                if (!result.CompareTag("Coral"))
                {
                    continue;
                }


                // A Coral may overlap more than one trough
                // trigger. Do not count it twice.
                if (troughObjects.Contains(
                        result.gameObject))
                {
                    continue;
                }


                troughObjects.Add(
                    result.gameObject
                );
            }
        }


        // ========================================================
        // RESET
        // ========================================================

        public void ResetTrough()
        {
            troughObjects.Clear();
            scoredInAuto.Clear();
            stillScored.Clear();
        }
    }
}