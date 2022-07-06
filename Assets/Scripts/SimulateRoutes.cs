using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SimulateRoutes : MonoBehaviour
{
    public float speed;

    private NavMeshAgent agent;
    private NavMeshPath path;
    private Transform pos;
    private Vector3 startPos;
    public Transform allPoints;
    public List<Transform> points = new List<Transform>();
    public float vel;
    
    private Transform[] outputList;
    private Transform point;
    private Transform deliveryZone;

    private List<List<Transform>> allLists = new List<List<Transform>>();

    float startTime;
    float endTime;
    float runTime;
    private List<float> timesList = new List<float>();

    private string outputText;

    // Start is called before the first frame update
    void Start()
    {
        Time.timeScale = speed;

        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        pos = GetComponent<Transform>();
        startPos = pos.localPosition;

        points = NewList(allPoints);
        outputList = new Transform[points.Count];
        points.CopyTo(outputList);

        startTime = Time.time;

    }

    // Update is called once per frame
    
    void FixedUpdate()
    {
        point = points[0];
        deliveryZone = point.Find("DeliveryZone");
        agent.destination = deliveryZone.position;

        vel = transform.InverseTransformVector(agent.velocity).z;

        if (vel < -4f) vel = -4f;;

        agent.velocity = transform.TransformVector(new Vector3(0, 0, vel));


        if ((int)pos.position.x == (int)deliveryZone.position.x && (int)pos.position.z == (int)deliveryZone.position.z)
        {
            points.Remove(point);
        }

        if (points.Count == 0)
        {
            ResetSimulation();
        }

    }

    private static List<Transform> NewList(Transform allPoints)
    {
        List<Transform> newList = new List<Transform>();

        for (int i = 0; i < allPoints.childCount; i++)
        {
            newList.Add(allPoints.GetChild(i));
        }

        newList.Shuffle(new System.Random());

        return newList;
    }

    private void ResetSimulation()
    {
        endTime = Time.time;
        runTime = endTime - startTime;
        Debug.Log("Time elapsed: " + (runTime/60f).ToString() + " min");
        Debug.Log("System Runtime: " + (runTime/speed).ToString() + " s");
        timesList.Add(runTime);
        WriteRoutes();

        points = NewList(allPoints);
        outputList = new Transform[points.Count];
        points.CopyTo(outputList);
        pos.localPosition = startPos;
        startTime = Time.time;
        Debug.Log("New Simulation");
        
    }

    private void WriteRoutes()
    {
        DateTime current = DateTime.Now;
        string filename = "Routes/" + current.Year.ToString() + "-" + current.Month.ToString() + "-" + current.Day.ToString() + ".csv";
        //string filename = "Routes/OvernightTrial.csv";
        FileStream fs = null;
        fs = new FileStream(filename, FileMode.Append);

        outputText = runTime.ToString();
        foreach (Transform p in outputList)
        {
            outputText = outputText + "," + p.gameObject.name;
        }

        using (StreamWriter file = new StreamWriter(fs))
        {
            file.Write(outputText + "\n");
        }

    }

}