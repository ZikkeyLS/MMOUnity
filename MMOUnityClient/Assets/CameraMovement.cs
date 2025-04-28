using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraMovement : MonoBehaviour
{
    [SerializeField] private float _acceleration = 50;
    [SerializeField] private float _sprintMultiplier = 4;
    [SerializeField] private float _lookSensitivity = 1;
    [SerializeField] private float _damping = 5;
    [SerializeField] private bool _focusOnEnable = true;

    private Vector3 _velocity = Vector3.zero;
    private Vector3 _moveInput = Vector3.zero;

    private void Update()
    {
        UpdateInput();

        _velocity = Vector3.Lerp(_velocity, Vector3.zero, _damping * Time.deltaTime);
        transform.position += _velocity * Time.deltaTime;
    }

    private void UpdateInput()
    {
        _velocity += GetAccelerationVector() * Time.deltaTime;

        Vector2 mouseDelta = _lookSensitivity * new Vector2(Input.GetAxis("Mouse X"), -Input.GetAxis("Mouse Y"));
        Quaternion horizontal = Quaternion.AngleAxis(mouseDelta.x, Vector3.up);
        Quaternion vertical = Quaternion.AngleAxis(mouseDelta.y, Vector3.right);
        transform.rotation = horizontal * transform.rotation * vertical;
    }

    private void AddMovement(KeyCode key, Vector3 dir)
    {
        if (Input.GetKey(key))
        {
            _moveInput += dir;
        }
    }

    private Vector3 GetAccelerationVector()
    {
        _moveInput = Vector3.zero;

        AddMovement(KeyCode.W, Vector3.forward);
        AddMovement(KeyCode.S, Vector3.back);
        AddMovement(KeyCode.D, Vector3.right);
        AddMovement(KeyCode.A, Vector3.left);
        AddMovement(KeyCode.Space, Vector3.up);
        AddMovement(KeyCode.LeftControl, Vector3.down);
        Vector3 direction = transform.TransformVector(_moveInput.normalized);

        if (Input.GetKey(KeyCode.LeftShift))
        {
            return direction * _acceleration * _sprintMultiplier;
        }

        return direction * _acceleration;
    }
}
