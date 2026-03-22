// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public class WorldObjectInfo
{
    public int Id;
    public WorldObjectType WorldObjectType;
    public string Name;
    public int MaxHP;
}



public partial class InfoManager
{
    public List<WorldObjectInfo> WorldObjectInfoList = new List<WorldObjectInfo>();
}
