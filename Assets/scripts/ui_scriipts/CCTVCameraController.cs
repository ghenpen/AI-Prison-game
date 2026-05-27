
using UnityEngine;

public class CCTVCameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 2f;
    public float maxPanX = 30f;
    public float maxPanY = 20f;

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minFOV = 20f;
    public float maxFOV = 80f;

    [Header("Smoothing")]
    public float smoothSpeed = 8f;

    private Camera _cam;
    private float _targetFOV;
    private Vector2 _targetRotation;
    private Vector3 _initialEuler;
    private bool _isDragging;
    private Vector3 _lastMousePos;

    void Start()
    {
        _cam = GetComponent<Camera>();
        _targetFOV = _cam.fieldOfView;
        _initialEuler = transform.eulerAngles;
        _targetRotation = Vector2.zero;
    }

    void Update()
    {
        if (!_cam.gameObject.activeSelf) return; 

        HandlePan();
        HandleZoom();
        ApplySmooth();
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            _isDragging = true;
            _lastMousePos = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
            _isDragging = false;

        if (_isDragging)
        {
            Vector3 delta = Input.mousePosition - _lastMousePos;
            _lastMousePos = Input.mousePosition;

            _targetRotation.x -= delta.y * panSpeed * Time.deltaTime * 10f;
            _targetRotation.y += delta.x * panSpeed * Time.deltaTime * 10f;

            _targetRotation.x = Mathf.Clamp(_targetRotation.x, -maxPanY, maxPanY);
            _targetRotation.y = Mathf.Clamp(_targetRotation.y, -maxPanX, maxPanX);
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            _targetFOV -= scroll * zoomSpeed * 10f;
            _targetFOV = Mathf.Clamp(_targetFOV, minFOV, maxFOV);
        }
    }

    private void ApplySmooth()
    {
        
        _cam.fieldOfView = Mathf.Lerp(
            _cam.fieldOfView,
            _targetFOV,
            Time.deltaTime * smoothSpeed);

        
        Vector3 targetEuler = new Vector3(
            _initialEuler.x + _targetRotation.x,
            _initialEuler.y + _targetRotation.y,
            _initialEuler.z);

        transform.eulerAngles = Vector3.Lerp(
            transform.eulerAngles,
            targetEuler,
            Time.deltaTime * smoothSpeed);
    }

   
    public void ResetView()
    {
        _targetRotation = Vector2.zero;
        _targetFOV = 60f;
    }
}