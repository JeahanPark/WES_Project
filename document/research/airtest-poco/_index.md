---
type: reference
source: Airtest + Poco (AirtestProject)
generated: multi-agent
note: 게임 콘텐츠 아님 — QA 자동화 프레임워크 외부 조사 문서
---

# Airtest / Poco 레퍼런스 (독립 조사 문서)

게임(WES) 콘텐츠와 분리된 **외부 QA 자동화 프레임워크** 정밀 문서.
대상: NetEase `AirtestProject/Airtest` + `AirtestProject/Poco` (둘 다 Python, Apache-2.0).
방식: 다운로드 소스 전수 정적 분석(실행 없음).

## 두 프레임워크 한눈에

| | Airtest | Poco |
|---|---|---|
| 인식 방식 | **이미지 인식**(template/keypoint matching) | **UI 계층 인스펙션**(런타임 트리) |
| 대상 | Android·iOS·Windows·Linux 화면 | Unity3D·cocos2dx·UE4·네이티브 등 엔진 |
| 셀렉터 | 좌표·템플릿 이미지 | 이름·타입·속성·계층 |
| Unity 연동 | 화면 캡처만 | **C# SDK 임베드 + TCP RPC(5001)** |
| 관계 | 입력·단언·이미지 폴백 | 노드 질의·정밀 클릭 |

보통 **함께 사용**: Poco로 노드 찾고 → Airtest로 이미지 폴백/입력/단언. 공용 GUI = AirtestIDE.

## 문서 맵

### Airtest
| 문서 | 내용 |
|------|------|
| [airtest-01-architecture-core-api](airtest-01-architecture-core-api.md) | Device 추상화·전역 G·ST 설정·`airtest.core.api` 전체 함수·Template/Cv 파이프라인 |
| [airtest-02-image-recognition](airtest-02-image-recognition.md) | `aircv` 전수 — template/multiscale/keypoint(SIFT·AKAZE·BRISK·ORB·KAZE), confidence 계산, CVSTRATEGY 폴백 |
| [airtest-03-platform-backends](airtest-03-platform-backends.md) | Android(adb·minicap·minitouch)·iOS(WDA)·Windows(pywinauto)·Linux, 연결 URI |
| [airtest-04-cli-reports-air-workflow](airtest-04-cli-reports-air-workflow.md) | `.air` 포맷·CLI(run/info/report)·HTML 리포트·로그·utils |

### Poco
| 문서 | 내용 |
|------|------|
| [poco-01-core-framework-api](poco-01-core-framework-api.md) | `Poco`·`UIObjectProxy` 전체 연산·좌표계(normalized/anchor/focus)·예외·freeze |
| [poco-02-sdk-internals-custom-engine](poco-02-sdk-internals-custom-engine.md) | AbstractDumper·AbstractNode·Attributor·Matcher·Selector — **신규 엔진 이식법**·dump JSON 포맷 |
| [poco-03-drivers-unity-deep](poco-03-drivers-unity-deep.md) | 드라이버 전체 + **Unity3D 심층**(UnityPoco·5001·에디터 플레이모드 연결, 임베드해야 할 C# SDK) |
| [poco-04-selectors-tutorials-tools](poco-04-selectors-tutorials-tools.md) | 셀렉터 문법·상대선택·튜토리얼·PocoHierarchyViewer·standalone inspector |
| [poco-05-rpc-and-platform-sdks](poco-05-rpc-and-platform-sdks.md) | JSON-RPC 2.0 전송 계층(5001 패킷 프레이밍)·OSX/Windows SDK 이식 사례 |

### 점검
| 문서 | 내용 |
|------|------|
| [_completeness](_completeness.md) | 소스 트리 vs 문서 대조 누락검사 (high=aircv 해소됨, 잔여 low만) |

## WES Unity 연동 핵심 (poco-03 / poco-05 요약)

```
UnityPoco → StdPoco → StdPocoAgent → RpcClient + TcpClient(5001)
                                          │
                              [4B len][utf-8 JSON-RPC 2.0]
                                          │
                              게임 런타임 내 C# Poco-SDK (별도 임베드 필요)
```

- Python 측(이 저장소)은 **클라이언트**. 게임 측 C# **Poco-SDK(PocoManager 프리팹)는 별도 repo**라 Unity 프로젝트에 임베드해야 함 → 상세는 poco-03.
- 에디터 플레이모드도 `localhost:5001`로 동일 연결. MCP와 입력 제어판이 분리되는 점 주의.
