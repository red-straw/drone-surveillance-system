# Drone Surveillance System

CCTV가 보안 구역 침입을 감지하면 드론이 출동해 YOLO로 침입자를 식별하고 추적하는 Unity 시뮬레이션입니다.

25-2, 개인 프로젝트

<p>
  <img src="docs/images/intrusion_dispatch.png" width="49%">
  <img src="docs/images/yolo_tracking.png" width="49%">
</p>

## 동작 흐름

1. 대기: CCTV 화면이 메인입니다. `IntruderSensor`(트리거 콜라이더)가 보안 구역을 감시합니다.
2. 침입 감지, 출동: 침입자가 구역에 들어오면 `SystemManager`가 드론을 출동시키고 화면을 드론 시점으로 바꿉니다.
3. 식별, 추적: 드론이 이륙해 웨이포인트를 순찰합니다. `Connection_Manager`가 드론 카메라 프레임을 YOLO 서버로 보내고, 받은 박스 좌표로 화면에 표시하면서 추적합니다.
4. 채증: 추적 시작 5초 후부터 5초마다 사진을 저장합니다.
5. 복귀: 복귀 버튼을 누르면 추적을 멈추고 스테이션에 착륙한 뒤 CCTV 화면으로 돌아갑니다.

![architecture](docs/images/architecture.png)

## 구현

- Unity는 프레임(JPEG)만 보내고 서버는 박스 좌표 JSON만 돌려줍니다. 응답을 기다리는 동안에는 다음 프레임을 보내지 않습니다.
- 추적 (`Drone_Controller.TrackByVisualServoing`)
  - 박스 중심과 화면 중앙의 차이로 회전하고, 데드존을 둬 떨림을 줄입니다.
  - 좌표를 Lerp로 부드럽게 하고, 이동 속도로 다음 위치를 예측합니다.
  - 박스 크기(면적, 높이)로 거리를 가늠해 감속하거나 멈추고, 너무 가까우면 뒤로 물러납니다.
  - 일정 시간 탐지가 끊기면 추적을 끝내고 순찰로 돌아갑니다.

## 구조

```text
.
├── unity/                       # Unity 6 (6000.0.57f1), URP
│   ├── Assets/
│   │   ├── Scripts/
│   │   │   ├── SystemManager.cs       # 상태 관리, 카메라 전환
│   │   │   ├── Drone_Controller.cs    # 이륙/순찰/추적/복귀, 사진 저장
│   │   │   ├── Connection_Manager.cs  # 프레임 전송, 응답 파싱
│   │   │   ├── UIManager.cs           # 상태 표시, 탐지 박스 표시
│   │   │   ├── IntruderSensor.cs      # 구역 침입 감지
│   │   │   ├── Intruder_Controller.cs # 침입자 이동
│   │   │   └── JsonHelper.cs          # JSON 배열 파싱
│   │   ├── Scenes/MainScene.unity
│   │   └── Settings/, RenderTextures/
│   ├── Packages/
│   └── ProjectSettings/
├── server/                      # YOLO 탐지 서버 (재구현, 아래 참고)
└── docs/                        # 프로그램 설명서, 발표 자료
```

## 실행

YOLO 서버

```bash
cd server
docker build -t yolo-server .
docker run -p 5000:5000 yolo-server
```

`POST /detect` (body: JPEG)를 받으면 `[{"box": [x1, y1, x2, y2], "confidence": 0.87}, ...]` 형식으로 응답합니다.

Unity

1. Unity Hub에서 `unity/` 폴더를 추가합니다. (6000.0.57f1)
2. 아래 에셋을 Asset Store에서 임포트합니다. 라이선스 때문에 저장소에는 넣지 않았습니다.
3. `Assets/Scenes/MainScene`을 열고 Game 창 해상도를 4K UHD(3840x2160)로 맞춘 뒤 실행합니다.

## 사용 에셋

| 용도 | 에셋 |
|---|---|
| 침입자 | [NPC Casual Set](https://assetstore.unity.com/packages/3d/characters/humanoids/humans/npc-casual-set-00-326131), [Mixamo Slow Run](https://www.mixamo.com/) |
| 드론 | [Simple Drone](https://assetstore.unity.com/packages/3d/vehicles/air/simple-drone-190684) |
| CCTV | [Surveillance Camera](https://assetstore.unity.com/packages/3d/props/surveillance-camera-264577) |
| 건물 | [White City](https://assetstore.unity.com/packages/3d/environments/urban/white-city-76766) |
| 군사기지 | [Military Assets (Mobile)](https://assetstore.unity.com/packages/3d/props/military-assets-mobile-314463), [Military Base Pack](https://assetstore.unity.com/packages/3d/environments/military-base-pack-326310), [Defense Line Vol.3](https://assetstore.unity.com/packages/3d/props/weapons/defense-line-vol-3-barrier-breaker-free-25180) |
| 펜스, 도로, 기타 | [Chainlink Fences](https://assetstore.unity.com/packages/3d/chainlink-fences-73107), [Modular Lowpoly Streets](https://assetstore.unity.com/packages/3d/environments/urban/modular-lowpoly-streets-free-192094), [High Quality Solar Panel](https://assetstore.unity.com/packages/3d/props/industrial/high-quality-solar-panel-175231), [Stylized Nature Textures](https://assetstore.unity.com/packages/2d/textures-materials/stylized-nature-textures-228680) |

## 정리 내역

포트폴리오로 올리면서 다음을 정리했습니다.

- 원본 YOLO 서버 코드가 남아 있지 않아, `Connection_Manager`의 통신 규격과 설명서의 Docker 실행 방식에 맞춰 `server/`를 다시 만들었습니다(Flask + YOLOv8n).
- C# 스크립트 인코딩을 UTF-8로 바꾸고, 쓰지 않는 using 문과 일부 주석을 정리했습니다.

## 기술 스택

Unity 6 (C#, URP), Python, Flask, Ultralytics YOLO, Docker

## 라이선스

- 저장소 전체: [MIT](LICENSE)
- `server/`: [AGPL-3.0](server/LICENSE). Ultralytics YOLO를 사용하므로 이 폴더만 AGPL-3.0입니다.
- 에셋과 폰트는 각 제공자의 라이선스를 따르며 저장소에 포함하지 않았습니다.
