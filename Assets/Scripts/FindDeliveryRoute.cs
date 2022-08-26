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
    private List<Transform[]> zoneSets;  // List contains sets of two delivery zones
    private List<Transform[]> nodeSets;  // List contains sets of three delivery zones
    private List<float> setTimes;  // List contains times to travel from one delivery zone to another - index corresponds to zoneSets
    private List<float> nodeTimes; // List contains times to rotate from one delivery to the next - index corresponds to nodeSets

    private float speed = 10f;
    private float angularSpeed = 45f;
    public float cutoffAngle;  // Ignore calculating turning times for values less than the cutoff - lower values prioritize straighter paths
    public float turnPenalty = 1f;  // Increases penalty for turns when calculating turning times - higher values prioritize staighter paths

    private void FindSetTimes()  // Given a set of locations determines travel times between all combinations of two zones
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


                path = new NavMeshPath();  // Contains path between two delivery zones
                segments = new List<Vector3>();  // Contains all straight line segments in the path
                float distance = 0;
                float angles = 0;

                if (NavMesh.CalculatePath(zone1.position, zone2.position, NavMesh.AllAreas, path))
                {
                    zoneSets.Add(new Transform[] { zone1, zone2 });  // Order of zones in a set doesn't matter - same travel time either way
                    segments.Add(path.corners[0] - zone1.position);  // Adds first segment of path 

                    for (int k = 1; k < path.corners.Length; k++)  // Adds remaining segments if the path contains turns
                    {
                        segments.Add(path.corners[k] - path.corners[k - 1]);
                    }

                    for (int k = 0; k < segments.Count; k++)  // Determines total distance traveled between locations
                    {
                        distance += segments[k].magnitude;
                    }

                    for (int k = 1; k < segments.Count; k++)  // Determines angles between all path segments
                    {
                        if (Vector3.Angle(segments[k - 1], segments[k]) > cutoffAngle)
                        {
                            angles += Vector3.Angle(segments[k - 1], segments[k]);
                        }
                    }

                    setTimes.Add(distance / speed + angles * turnPenalty / angularSpeed);  // Calculates travel time using distance, speed, total angles turned, and angular speed

                }
            }
    }

    private void FindNodeTimes()  // Given a set of locations determines rotation times between deliveries at a middle node in the path
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
                        nodeSets.Add(new Transform[] { node, last, next });  // first position in set is important, order of last and next doesn't matter
                        float angle = Vector3.Angle(node.position - path1.corners[0], path2.corners[0] - node.position);
                        nodeTimes.Add(angle * turnPenalty / angularSpeed);  // Note that the cutoff frequency doesn't affect the calculation of node times
                    }
                }
            }
        }
    }

    private float FindNodeTime(Transform last, Transform node, Transform next)
    {
        for (int i = 0; i < nodeSets.Count; i++)
        {
            if (nodeSets[i].Contains(last) && nodeSets[i][0] == node && nodeSets[i].Contains(next))  // Ensures the set has the node in the first index position
            {
                return nodeTimes[i];
            }
        }

        return 0f;
    }

    public List<Transform> FindRoute(List<Transform> allZones, int runs)
    {
        locations = allZones;
        FindSetTimes();
        Debug.Log("Sets: " + zoneSets.Count.ToString() + "\tTimes: " + setTimes.Count.ToString());  // Output values should be the same as one another and consistent across trials - indicates a miscalculated path if otherwise
        FindNodeTimes();
        Debug.Log("Nodes: " + nodeSets.Count.ToString() + "\tTimes: " + nodeTimes.Count.ToString());  // " " " "

        float minRunTime = 0f;

        for (int n = 0; n < runs; n++)  // loop is currently useless since all calculated paths will be identical with this algorithm - runs is set to 1
        {
            sequence = new List<Transform>();

            List<Transform> zones = new List<Transform>(locations.ToArray());

            //zones.Shuffle(new System.Random());

            float runTime = 0f;

            List<Transform[]> sets = new List<Transform[]>(zoneSets.ToArray());
            
            List<float> times = new List<float>(setTimes.ToArray());

            Transform currentZone = zones.First();  // Selects the first zone in the list to build the sequence off of - currently set to be the post office

            int nZones = zones.Count;

            while (zones.Count > 0)
            {
                int minIndex = 0;
                float minTime = 0f;

                if (zones.Count > 1)
                    for (int i = 0; i < sets.Count; i++)
                    {
                        if (sets[i].Contains(currentZone) && sets[i].IsValid(currentZone, zones))  // Finds the set with the shortest travel time containing the current zone and a valid next delivery zone 
                        {
                            float setTime = times[i] + ((zones.Count != nZones) ? FindNodeTime(sequence.Last(), sets[i][0], sets[i][1]) : 0f);  // travel time factors in node time of the current delivery zone
                            if (setTime < minTime || minTime == 0f)
                            {
                                minTime = setTime;
                                minIndex = i;
                            }
                        }
                    }

                else
                {
                    for (int i = 0; i < sets.Count; i++)  // Finds the set containing the final delivery zone and the first location (post office)
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

                if (sets[minIndex][0] != currentZone) sets[minIndex].Swap(0, 1);  // selects the next delivery zone to be the current

                currentZone = sets[minIndex][1];

                runTime += minTime;

                times.RemoveAt(minIndex);
                sets.RemoveAt(minIndex);

            }

            sequence.Add(currentZone);  // Builds the sequence
            
            if (runTime < minRunTime || minRunTime == 0f)
            {
                minRunTime = runTime;
                route = sequence;
            }
        }

        route.RemoveAt(0);  // Removes the start location - no need to travel to there

        string outputTxt = "";
        foreach (Transform zone in route) outputTxt += zone.name + "\n";
        Debug.Log(outputTxt);  // Outputs list of delivery locations to debug log

        Debug.Log("Number of Destinations: " + route.Count.ToString());
        
        return route;
    }

    public Transform FindMiddle(List<Transform> visitedZones, Transform last, Transform next)  // Finds a repeat location that adds the least amount of additional travel time between two zones
    {
        float minTime = 0f;
        Transform middle = null;

        foreach (Transform location in visitedZones)
        {
            float midTime = 0f;
            for (int i = 0; i < zoneSets.Count; i++)
            {
                if (zoneSets[i].Contains(last) && zoneSets[i].Contains(location))
                {
                    midTime += setTimes[i];
                }
                else if (zoneSets[i].Contains(next) && zoneSets[i].Contains(location))
                {
                    midTime += setTimes[i];
                }
            }
            for (int i = 0; i < nodeSets.Count; i++)
            {
                if (nodeSets[i].Contains(last) && nodeSets[i].Contains(location) && nodeSets[i].Contains(next))
                {
                    midTime += nodeTimes[i];
                    break;
                }
            }

            if (midTime < minTime || minTime == 0)
            {
                minTime = midTime;
                middle = location;
            }

        }

        return middle;
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

        if (sampleList.Contains(otherItem)) return true;  // item is valid if contained in the sample list
        else return false;  // item is invalid if otherwise

    }
}
