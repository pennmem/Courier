using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// using Luminosity.IO;

using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    // TODO: JPB: Make these configuration variables
    private const bool NICLS_COURIER = true;
#if !UNITY_WEBGL
    private const bool COURIER_ONLINE = false;
#else
    private const bool COURIER_ONLINE = true;
#endif // !UNITY_WEBGL

    protected float maxTurnSpeed = Config.maxTurnSpeed; //45f;
    protected float maxForwardSpeed = Config.maxForwardSpeed;//10f;
    protected float maxBackwardSpeed = Config.maxBackwardSpeed; //4f;
    private float forwardSpeed;

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
    public float sprintMultiplier = Config.sprintMultiplier;   // tune

    // Input System fields
    private InputAction moveAction;
    private InputAction sprintAction;
    private Vector2 moveInput;
    private bool sprintHeld;


    void Start()
    {
        originalPosition = gameObject.transform.position;
        originalRotation = gameObject.transform.rotation;

        playerBody = GetComponent<Rigidbody>();

        // Setup Input Actions
        moveAction = new InputAction("Move", binding: "<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("Dpad")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.Enable();

        sprintAction = new InputAction("Sprint", binding: "<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/buttonWest"); // e.g. A button
        sprintAction.Enable();
    }

    public float horizontalInput;
    public float verticalInput;

    public Vector3 dampedHorizInput;
    public Vector3 horizVel = Vector3.zero;

    void Update()
    {
        // Read input from Input System
        moveInput = moveAction.ReadValue<Vector2>();
        sprintHeld = sprintAction.ReadValue<float>() > 0.5f;

        if (temporallySmoothedTurning)
        {
            if (!IsFrozen())
            {
                horizontalInput = moveInput.x;
                if (sinSmoothedTurning)
                    horizontalInput = SinCurve(horizontalInput);
                else if (cubicSmoothedTurning)
                    horizontalInput = CubicCurve(horizontalInput);
                verticalInput = moveInput.y;

                dampedHorizInput = Vector3.SmoothDamp(dampedHorizInput, Vector3.up * horizontalInput, ref horizVel, rotDampingTime);
                Quaternion deltaRotation = Quaternion.Euler(dampedHorizInput * maxTurnSpeed * Time.smoothDeltaTime);
                playerBody.MoveRotation(playerBody.rotation * deltaRotation);

                float speedMult = IsSprinting() ? sprintMultiplier : 1f;

                if (verticalInput > joystickDeadZone)
                {
                    float spd = maxForwardSpeed * speedMult;
                    playerBody.velocity = Vector3.ClampMagnitude(
                        playerBody.transform.forward * (verticalInput - Mathf.Abs(dampedHorizInput.y) * 0.2f) * spd,
                        spd
                    );
                }
                else if (verticalInput < -joystickDeadZone)
                {
                    playerBody.velocity = Vector3.ClampMagnitude(
                        playerBody.transform.forward * (verticalInput - Mathf.Abs(dampedHorizInput.y) * 0.2f) * maxBackwardSpeed,
                        maxBackwardSpeed
                    );
                }
                else
                {
                    playerBody.velocity = new Vector3(0, 0, 0);
                }
            }
        }
    }

    private bool IsSprinting()
    {
        return sprintHeld;
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
            // Example: adjust forwardSpeed with keys (optional, can be mapped to InputSystem as well)
            if (Keyboard.current != null)
            {
                if (Keyboard.current.zKey.isPressed || Keyboard.current.leftShiftKey.isPressed) forwardSpeed = maxForwardSpeed * 0.5f;
                else if (Keyboard.current.rKey.isPressed || Keyboard.current.leftCommandKey.isPressed) forwardSpeed = maxForwardSpeed * 1.5f;
                else forwardSpeed = maxForwardSpeed;
            }

            horizontalInput = moveInput.x;
            if (sinSmoothedTurning)
                horizontalInput = SinCurve(horizontalInput);
            else if (cubicSmoothedTurning)
                horizontalInput = CubicCurve(horizontalInput);
            verticalInput = moveInput.y;
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

                float speedMult = IsSprinting() ? sprintMultiplier : 1f;

                if (verticalInput > joystickDeadZone)
                {
                    float spd = maxForwardSpeed * speedMult;
                    playerBody.velocity = Vector3.ClampMagnitude(
                        playerBody.transform.forward * (verticalInput - Mathf.Abs(dampedHorizInput.y) * 0.2f) * spd,
                        spd
                    );
                }
                else if (verticalInput < -joystickDeadZone)
                {
                    playerBody.velocity = Vector3.ClampMagnitude(
                        playerBody.transform.forward * (verticalInput - Mathf.Abs(dampedHorizInput.y) * 0.2f) * maxBackwardSpeed,
                        maxBackwardSpeed
                    );
                }
                else
                {
                    playerBody.velocity = new Vector3(0, 0, 0);
                }
            }
        }
    }
    private void OnEnable()
    {
        moveAction?.Enable();
        sprintAction?.Enable();
    }

    private void OnDisable()
    {
        moveAction?.Disable();
        sprintAction?.Disable();
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
