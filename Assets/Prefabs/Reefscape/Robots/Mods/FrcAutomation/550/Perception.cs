using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class Perception : MonoBehaviour
{
    private class TrackedObjectGroup
    {
        public string Tag { get; }
        private int UpdatePeriodFrames { get; set; }
        PointOctree<GameObject> Octree { get; set; }
        Func<GameObject[]> UpdateCallback;
        
        private int framesSinceUpdate = 0;
        ReaderWriterLock octreeLock;

        /// <param name="tag">tag of GameObjects to track</param>
        /// <param name="updatePeriodFrames">periodicity of updates for this group in frames (0 => no updates, 1 => 
        /// every frame, 2 => every other frame, etc.)</param>
        /// <param name="updateCallback">(optional) function returning an array of GameObjects to call during Update; 
        /// defaults to GameObject.FindGameObjectsWithTag(tag)</param>
        public TrackedObjectGroup(string tag, int updatePeriodFrames, Func<GameObject[]> updateCallback = null)
        {
            this.Tag = tag;
            this.UpdatePeriodFrames = updatePeriodFrames;

            Octree = new PointOctree<GameObject>(5f, new Vector3(), 0.5f);

            if(updateCallback == null)
            {
                UpdateCallback = () => { return GameObject.FindGameObjectsWithTag(Tag); };
            }
            else
            {
                this.UpdateCallback = updateCallback;
            }

            octreeLock = new ReaderWriterLock();
        }

        public void TryUpdate()
        {
            if(UpdatePeriodFrames > 0)
            {
                framesSinceUpdate += 1;
                if (framesSinceUpdate >= UpdatePeriodFrames)
                {
                    Update();
                }
            }
        }

        public void Update()
        {
            try
            {
                octreeLock.AcquireWriterLock(1000);
                // TODO store last coords in hashmap and only update things that move?
                Octree = new PointOctree<GameObject>(5f, new Vector3(), 0.5f);
                foreach (GameObject g in UpdateCallback())
                {
                    Octree.Add(g, g.transform.position);
                }
                framesSinceUpdate = 0;
            }
            finally
            {
                octreeLock.ReleaseWriterLock();
            }
        }

        public ICollection<GameObject> GetNearest(Vector3 position, int resultCount = 1)
        {
            try
            {
                octreeLock.AcquireReaderLock(50);
                // TODO this octree implementation doesn't actually provide a good nearest-neighbor API
                GameObject[] result = Octree.GetNearby(position, 5f);
                if(result != null)
                {
                    var resultList = new List<GameObject>(result);
                    resultList.Sort((g1, g2) => (int)((g1.transform.position - position).magnitude - (g2.transform.position - position).magnitude));
                    if(resultCount <  resultList.Count)
                    {
                        resultList.RemoveRange(resultCount, resultList.Count - resultCount);
                    }
                    return resultList.ToArray();
                }
                else
                {
                    Debug.LogWarning($"No objects found with tag {Tag}");
                    return null;
                }
                
            }
            catch(TimeoutException e)
            {
                Debug.LogError($"Timeout in GetNearest for tag {Tag}");
                return null;
            }
            finally
            {
                octreeLock.ReleaseReaderLock();
            }
        }

        public ICollection<GameObject> GetAllObjects()
        {
            try
            {
                octreeLock.AcquireReaderLock(50);
                return Octree.GetAll();
            }
            catch (TimeoutException e)
            {
                Debug.LogError($"Timeout in GetAllObjects for tag {Tag}");
                return null;
            }
            finally
            {
                octreeLock.ReleaseReaderLock();
            }
        }
    }

    private Dictionary<string, TrackedObjectGroup> trackedGroups;

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Perception starting");
        trackedGroups = new Dictionary<string, TrackedObjectGroup>();
        
        // Static objects
        TrackTag("ReefFace", 0);
        TrackTag("BlueBargeZone", 0);
        TrackTag("RedBargeZone", 0);

        // Dynamic objects
        TrackTag("BlueRobot", 10);
        TrackTag("RedRobot", 10);
        TrackTag("Cage", 60);
        TrackTag("Algae", 1);
        TrackTag("Coral", 1);
    }

    void FixedUpdate()
    {
        // TODO this is slow, try replacing with coroutines
        foreach(TrackedObjectGroup group in trackedGroups.Values)
        {
            group.TryUpdate();
        }
    }

    void TrackTag(string tag, int updatePeriodFrames, Func<GameObject[]> updateCallback = null)
    {
        if(trackedGroups.ContainsKey(tag))
        {
            Debug.LogWarning($"{tag} is already tracked by Perception");
            return;
        }
        trackedGroups.Add(tag, new TrackedObjectGroup(tag, updatePeriodFrames, updateCallback));
    }

    public ICollection<GameObject> GetNearestWithTag(string tag, Vector3 position, int resultCount = 1)
    {
        if(trackedGroups.ContainsKey(tag))
        {
            return trackedGroups[tag].GetNearest(position, resultCount);
        }
        else
        {
            Debug.LogWarning($"Trying to GetNearestByTag on untracked tag {tag}");
            return null;
        }
    }

    public ICollection<GameObject> GetAllWithTag(string tag)
    {
        if (trackedGroups.ContainsKey(tag))
        {
            return trackedGroups[tag].GetAllObjects();
        }
        else
        {
            Debug.LogWarning($"Trying to GetAllWithTag on untracked tag {tag}; calling Unity builtin");
            return GameObject.FindGameObjectsWithTag(tag);
        }
    }
}
