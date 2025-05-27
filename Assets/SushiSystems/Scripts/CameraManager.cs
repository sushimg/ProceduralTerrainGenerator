using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Linq;
using TMPro;

public class CameraManager : MonoBehaviour
{
    [SerializeField] private Camera cam;

    [Header("Movement")]
    [Range(1f, 3f)] public float moveSensitivity;
    [SerializeField] private float moveLerpSpeed;
    [SerializeField] private Dictionary<string, bool> doubleTap = new Dictionary<string, bool>();

    [Header("Zoom")]
    [SerializeField] private float minZoomDistance = 5f;
    [SerializeField] private float maxZoomDistance = 20f;
    [Range(1f, 20f)] public float zoomSensitivity = 1.2f;

    [Header("Rotation")]
    [Range(1f, 20f)] public float rotationSensitivity = 1f;
    [SerializeField] private float minVerticalAngle = 10f;
    [SerializeField] private float maxVerticalAngle = 50f;

    [Header("Bordering")]
    [SerializeField] private bool clampPosition = true;
    [SerializeField] private Vector2 clampX = new Vector2(-100, 100);
    [SerializeField] private Vector2 clampZ = new Vector2(-100, 100);

    public float targetX = 0f;
    public float targetY = 0f;

    [SerializeField] private Terrain terrain;
    private int movementButtonPressCount = 0;


    private void Awake() => Initialization();

    private void Update()
    {
        ApplyMovement();
        HandlePlayerInput();
        ClampPositionAndAngles();
    }

    private void LateUpdate() => TerrainBoundMovement();

    #region Initialization

    private void Initialization()
    {
        if (cam == null)
            cam = GetComponentInChildren<Camera>();

        doubleTap.Add("Up", false);
        doubleTap.Add("Down", false);
        doubleTap.Add("Left", false);
        doubleTap.Add("Right", false);
    }

    #endregion

    #region Handle Player Inputs

    private void HandlePlayerInput()
    {
        List<Touch> gameTouches = new List<Touch>();

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (!IsTouchOverUI(t))
                gameTouches.Add(t);
        }

        if (movementButtonPressCount == 0)
        {
            if (gameTouches.Count == 1)
            {
                //Rotation
                HandleRotation(gameTouches[0]);
            }
            else if (gameTouches.Count == 2)
            {
                //Pinch Zoom
                HandlePinchZoom(gameTouches[0], gameTouches[1]);
            }
        }
        else
        {
            if (gameTouches.Count >= 1)
                HandleRotation(gameTouches[0]);
        }
    }

    #endregion

    #region Pinch Zoom & Rotation

    private void HandlePinchZoom(Touch t0, Touch t1)
    {
        Vector2 t0PrevPos = t0.position - t0.deltaPosition;
        Vector2 t1PrevPos = t1.position - t1.deltaPosition;

        float prevMag = (t0PrevPos - t1PrevPos).magnitude;
        float currMag = (t0.position - t1.position).magnitude;
        float diff = currMag - prevMag;

        float zoomAmountY = Mathf.Sin(Mathf.Deg2Rad * 40f) * diff * zoomSensitivity * Time.deltaTime;
        float zoomAmountZ = Mathf.Cos(Mathf.Deg2Rad * 40f) * diff * zoomSensitivity * Time.deltaTime;

        Vector3 localPos = cam.transform.localPosition;
        float newY = localPos.y - zoomAmountY;
        float newZ = localPos.z - zoomAmountZ;

        newY = Mathf.Clamp(newY, minZoomDistance, maxZoomDistance);
        float zoomedYRatio = newY / localPos.y;
        newZ = localPos.z * zoomedYRatio;

        cam.transform.localPosition = new Vector3(localPos.x, newY, newZ);
    }

    private void HandleRotation(Touch touch)
    {
        if (touch.phase == TouchPhase.Moved)
        {
            float deltaX = touch.deltaPosition.x * rotationSensitivity * Time.deltaTime;
            float deltaY = touch.deltaPosition.y * rotationSensitivity * Time.deltaTime;

            //Horizontal
            transform.Rotate(0f, deltaX, 0f, Space.World);

            //Vertical
            Vector3 camEuler = cam.transform.localEulerAngles;

            float desiredX = camEuler.x - deltaY;

            desiredX = NormalizeAngle(desiredX);
            desiredX = Mathf.Clamp(desiredX, minVerticalAngle, maxVerticalAngle);

            cam.transform.localEulerAngles = new Vector3(desiredX, camEuler.y, camEuler.z);
        }
    }

    #endregion

    #region Movement

    private void ApplyMovement()
    {
        float currentX = Mathf.Lerp(0, targetX, moveLerpSpeed * Time.deltaTime);
        float currentY = Mathf.Lerp(0, targetY, moveLerpSpeed * Time.deltaTime);

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 moveDir = (forward * currentY + right * currentX) * (moveSensitivity * 200f) * Time.deltaTime;

        transform.position += moveDir;
    }

    private void TerrainBoundMovement()
    {
        Vector3 pos = transform.position;
        float terrainHeight = terrain.SampleHeight(pos);

        pos.y = terrainHeight;
        transform.position = pos;
    }

    #endregion

    #region Bordering

    private void ClampPositionAndAngles()
    {
        if (clampPosition)
        {
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, clampX.x, clampX.y);
            pos.z = Mathf.Clamp(pos.z, clampZ.x, clampZ.y);
            transform.position = pos;
        }
    }

    #endregion

    #region Button Methods
    //UP, 0
    public void OnUpButtonDown()
    {
        movementButtonPressCount++;

        if (doubleTap["Up"])
            targetY = 3f;
        else
            targetY = 1f;
    }
    public void OnUpButtonDoubleTap()
    {
        doubleTap["Up"] = true;
    }
    public void OnUpButtonUp()
    {
        movementButtonPressCount--;
        doubleTap["Up"] = false;
        targetY = 0f;
    }

    //DOWN, 1
    public void OnDownButtonDown()
    {
        movementButtonPressCount++;

        if (doubleTap["Down"])
            targetY = -3f;
        else
            targetY = -1f;
    }
    public void OnDownButtonDoubleTap()
    {
        doubleTap["Down"] = true;
    }
    public void OnDownButtonUp()
    {
        movementButtonPressCount--;
        doubleTap["Down"] = false;
        targetY = 0f;
    }

    //LEFT, 2
    public void OnLeftButtonDown()
    {
        movementButtonPressCount++;

        if (doubleTap["Left"])
            targetX = -3f;
        else
            targetX = -1f;
    }
    public void OnLeftButtonDoubleTap()
    {
        doubleTap["Left"] = true;
    }
    public void OnLeftButtonUp()
    {
        movementButtonPressCount--;
        doubleTap["Left"] = false;
        targetX = 0f;
    }

    //RIGHT, 3
    public void OnRightButtonDown()
    {
        movementButtonPressCount++;

        if (doubleTap["Right"])
            targetX = 3f;
        else
            targetX = 1f;
    }
    public void OnRightButtonDoubleTap()
    {
        doubleTap["Right"] = true;
    }
    public void OnRightButtonUp()
    {
        movementButtonPressCount--;
        doubleTap["Right"] = false;
        targetX = 0f;
    }
    #endregion

    #region Misc

    private bool IsTouchOverUI(Touch touch)
    {
        if (!EventSystem.current) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = touch.position;

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        return results.Count > 0;
    }

    private float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }

    /*private void GatherDataFromPlayerPrefs()
    {
        zoomSensitivity = PlayerPrefs.GetFloat("Zoom Sensitivity");
        moveSensitivity = PlayerPrefs.GetFloat("Movement Sensitivity");
        rotationSensitivity = PlayerPrefs.GetFloat("Rotation Sensitivity");
    }*/

    #endregion
}
