using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class FindDeliveryRoute : MonoBehaviour
{
    private List<Transform> route = new List<Transform>();
    private List<Transform> sequence;
    private List<Transform> allStores;
    private List<Transform[]> storeSets;
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
        storeSets = new List<Transform[]>();
        setTimes = new List<float>();

        for (int i = 0; i < allStores.Count - 1; i++)
        
            for (int j = i + 1; j < allStores.Count; j++)
            {
                Transform store1 = allStores[i];
                Transform store2 = allStores[j];


                path = new NavMeshPath();
                segments = new List<Vector3>();
                float distance = 0;
                float angles = 0;

                if (NavMesh.CalculatePath(store1.Find("DeliveryZone").position, store2.Find("DeliveryZone").position, NavMesh.AllAreas, path))
                {
                    storeSets.Add(new Transform[] { store1, store2 });
                    segments.Add(path.corners[0] - store1.position);

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

        for (int i = 0; i < allStores.Count; i++)
        {
            for (int j = 0; j < allStores.Count-1; j++)
            {
                if (j == i) j++;
                if (j == allStores.Count - 1) break;

                for (int k = j + 1; k < allStores.Count; k++)
                {
                    if (k == i) k++;
                    if (k == allStores.Count) break;

                    Transform node = allStores[i];
                    Transform last = allStores[j];
                    Transform next = allStores[k];
                    
                    path1 = new NavMeshPath();
                    path2 = new NavMeshPath();

                    if (NavMesh.CalculatePath(node.Find("DeliveryZone").position, last.Find("DeliveryZone").position, NavMesh.AllAreas, path1) && NavMesh.CalculatePath(node.Find("DeliveryZone").position, next.Find("DeliveryZone").position, NavMesh.AllAreas, path2))
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

    public List<Transform> FindRoute(List<StoreComponent> locations, int runs)
    {
        allStores = new List<Transform>();
        foreach (StoreComponent location in locations) allStores.Add(location.gameObject.transform);
        allStores.Shuffle(new System.Random());
        FindSetTimes();
        Debug.Log("Sets: " + storeSets.Count.ToString() + "\tTimes: " + setTimes.Count.ToString());
        FindNodeTimes();
        Debug.Log("Nodes: " + nodeSets.Count.ToString() + "\tTimes: " + nodeTimes.Count.ToString());

        float minRunTime = 0f;

        for (int n = 0; n < runs; n++)
        {
            sequence = new List<Transform>();

            List<Transform> stores = new List<Transform>(allStores.ToArray());

            stores.Shuffle(new System.Random());

            float runTime = 0f;

            List<Transform[]> sets = new List<Transform[]>(storeSets.ToArray());
            
            List<float> times = new List<float>(setTimes.ToArray());

            Transform currentStore = stores.First();

            int nStores = stores.Count;

            while (stores.Count > 0)
            {
                int minIndex = 0;
                float minTime = 0f;

                if (stores.Count > 1)
                    for (int i = 0; i < sets.Count; i++)
                    {
                        if (sets[i].Contains(currentStore) && sets[i].IsValid(currentStore, stores))
                        {
                            float setTime = times[i] + ((stores.Count != nStores) ? FindNodeTime(sequence.Last(), sets[i][0], sets[i][1]) : 0f);
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
                        if (sets[i].Contains(currentStore) && sets[i].Contains(sequence.First()))
                        {
                            float setTime = times[i] + FindNodeTime(sequence.Last(), currentStore, sequence.First());
                            minTime = setTime;
                            minIndex = i;
                            break;

                        }
                    }
                }

                sequence.Add(currentStore);
                stores.Remove(currentStore);

                if (sets[minIndex][0] != currentStore) sets[minIndex].Swap(0, 1);

                currentStore = sets[minIndex][1];

                runTime += minTime;

                times.RemoveAt(minIndex);
                sets.RemoveAt(minIndex);

            }

            sequence.Add(currentStore);
            
            if (runTime < minRunTime || minRunTime == 0f)
            {
                minRunTime = runTime;
                route = sequence;
            }
        }

        string outputTxt = "";
        foreach (Transform store in route) outputTxt += store.name + "\n";
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
