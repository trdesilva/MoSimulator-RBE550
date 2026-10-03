using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class AutoDriver : MonoBehaviour
{
    [SerializeField]
    private Perception perception;

    // Start is called before the first frame update
    void Start()
    {
        Debug.LogWarning("AutoDriver started");
        InvokeRepeating(nameof(LogNearestAlgae), 5f, 1f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void LogNearestAlgae()
    {
        // TODO for some reason, this seems to update slowly with algae on the reef, but not algae on the floor
        ICollection<GameObject> algae = perception.GetNearestWithTag("Algae", transform.position);
        if (algae != null)
        {
            if (algae.Count > 0)
            {
                Debug.Log($"Nearest algae: {algae.First().transform.position}");
            }
            else
            {
                Debug.LogWarning("GetNearestWithTag returned empty array");
            }
        }
        else
        {
            Debug.LogWarning("GetNearestWithTag returned null array");
        }
    }
}
