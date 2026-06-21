// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public partial class DropSourceInfo
{
    public WorldObjectType WorldObjectType;
    public int SourceId;
    public int DropTableId;
}



public partial class InfoManager
{
    public List<DropSourceInfo> DropSourceInfoList = new List<DropSourceInfo>();
}
