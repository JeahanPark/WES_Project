// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public class BlueprintInfo
{
    public int Id;
    public int BlueprintItemId;
    public int UnlockCraftId;
}



public partial class InfoManager
{
    public List<BlueprintInfo> BlueprintInfoList = new List<BlueprintInfo>();
}
