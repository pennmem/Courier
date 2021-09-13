using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Luminosity.IO;

public class PlayerMovement : MonoBehaviour
{
    // JPB: TODO: Make these configuration variables
    private const bool NICLS_COURIER = true;
    private const bool SHOW_FPS = true;

    protected float maxTurnSpeed = NICLS_COURIER ? 100f : 45f;
    protected float maxForwardForce = 160f;
    protected float maxBackwardForce = 160f;
    protected float forceReductionFactorForRotation = 0.2f;

    private int freeze_level = 0;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private Rigidbody playerBody;
    public GameObject playerPerspective;

    public GameObject handlebars;
    protected float maxHandlebarRotationX = 30f;
    protected float maxHandlebarRotationY = 15f;

    private float deltaTime;
    private float fixedDeltaTime;

    void Start()
    {
        originalPosition = gameObject.transform.position;
        originalRotation = gameObject.transform.rotation;

        playerBody = GetComponent<Rigidbody>();
        // The drag of 8.3330 creates a max speed of 16 with a max force of 160
        // The drag of 14.2857 creates a max speed of 8 with a max force of 160
        playerBody.drag = NICLS_COURIER ? 8.333f : 14.2857f;
    }

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void FixedUpdate ()
    {
        fixedDeltaTime = Time.fixedDeltaTime;
        if (!IsFrozen())
        {
            float horizontalInput = InputManager.GetAxisRaw("Horizontal");
            float verticalInput = InputManager.GetAxisRaw("Vertical");

            // Rotate the player
            // transform.Rotate(0, threshedHorizontalInput * turnSpeed * Time.deltaTime, 0);
            Quaternion deltaRotation = Quaternion.Euler(Vector3.up * horizontalInput * maxTurnSpeed * Time.fixedDeltaTime);
            playerBody.MoveRotation(playerBody.rotation * deltaRotation);

            // Move the player
            if (verticalInput > 0.05)
                playerBody.AddRelativeForce(Vector3.forward * maxForwardForce * (verticalInput - Mathf.Abs(horizontalInput) * forceReductionFactorForRotation));
            else if (verticalInput < -0.05)
                playerBody.AddRelativeForce(Vector3.forward * maxBackwardForce * (verticalInput + Mathf.Abs(horizontalInput) * forceReductionFactorForRotation));
            //playerBody.velocity = Vector3.ClampMagnitude(playerBody.velocity, forwardSpeed);

            // Rotate the bike handlebars
            handlebars.transform.localRotation = Quaternion.Euler(horizontalInput * maxHandlebarRotationX, horizontalInput * maxHandlebarRotationY, 0);

            // Rotate the player's perspective
            //playerPerspective.transform.localRotation = Quaternion.Euler(0, 0, -horizontalInput * 5f);
        }
    }

    // This FPS monitor is from http://wiki.unity3d.com/index.php?title=FramesPerSecond
    void OnGUI()
    {
        if (SHOW_FPS)
        {
            int w = Screen.width, h = Screen.height;
            GUIStyle style = new GUIStyle();

            Rect rect = new Rect(0, 0, w, h * 2 / 100);
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = h * 4 / 100;
            style.normal.textColor = new Color(0.5f, 0.0f, 0.0f, 1.0f);
            float msec = deltaTime * 1000.0f;
            float fps = 1.0f / deltaTime;
            float fixedFps = 1.0f / fixedDeltaTime;
            string text = string.Format("{0:0.0} ms ({1:0.} fps) ({1:0.} fixed fps)", msec, fps, fixedFps);
            GUI.Label(rect, text, style);
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

    public void Reset()
    {
        gameObject.transform.position = originalPosition;
        gameObject.transform.rotation = originalRotation;
    }
}
