using UnityEngine;
using UnityEngine.EventSystems;

public sealed class ObjOrbitCamera : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 0.22f;
    [SerializeField] private float panSpeed = 0.0025f;
    [SerializeField] private float zoomSpeed = 0.8f;
    [SerializeField] private float minimumDistance = 0.25f;
    [SerializeField] private float maximumDistance = 500f;

    private Vector3 target;
    private float distance = 6f;
    private float yaw = 30f;
    private float pitch = 20f;
    private bool hasFocus;

    public void Focus(Bounds bounds)
    {
        target = bounds.center;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
        distance = Mathf.Clamp(radius * 2.8f, minimumDistance, maximumDistance);
        yaw = 30f;
        pitch = 20f;
        hasFocus = true;
        ApplyTransform();
    }

    private void LateUpdate()
    {
        if (!hasFocus)
        {
            return;
        }

        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!pointerOverUI && Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
        }

        if (!pointerOverUI && Input.GetMouseButton(2))
        {
            Vector3 pan = (-transform.right * Input.GetAxis("Mouse X") - transform.up * Input.GetAxis("Mouse Y"))
                          * distance * panSpeed;
            target += pan;
        }

        if (!pointerOverUI)
        {
            distance = Mathf.Clamp(distance - Input.mouseScrollDelta.y * zoomSpeed, minimumDistance, maximumDistance);
        }

        ApplyTransform();
    }

    private void ApplyTransform()
    {
        Quaternion orbitRotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.rotation = orbitRotation;
        transform.position = target + orbitRotation * Vector3.back * distance;
    }
}
