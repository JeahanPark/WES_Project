---
name: AudioManager
category: Manager
parent: "[[MonoSingleton]]"
file_path: WES/Assets/Scripts/Manager/AudioManager.cs
role: "R4 ③ 사운드 매니저(골격). 4채널(BGM/Ambient/SFX/Stinger) 재생.  음원이 0개여도 동작한다(null 가드) — 키에 해당하는 AudioClip이 Addressable에 없으면 조용히 무음 통과하고 실패 키를 캐시해 재시도/스팸을 막는다(director 확정: 무음클립 불필요). 음원이 채워지면 Addressable에 같은 키로 등록만 하면 곧장 소리가 난다.  모든 재생은 각 클라이언트 로컬(동기화 무관). 동료 사망 stinger 등도 이미 동기화된 상태를 각 클라가 읽어 로컬 발화한다(R4 §5).  믹스 우선순위(기획 §5-3): Stinger > Sfx > Ambient > Bgm. (현재 골격은 채널별 볼륨 가중으로 표현)"
status: Active
signals: []
---

# AudioManager

R4 ③ 사운드 매니저(골격). 4채널(BGM/Ambient/SFX/Stinger) 재생.  음원이 0개여도 동작한다(null 가드) — 키에 해당하는 AudioClip이 Addressable에 없으면 조용히 무음 통과하고 실패 키를 캐시해 재시도/스팸을 막는다(director 확정: 무음클립 불필요). 음원이 채워지면 Addressable에 같은 키로 등록만 하면 곧장 소리가 난다.  모든 재생은 각 클라이언트 로컬(동기화 무관). 동료 사망 stinger 등도 이미 동기화된 상태를 각 클라가 읽어 로컬 발화한다(R4 §5).  믹스 우선순위(기획 §5-3): Stinger > Sfx > Ambient > Bgm. (현재 골격은 채널별 볼륨 가중으로 표현)

## 관련

- 부모: [[MonoSingleton]]
