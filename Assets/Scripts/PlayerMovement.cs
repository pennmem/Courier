using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
	protected float forwardSpeed = 16f;
	protected float backwardSpeed = 10f;
	protected float turnSpeed = 80f;
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
        float turnAmount = Input.GetAxis("Horizontal");
        if (Mathf.Abs(turnAmount) < turnThreshhold)
            turnAmount = 0;
        turnAmount = turnAmount * turnSpeed * Time.deltaTime;
        if (!IsFrozen())
        {
            xform.Rotate(new Vector3(0, turnAmount, 0));

            //move forward or more slowly backward
            if (Input.GetAxis("Vertical") > 0)
                xform.position = Vector3.Lerp(xform.position, xform.position + Input.GetAxis("Vertical") * xform.forward, forwardSpeed * Time.deltaTime);
            else
                xform.position = Vector3.Lerp(xform.position, xform.position + Input.GetAxis("Vertical") * xform.forward, backwardSpeed * Time.deltaTime);
            
            //rotate the handlebars smoothly, limit to maxRotation
            rotateMe.transform.localRotation = Quaternion.Euler(rotateMe.transform.rotation.eulerAngles.x, Input.GetAxis("Horizontal") * maxRotation, rotateMe.transform.rotation.eulerAngles.z);
        }
    }

    public bool IsFrozen()
    {
        return freeze_level > 0;
    }

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
