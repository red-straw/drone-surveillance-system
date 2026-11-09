using UnityEngine;

public class IntruderSensor : MonoBehaviour
{
    private SystemManager systemManager;
    private bool alarmTriggered = false;

    // 초기화: 시스템 매니저 연결
    void Start()
    {
        systemManager = FindFirstObjectByType<SystemManager>();

        if (systemManager == null)
        {
            UnityEngine.Debug.LogError("오류: SystemManager가 씬에 없거나 활성화되지 않았습니다. 신호 전송 불가.");
        }
    }

    // 침입자 감지 및 신호 전송
    private void OnTriggerEnter(Collider other)
    {
        if (!alarmTriggered && other.CompareTag("Intruder"))
        {
            if (systemManager != null)
            {
                systemManager.OnIntruderDetected();
                alarmTriggered = true;

                UnityEngine.Debug.Log("CCTV 감지 성공: 침입자 트리거 진입.");
            }
        }
    }
}