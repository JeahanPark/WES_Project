# 문서화 완전성 점검 (Airtest / Poco)

작성된 7개 문서의 manifest와 두 저장소 전체 소스 트리를 대조한 결과.

대상 루트
- Airtest: `C:\Users\cgq02\Downloads\Airtest-master\Airtest-master`
- Poco: `C:\Users\cgq02\Downloads\Poco-master\Poco-master`

## 누락된 의미있는 소스 파일

| Repo | 파일 | severity | 사유 |
|------|------|----------|------|
| Airtest | `airtest/aircv/__init__.py` | high | aircv 패키지 진입점. find_sift / find_template 노출 |
| Airtest | `airtest/aircv/aircv.py` | high | 이미지 로드·crop·마스킹 등 CV 유틸 핵심 |
| Airtest | `airtest/aircv/template.py` | high | 템플릿 매칭(`find_template`/`find_all_template`) — touch/wait/exists 의 실제 매칭 엔진 |
| Airtest | `airtest/aircv/template_matching.py` | high | OpenCV matchTemplate 래퍼 |
| Airtest | `airtest/aircv/multiscale_template_matching.py` | high | 멀티스케일 템플릿 매칭(해상도 비종속) |
| Airtest | `airtest/aircv/sift.py` | high | SIFT 특징점 매칭 진입 |
| Airtest | `airtest/aircv/keypoint_base.py` | high | 키포인트 매칭 추상 베이스 |
| Airtest | `airtest/aircv/keypoint_matching.py` | high | KAZE/AKAZE/BRISK/ORB 키포인트 매칭 구현 |
| Airtest | `airtest/aircv/keypoint_matching_contrib.py` | medium | SIFT/SURF(opencv-contrib) 매칭 구현 |
| Airtest | `airtest/aircv/cal_confidence.py` | medium | 매칭 신뢰도(confidence) 계산 |
| Airtest | `airtest/aircv/screen_recorder.py` | medium | 화면 녹화 |
| Airtest | `airtest/aircv/error.py` | medium | aircv 전용 예외(NoModuleError, TemplateInputError 등) |
| Airtest | `airtest/aircv/utils.py` | medium | 이미지 변환/스케일/색상 유틸 |
| Airtest | `airtest/aircv/template.py` 외 패키지 전체 | high | **`airtest/aircv/` 패키지 전체가 미문서화** — Airtest 이미지 인식의 심장부 |
| Poco | `poco/utils/simplerpc/jsonrpc/manager.py` 외 jsonrpc 코어 | medium | manager는 manifest에 있으나 base/jsonrpc/jsonrpc1/jsonrpc2/exceptions 미포함 |
| Poco | `poco/utils/simplerpc/jsonrpc/base.py` | medium | JSON-RPC 요청/응답 추상 베이스 |
| Poco | `poco/utils/simplerpc/jsonrpc/jsonrpc.py` | medium | JSON-RPC 버전 디스패치 |
| Poco | `poco/utils/simplerpc/jsonrpc/jsonrpc1.py` | low | JSON-RPC 1.0 구현(Poco는 사실상 2.0 사용) |
| Poco | `poco/utils/simplerpc/jsonrpc/jsonrpc2.py` | medium | JSON-RPC 2.0 구현(실제 사용 프로토콜) |
| Poco | `poco/utils/simplerpc/jsonrpc/exceptions.py` | low | JSON-RPC 에러 코드 정의 |
| Poco | `poco/drivers/osx/sdk/OSXUIDumper.py` | medium | OSX UI 계층 덤프(드라이버 SDK 핵심). manifest는 OSXUI.py만 포함 |
| Poco | `poco/drivers/osx/sdk/OSXUINode.py` | medium | OSX UI 노드 어댑터 |
| Poco | `poco/drivers/osx/sdk/OSXUIFunc.py` | medium | OSX Accessibility API 래퍼 |
| Poco | `poco/drivers/windows/sdk/WindowsUIDumper.py` | medium | Windows UI 덤퍼. manifest는 WindowsUI/WindowsUINode만 포함 |
| Poco | `poco/drivers/android/utils/__init__.py` | low | 패키지 init(installation.py만 covered) |
| Poco | `poco/utils/simplerpc/jsonrpc/backend/django.py` | low | Django JSON-RPC 백엔드(테스트/예시성) |
| Poco | `poco/utils/simplerpc/jsonrpc/backend/flask.py` | low | Flask JSON-RPC 백엔드(테스트/예시성) |

## 문서화 제외 (의도적)

| 분류 | 경로 패턴 | 사유 |
|------|-----------|------|
| 단위 테스트 | `tests/*`, `test/*`, `**/test/*`, `**/tests/*`, `**/*test*.py` | 테스트 더미 |
| 벤치마크 | `benchmark/*.py` | 성능 측정 스크립트 |
| 빌드/문서 설정 | `docs/conf.py`, `DocBuilder/conf.py` | Sphinx 설정 |
| 샘플/플레이그라운드 | `playground/*` (test_blackjack 제외, 이미 covered) | 데모 스크립트 |
| Unity3d 튜토리얼 잔여 | `poco/drivers/unity3d/test/tutorial/{click,long_click,scroll1,scroll2,buttons_and_labels,frozen_ui,wait_all_ui,wait_any_ui,exception1-3,local_positioning2-3}.py` | poco-03/04에서 대표 케이스만 covered. 나머지는 동형 예제 |
| cocosjs 테스트 | `poco/drivers/cocosjs/test/simple.py` | 데모 |
| 벤더링 third-party | `poco/utils/six.py`, `poco/utils/simplerpc/jsonrpc/six.py` | 외부 라이브러리(six) 복붙 |

## 결론

가장 큰 공백 = **Airtest `aircv` 패키지 전체(14파일)**. touch/exists/wait/assert_exists 의 실제 이미지 매칭이 전부 여기서 일어나는데 7개 문서 어디에도 다뤄지지 않았다. 별도 문서(예: `airtest-05-aircv-image-matching.md`) 1건 신설 권장.

그 외는 medium 이하: Poco의 JSON-RPC 코어(base/jsonrpc2 등), OSX/Windows SDK 덤퍼·노드 보강 정도.
