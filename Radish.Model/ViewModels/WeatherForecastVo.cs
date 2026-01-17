namespace Radish.Model.ViewModels;

/// <summary>天气预报 ViewModel</summary>
/// <remarks>用于替代直接返回 WeatherForecast 实体</remarks>
public class WeatherForecastVo
{
    /// <summary>日期</summary>
    public DateOnly Date { get; set; }

    /// <summary>摄氏温度</summary>
    public int TemperatureC { get; set; }

    /// <summary>华氏温度</summary>
    public int TemperatureF { get; set; }

    /// <summary>天气描述</summary>
    public string? Summary { get; set; }
}