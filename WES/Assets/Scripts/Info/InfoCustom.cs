// 자동 생성 Info 클래스의 커스텀 멤버(계산 프로퍼티 등).
// ConvertInfo/*.cs 는 InfoConvert 실행 시 덮어써지므로, 보존이 필요한 멤버는 여기 partial 로 둔다.

public partial class ItemInfo
{
    public bool IsBuilding => BuildingInfoId > 0;
}
