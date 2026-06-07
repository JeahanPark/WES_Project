# 멀티 QA 기능 — 설계 브리프 (team-lead)

> 작성: 2026-06-02 · 슬러그: `mppm-qa` · 팀: director / client / qa (3-에이전트)
> 본 문서는 team-lead 브리프. 정식 기획서는 director가 `game-design/mppm-qa/기획.md`, 코드명세는 client가 `client-spec/mppm-qa/코드명세.md`에 작성한다.

## 한 줄 컨셉

Unity MPPM(Multiplayer Play Mode) Virtual Player를 활용해, **사용자 개입 0**으로 호스트+클론 멀티 시나리오를 자동 실행·검증하는 QA 기능.

## 1순위 제약 (불가침)

**테스트 실행 시 사용자 개입이 전혀 없어야 한다 (per-run zero intervention).**
- QA 명령 1회 → 런치·구동·프로브·검증·정리까지 전 자동.
- per-run 개입 0이면 됨. MPPM Virtual Player를 *최초 1회* 활성화하는 일회성 셋업은 허용(매 실행마다 클릭은 금지).

## 검증 방식

**양쪽 관찰 (클론 프로브).** 호스트는 MCP(또는 브릿지)로 직접 관찰, 클론은 프로브로 상태를 내보내 QA가 수집.

## 자동 파이프라인

```
QA 명령 1회
  ① 런치    host + 클론 자동 접속 (부트스트랩 복원, JoinCode temp 공유, 태그 자동)
  ② 구동    TestManager 호스트 권위 시나리오 자동 실행
  ③ 프로브  host·클론 상태 스냅샷 → 브릿지가 수집
  ④ 검증    host vs 클론 스냅샷 대조 → 명세 기준 판정
  ⑤ 정리    플레이 종료 + temp 정리 (전부 자동)
```

## 빌드 파트

| # | 파트 | 내용 | 담당 |
|---|------|------|------|
| 1 | MPPM 자동 부트스트랩 | Play 1회로 host+클론 자동 접속·태그·Ingame 진입 (삭제된 `MultiplayerTestBootstrap` 재구축) | client |
| 2 | 브릿지 프로브 수집 | `MultiplayerQaProbe`가 각 플레이어 상태를 공유 위치에 기록 → McpBridge에 수집 엔드포인트 추가, QA 1콜로 양쪽 스냅샷 획득 | client |
| 3 | QA 모드 C (multiplayer) | qa.md에 멀티 워크플로우 추가 (호스트 관찰 + 클론 프로브, 명세 기반 시나리오, 자동 판정·리포트) | director(기준)+qa |

## 실현성 게이트 (client가 Phase 1에서 최우선 검증)

**MPPM Virtual Player를 프로그램적으로 활성/유지할 수 있는가?** (`com.unity.multiplayer.playmode` 1.6.2 API 조사)
- 가능/지속 → u_play 진입만으로 부트스트랩 자동 접속.
- 불가(매 세션 수동) → fallback: 브릿지가 2nd 에디터/스탠드얼론 클라 프로세스 직접 런치(ParrelSync 방식).
- 어느 쪽이든 per-run 개입 0 보장. 효율적인 쪽 채택.

## 현재 코드 사실 (Trust-but-Verify 기준점)

- `MultiplayerTestBootstrap.cs` 는 **삭제됨** (vault catalog는 stale). 재구축 대상.
- 멀티 진입: `LobbyPopup`(Relay JoinCode) → `GameNetworkManager` 수동 흐름.
- 네트워크 RPC 베이스: `NetworkGameController`(NetworkBehaviour). 호스트 권위.
- MPPM 패키지: `com.unity.multiplayer.playmode` 1.6.2, `com.unity.multiplayer.center` 1.0.0 설치됨.
- McpBridge: `Assets/MCP_Unity_Plugin/Editor/McpBridge.cs`. 수정 시 원본(`C:\GitFork\MCP_Unity\MCP_Unity_Plugin`) 먼저.

## 성공 기준

- QA가 단일 명령으로 멀티 시나리오 실행, 사용자 클릭 0.
- 호스트·클론 양쪽 상태 대조로 동기화 버그(스폰 누락·RPC 미전파·NetworkVariable 불일치) 검출 가능.
- qa.md 모드 C가 독립 워크플로우로 문서화됨.
