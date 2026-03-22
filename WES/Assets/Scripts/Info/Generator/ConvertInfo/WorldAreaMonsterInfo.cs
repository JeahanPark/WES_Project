// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public class WorldAreaMonsterInfo
{
    public int Id;
    public int AreaId;
    public int MonsterId;
}



public partial class InfoManager
{
    public List<WorldAreaMonsterInfo> WorldAreaMonsterInfoList = new List<WorldAreaMonsterInfo>();
}
