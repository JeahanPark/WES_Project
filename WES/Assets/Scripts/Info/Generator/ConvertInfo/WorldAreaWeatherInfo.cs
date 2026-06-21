// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public partial class WorldAreaWeatherInfo
{
    public int Id;
    public int AreaId;
    public WeatherType WeatherType;
    public float Chance;
}



public partial class InfoManager
{
    public List<WorldAreaWeatherInfo> WorldAreaWeatherInfoList = new List<WorldAreaWeatherInfo>();
}
