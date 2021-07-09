using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Luminosity.IO;

public class PlayerMovement : MonoBehaviour
{
    // JPB: TODO: Make these configuration variables
    private const bool NICLS_COURIER = true;

    protected float forwardSpeed = NICLS_COURIER ? 16f : 8f;
    protected float backwardSpeed = NICLS_COURIER ? 10f : 4f;
    protected float turnSpeed = NICLS_COURIER ? 80f : 45f;
    protected float turnThreshhold = 0.5f;

	public GameObject rotateMe;
	protected float maxRotation = 30f;

    private int freeze_level = 0;
    private Transform xform;

    void Start() {
        xform = gameObject.transform;
    }

    void Update ()
    {
        float turnAmount = InputManager.GetAxis("Horizontal");
        if (Mathf.Abs(turnAmount) < turnThreshhold)
            turnAmount = 0;
        turnAmount = turnAmount * turnSpeed * Time.deltaTime;
        if (!IsFrozen())
        {
            xform.Rotate(new Vector3(0, turnAmount, 0));

            //move forward or more slowly backward
            if (InputManager.GetAxis("Vertical") > 0)
                xform.position = Vector3.Lerp(xform.position, xform.position + InputManager.GetAxis("Vertical") * xform.forward, forwardSpeed * Time.deltaTime);
            else
                xform.position = Vector3.Lerp(xform.position, xform.position + InputManager.GetAxis("Vertical") * xform.forward, backwardSpeed * Time.deltaTime);
            
            //rotate the handlebars smoothly, limit to maxRotation
            rotateMe.transform.localRotation = Quaternion.Euler(rotateMe.transform.rotation.eulerAngles.x, InputManager.GetAxis("Horizontal") * maxRotation, rotateMe.transform.rotation.eulerAngles.z);
        }
    }

    public bool IsFrozen()
    {
        return freeze_level > 0;
    }

    // JPB: TODO: Fix this whole system
    public bool IsDoubleFrozen()
    {
        return freeze_level > 1;
    }

    public void Freeze()
    {
        freeze_level++;
    }

    public void Unfreeze()
    {
        freeze_level--;
    }

    public void Zero()
    {
        freeze_level = 0;
    }
}
