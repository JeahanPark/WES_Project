#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using UnityEngine;

// MPPM QA 프로브: 자기 플레이어(메인=host / 클론=clone)의 관찰 가능한 네트워크 상태를
// 주기적으로 공유 temp 파일(Temp/mppm-qa/probe_<role>.json)에 기록한다.
// 클론은 McpBridge 파이프를 열지 않으므로(IsCloneEditor 차단), 파일 경유가 클론 상태 수집의 정답.
// McpBridge의 mppm_collect 엔드포인트가 양측 파일을 읽어 host/clone 스냅샷을 한 번에 반환한다.
//
// 추가로, 클론 한정으로 cmd_clone.json(역방향 커맨드 채널)을 폴링한다. QA가 클론 메서드를
// 호출하려면 파이프가 막혀 있어 파일 경유가 유일 경로다(V6 클론 트리거 등). seq 기반 1회 실행 +
// TestMp* 화이트리스트로 임의 호출을 차단한다.
// 에디터 전용.
public class MultiplayerQaProbe : MonoBehaviour
{
    // 시스템 temp 하위 고정 폴더명. 상대경로(Temp/..)를 쓰지 않는 이유:
    // MPPM 가상 플레이어(클론)는 메인 에디터와 작업디렉터리가 다를 수 있어, 상대경로면
    // probe_host.json / probe_clone.json 이 서로 다른 절대폴더에 생겨 수집이 깨진다.
    // Path.GetTempPath()는 모든 프로세스(메인·클론·QA·브릿지)가 동일 절대경로로 해석 → 공유 보장.
    public const string PROBE_DIR_NAME = "wes-mppm-qa";
    public const string HOST_FILE = "probe_host.json";
    public const string CLONE_FILE = "probe_clone.json";
    public const string CLONE_CMD_FILE = "cmd_clone.json";
    // 클론에서 파일 채널로 호출 허용할 메서드 접두사(화이트리스트). 임의 메서드 실행 차단.
    private const string CMD_METHOD_PREFIX = "TestMp";

    // 모든 프로세스가 동일하게 해석하는 절대 공유 폴더(시스템 temp 하위).
    public static string ProbeDir => Path.Combine(Path.GetTempPath(), PROBE_DIR_NAME);

    [SerializeField] private float m_SnapshotIntervalSeconds = 0.5f;

    private float m_Timer;
    private string m_FilePath;
    private string m_CmdFilePath;
    private bool m_IsMain;
    private int m_LastHandledCmdSeq = -1;

    private void Start()
    {
        m_IsMain = CurrentPlayer.IsMainEditor;

        string dir = ProbeDir;
        Directory.CreateDirectory(dir);
        m_FilePath = Path.Combine(dir, m_IsMain ? HOST_FILE : CLONE_FILE);
        m_CmdFilePath = Path.Combine(dir, CLONE_CMD_FILE);
        GameDebug.Log($"[MultiplayerQaProbe] role={(m_IsMain ? "host" : "clone")} probeFile={m_FilePath}");

        WriteSnapshot();
    }

    private void Update()
    {
        // 클론 한정 커맨드 폴링(호스트는 QA가 u_play_invoke로 직접 호출 가능하므로 불필요).
        if (!m_IsMain)
        {
            PollCloneCommand();
        }

        m_Timer += Time.unscaledDeltaTime;
        if (m_Timer < m_SnapshotIntervalSeconds)
        {
            return;
        }

        m_Timer = 0f;
        WriteSnapshot();
    }

    private void OnDestroy()
    {
        // 플레이 종료 시 자기 스냅샷 파일 정리(전 자동 정리, QA 관여 0).
        TryDeleteFile();
    }

    private void WriteSnapshot()
    {
        ProbeSnapshot snapshot = BuildSnapshot();
        string json = JsonUtility.ToJson(snapshot);

        // temp→rename 원자 쓰기: 수집 측이 부분 쓰기 파일을 읽지 않도록 보장.
        string tempPath = m_FilePath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, json);
            if (File.Exists(m_FilePath))
            {
                File.Delete(m_FilePath);
            }
            File.Move(tempPath, m_FilePath);
        }
        catch (IOException)
        {
            // 다음 주기에 재시도. 일시적 IO 경합은 무시.
        }
    }

    // 클론 한정 역방향 커맨드 채널. cmd_clone.json { seq, method } 폴링.
    // 새 seq 감지 시 1회만 화이트리스트(TestMp*) 메서드를 Managers.Test에서 호출한다.
    private void PollCloneCommand()
    {
        try
        {
            if (!File.Exists(m_CmdFilePath))
            {
                return;
            }

            string json = File.ReadAllText(m_CmdFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            CloneCommand cmd = JsonUtility.FromJson<CloneCommand>(json);
            if (cmd == null || cmd.seq <= m_LastHandledCmdSeq)
            {
                return; // 이미 처리한 seq(또는 무효) — 재실행 금지.
            }

            m_LastHandledCmdSeq = cmd.seq;
            InvokeWhitelistedCommand(cmd.method);
        }
        catch (IOException)
        {
            // QA가 쓰는 중일 수 있음. 다음 주기 재시도.
        }
    }

    private void InvokeWhitelistedCommand(string _method)
    {
        if (string.IsNullOrEmpty(_method) || !_method.StartsWith(CMD_METHOD_PREFIX))
        {
            GameDebug.LogWarning($"[MultiplayerQaProbe] 클론 커맨드 거부(화이트리스트 위반): '{_method}'. '{CMD_METHOD_PREFIX}*'만 허용.");
            return;
        }

        TestManager test = TestManager.Instance;
        if (test == null)
        {
            GameDebug.LogError("[MultiplayerQaProbe] TestManager 없음 — 클론 커맨드 무시.");
            return;
        }

        // 파라미터 없는 public 인스턴스 메서드만 호출(V6 등 트리거는 무인자).
        MethodInfo mi = typeof(TestManager).GetMethod(
            _method, BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);
        if (mi == null)
        {
            GameDebug.LogError($"[MultiplayerQaProbe] 클론 커맨드 메서드 없음/무인자 아님: '{_method}'.");
            return;
        }

        GameDebug.Log($"[MultiplayerQaProbe] 클론 커맨드 실행: {_method}()");
        mi.Invoke(test, null);
    }

    private ProbeSnapshot BuildSnapshot()
    {
        ProbeSnapshot s = new ProbeSnapshot
        {
            role = m_IsMain ? "host" : "clone",
            isMainEditor = m_IsMain,
            timestamp = Time.realtimeSinceStartup,
            objects = new List<ProbeObject>(),
            localInventory = new List<ProbeInventoryEntry>()
        };

        NetworkManager nm = NetworkManager.Singleton;
        s.connected = nm != null && nm.IsListening && (nm.IsClient || nm.IsServer);
        s.localClientId = nm != null ? (long)nm.LocalClientId : -1;
        s.connectedClientCount = nm != null && nm.ConnectedClients != null ? nm.ConnectedClients.Count : 0;

        if (nm != null && nm.SpawnManager != null)
        {
            foreach (var kv in nm.SpawnManager.SpawnedObjects)
            {
                NetworkObject no = kv.Value;
                if (no == null)
                {
                    continue;
                }

                s.objects.Add(BuildObject(no));
            }
        }

        BuildLocalInventory(s);
        return s;
    }

    private ProbeObject BuildObject(NetworkObject _no)
    {
        Vector3 p = _no.transform.position;
        ProbeObject o = new ProbeObject
        {
            id = (long)_no.NetworkObjectId,
            kind = "other",
            posX = p.x,
            posY = p.y,
            posZ = p.z,
            hp = -1,
            maxHp = -1,
            alive = false,
            cold = -1,
            maxCold = -1,
            itemId = -1,
            count = -1
        };

        // 종류 판별: 컴포넌트 유무로 분류. PlayerCharacter는 CharacterBase 파생이므로 먼저 검사.
        if (_no.TryGetComponent(out PlayerCharacter player))
        {
            o.kind = "player";
            o.hp = player.HP;
            o.maxHp = player.MaxHP;
            o.cold = player.Cold;
            o.maxCold = player.MaxCold;
        }
        else if (_no.TryGetComponent(out MonsterBase monster))
        {
            o.kind = "monster";
            o.hp = monster.HP;
            o.maxHp = monster.MaxHP;
            o.alive = !monster.IsDead;
        }
        else if (_no.TryGetComponent(out WorldDropItem drop))
        {
            o.kind = "dropitem";
            // NetworkVariable 원본(ItemInfoId)을 쓴다 — 캐싱 m_ItemInfo는 클론서 Load 타이밍에 null 가능.
            o.itemId = drop.ItemInfoId;
            o.count = drop.Count;
        }
        else if (_no.TryGetComponent(out WorldBuildingObject building))
        {
            o.kind = "building";
            // 주의: BuildingInfoId는 plain int(NetworkVariable 아님) — 클론에서 값 미보장(P1/V8 별도).
            o.itemId = building.BuildingInfoId;
        }

        return o;
    }

    private void BuildLocalInventory(ProbeSnapshot _s)
    {
        // 자기 플레이어의 로컬 인벤토리(복제 안 됨, 단측). V6 수집자 +1 검증용.
        InGameController controller = InGameController.Instance;
        if (controller == null || controller.ObjectDataWorker == null)
        {
            return;
        }

        InventoryRegistry inventory = controller.ObjectDataWorker.GetInventoryRegistry();
        if (inventory == null)
        {
            return;
        }

        Dictionary<int, int> counts = new Dictionary<int, int>();
        ItemData[] slots = inventory.GetSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            ItemData item = slots[i];
            if (item == null || item.Info == null)
            {
                continue;
            }

            int id = item.Info.Id;
            counts.TryGetValue(id, out int prev);
            counts[id] = prev + item.Count;
        }

        foreach (var kv in counts)
        {
            _s.localInventory.Add(new ProbeInventoryEntry { itemId = kv.Key, count = kv.Value });
        }
    }

    private void TryDeleteFile()
    {
        try
        {
            if (!string.IsNullOrEmpty(m_FilePath) && File.Exists(m_FilePath))
            {
                File.Delete(m_FilePath);
            }
        }
        catch (IOException)
        {
        }
    }
}

// JsonUtility 직렬화용 스냅샷 DTO. 양측(host/clone) 동일 포맷.
[System.Serializable]
public class ProbeSnapshot
{
    public string role;
    public bool isMainEditor;
    public long localClientId;
    public bool connected;
    public int connectedClientCount;
    public float timestamp;
    public List<ProbeObject> objects;
    public List<ProbeInventoryEntry> localInventory;
}

[System.Serializable]
public class ProbeObject
{
    public long id;            // NetworkObjectId (양측 동일 키)
    public string kind;        // player|monster|dropitem|building|other
    public float posX;
    public float posY;
    public float posZ;
    public int hp;             // char만 (-1=미해당)
    public int maxHp;
    public bool alive;         // monster만
    public int cold;           // player만 (-1=미해당)
    public int maxCold;
    public int itemId;         // dropitem/building만 (-1=미해당)
    public int count;          // dropitem만
}

[System.Serializable]
public class ProbeInventoryEntry
{
    public int itemId;
    public int count;
}

// 클론 역방향 커맨드 DTO. QA가 cmd_clone.json에 기록 → 클론 프로브가 폴링.
// seq는 단조 증가(같은 seq 재실행 금지). method는 TestMp* 화이트리스트.
[System.Serializable]
public class CloneCommand
{
    public int seq;
    public string method;
}
#endif
