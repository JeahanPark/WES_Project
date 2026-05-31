using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class BuildingPrefabBuilder
{
    private const string BARREL_PREFAB_PATH = "Assets/Synty/PolygonGeneric/Prefabs/Props/SM_Gen_Prop_Barrel_Metal_02.prefab";
    private const string BUILDING_FOLDER = "Assets/GameResource/Building";
    private const string CAMPFIRE_PREFAB_PATH = "Assets/GameResource/Building/Campfire.prefab";
    private const string CAMPFIRE_ADDRESS = "Campfire";
    private const string DEFAULT_NETWORK_PREFABS_PATH = "Assets/Resources/DefaultNetworkPrefabs.asset";

    [MenuItem("Tools/Building/Setup Campfire Prefab")]
    public static void SetupCampfirePrefab()
    {
        EnsureFolder(BUILDING_FOLDER);

        var barrelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BARREL_PREFAB_PATH);
        if (barrelPrefab == null)
        {
            Debug.LogError($"[BuildingPrefabBuilder] Barrel prefab not found: {BARREL_PREFAB_PATH}");
            return;
        }

        // 1. 임시 인스턴스 생성 → 컴포넌트 부착 → 프리팹으로 저장
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(barrelPrefab);
        instance.name = "Campfire";

        if (instance.GetComponent<Collider>() == null)
        {
            var box = instance.AddComponent<BoxCollider>();
            box.size = new Vector3(0.8f, 1.2f, 0.8f);
            box.center = new Vector3(0f, 0.6f, 0f);
        }

        if (instance.GetComponent<NetworkObject>() == null)
            instance.AddComponent<NetworkObject>();

        if (instance.GetComponent<WorldBuildingObject>() == null)
            instance.AddComponent<WorldBuildingObject>();

        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, CAMPFIRE_PREFAB_PATH, out bool success);
        Object.DestroyImmediate(instance);

        if (!success || savedPrefab == null)
        {
            Debug.LogError($"[BuildingPrefabBuilder] Prefab 저장 실패: {CAMPFIRE_PREFAB_PATH}");
            return;
        }

        // 2. Addressable 등록
        RegisterAddressable(savedPrefab, CAMPFIRE_ADDRESS);

        // 3. NetworkPrefabsList에 등록
        RegisterNetworkPrefab(savedPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BuildingPrefabBuilder] Campfire 프리팹 셋업 완료: {CAMPFIRE_PREFAB_PATH}");
    }

    private static void EnsureFolder(string _folder)
    {
        if (AssetDatabase.IsValidFolder(_folder))
            return;

        var parent = Path.GetDirectoryName(_folder).Replace('\\', '/');
        var name = Path.GetFileName(_folder);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, name);
    }

    private static void RegisterAddressable(Object _asset, string _address)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[BuildingPrefabBuilder] AddressableAssetSettings 없음");
            return;
        }

        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_asset));
        var group = settings.DefaultGroup;
        var entry = settings.CreateOrMoveEntry(guid, group, false, false);
        entry.address = _address;
        settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true, true);
        Debug.Log($"[BuildingPrefabBuilder] Addressable 등록: address='{_address}', group='{group.Name}'");
    }

    private static void RegisterNetworkPrefab(GameObject _prefab)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DEFAULT_NETWORK_PREFABS_PATH);
        if (list == null)
        {
            Debug.LogWarning($"[BuildingPrefabBuilder] NetworkPrefabsList 없음: {DEFAULT_NETWORK_PREFABS_PATH}");
            return;
        }

        // 이미 등록되어 있는지 확인
        foreach (var entry in list.PrefabList)
        {
            if (entry != null && entry.Prefab == _prefab)
            {
                Debug.Log("[BuildingPrefabBuilder] NetworkPrefabsList 이미 등록됨");
                return;
            }
        }

        list.Add(new NetworkPrefab { Prefab = _prefab });
        EditorUtility.SetDirty(list);
        Debug.Log("[BuildingPrefabBuilder] NetworkPrefabsList 등록 완료");
    }
}
