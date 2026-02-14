using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class InfoConvertEditor
{
    private const string CSV_FOLDER_PATH = "Assets/CSVInfo";
    private const string OUTPUT_FOLDER_PATH = "Assets/Scripts/Info/Generator/ConvertInfo";
    private const string LOADER_OUTPUT_PATH = "Assets/Scripts/Info/Generator";

    [MenuItem("Assets/InfoConvert", true)]
    private static bool ValidateConvertSelectedCSV()
    {
        // 선택된 오브젝트가 있는지 확인
        if (Selection.activeObject == null)
            return false;

        // 선택된 파일의 경로 가져오기
        string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);

        // 경로를 정규화 (슬래시 통일)
        assetPath = assetPath.Replace("\\", "/");

        // CSV 파일인지 확인
        if (!assetPath.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase))
            return false;

        // CSVInfo 폴더 안에 있는지 확인
        if (!assetPath.StartsWith(CSV_FOLDER_PATH + "/", System.StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    [MenuItem("Assets/InfoConvert", false, 20)]
    private static void ConvertSelectedCSV()
    {
        string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);

        // CSVInfo 폴더 체크
        if (!assetPath.StartsWith(CSV_FOLDER_PATH + "/"))
        {
            Debug.LogError($"선택한 파일이 {CSV_FOLDER_PATH} 폴더 안에 있지 않습니다.");
            return;
        }

        // 출력 폴더가 없으면 생성
        if (!Directory.Exists(OUTPUT_FOLDER_PATH))
        {
            Directory.CreateDirectory(OUTPUT_FOLDER_PATH);
        }

        // 선택된 파일만 변환
        ProcessCSVFile(assetPath, OUTPUT_FOLDER_PATH);

        // 모든 CSV 파일 이름 수집 (InfoLoader 재생성용)
        string[] allCsvFiles = Directory.GetFiles(CSV_FOLDER_PATH, "*.csv");
        List<string> allFileNames = new List<string>();
        foreach (string csvFilePath in allCsvFiles)
        {
            allFileNames.Add(Path.GetFileNameWithoutExtension(csvFilePath));
        }

        // InfoLoader.cs 재생성
        GenerateInfoLoader(allFileNames, LOADER_OUTPUT_PATH);

        // 에셋 데이터베이스 리프레시
        AssetDatabase.Refresh();
        Debug.Log($"CSV conversion completed: {Path.GetFileName(assetPath)}");
    }

    [MenuItem("Tools/InfoConvert")]
    public static void ConvertAllCSV()
    {
        string csvFolderPath = CSV_FOLDER_PATH;
        string outputFolderPath = OUTPUT_FOLDER_PATH;
        string loaderOutputPath = LOADER_OUTPUT_PATH;

        // 출력 폴더가 없으면 생성
        if (!Directory.Exists(outputFolderPath))
        {
            Directory.CreateDirectory(outputFolderPath);
        }

        // Assets/Info 폴더의 모든 CSV 파일 찾기
        string[] csvFiles = Directory.GetFiles(csvFolderPath, "*.csv");

        if (csvFiles.Length == 0)
        {
            Debug.LogWarning("No CSV files found in " + csvFolderPath);
            return;
        }

        List<string> fileNames = new List<string>();

        foreach (string csvFilePath in csvFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(csvFilePath);
            fileNames.Add(fileName);
            ProcessCSVFile(csvFilePath, outputFolderPath);
        }

        // InfoLoader.cs 생성
        GenerateInfoLoader(fileNames, loaderOutputPath);

        // 에셋 데이터베이스 리프레시
        AssetDatabase.Refresh();
        Debug.Log("CSV conversion completed!");
    }

    private static void ProcessCSVFile(string csvFilePath, string outputFolderPath)
    {
        string fileName = Path.GetFileNameWithoutExtension(csvFilePath);
        Debug.Log($"Processing: {fileName}.csv");

        // CSV 파일 읽기
        string[] lines = File.ReadAllLines(csvFilePath);

        if (lines.Length < 2)
        {
            Debug.LogWarning($"CSV file {fileName} has insufficient data (needs at least 2 lines)");
            return;
        }

        // 1행: 필드 정의
        string headerLine = lines[0];
        string[] headers = headerLine.Split(',');

        // 필드 정보 파싱
        List<FieldInfo> fields = new List<FieldInfo>();
        foreach (string header in headers)
        {
            string[] parts = header.Split('.');
            if (parts.Length != 2)
            {
                Debug.LogError($"Invalid header format: {header}. Expected format: VariableName.TYPE");
                return;
            }

            string varName = parts[0].Trim();
            string typeName = parts[1].Trim().ToUpper();
            string csharpType = ConvertTypeToCSharp(typeName);

            if (csharpType == null)
            {
                Debug.LogError($"Unsupported type: {typeName}");
                return;
            }

            fields.Add(new FieldInfo
            {
                Name = varName,
                Type = csharpType,
                TypeName = typeName
            });
        }

        // 2행 이후: 데이터
        List<string[]> dataRows = new List<string[]>();
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            string[] values = line.Split(',');
            dataRows.Add(values);
        }

        // C# 파일 생성 (Info 클래스 + partial InfoManager 포함)
        GenerateCSharpFile(fileName, fields, dataRows, outputFolderPath);
    }

    private static string ConvertTypeToCSharp(string typeName)
    {
        switch (typeName)
        {
            case "INT": return "int";
            case "LONG": return "long";
            case "FLOAT": return "float";
            case "DOUBLE": return "double";
            case "STRING": return "string";
            case "BOOL": return "bool";
            case "ENUM": return "ENUM"; // ENUM은 특수 처리
            default: return null;
        }
    }

    private static void GenerateCSharpFile(string fileName, List<FieldInfo> fields, List<string[]> dataRows, string outputFolderPath)
    {
        // 파일명이 이미 Info로 끝나면 Info를 붙이지 않음
        string className = fileName.EndsWith("Info") ? fileName : $"{fileName}Info";
        string outputFilePath = Path.Combine(outputFolderPath, $"{className}.cs");

        StringBuilder sb = new StringBuilder();

        // 주석 추가
        sb.AppendLine("// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.");
        sb.AppendLine();

        // using 문 추가
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();

        // 클래스 시작
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");

        // 필드 선언 (클래스 레벨)
        foreach (FieldInfo field in fields)
        {
            // ENUM 타입이면 변수명을 enum 타입명으로 사용
            string fieldType = field.Type == "ENUM" ? field.Name : field.Type;
            sb.AppendLine($"    public {fieldType} {field.Name};");
        }

        // 클래스 종료
        sb.AppendLine("}");

        // 빈 줄 추가
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine();

        // partial InfoManager 클래스 추가
        sb.AppendLine("public partial class InfoManager");
        sb.AppendLine("{");
        sb.AppendLine($"    public List<{className}> {fileName}List = new List<{className}>();");
        sb.AppendLine("}");

        // 파일 쓰기
        File.WriteAllText(outputFilePath, sb.ToString());
        Debug.Log($"Generated: {className}.cs");
    }

    private static void GenerateInfoLoader(List<string> fileNames, string outputFolderPath)
    {
        string outputFilePath = Path.Combine(outputFolderPath, "InfoLoader.cs");

        StringBuilder sb = new StringBuilder();

        // 주석 추가
        sb.AppendLine("// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.");
        sb.AppendLine();

        // using 문 추가
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using Cysharp.Threading.Tasks;");
        sb.AppendLine();

        // partial InfoManager 클래스 시작
        sb.AppendLine("public partial class InfoManager");
        sb.AppendLine("{");

        // LoadAllInfo 함수
        sb.AppendLine("    public async UniTask LoadAllInfo()");
        sb.AppendLine("    {");

        foreach (string fileName in fileNames)
        {
            // 파일명이 이미 Info로 끝나면 Info를 붙이지 않음
            string className = fileName.EndsWith("Info") ? fileName : $"{fileName}Info";
            sb.AppendLine($"        {fileName}List = await LoadInfo<{className}>();");
        }

        sb.AppendLine("    }");
        sb.AppendLine();

        // LoadInfo<T> 함수
        sb.AppendLine("    private async UniTask<List<T>> LoadInfo<T>() where T : new()");
        sb.AppendLine("    {");
        sb.AppendLine("        await UniTask.Yield();");
        sb.AppendLine();
        sb.AppendLine("        string typeName = typeof(T).Name;");
        sb.AppendLine("        string fileName = typeName;");
        sb.AppendLine("        string csvPath = $\"Assets/CSVInfo/{fileName}.csv\";");
        sb.AppendLine();
        sb.AppendLine("        if (!File.Exists(csvPath))");
        sb.AppendLine("        {");
        sb.AppendLine("            UnityEngine.Debug.LogError($\"CSV file not found: {csvPath}\");");
        sb.AppendLine("            return new List<T>();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        string[] lines = File.ReadAllLines(csvPath);");
        sb.AppendLine("        if (lines.Length < 2)");
        sb.AppendLine("        {");
        sb.AppendLine("            UnityEngine.Debug.LogWarning($\"CSV file {fileName} has insufficient data\");");
        sb.AppendLine("            return new List<T>();");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        // 헤더 파싱");
        sb.AppendLine("        string[] headers = lines[0].Split(',');");
        sb.AppendLine("        List<T> list = new List<T>();");
        sb.AppendLine();
        sb.AppendLine("        // 데이터 파싱");
        sb.AppendLine("        for (int i = 1; i < lines.Length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            string line = lines[i].Trim();");
        sb.AppendLine("            if (string.IsNullOrEmpty(line)) continue;");
        sb.AppendLine();
        sb.AppendLine("            string[] values = line.Split(',');");
        sb.AppendLine("            T item = new T();");
        sb.AppendLine();
        sb.AppendLine("            // Reflection으로 필드에 값 할당");
        sb.AppendLine("            for (int j = 0; j < headers.Length && j < values.Length; j++)");
        sb.AppendLine("            {");
        sb.AppendLine("                string header = headers[j].Trim();");
        sb.AppendLine("                string fieldName = header.Split('.')[0];");
        sb.AppendLine("                FieldInfo field = typeof(T).GetField(fieldName);");
        sb.AppendLine();
        sb.AppendLine("                if (field != null)");
        sb.AppendLine("                {");
        sb.AppendLine("                    object value = ParseValue(values[j].Trim(), field.FieldType);");
        sb.AppendLine("                    field.SetValue(item, value);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            list.Add(item);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return list;");
        sb.AppendLine("    }");
        sb.AppendLine();

        // ParseValue 헬퍼 함수
        sb.AppendLine("    private static object ParseValue(string value, Type targetType)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (targetType == typeof(int))");
        sb.AppendLine("            return int.Parse(value);");
        sb.AppendLine("        else if (targetType == typeof(long))");
        sb.AppendLine("            return long.Parse(value);");
        sb.AppendLine("        else if (targetType == typeof(float))");
        sb.AppendLine("            return float.Parse(value);");
        sb.AppendLine("        else if (targetType == typeof(double))");
        sb.AppendLine("            return double.Parse(value);");
        sb.AppendLine("        else if (targetType == typeof(bool))");
        sb.AppendLine("            return value == \"1\" || value.ToLower() == \"true\";");
        sb.AppendLine("        else if (targetType == typeof(string))");
        sb.AppendLine("            return value;");
        sb.AppendLine("        else if (targetType.IsEnum)");
        sb.AppendLine("            return Enum.Parse(targetType, value);");
        sb.AppendLine("        else");
        sb.AppendLine("            return value;");
        sb.AppendLine("    }");

        // 클래스 종료
        sb.AppendLine("}");

        // 파일 쓰기
        File.WriteAllText(outputFilePath, sb.ToString());
        Debug.Log("Generated: InfoLoader.cs");
    }

    private class FieldInfo
    {
        public string Name;
        public string Type;
        public string TypeName;
    }
}
