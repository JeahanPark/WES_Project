// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public partial class DropTableItemInfo
{
    public int DropTableId;
    public RewardType RewardType;
    public int RewardId;
    public int Min;
    public int Max;
    public float Chance;
}



public partial class InfoManager
{
    public List<DropTableItemInfo> DropTableItemInfoList = new List<DropTableItemInfo>();
}
