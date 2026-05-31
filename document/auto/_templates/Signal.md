---
name: <Owner.EventName | RpcName | NetworkVariable명>
kind: <Event | NetworkVariable | ServerRpc | ClientRpc | UnityEvent | Delegate>
owner: "PUT_OWNER_CLASS_NAME"
signature: "<Action<T> | void | Function signature>"
direction: <Local | ServerToClient | ClientToServer | Broadcast>
authority: <Server | Client | Local>
frequency: <발사 조건 / 호출 주기>
subscribers: []                       # 구독 클래스 wiki link 배열
status: Active
---

# <name>

<한 줄 설명>

## 시그니처

```csharp
<C# 코드 한 줄 또는 짧은 블록>
```

## 발사 조건

- <조건 1>
- <조건 2>

## 관련

- 발사 주체: (Owner 클래스 wiki link)
- 페이로드 타입: `<TypeName>`
- 구독자: (시드 시 자동 채움)
