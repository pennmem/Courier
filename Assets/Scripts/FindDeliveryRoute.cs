using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class FindDeliveryRoute : MonoBehaviour
{
    private List<Transform> route = new List<Transform>();
    private List<Transform> sequence;
    private List<Transform> locations;
    private List<Transform[]> zoneSets;
    private List<Transform[]> nodeSets;
    private List<float> setTimes;
    private List<float> nodeTimes;

    private float speed = 10f;
    private float angularSpeed = 45f;
    public float cutoffAngle;
    public float turnPenalty = 1f;

    private void FindSetTimes()
    {
        NavMeshPath path;
        List<Vector3> segments;
        zoneSets = new List<Transform[]>();
        setTimes = new List<float>();

        for (int i = 0; i < locations.Count - 1; i++)
        
            for (int j = i + 1; j < locations.Count; j++)
            {
                Transform zone1 = locations[i];
                Transform zone2 = locations[j];


                path = new NavMeshPath();
                segments = new List<Vector3>();
                float distance = 0;
                float angles = 0;

                if (NavMesh.CalculatePath(zone1.position, zone2.position, NavMesh.AllAreas, path))
                {
                    zoneSets.Add(new Transform[] { zone1, zone2 });
                    segments.Add(path.corners[0] - zone1.position);

                    for (int k = 1; k < path.corners.Length; k++)
                    {
                        segments.Add(path.corners[k] - path.corners[k - 1]);
                    }

                    for (int k = 0; k < segments.Count; k++)
                    {
                        distance += segments[k].magnitude;
                    }

                    for (int k = 1; k < segments.Count; k++)
                    {
                        if (Vector3.Angle(segments[k - 1], segments[k]) > cutoffAngle)
                        {
                            angles += Vector3.Angle(segments[k - 1], segments[k]);
                        }
                    }

                    setTimes.Add(distance / speed + angles * turnPenalty / angularSpeed);

                }
            }
    }

    private void FindNodeTimes()
    {
        NavMeshPath path1;
        NavMeshPath path2;
        nodeSets = new List<Transform[]>();
        nodeTimes = new List<float>();

        for (int i = 0; i < locations.Count; i++)
        {
            for (int j = 0; j < locations.Count-1; j++)
            {
                if (j == i) j++;
                if (j == locations.Count - 1) break;

                for (int k = j + 1; k < locations.Count; k++)
                {
                    if (k == i) k++;
                    if (k == locations.Count) break;

                    Transform node = locations[i];
                    Transform last = locations[j];
                    Transform next = locations[k];
                    
                    path1 = new NavMeshPath();
                    path2 = new NavMeshPath();

                    if (NavMesh.CalculatePath(node.position, last.position, NavMesh.AllAreas, path1) && NavMesh.CalculatePath(node.position, next.position, NavMesh.AllAreas, path2))
                    {
                        nodeSets.Add(new Transform[] { node, last, next });
                        float angle = Vector3.Angle(node.position - path1.corners[0], path2.corners[0] - node.position);
                        nodeTimes.Add(angle * turnPenalty / angularSpeed);
                    }
                }
            }
        }
    }

    private float FindNodeTime(Transform last, Transform node, Transform next)
    {
        for (int i = 0; i < nodeSets.Count; i++)
        {
            if (nodeSets[i].Contains(last) && nodeSets[i].Contains(node) && nodeSets[i].Contains(next))
            {
                return nodeTimes[i];
            }
        }

        return 0f;
    }

    public List<Transform> FindRoute(List<Transform> allZones, int runs)
    {
        locations = allZones;
        //allZones.Shuffle(new System.Random());
        FindSetTimes();
        Debug.Log("Sets: " + zoneSets.Count.ToString() + "\tTimes: " + setTimes.Count.ToString());
        FindNodeTimes();
        Debug.Log("Nodes: " + nodeSets.Count.ToString() + "\tTimes: " + nodeTimes.Count.ToString());

        float minRunTime = 0f;

        for (int n = 0; n < runs; n++)
        {
            sequence = new List<Transform>();

            List<Transform> zones = new List<Transform>(locations.ToArray());

            //zones.Shuffle(new System.Random());

            float runTime = 0f;

            List<Transform[]> sets = new List<Transform[]>(zoneSets.ToArray());
            
            List<float> times = new List<float>(setTimes.ToArray());

            Transform currentZone = zones.First();

            int nZones = zones.Count;

            while (zones.Count > 0)
            {
                int minIndex = 0;
                float minTime = 0f;

                if (zones.Count > 1)
                    for (int i = 0; i < sets.Count; i++)
                    {
                        if (sets[i].Contains(currentZone) && sets[i].IsValid(currentZone, zones))
                        {
                            float setTime = times[i] + ((zones.Count != nZones) ? FindNodeTime(sequence.Last(), sets[i][0], sets[i][1]) : 0f);
                            if (setTime < minTime || minTime == 0f)
                            {
                                minTime = setTime;
                                minIndex = i;
                            }
                        }
                    }

                else
                {
                    for (int i = 0; i < sets.Count; i++)
                    {
                        if (sets[i].Contains(currentZone) && sets[i].Contains(sequence.First()))
                        {
                            float setTime = times[i] + FindNodeTime(sequence.Last(), currentZone, sequence.First());
                            minTime = setTime;
                            minIndex = i;
                            break;

                        }
                    }
                }

                sequence.Add(currentZone);
                zones.Remove(currentZone);

                if (sets[minIndex][0] != currentZone) sets[minIndex].Swap(0, 1);

                currentZone = sets[minIndex][1];

                runTime += minTime;

                times.RemoveAt(minIndex);
                sets.RemoveAt(minIndex);

            }

            sequence.Add(currentZone);
            
            if (runTime < minRunTime || minRunTime == 0f)
            {
                minRunTime = runTime;
                route = sequence;
            }
        }

        route.RemoveAt(0);

        string outputTxt = "";
        foreach (Transform zone in route) outputTxt += zone.name + "\n";
        Debug.Log(outputTxt);

        Debug.Log("Number of Destinations: " + route.Count.ToString());
        
        return route;
    }
}

public static class IEnumerableExtensions
{
    public static bool Swap<T>(this T[] objectArray, int x, int y)
    {

        // check for out of range
        if (objectArray.Length <= y || objectArray.Length <= x) return false;


        // swap index x and y
        T temp = objectArray[x];
        objectArray[x] = objectArray[y];
        objectArray[y] = temp;


        return true;
    }

    public static bool IsValid(this Transform[] objectArray, Transform item, List<Transform> sampleList)
    {
        Transform otherItem;
        if (objectArray[0] != item) otherItem = objectArray[0];
        else otherItem = objectArray[1];

        if (sampleList.Contains(otherItem)) return true;
        else return false;

    }
}
