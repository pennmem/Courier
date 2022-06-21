using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Navigation : MonoBehaviour
{
    public Transform target;
    public float pointerOffset;
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
        arrow = truePos.parent.Find("pointing stuff").Find("pointer");
    }

    // Update is called once per frame
    void Update()
    {
        pointer.destination = target.position;
        pos.position = new Vector3(truePos.position.x , truePos.position.y, truePos.position.z);
        arrow.rotation = pos.rotation;

    }
}
