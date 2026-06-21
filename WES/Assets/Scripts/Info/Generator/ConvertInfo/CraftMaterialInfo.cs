// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public partial class CraftMaterialInfo
{
    public int Id;
    public int CraftId;
    public int MaterialItemId;
    public int RequiredCount;
}



public partial class InfoManager
{
    public List<CraftMaterialInfo> CraftMaterialInfoList = new List<CraftMaterialInfo>();
}
