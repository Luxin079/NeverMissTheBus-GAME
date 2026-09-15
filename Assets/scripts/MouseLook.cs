using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("Mouse Settings")]
    public float mouseSensitivity = 2f;

    public Transform playerBody;
    private float xRotation = 0f;

    void Start()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", mouseSensitivity);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }
}