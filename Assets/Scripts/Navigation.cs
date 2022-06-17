using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Navigation : MonoBehaviour
{
    public Transform target;
    private Transform truePos;

    private NavMeshAgent pointer;
    private Transform pos;

    // Start is called before the first frame update
    void Awake()
    {
        truePos = transform.parent;
        pointer = GetComponent<NavMeshAgent>();
        pos = GetComponent<Transform>();
    }

    // Update is called once per frame
    void Update()
    {
        pointer.destination = target.position;
        pos.position = new Vector3(truePos.position.x , truePos.position.y, truePos.position.z);

    }
}
