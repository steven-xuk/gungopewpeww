// VRCharacterController.cs
using UnityEngine;
using UnityEngine.XR;

[RequireComponent(typeof(CharacterController))]
public class VRCharacterController : MonoBehaviour
{
    [Header("Input Settings")]
    [Tooltip("Which hand's input to read")]
    public XRNode inputSource = XRNode.LeftHand;
    private Vector2 inputAxis;
    private bool interactPressed;
    private float triggerValue;

    [Header("Movement Settings")]
    [Tooltip("Movement speed in meters per second")]
    public float speed = 1.5f;
    [Tooltip("Jump force applied upwards")]
    public float jumpForce = 1.5f;
    [Tooltip("Gravity acceleration (negative downwards)")]
    public float gravity = -9.81f;
    private float verticalVelocity;

    private CharacterController characterController;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Get the VR input device
        InputDevice device = InputDevices.GetDeviceAtXRNode(inputSource);
        if (!device.isValid)
            return;

        // Read movement joystick
        device.TryGetFeatureValue(CommonUsages.primary2DAxis, out inputAxis);
        // Read jump trigger (analog)
        device.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);
        // Read interaction button (primary button)
        device.TryGetFeatureValue(CommonUsages.primaryButton, out interactPressed);

        // Handle jump
        if (triggerValue > 0.2f && characterController.isGrounded)
        {
            verticalVelocity = jumpForce;
        }

        // Apply gravity when not grounded
        if (!characterController.isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        else if (verticalVelocity < 0)
        {
            verticalVelocity = 0;
        }
    }

    void FixedUpdate()
    {
        // Orient movement relative to headset forward
        Transform head = Camera.main.transform;
        Vector3 direction = head.forward * inputAxis.y + head.right * inputAxis.x;
        direction.y = 0;
        Vector3 move = direction * speed * Time.fixedDeltaTime;

        // Apply vertical movement (jump/gravity)
        move.y = verticalVelocity * Time.fixedDeltaTime;

        characterController.Move(move);
    }
}
