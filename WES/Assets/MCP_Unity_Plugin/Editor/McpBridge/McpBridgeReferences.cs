// McpBridgeReferences.cs
// McpBridge partial - Inspector Object 참조 배치 매핑 핸들러.
// action: set_reference
//
// mappingsJson 형식:
// [{"propertyName":"m_Field","referenceTarget":"ChildName"},...]
// referenceTarget이 "Assets/"로 시작하면 에셋 경로로 처리.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static partial class McpBridge
{
    private static string SetReference(BridgeRequest _req)
    {
        if (string.IsNullOrEmpty(_req.componentType))
            return BuildError("'componentType' is required");
        if (string.IsNullOrEmpty(_req.mappingsJson))
            return BuildError("'mappingsJson' is required");

        var compType = FindComponentType(_req.componentType);
        if (compType == null)
            return BuildError($"Component type '{_req.componentType}' not found");

        var (go, prefabRoot) = FindTarget(_req);
        if (go == null)
            return BuildError($"Target '{_req.target}' not found");

        var component = go.GetComponent(compType);
        if (component == null)
            return BuildError($"Component '{_req.componentType}' not found on '{go.name}'");

        // mappingsJson 파싱 (JsonUtility 배열 래퍼 트릭)
        ReferenceMapping[] mappings;
        try
        {
            string wrapped = "{\"items\":" + _req.mappingsJson + "}";
            var wrapper = JsonUtility.FromJson<ReferenceMappingArray>(wrapped);
            mappings = wrapper?.items;
        }
        catch (Exception e)
        {
            return BuildError($"Failed to parse mappingsJson: {e.Message}");
        }

        if (mappings == null || mappings.Length == 0)
            return BuildError("mappingsJson is empty or invalid");

        Undo.RecordObject(component, "MCP Set Reference");

        var errors = new List<string>();
        int successCount = 0;

        foreach (var mapping in mappings)
        {
            var error = ApplyReferenceMapping(component, compType, mapping, go, prefabRoot);
            if (error != null)
                errors.Add($"{mapping.propertyName}: {error}");
            else
                successCount++;
        }

        SaveTarget(go, prefabRoot);

        if (errors.Count == 0)
            return BuildSuccess($"{successCount}/{mappings.Length} mappings applied to '{_req.componentType}'");

        string errorSummary = string.Join(", ", errors);
        return BuildError($"{successCount}/{mappings.Length} applied. Failed: {errorSummary}");
    }

    private static string ApplyReferenceMapping(
        Component _component, Type _compType,
        ReferenceMapping _mapping,
        GameObject _contextGo, GameObject _prefabRoot)
    {
        if (string.IsNullOrEmpty(_mapping.propertyName))
            return "propertyName is required";
        if (string.IsNullOrEmpty(_mapping.referenceTarget))
            return "referenceTarget is required";

        // 필드/프로퍼티 타입 추론
        var field = _compType.GetField(_mapping.propertyName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Type fieldType = field?.FieldType;

        if (fieldType == null)
        {
            var prop = _compType.GetProperty(_mapping.propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            fieldType = prop?.PropertyType;
        }

        if (fieldType == null)
            return $"Field or property '{_mapping.propertyName}' not found on '{_compType.Name}'";

        // 참조 오브젝트 해석
        var resolveError = ResolveReference(_mapping, fieldType, _contextGo, _prefabRoot, out var refObj);
        if (resolveError != null)
            return resolveError;

        // 필드에 할당
        try
        {
            if (field != null)
            {
                field.SetValue(_component, refObj);
            }
            else
            {
                var prop = _compType.GetProperty(_mapping.propertyName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                prop.SetValue(_component, refObj);
            }
            return null;
        }
        catch (Exception e)
        {
            return $"Assignment failed: {e.Message}";
        }
    }

    private static string ResolveReference(
        ReferenceMapping _mapping, Type _fieldType,
        GameObject _contextGo, GameObject _prefabRoot,
        out UnityEngine.Object _result)
    {
        _result = null;

        // 에셋 경로 (Assets/ 로 시작)
        if (_mapping.referenceTarget.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            _result = AssetDatabase.LoadAssetAtPath(_mapping.referenceTarget, _fieldType);
            if (_result == null)
                return $"Asset not found at '{_mapping.referenceTarget}'";
            return null;
        }

        // 이름으로 GameObject 탐색 (같은 프리팹 or 씬)
        GameObject refGo = null;
        if (_prefabRoot != null)
        {
            refGo = _prefabRoot.name == _mapping.referenceTarget
                ? _prefabRoot
                : FindInHierarchy(_prefabRoot.transform, _mapping.referenceTarget);
        }
        else
        {
            foreach (var root in GetAllSceneRoots())
            {
                if (root.name == _mapping.referenceTarget) { refGo = root; break; }
                var found = FindInHierarchy(root.transform, _mapping.referenceTarget);
                if (found != null) { refGo = found; break; }
            }
        }

        if (refGo == null)
            return $"GameObject '{_mapping.referenceTarget}' not found";

        // 필드 타입에 맞게 추출
        if (_fieldType == typeof(GameObject))
        {
            _result = refGo;
        }
        else if (_fieldType == typeof(Transform))
        {
            _result = refGo.transform;
        }
        else if (typeof(Component).IsAssignableFrom(_fieldType))
        {
            Type resolveType = _fieldType;
            if (!string.IsNullOrEmpty(_mapping.referenceComponentType))
                resolveType = FindComponentType(_mapping.referenceComponentType) ?? _fieldType;

            _result = refGo.GetComponent(resolveType);
            if (_result == null)
                return $"Component '{resolveType.Name}' not found on '{refGo.name}'";
        }
        else
        {
            return $"Field type '{_fieldType.Name}' is not a supported reference type";
        }

        return null;
    }

    // ---- DTO ----

    [Serializable]
    private class ReferenceMapping
    {
        public string propertyName;
        public string referenceTarget;
        public string referenceComponentType;
    }

    [Serializable]
    private class ReferenceMappingArray
    {
        public ReferenceMapping[] items;
    }
}
