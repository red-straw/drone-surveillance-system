using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Drone_Controller : MonoBehaviour
{
    [Header("Patrol Settings")]
    public List<Transform> waypoints;
    public float moveSpeed = 5.0f;
    public float rotationSpeed = 2.0f;
    public float patrolAltitude = 50.0f;

    [Header("Tracking Settings")]
    public float trackingSpeed = 8.0f;
    public float trackingTurnSpeed = 100.0f;
    public float safeReturnAltitude = 20.0f;
    public float takeoffSpeed = 3.0f;

    [Header("Advanced Tracking")]
    public float minDetectionArea = 10f;
    public float lostTimeout = 3.0f;
    public float minConfidence = 0.1f;

    [Header("Control Sensitivity")]
    public float deadzone = 100.0f;
    public float dataSmoothFactor = 15.0f;
    public float predictionFactor = 1.5f;

    [Header("YOLO Resolution Info")]
    public float yoloImageWidth = 1920f;
    public float yoloImageHeight = 1080f;

    private float screenCenterX = 0f;
    private float screenCenterY = 0f;

    private int currentWaypointIndex = 0;
    private bool isPatrolling = false;
    private bool isTracking = false;

    private Camera droneCamera;
    private Quaternion initialCameraRotation;

    private YoloDetection currentData;
    private float lastDataTime;

    private float _lastDbgTime = 0f;

    private float smoothedCenterX = 0f;
    private float smoothedArea = 0f;
    private float prevRawCenterX = 0f;

    void Start()
    {
        droneCamera = GetComponentInChildren<Camera>();
        if (droneCamera != null)
        {
            initialCameraRotation = droneCamera.transform.localRotation;
            screenCenterX = yoloImageWidth / 2f;
            screenCenterY = yoloImageHeight / 2f;
        }

        isPatrolling = false;
        isTracking = false;
    }

    public void StartPatrol()
    {
        if (isTracking) return;

        isPatrolling = false;
        isTracking = false;

        StopAllCoroutines();
        StartCoroutine(TakeOffAndStartPatrol());
    }

    public void StartTracking(YoloDetection detectionData)
    {
        if (detectionData.confidence < minConfidence)
        {
            return;
        }

        float width = detectionData.box[2] - detectionData.box[0];
        float height = detectionData.box[3] - detectionData.box[1];
        float area = width * height;

        if (area < minDetectionArea)
        {
            return;
        }

        if (isTracking)
        {
            UpdateTrackingTarget(detectionData);
            return;
        }

        UnityEngine.Debug.Log($"[추적 시작] Conf: {detectionData.confidence}, Area: {area:F0}");

        isPatrolling = false;
        isTracking = true;

        StopAllCoroutines();

        float x1 = detectionData.box[0];
        float x2 = detectionData.box[2];
        float currentX = (x1 + x2) / 2.0f;

        smoothedCenterX = currentX;
        prevRawCenterX = currentX;
        smoothedArea = area;

        UpdateTrackingTarget(detectionData);

        // 추적 시작 5초 후부터 5초 간격으로 증거 사진 캡처
        StartCoroutine(AutoCaptureRoutine());
    }

    // 5초 대기 후 5초마다 캡처하는 코루틴
    private IEnumerator AutoCaptureRoutine()
    {
        UnityEngine.Debug.Log("[채증] 5초 후 자동 캡처 시작...");

        // 추적 시작 후 5초 대기
        yield return new WaitForSeconds(5.0f);

        while (isTracking)
        {
            CaptureAndSaveScreenshot();
            // 5초 간격으로 반복
            yield return new WaitForSeconds(5.0f);
        }
    }

    public void UpdateTrackingTarget(YoloDetection detectionData)
    {
        if (!isTracking) return;
        if (detectionData.confidence < minConfidence) return;

        if (detectionData != null && detectionData.box != null)
        {
            currentData = detectionData;
            lastDataTime = Time.time;
        }
    }

    public void ReturnToPad()
    {
        isPatrolling = false;
        isTracking = false;
        currentData = null;
        StopAllCoroutines();

        if (droneCamera != null) StartCoroutine(ResetCameraRotation());
        StartCoroutine(ReturnToPadCoroutine());
    }

    void Update()
    {
        if (isTracking)
        {
            if (currentData != null && (Time.time - lastDataTime < lostTimeout))
            {
                TrackByVisualServoing();
            }
            else
            {
                if (Time.time - lastDataTime > lostTimeout)
                {
                    UnityEngine.Debug.Log($"[추적 종료] 데이터 끊김({lostTimeout}초). 순찰 복귀.");
                    isTracking = false;
                    StartPatrol();
                }
            }
            return;
        }

        if (isPatrolling && waypoints != null && waypoints.Count > 0)
        {
            PatrolMovement();
        }
    }

    private void PatrolMovement()
    {
        Transform targetWaypoint = waypoints[currentWaypointIndex];
        transform.position = Vector3.MoveTowards(transform.position, targetWaypoint.position, moveSpeed * Time.deltaTime);

        Vector3 direction = targetWaypoint.position - transform.position;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), rotationSpeed * Time.deltaTime);
        }

        if (Vector3.Distance(transform.position, targetWaypoint.position) < 0.1f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        }
    }

    private float prevRawX1 = 0f;
    private float prevRawY1 = 0f;

    private void TrackByVisualServoing()
    {
        if (currentData == null || currentData.box == null || currentData.box.Length < 4) return;

        float x1 = currentData.box[0];
        float x2 = currentData.box[2];
        float y1 = currentData.box[1];
        float y2 = currentData.box[3];

        float rawCenterX = (x1 + x2) / 2f;
        float rawArea = Mathf.Abs(x2 - x1) * Mathf.Abs(y2 - y1);
        float boxHeight = Mathf.Abs(y2 - y1);
        float boxCenterY = (y1 + y2) / 2f;

        smoothedCenterX = Mathf.Lerp(smoothedCenterX, rawCenterX, Time.deltaTime * dataSmoothFactor);

        bool coordsLooksNormalized = (x1 <= 1f && x2 <= 1f && y1 <= 1f && y2 <= 1f);

        float normalizedRawArea;
        float boxHeightNorm;

        if (coordsLooksNormalized)
        {
            normalizedRawArea = rawArea;
            boxHeightNorm = boxHeight;
        }
        else
        {
            float imageArea = Mathf.Max(1f, yoloImageWidth * yoloImageHeight);
            normalizedRawArea = rawArea / imageArea;
            boxHeightNorm = boxHeight / Mathf.Max(1f, yoloImageHeight);
        }

        smoothedArea = Mathf.Lerp(smoothedArea, normalizedRawArea, Time.deltaTime * dataSmoothFactor);

        // 객체 이동 감지
        bool objectIsMoving = Mathf.Abs(x1 - prevRawX1) > 0.5f || Mathf.Abs(y1 - prevRawY1) > 0.5f; // 픽셀 기준
        prevRawX1 = x1;
        prevRawY1 = y1;

        float velocityX = rawCenterX - prevRawCenterX;
        float predictedX = smoothedCenterX + (velocityX * predictionFactor);
        prevRawCenterX = rawCenterX;

        float errorX = predictedX - screenCenterX;

        if (Mathf.Abs(errorX) > deadzone)
        {
            float ratioX = errorX / (yoloImageWidth * 0.5f);
            float turnPower = Mathf.Clamp01(Mathf.Abs(ratioX));
            turnPower = turnPower * turnPower * 3.0f + 0.2f;
            float dir = Mathf.Sign(errorX);
            float rotateY = dir * trackingTurnSpeed * turnPower * Time.deltaTime;
            transform.Rotate(Vector3.up, rotateY);
        }

        float forwardSpeedFactor = 1f;

        float stopNorm = 0.004f;
        float slowNorm = 0.0025f;
        float stopBoxH = 0.09f;
        float slowBoxH = 0.06f;

        // 객체가 거의 움직이지 않을 때만 정지
        if ((smoothedArea >= stopNorm || boxHeightNorm >= stopBoxH) && !objectIsMoving)
        {
            forwardSpeedFactor = 0f;
        }
        else
        {
            if (boxHeightNorm > slowBoxH)
            {
                float t = Mathf.InverseLerp(slowBoxH, stopBoxH, boxHeightNorm);
                t = Mathf.Pow(t, 1.1f);
                forwardSpeedFactor = Mathf.Lerp(1f, 0f, t);
            }
            else if (smoothedArea > slowNorm)
            {
                float t = Mathf.InverseLerp(slowNorm, stopNorm, smoothedArea);
                t = Mathf.Pow(t, 1.1f);
                forwardSpeedFactor = Mathf.Lerp(1f, 0f, t);
            }
            else
            {
                forwardSpeedFactor = 1f;
            }
        }

        float verticalError = screenCenterY - boxCenterY;
        bool shouldReverse = verticalError < -0.05f;

        float finalSpeedFactor = forwardSpeedFactor;
        if (shouldReverse) finalSpeedFactor = -0.6f;

        Vector3 move = Vector3.zero;

        Vector3 forwardVec = transform.forward;
        forwardVec.y = 0f;
        forwardVec.Normalize();

        move += forwardVec * trackingSpeed * finalSpeedFactor * Time.deltaTime;

        transform.Translate(move, Space.World);

        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(transform.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, 5f * Time.deltaTime);
        }

        if (transform.position.y < 0.5f)
        {
            Vector3 pos = transform.position;
            pos.y = 0.5f;
            transform.position = pos;
        }

    }

    private IEnumerator TakeOffAndStartPatrol()
    {
        UnityEngine.Debug.Log("[이륙] 목표 고도로 상승 중...");

        Vector3 takeoffPos = transform.position;
        // 현재 위치에서 Y축으로만 상승
        takeoffPos.y = patrolAltitude;

        //수직 상승 로직 (순찰 안 함)
        while (Mathf.Abs(transform.position.y - patrolAltitude) > 0.1f)
        {
            // Y축만 변경
            Vector3 newPos = transform.position;
            newPos.y = Mathf.MoveTowards(transform.position.y, patrolAltitude, takeoffSpeed * Time.deltaTime);
            transform.position = newPos;
            yield return null;
        }

        UnityEngine.Debug.Log("[순찰 시작] 이륙 완료. 웨이포인트 이동 시작.");

        //상승 완료 후 비로소 순찰 시작
        isPatrolling = true;
    }

    private IEnumerator ReturnToPadCoroutine()
    {
        GameObject pad = GameObject.Find("LandingPad");
        if (pad != null)
        {
            Vector3 padPos = pad.transform.position;

            float padTopY = padPos.y;
            Collider padCol = pad.GetComponent<Collider>();
            if (padCol != null)
            {
                padTopY = padCol.bounds.max.y;
            }

            float targetLandingY = padTopY - 0.17f;
            Vector3 landingPos = new Vector3(padPos.x, targetLandingY, padPos.z);

            float correctedAltitude = Mathf.Max(safeReturnAltitude, padTopY + 2f);

            if (transform.position.y < correctedAltitude)
            {
                Vector3 upPos = new Vector3(transform.position.x, correctedAltitude, transform.position.z);

                while (Vector3.Distance(transform.position, upPos) > 0.1f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, upPos, takeoffSpeed * Time.deltaTime);
                    yield return null;
                }
            }

            Vector3 abovePad = new Vector3(padPos.x, correctedAltitude, padPos.z);

            while (Vector3.Distance(transform.position, abovePad) > 0.1f)
            {
                transform.position = Vector3.MoveTowards(transform.position, abovePad, moveSpeed * Time.deltaTime);

                Vector3 dir = abovePad - transform.position;
                dir.y = 0;
                if (dir != Vector3.zero)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(dir),
                        rotationSpeed * Time.deltaTime
                    );

                yield return null;
            }

            Quaternion landRot = Quaternion.LookRotation(pad.transform.forward);

            while (Vector3.Distance(transform.position, landingPos) > 0.05f)
            {
                transform.position = Vector3.MoveTowards(transform.position, landingPos, takeoffSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, landRot, rotationSpeed * Time.deltaTime);
                yield return null;
            }
        }

        yield return null;
    }


    private IEnumerator ResetCameraRotation()
    {
        float t = 0;
        Quaternion startRot = droneCamera.transform.localRotation;
        while (t < 1.0f)
        {
            t += Time.deltaTime;
            droneCamera.transform.localRotation = Quaternion.Slerp(startRot, initialCameraRotation, t);
            yield return null;
        }
    }

    private void CaptureAndSaveScreenshot()
    {
        if (droneCamera == null || droneCamera.targetTexture == null) return;

        RenderTexture rt = droneCamera.targetTexture;
        RenderTexture.active = rt;
        Texture2D screen = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        screen.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        screen.Apply();
        RenderTexture.active = null;

        byte[] bytes = screen.EncodeToJPG();
        string path = UnityEngine.Application.dataPath + "/CapturedEvidence";
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        string file = path + $"/Evidence_{System.DateTime.Now:yyyyMMdd_HHmmss}.jpg";
        File.WriteAllBytes(file, bytes);
        Destroy(screen);

        UnityEngine.Debug.Log($" 캡처 완료: {file}");
    }
}