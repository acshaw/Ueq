using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] Transform cameraHolder;
    [SerializeField] float moveSpeed = 5f;
    [SerializeField] float sprintSpeed = 9f;
    [SerializeField] float jumpHeight = 1.5f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float lookSensitivity = 0.15f;
    [SerializeField] float maxPitch = 80f;

    CharacterController _cc;
    float _verticalVelocity;
    float _pitch;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        // Mouse look
        if (mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            transform.Rotate(0f, delta.x * lookSensitivity, 0f);
            _pitch = Mathf.Clamp(_pitch - delta.y * lookSensitivity, -maxPitch, maxPitch);
            if (cameraHolder) cameraHolder.localEulerAngles = new Vector3(_pitch, 0f, 0f);
        }

        // WASD movement
        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        float speed = kb.leftShiftKey.isPressed ? sprintSpeed : moveSpeed;
        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f) move.Normalize();

        // Gravity and jump
        if (_cc.isGrounded) _verticalVelocity = -2f;
        if (kb.spaceKey.wasPressedThisFrame && _cc.isGrounded)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        _verticalVelocity += gravity * Time.deltaTime;

        _cc.Move((move * speed + Vector3.up * _verticalVelocity) * Time.deltaTime);
    }
}
