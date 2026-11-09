FROM python:3.11-slim

RUN apt-get update && apt-get install -y --no-install-recommends libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY yolo_server.py .
# 첫 실행 시 yolov8n.pt를 자동으로 내려받음 (이미지에 미리 포함)
RUN python -c "from ultralytics import YOLO; YOLO('yolov8n.pt')"

EXPOSE 5000
CMD ["python", "yolo_server.py"]
