// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public partial class ItemInfo
{
    public int Id;
    public string Name;
    public string Description;
    public bool IsStackable;
    public int MaxStack;
    public string PrefabKey;
    public string IconKey;
    public int BuildingInfoId;
}



public partial class InfoManager
{
    public List<ItemInfo> ItemInfoList = new List<ItemInfo>();
}
