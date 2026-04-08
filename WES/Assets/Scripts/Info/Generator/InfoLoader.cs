// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cysharp.Threading.Tasks;

public partial class InfoManager
{
    public async UniTask LoadAllInfo()
    {
        CraftConditionInfoList = await LoadInfo<CraftConditionInfo>();
        CraftInfoList = await LoadInfo<CraftInfo>();
        CraftMaterialInfoList = await LoadInfo<CraftMaterialInfo>();
        DropSourceInfoList = await LoadInfo<DropSourceInfo>();
        DropTableItemInfoList = await LoadInfo<DropTableItemInfo>();
        ItemInfoList = await LoadInfo<ItemInfo>();
        MonsterInfoList = await LoadInfo<MonsterInfo>();
        WorldAreaInfoList = await LoadInfo<WorldAreaInfo>();
        WorldAreaMonsterInfoList = await LoadInfo<WorldAreaMonsterInfo>();
        WorldObjectInfoList = await LoadInfo<WorldObjectInfo>();
    }

    private async UniTask<List<T>> LoadInfo<T>() where T : new()
    {
        await UniTask.Yield();

        string typeName = typeof(T).Name;
        string fileName = typeName;
        string csvPath = $"Assets/CSVInfo/{fileName}.csv";

        if (!File.Exists(csvPath))
        {
            UnityEngine.Debug.LogError($"CSV file not found: {csvPath}");
            return new List<T>();
        }

        string[] lines = File.ReadAllLines(csvPath);
        if (lines.Length < 2)
        {
            UnityEngine.Debug.LogWarning($"CSV file {fileName} has insufficient data");
            return new List<T>();
        }

        // 헤더 파싱
        string[] headers = lines[0].Split(',');
        List<T> list = new List<T>();

        // 데이터 파싱
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] values = line.Split(',');
            T item = new T();

            // Reflection으로 필드에 값 할당
            for (int j = 0; j < headers.Length && j < values.Length; j++)
            {
                string header = headers[j].Trim();
                string fieldName = header.Split('.')[0];
                FieldInfo field = typeof(T).GetField(fieldName);

                if (field != null)
                {
                    object value = ParseValue(values[j].Trim(), field.FieldType);
                    field.SetValue(item, value);
                }
            }

            list.Add(item);
        }

        return list;
    }

    private static object ParseValue(string value, Type targetType)
    {
        if (targetType == typeof(int))
            return int.Parse(value);
        else if (targetType == typeof(long))
            return long.Parse(value);
        else if (targetType == typeof(float))
            return float.Parse(value);
        else if (targetType == typeof(double))
            return double.Parse(value);
        else if (targetType == typeof(bool))
            return value == "1" || value.ToLower() == "true";
        else if (targetType == typeof(string))
            return value;
        else if (targetType.IsEnum)
            return Enum.Parse(targetType, value);
        else
            return value;
    }
}
