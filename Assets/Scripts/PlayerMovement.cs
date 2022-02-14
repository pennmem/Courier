using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Luminosity.IO;

public class PlayerMovement : MonoBehaviour
{
    // TODO: JPB: Make these configuration variables
    private const bool NICLS_COURIER = true;
    #if !UNITY_WEBGL
        private const bool COURIER_ONLINE = false;
    #else
        private const bool COURIER_ONLINE = true;
    #endif // !UNITY_WEBGL

    protected float maxTurnSpeed = NICLS_COURIER ? 50f : COURIER_ONLINE ? 120f : 45f;
    protected const float maxForwardSpeed = COURIER_ONLINE ? 18f : 10f;
    protected const float maxBackwardSpeed = COURIER_ONLINE ? 13f : 4f;

    protected const float rotDampingTime = 0.05f;

    protected const float joystickDeadZone = 0.02f;

    private int freeze_level = 0;

    private Vector3 originalPosition;
    private Quaternion originalRotation;

    private Rigidbody playerBody;
    public GameObject playerPerspective;

    public GameObject handlebars;
    protected const float maxHandlebarRotationX = 20f;
    protected const float maxHandlebarRotationY = 15f;

    private bool temporallySmoothedTurning = false;
    private bool sinSmoothedTurning = false;
    private bool cubicSmoothedTurning = true;

    void Start()
    {
        originalPosition = gameObject.transform.position;
        originalRotation = gameObject.transform.rotation;

        playerBody = GetComponent<Rigidbody>();
        #if !UNITY_WEBGL
            temporallySmoothedTurning = Config.Get(() => Config.temporallySmoothedTurning, false);
            sinSmoothedTurning = Config.Get(() => Config.sinSmoothedTurning, false);
            cubicSmoothedTurning = Config.Get(() => Config.cubicSmoothedTurning, true);
        #endif
    }

    public float horizontalInput;
    public float verticalInput;

    public Vector3 dampedHorizInput;
    public Vector3 horizVel = Vector3.zero;

    void Update()
    {
        if (temporallySmoothedTurning)
        {
            // This is only in Update because we want it locked to frame rate.
            // Because MoveRotation is used, the rotation doesn't occur until the next FixedUpdate
            // Also, adjusting the velocity doesn't change any position until the next FixedUpdate
            if (!IsFrozen())
            {
                horizontalInput = InputManager.GetAxis("Horizontal");
                if (sinSmoothedTurning)
                    horizontalInput = SinCurve(horizontalInput);
                else if (cubicSmoothedTurning)
                    horizontalInput = CubicCurve(horizontalInput);
                verticalInput = InputManager.GetAxis("Vertical");

                // Rotate the bike handlebars
                //handlebars.transform.localRotation = Quaternion.Euler(horizontalInput * maxHandlebarRotationX, dampedHorizInput * maxHandlebarRotationY, 0);

                // Rotate the player's perspective
                //playerPerspective.transform.localRotation = Quaternion.Euler(0, 0, -dampedHorizInput * 5f);

                // Rotate the player
                dampedHorizInput = Vector3.SmoothDamp(dampedHorizInput, Vector3.up * horizontalInput, ref horizVel, rotDampingTime);
                Quaternion deltaRotation = Quaternion.Euler(dampedHorizInput * maxTurnSpeed * Time.smoothDeltaTime);
                playerBody.MoveRotation(playerBody.rotation * deltaRotation);

                // Move the player
                if (verticalInput > joystickDeadZone)
                    playerBody.velocity = Vector3.ClampMagnitude(playerBody.transform.forward * (verticalInput - Mathf.Abs(dampedHorizInput.y) * 0.2f) * maxForwardSpeed, maxForwardSpeed);
                else if (verticalInput < -joystickDeadZone)
                    playerBody.velocity = Vector3.ClampMagnitude(playerBody.transform.forward * (verticalInput - Mathf.Abs(dampedHorizInput.y) * 0.2f) * maxBackwardSpeed, maxBackwardSpeed);
                else
                    playerBody.velocity = new Vector3(0, 0, 0);
            }
        }
    }

    float SinCurve(float x)
    {
        var xAbs = Mathf.Abs(x);
        var y = 0.5f * (Mathf.Sin(Mathf.PI * xAbs - Mathf.PI / 2) + 1);
        return y * Mathf.Sign(x);
    }

    float CubicCurve(float x)
    {
        var xAbs = Mathf.Abs(x);
        var y = (xAbs < 0.5f)
                ? 4 * Mathf.Pow(xAbs, 3)
                : 1 - Mathf.Pow(-2 * xAbs + 2, 3) / 2;
        return y * Mathf.Sign(x);
    }

    void FixedUpdate()
    {
        if (!temporallySmoothedTurning)
        {
            horizontalInput = InputManager.GetAxis("Horizontal");
            if (sinSmoothedTurning)
                horizontalInput = SinCurve(horizontalInput);
            else if (cubicSmoothedTurning)
                horizontalInput = CubicCurve(horizontalInput);
            verticalInput = InputManager.GetAxis("Vertical");
            if (!IsFrozen())
            {
                // Rotate the bike handlebars
                //handlebars.transform.localRotation = Quaternion.Euler(horizontalInput * maxHandlebarRotationX, horizontalInput * maxHandlebarRotationY, 0);

                // Rotate the player's perspective
                //playerPerspective.transform.localRotation = Quaternion.Euler(0, 0, -horizontalInput * 5f);

                // Rotate the player
                if (Mathf.Abs(horizontalInput) > joystickDeadZone)
                {
                    Quaternion deltaRotation = Quaternion.Euler(Vector3.up * horizontalInput * maxTurnSpeed * Time.fixedDeltaTime);
                    playerBody.MoveRotation(playerBody.rotation * deltaRotation);
                }

                // Move the player
                if (verticalInput > joystickDeadZone)
                    playerBody.velocity = Vector3.ClampMagnitude(playerBody.transform.forward * verticalInput * maxForwardSpeed, maxForwardSpeed);
                else if (verticalInput < -joystickDeadZone)
                    playerBody.velocity = Vector3.ClampMagnitude(playerBody.transform.forward * verticalInput * maxBackwardSpeed, maxBackwardSpeed);
                else
                    playerBody.velocity = new Vector3(0, 0, 0);
            }
        }
    }

    public bool IsFrozen()
    {
        return freeze_level > 0;
    }

    // TODO: JPB: Fix this whole system
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
