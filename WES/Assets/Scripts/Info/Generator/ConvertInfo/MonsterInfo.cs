// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public class MonsterInfo
{
    public int Id;
    public string Name;
    public int MaxHP;
    public string PrefabKey;
    public int DropTableId;
}



public partial class InfoManager
{
    public List<MonsterInfo> MonsterInfoList = new List<MonsterInfo>();
}
