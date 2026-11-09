using UnityEngine;

public class SystemManager : MonoBehaviour
{
    [Header("Component Links")]
    public Drone_Controller droneController;
    public UIManager uiManager;

    [Header("Camera Settings")]
    public Camera cctvCamera;
    public Camera droneCamera;
    public GameObject droneScreenUI;

    [Header("Return Settings")]
    public Transform stationPoint;

    public bool isPatrolStarted = false;

    private bool isTracking = false;
    private bool isReturning = false;

    // 초기화 및 CCTV 화면 설정
    void Start()
    {
        SwitchToCCTVView();

        if (uiManager != null)
        {
            uiManager.UpdateStatus("시스템 대기 중");
        }
    }

    // 복귀 완료 거리 체크
    void Update()
    {
        if (isReturning && droneController != null && stationPoint != null)
        {
            float distance = Vector3.Distance(droneController.transform.position, stationPoint.position);
            if (distance <= 0.5f)
            {
                OnReturnCompleted();
            }
        }
    }

    // 침입 감지 시
    public void OnIntruderDetected()
    {
        if (isPatrolStarted) return;

        isPatrolStarted = true;
        isReturning = false;
        UnityEngine.Debug.Log("침입 감지 신호 수신");

        SwitchToDroneView();

        if (uiManager != null) uiManager.UpdateStatus("침입 감지. 드론 출동");
        if (droneController != null) droneController.StartPatrol();
    }

    // 수동 복귀 버튼 클릭
    public void OnReturnButtonPushed()
    {
        UnityEngine.Debug.Log("수동 복귀 명령");

        isPatrolStarted = false;
        isTracking = false;
        isReturning = true;

        if (droneController != null) droneController.ReturnToPad();
        if (uiManager != null)
        {
            uiManager.UpdateStatus("복귀 중");
            uiManager.UpdateYoloOverlay(new YoloDetection[0]);
        }
    }

    // 복귀 완료 시
    public void OnReturnCompleted()
    {
        if (!isReturning) return;

        isReturning = false;
        UnityEngine.Debug.Log("복귀 완료 확인. CCTV 화면 전환.");

        SwitchToCCTVView();

        if (uiManager != null)
        {
            uiManager.UpdateStatus("시스템 대기 중");
        }
    }

    // YOLO 식별 데이터 수신 시
    public void OnIntruderIdentified(YoloDetection[] detections)
    {
        if (!isPatrolStarted) return;
        if (isReturning) return;

        if (droneController == null) return;

        if (uiManager != null)
        {
            uiManager.UpdateYoloOverlay(detections);
            uiManager.UpdateStatus($"침입자 식별됨");
        }

        if (detections.Length > 0)
        {
            droneController.StartTracking(detections[0]);

            if (!isTracking)
            {
                isTracking = true;
                UnityEngine.Debug.Log(">>> SystemManager: 드론에게 추적 명령 전달함");
            }
        }
    }

    // CCTV 카메라 활성화
    private void SwitchToCCTVView()
    {
        if (cctvCamera != null) cctvCamera.enabled = true;
        if (droneCamera != null) droneCamera.enabled = false;

        if (droneScreenUI != null) droneScreenUI.SetActive(false);
    }

    // 드론 카메라 활성화
    private void SwitchToDroneView()
    {
        if (cctvCamera != null) cctvCamera.enabled = false;
        if (droneCamera != null) droneCamera.enabled = true;

        if (droneScreenUI != null) droneScreenUI.SetActive(true);
    }
}