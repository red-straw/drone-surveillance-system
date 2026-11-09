"""드론 영상 프레임을 받아 사람(침입자)을 탐지하는 YOLO 서버.

Unity의 Connection_Manager가 드론 카메라 화면을 JPEG로 캡처해
POST /detect 로 보내면, 사람(COCO class 0) 탐지 결과를 JSON 배열로 돌려준다.

    응답 예: [{"box": [x1, y1, x2, y2], "confidence": 0.87}, ...]
    (box는 원본 프레임 기준 픽셀 좌표)

실행:  python yolo_server.py        → http://127.0.0.1:5000
"""
import os

import cv2
import numpy as np
from flask import Flask, jsonify, request
from ultralytics import YOLO

MODEL_PATH = os.getenv("YOLO_MODEL", "yolov8n.pt")
CONF_THRESHOLD = float(os.getenv("YOLO_CONF", "0.25"))
PERSON_CLASS_ID = 0

app = Flask(__name__)
model = YOLO(MODEL_PATH)


@app.route("/health")
def health():
    return jsonify({"status": "ok", "model": MODEL_PATH})


@app.route("/detect", methods=["POST"])
def detect():
    frame = cv2.imdecode(np.frombuffer(request.get_data(), np.uint8), cv2.IMREAD_COLOR)
    if frame is None:
        return jsonify({"error": "JPEG 이미지를 디코딩할 수 없습니다."}), 400

    result = model.predict(frame, conf=CONF_THRESHOLD, classes=[PERSON_CLASS_ID], verbose=False)[0]

    detections = [
        {"box": [int(v) for v in box.xyxy[0].tolist()], "confidence": round(float(box.conf[0]), 4)}
        for box in result.boxes
    ]
    detections.sort(key=lambda d: d["confidence"], reverse=True)
    return jsonify(detections)


if __name__ == "__main__":
    app.run(host=os.getenv("HOST", "0.0.0.0"), port=int(os.getenv("PORT", "5000")))
