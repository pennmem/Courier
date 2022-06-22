using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Navigation : MonoBehaviour
{
    public Transform target;
    public float pointerOffset;  // Distance in front of player the pointer uses as a reference
    private Transform truePos;  

    private NavMeshAgent pointer;
    private Transform pos;
    private Transform arrow;

    // Start is called before the first frame update
    void Start()
    {
        truePos = transform.parent;
        truePos.localPosition = new Vector3(0, 0, pointerOffset);
        pointer = GetComponent<NavMeshAgent>();
        pos = GetComponent<Transform>();
        arrow = truePos.parent.Find("pointing stuff").Find("pointer");  // Gets visible pointer
    }

    // Update is called once per frame
    void Update()
    {
        pointer.ResetPath();  // Resets path to recompute shortest path to destination
        pointer.destination = target.position;
        pos.localPosition = new Vector3(0, 0, 0);  // Sets pointer to default location - prevents it from moving towards destination, only rotates in its direction
        arrow.rotation = pos.rotation;  // sets rotation of visible pointer to the true pointer

    }
}
