// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public partial class WorldAreaInfo
{
    public int Id;
    public string Name;
    public int MaxCount;
    public float RespawnDelay;
    public float MoveCostMultiplier;
    public float AxisMin;
    public float AxisMax;
}



public partial class InfoManager
{
    public List<WorldAreaInfo> WorldAreaInfoList = new List<WorldAreaInfo>();
}
