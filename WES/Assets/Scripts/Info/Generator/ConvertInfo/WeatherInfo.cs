// 해당 코드는 엑셀로 뽑은 .cs파일이라 수정해도 의미가 없습니다.

using System.Collections.Generic;

public partial class WeatherInfo
{
    public int Id;
    public WeatherType WeatherType;
    public string Name;
    public float ThreatMul;
    public float VisionMul;
    public float ColdDrainMul;
    public float MoveCostMul;
}



public partial class InfoManager
{
    public List<WeatherInfo> WeatherInfoList = new List<WeatherInfo>();
}
