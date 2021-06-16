using UnityEngine;
using System.Collections;
using Luminosity.IO;

public class Look : MonoBehaviour {

	public float speedH = 2.0f;
	public float speedV = 2.0f;

	private float yaw = 0.0f;
	private float pitch = 0.0f;

	void Update () {
		yaw += speedH * InputManager.GetAxis("Mouse X");
		pitch -= speedV * InputManager.GetAxis("Mouse Y");

		transform.localEulerAngles = new Vector3(pitch, yaw, 0.0f);
	}
}