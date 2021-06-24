using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControllerTest : MonoBehaviour {

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update ()
    {
        Debug.Log(Input.GetAxis("Horizontal"));
        Debug.Log(Input.GetAxis("Vertical"));
        Debug.Log(Input.GetButton("Continue"));
        Debug.Log(Input.GetButton("Pause"));
        Debug.Log(Input.GetButton("Secret"));
	}
}
