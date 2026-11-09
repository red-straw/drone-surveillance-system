using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class Connection_Manager : MonoBehaviour
{
    [Header("Settings")]
    public string yoloServerURL = "http://127.0.0.1:5000/detect";
    public float captureInterval = 0.05f;

    [Header("Links")]
    public RenderTexture droneRenderTexture;
    public UIManager uiManager;
    public SystemManager systemManager;

    private Texture2D texture2D;
    private bool isSending = false;
    private bool firstSuccessLogged = false;

    void Start()
    {
        if (droneRenderTexture == null)
        {
            UnityEngine.Debug.LogError("Drone Render Texture가 연결되지 않았습니다!");
            return;
        }

        texture2D = new Texture2D(droneRenderTexture.width, droneRenderTexture.height, TextureFormat.RGB24, false);

        StartCoroutine(SendFrameCoroutine());
    }

    // 주기적으로 화면을 캡처하여 업로드
    private IEnumerator SendFrameCoroutine()
    {
        while (true)
        {
            if (!isSending)
            {
                RenderTexture.active = droneRenderTexture;
                texture2D.ReadPixels(new Rect(0, 0, droneRenderTexture.width, droneRenderTexture.height), 0, 0);
                texture2D.Apply();
                RenderTexture.active = null;

                byte[] jpgData = texture2D.EncodeToJPG(40);

                StartCoroutine(UploadImage(jpgData));
            }

            yield return new WaitForSeconds(captureInterval);
        }
    }

    //서버로 이미지를 전송하고 응답
    private IEnumerator UploadImage(byte[] jpgData)
    {
        isSending = true;

        UnityWebRequest www = new UnityWebRequest(yoloServerURL, "POST");
        UploadHandlerRaw uploadHandler = new UploadHandlerRaw(jpgData);
        uploadHandler.contentType = "image/jpeg";
        www.uploadHandler = uploadHandler;
        www.downloadHandler = new DownloadHandlerBuffer();

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            if (!firstSuccessLogged)
            {
                UnityEngine.Debug.Log("YOLO 서버 연결 성공");
                firstSuccessLogged = true;
            }

            string jsonResponse = www.downloadHandler.text;
            ProcessYoloResponse(jsonResponse);
        }
        else
        {
            UnityEngine.Debug.LogError("YOLO Error: " + www.error);
        }

        isSending = false;
    }

    // 수신된 JSON 데이터를 파싱하고 매니저에 전달
    private void ProcessYoloResponse(string jsonResponse)
    {

        if (string.IsNullOrEmpty(jsonResponse) || jsonResponse == "[]")
        {
            return;
        }

        YoloDetection[] detections = JsonHelper.FromJson<YoloDetection>(jsonResponse);

        if (detections != null && detections.Length > 0)
        {
            if (systemManager != null)
            {
                systemManager.OnIntruderIdentified(detections);
            }

            foreach (var det in detections)
            {
                if (det.box != null && det.box.Length >= 4)
                {
                    UnityEngine.Debug.Log($" 탐지됨: Conf={det.confidence:F2}, Box=[{det.box[0]}, {det.box[1]}, {det.box[2]}, {det.box[3]}]");
                }
            }
        }
    }
}