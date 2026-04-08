// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public class CraftConditionInfo
{
    public int Id;
    public int CraftId;
    public CraftConditionType ConditionType;
    public int ConditionValue;
}



public partial class InfoManager
{
    public List<CraftConditionInfo> CraftConditionInfoList = new List<CraftConditionInfo>();
}
