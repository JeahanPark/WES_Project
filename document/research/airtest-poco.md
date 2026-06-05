# Airtest / Poco 조사 (UI QA 개선 후보)

> 출처: NetEase AirtestProject. Airtest = 이미지 인식 자동화, Poco = UI 계층 인스펙션 자동화. 둘은 보완 관계 (한 스크립트에서 같이 씀).
> 작성일 2026-05-31. 검증 필요 항목은 ⚠️ 표기.

---

## 1. 한눈에 — 두 프레임워크의 역할 분담

| 축 | **Airtest** | **Poco** |
|----|-------------|----------|
| 기반 | 화면 **이미지** (스크린샷 픽셀) | 게임 **런타임 UI 트리** (오브젝트) |
| 요소 찾기 | 템플릿 이미지 매칭 (OpenCV) | 셀렉터 (이름/타입/속성/계층) |
| 코드 주입 | **불필요** (화면만 봄) | **SDK 설치 필요** (게임 안에 서버) |
| 좌표 | 스크린샷 픽셀 좌표 | 정규화 좌표(0~1) + 실제 size/pos |
| 강점 | 엔진 무관, 설치 0, 시각 그대로 | 정확한 수치·속성, 빠름, 안정적 |
| 약점 | 해상도·테마 바뀌면 깨짐, 느림 | SDK 설치·엔진 의존 |
| 언어 | Python | Python (클라) + C#(Unity 서버) |
| IDE | AirtestIDE (녹화·이미지 캡처·인스펙터 통합) | PocoHierarchyViewer / AirtestIDE 인스펙터 |

핵심 통찰: **Poco = 우리 MCP의 "수치 백업"을 런타임에서 정확하게**, **Airtest = LLM 시각 판정을 결정론적 이미지 매칭으로 대체/보완**.

---

## 2. Airtest 상세

### 2.1 아키텍처
- Python 라이브러리 `airtest` + 이미지 처리 코어 `aircv`(OpenCV 래퍼).
- 디바이스 추상화 계층: 같은 API(`touch` 등)가 현재 연결된 플랫폼에 맞는 입력으로 변환.
- 스크립트 단위 = `.air` 폴더 = `메인.py` + 템플릿 PNG들 묶음.

### 2.2 디바이스 연결
```python
from airtest.core.api import *
connect_device("Windows:///?title_re=Unity.*")  # pywinauto 백엔드, 창 제목 정규식
# 또는
connect_device("Android:///")                    # ADB
connect_device("iOS:///http://...")
```
- **Windows**: `pywinauto`로 창을 찾음 → 우리 Unity 빌드(.exe) 또는 에디터 Game 창을 대상으로 가능 ⚠️(에디터 창 캡처 가능 여부 검증 필요).
- 창 핸들 지정 시 해당 창 영역만 캡처/입력.

### 2.3 이미지 매칭 — `Template`
```python
btn = Template(r"tpl_craft_button.png",
               threshold=0.7,      # 매칭 유사도 임계 (기본 0.7)
               rgb=False,          # True면 색상까지 비교 (색 다른 버튼 구분)
               target_pos=5,       # 매칭 영역 내 클릭 지점 (1~9, 5=중앙)
               record_pos=(x, y),  # 화면 비율 기준 예상 위치 (다중 매칭 시 우선순위)
               resolution=(w, h))  # 템플릿 캡처 당시 해상도
```
- 매칭 알고리즘 (자동 선택/체이닝):
  - **Template Matching** (`cv2.matchTemplate`) — 같은 해상도/배율일 때 빠르고 정확.
  - **Keypoint Matching** (SIFT/SURF/BRIEF 계열) — 해상도·회전 변해도 매칭 (느림).
  - aircv가 여러 방법을 순차 시도해 best 반환.
- 반환: 매칭 좌표 또는 `None`.

### 2.4 핵심 API
| 함수 | 동작 |
|------|------|
| `touch(v)` | `v`(Template 또는 좌표) 클릭/탭 |
| `double_click(v)` | 더블클릭 |
| `swipe(v1, v2)` | 드래그/스와이프 |
| `text(s)` | 텍스트 입력 (포커스된 필드) |
| `keyevent(k)` | 키 입력 |
| `wait(v, timeout, interval)` | 요소가 나타날 때까지 대기 (없으면 예외) |
| `exists(v)` | 존재 여부 → 좌표 또는 False |
| `assert_exists(v, msg)` | 요소 존재 단언 (실패 시 리포트에 기록) |
| `assert_not_exists(v, msg)` | 부재 단언 |
| `assert_equal(a, b, msg)` | 값 동등 단언 |
| `snapshot(filename, msg)` | 스크린샷 저장 (리포트에 삽입) |
| `sleep(secs)` | 대기 |

### 2.5 리포트
```bash
airtest report 경로/스크립트.air --log_dir 경로/log
```
- **HTML 리포트**: 각 스텝의 스크린샷 + 매칭 박스 시각화 + 통과/실패 + 화면 녹화.
- 실패 지점을 시각적으로 즉시 추적 → 우리 ui-review 리포트와 결합 시 근거 강함.

### 2.6 AirtestIDE
- 화면 보며 클릭 → 템플릿 자동 캡처 → 스크립트 자동 생성(녹화).
- Poco 인스펙터 내장 (UI 트리 보며 셀렉터 생성).
- ⚠️ GUI 도구라 헤드리스/에이전트 자동화 흐름엔 직접 안 맞음 — 스크립트 작성은 결국 코드로.

---

## 3. Poco 상세

### 3.1 아키텍처 (2-파트)
```
[게임 프로세스]                      [Python QA 스크립트]
 PocoManager.cs  ── TCP(RPC) ──►  UnityPoco() 클라이언트
 (UI 트리 직렬화 서버)              poco('btn').click() 등
```
- 게임 안에 **SDK 서버**가 떠서 현재 프레임의 UI 계층을 직렬화해 RPC로 응답.
- 클라이언트는 셀렉터로 노드를 질의하고, 노드의 속성 조회 / 입력 명령.
- "impact-free, super fast" — 실시간 게임 중 조회 가능.

### 3.2 Unity 설치 (서버 측)
1. `poco-sdk` repo에서 `Unity3D/` 폴더를 프로젝트 스크립트 경로로 복사.
2. UI 프레임워크에 맞춰 하위 폴더 정리: **uGUI 사용 → `Unity3D/ngui` 삭제**, NGUI 사용 → `Unity3D/ugui` 삭제. (WES는 uGUI → ngui 폴더 제거)
3. `Unity3D/PocoManager.cs`를 임의 GameObject(보통 Main Camera)에 컴포넌트로 부착.
4. 실행하면 서버가 포트에서 listen. ⚠️ **기본 포트 검증 필요** (Unity는 통상 `5001`, cocos 계열 문서엔 `15004`로 표기됨 — SDK 버전별 상이).

### 3.3 Python 연결 (클라이언트 측)
```python
from poco.drivers.unity3d import UnityPoco
poco = UnityPoco()                  # 기본 ('', 5001) ⚠️
# 또는 명시
poco = UnityPoco(("127.0.0.1", 5001))
```

### 3.4 셀렉터
```python
poco('CraftButton')                       # 이름
poco(type='Button')                       # 타입
poco(text='제작')                          # 텍스트 속성
poco('Panel').child('Item').offspring('Icon')  # 상대 계층
poco('Item')[0]                           # 시퀀스(반복 행) 인덱스
for cell in poco('MaterialItem'):         # 반복 순회
    print(cell.attr('size'))
```

### 3.5 노드 속성 (`.attr(name)` / `.get_text()`)
| 속성 | 의미 | 우리 검사에의 가치 |
|------|------|-------------------|
| `name` / `type` | 오브젝트 이름 / 컴포넌트 타입 | 셀렉터 |
| `pos` | 정규화 화면 좌표 (0~1, 중심 기준) | 정렬 B1 |
| `size` | 정규화 크기 (0~1) | 크기/비율 B2, B10 |
| `anchorPoint` | 앵커 | 레이아웃 |
| `visible` | 화면 표시 여부 | 가시성, 겹침 B3 |
| `text` | 텍스트 내용 | 극단값 B5 |
| `zOrders` | 렌더 순서 | 겹침/비침 B3 |
| `scale` | 스케일 | 비율 |
| `components` | 부착 컴포넌트 목록 | 검증 |

> Poco의 `pos`/`size`는 **정규화(0~1)** — 픽셀 환산하려면 화면 해상도 곱. 실제 빌드 해상도 기준이라 **MCP 에디터 rect보다 "유저가 실제 보는 값"에 가까움**.

### 3.6 노드 조작
`.click()`, `.long_click()`, `.swipe(dir)`, `.drag_to(node)`, `.exists()`, `.wait_for_appearance()`, `.wait_for_disappearance()`, `.attr()`, `.set_text()`.

### 3.7 인스펙터
- **PocoHierarchyViewer** (독립 실행) 또는 AirtestIDE 인스펙터로 실행 중 게임의 UI 트리·속성을 GUI로 덤프 → 셀렉터/속성 확인.

---

## 4. 우리(WES) UI QA에의 적용 매핑

| 현재 ui-review 항목 | Poco | Airtest |
|---------------------|------|---------|
| A1 텍스트 오버플로우 | `text` + `size` 비교 | — |
| A4 누락 참조 | `visible`/속성 null | 이미지 부재 매칭 |
| A5/B9 비율 왜곡 | `size` aspect 계산 (런타임 실측) | 템플릿 매칭 실패로 간접 탐지 |
| B1 정렬/간격 | 인접 노드 `pos` 차 (정확) | 시각 |
| B2 크기/비율 | `size` 비교 (정확) | — |
| B3 겹침/비침 | `pos`+`size`+`zOrders` 교집합 | 시각 |
| B10 반복 행 균등 | 클론 순회 `size` 비교 (정확) | — |
| "버튼이 거기 있나" | `exists()` | `assert_exists(Template)` |
| 상태 전환 시퀀스 | `.click()` → 재조회 | `touch` → `wait` |

→ **Poco가 우리 "수치 백업"을 에디터가 아닌 실제 런타임에서, 더 정확히 대체**. **Airtest는 자동 입력·시퀀스·HTML 회귀 리포트** 담당.

---

## 5. 도입 시 고려사항 / 리스크

| 항목 | 내용 |
|------|------|
| Netcode 멀티플레이 | 서버 SDK가 단일 프로세스 UI만 봄 — 호스트/클라 각각 띄워야 멀티 검증 ⚠️ |
| 포트/방화벽 | PocoManager 포트가 MCP·기타와 충돌 안 하는지 확인 |
| 에디터 vs 빌드 | Poco는 Play 모드 에디터에서도 동작 (SDK가 런타임 컴포넌트) — 빌드 불필요 가능성 높음 ⚠️검증 |
| MCP와의 관계 | 현재 흐름은 Claude→MCP(C#)→Unity. Poco/Airtest는 Python 프로세스 → **새 실행 경로** 필요 (Claude가 python 스크립트 실행) |
| 한글 텍스트 | `text` 속성·이미지 템플릿에서 한글 처리 ⚠️ |
| 라이선스 | Airtest/Poco 모두 Apache-2.0 (상업 사용 가능) |
| AirtestIDE 의존성 | 녹화는 GUI 필요 — 에이전트 자동화는 코드 직접 작성으로 우회 |

---

## 6. 미검증 항목 (다음 단계 확인 필요)
1. Unity PocoSDK 정확한 기본 포트 (5001 vs 다른 값) + WES uGUI 버전 호환.
2. Play 모드 에디터에서 Poco 서버 동작 여부 (빌드 없이).
3. Airtest `Windows://` 가 **에디터 Game 창** 또는 **빌드 exe** 중 무엇을 대상으로 해야 안정적인지.
4. MCP 기반 현 워크플로우와 Python 기반 Airtest/Poco를 **어떻게 한 파이프라인으로 묶을지** (에이전트가 python 실행 → 결과 회수).
5. Netcode 2-인스턴스 환경에서 UI 검증 전략.

---

## 7. 참고 링크
- Airtest: https://github.com/AirtestProject/Airtest
- Poco: https://github.com/AirtestProject/Poco
- Poco-SDK (엔진별): https://github.com/AirtestProject/Poco-SDK
- Poco 통합 가이드: https://poco.readthedocs.io/en/latest/source/doc/integration.html
- Airtest API: https://airtest.readthedocs.io/en/latest/
