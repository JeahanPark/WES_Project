---
name: AudioKey
category: Manager
parent: null
file_path: WES/Assets/Scripts/Manager/AudioKey.cs
role: "R4 ③ 사운드 트리거 키 단일소스. 호출부는 이 상수를 쓰고, sound가 Addressable에 같은 키로 AudioClip을 등록하면 소리가 난다(키 미등록 = 무음, AudioManager null 가드). Addressable Address = 키 문자열 그대로(예: \"sfx_hit\"). 기획 §6 트리거 약 12개 + BGM/Ambient/Stinger."
status: Active
signals: []
---

# AudioKey

R4 ③ 사운드 트리거 키 단일소스. 호출부는 이 상수를 쓰고, sound가 Addressable에 같은 키로 AudioClip을 등록하면 소리가 난다(키 미등록 = 무음, AudioManager null 가드). Addressable Address = 키 문자열 그대로(예: "sfx_hit"). 기획 §6 트리거 약 12개 + BGM/Ambient/Stinger.

## 관련

- 부모: (없음)
