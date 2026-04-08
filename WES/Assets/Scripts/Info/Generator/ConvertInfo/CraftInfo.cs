// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public class CraftInfo
{
    public int Id;
    public CraftCategoryType CategoryType;
    public string Name;
    public string Description;
    public string IconKey;
    public int Value01;
    public int ResultCount;
}



public partial class InfoManager
{
    public List<CraftInfo> CraftInfoList = new List<CraftInfo>();
}
