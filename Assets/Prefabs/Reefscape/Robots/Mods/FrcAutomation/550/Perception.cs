using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Perception : MonoBehaviour
{
    private HashSet<string> RELEVANT_TAGS = new HashSet<string> { "GamePieceWorld" };//{ "ReefFace", "Algae", "Coral", "BlueRobot", "RedRobot", "Cage", "BlueBargeZone", "RedBargeZone", "GamePieceWorld" };
    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Perception starting");
        InvokeRepeating(nameof(logGameObjects), 0, 5.0f);
    }

    void logGameObjects()
    {
        Debug.Log("Game object tags:");
        Dictionary<string, int> tagsLogged = new Dictionary<string, int>();
        foreach(GameObject obj in GetAllObjectsOnlyInScene())
        {
            if (obj.tag == null || !RELEVANT_TAGS.Contains(obj.tag))
                continue;
            string tag = obj.tag != null ? obj.tag : "<null tag>";
            if (!tagsLogged.ContainsKey(tag))
            {
                tagsLogged.Add(tag, 0);
            }
            if (tagsLogged[tag] < 10)
            {
                Debug.Log($"{obj.name}, {tag}, {obj.transform.position}");
            }
            tagsLogged[tag] = tagsLogged[tag] + 1;
        }
        Debug.Log("Tag totals:");
        foreach(KeyValuePair<string, int> entry in tagsLogged)
        {
            Debug.Log($"{entry.Key}, {entry.Value}");
        }
    }

    // https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Resources.FindObjectsOfTypeAll.html
    List<GameObject> GetAllObjectsOnlyInScene()
    {
        List<GameObject> objectsInScene = new List<GameObject>();

        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            objectsInScene.Add(go);
        }

        return objectsInScene;
    }
}
