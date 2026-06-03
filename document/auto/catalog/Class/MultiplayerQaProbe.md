---
name: MultiplayerQaProbe
category: Component
parent: null
file_path: WES/Assets/Scripts/Component/MultiplayerQaProbe.cs
role: "MPPM QA 프로브. 자기 플레이어(메인=host / 클론=clone)의 관찰 가능한 네트워크 상태(연결 여부·LocalClientId·접속 수, 스폰된 NetworkObject 목록 — player/monster/dropitem/building별 위치·HP·Cold·itemId, 로컬 인벤토리 합계)를 주기적으로 시스템 temp 공유폴더(Path.GetTempPath()/wes-mppm-qa)에 JSON 스냅샷으로 기록한다(probe_host.json / probe_clone.json, temp→rename 원자 쓰기). 모든 프로세스가 동일 절대경로로 해석하도록 상대 Temp/ 대신 시스템 temp를 쓴다. McpBridge의 mppm_collect 엔드포인트가 양측 파일을 한 번에 읽어 host/clone 스냅샷을 반환한다. 추가로 클론 한정 역방향 커맨드 채널(cmd_clone.json) — QA가 클론 파이프(IsCloneEditor 차단)를 우회해 TestMp* 화이트리스트 무인자 메서드를 seq 기반 1회 호출하도록 폴링한다(V6 클론 트리거 등). 에디터 전용."
status: Active
signals: []
---

# MultiplayerQaProbe

MPPM QA 프로브. 양측(메인=host / 클론=clone) 플레이어의 관찰 가능한 네트워크 상태를 주기적으로 시스템 temp 공유폴더에 JSON 스냅샷으로 기록하고, 클론 한정 역방향 커맨드 채널을 폴링한다.

## 동작

- **공유 폴더**: `Path.Combine(Path.GetTempPath(), "wes-mppm-qa")`. 상대 `Temp/`를 쓰지 않는 이유 — MPPM 클론은 작업디렉터리가 달라 상대경로면 파일이 서로 다른 절대폴더에 생겨 수집이 깨진다.
- **스냅샷 쓰기** (`m_SnapshotIntervalSeconds` 주기): `probe_host.json` / `probe_clone.json`. `ProbeSnapshot` DTO = 연결 여부·LocalClientId·접속 수 + `SpawnedObjects` 순회로 `ProbeObject`(kind=player/monster/dropitem/building, 위치·HP·MaxHP·Cold·alive·itemId·count) + 자기 로컬 인벤토리 합계(`ProbeInventoryEntry`). temp→rename 원자 쓰기로 부분 쓰기 파일 노출 차단. `OnDestroy`에서 자기 파일 정리.
- **수집**: McpBridge `mppm_collect` 엔드포인트가 양측 파일을 1콜로 읽어 `{ host, clone }` 반환.
- **클론 역방향 커맨드** (`cmd_clone.json`): 클론은 McpBridge 파이프가 막혀 있어(IsCloneEditor 차단) QA가 클론 메서드를 직접 호출 불가. `{ seq, method }` 폴링 → 새 `seq` 1회만 `TestMp*` 화이트리스트의 무인자 public 메서드를 `TestManager.Instance`에서 리플렉션 호출(V6 클론 수집 트리거 등). seq 단조 증가로 재실행 차단, 화이트리스트로 임의 메서드 차단.

## 관련

- 부모: (없음)
- 협력: [[MppmBootstrapWorker]] (자동 접속), [[TestManager]] (클론 커맨드 대상 TestMp*), McpBridge `mppm_collect`
