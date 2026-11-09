using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI statusText;
    public Button returnButton;

    [Header("YOLO Resolution Settings")]
    public float serverImageWidth = 1920f;
    public float serverImageHeight = 1080f;

    [Header("Overlay Settings")]
    [Tooltip("박스 테두리 두께")]
    public float boxThickness = 5.0f;
    [Tooltip("텍스트 폰트 크기 (YOLO 박스용)")]
    public int textSize = 40;

    private SystemManager systemManager;
    private YoloDetection[] currentDetections;
    private Texture2D boxTexture;

    // 초기화 및 버튼 이벤트 연결
    void Start()
    {
        systemManager = FindFirstObjectByType<SystemManager>();

        if (returnButton != null)
        {
            returnButton.onClick.AddListener(OnReturnButtonClicked);
        }

        boxTexture = new Texture2D(1, 1);
        boxTexture.SetPixel(0, 0, Color.red);
        boxTexture.Apply();

        UpdateStatus("시스템 대기 중...");
    }

    // 상태 텍스트 갱신
    public void UpdateStatus(string newStatus)
    {
        if (statusText != null)
        {
            statusText.text = "Status: " + newStatus;
        }
    }

    // 복귀 버튼 클릭 처리
    private void OnReturnButtonClicked()
    {
        if (systemManager != null)
        {
            systemManager.OnReturnButtonPushed();
        }
    }

    // YOLO 데이터 수신 및 갱신
    public void UpdateYoloOverlay(YoloDetection[] detections)
    {
        currentDetections = detections;
    }

    // 화면에 바운딩 박스 그리기
    void OnGUI()
    {
        if (currentDetections == null || currentDetections.Length == 0) return;

        float scaleX = Screen.width / serverImageWidth;
        float scaleY = Screen.height / serverImageHeight;

        foreach (var data in currentDetections)
        {
            if (data.box == null || data.box.Length < 4) continue;

            float x1 = data.box[0];
            float y1 = data.box[1];
            float x2 = data.box[2];
            float y2 = data.box[3];

            float w = x2 - x1;
            float h = y2 - y1;

            Rect rect = new Rect(x1 * scaleX, y1 * scaleY, w * scaleX, h * scaleY);

            // 박스 그리기
            DrawScreenRectBorder(rect, boxThickness, Color.red);

            // 라벨 그리기
            GUIStyle style = new GUIStyle();
            style.normal.textColor = Color.red;
            style.fontSize = textSize;
            style.fontStyle = FontStyle.Bold;

            GUI.Label(new Rect(rect.x, rect.y - (textSize + 5), 300, textSize + 10), $"INTRUDER {data.confidence:F2}", style);
        }
    }

    // 박스 테두리 그리기 함수
    private void DrawScreenRectBorder(Rect rect, float thickness, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), boxTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), boxTexture);
        GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), boxTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), boxTexture);

        GUI.color = oldColor;
    }
}