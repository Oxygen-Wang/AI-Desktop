using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QiDesk {
public sealed class WeatherSnapshot {
    public string City,Condition;
    public double Temperature,High,Low,Wind;
    public int Humidity,Code;
    public DateTime FetchedAt;
}
public static class WeatherService {
    static readonly Dictionary<string,WeatherSnapshot> cache=new Dictionary<string,WeatherSnapshot>(StringComparer.OrdinalIgnoreCase);
    static readonly object gate=new object();
    static Dictionary<string,object> Object(object value){var result=value as Dictionary<string,object>;if(result==null)throw new InvalidOperationException("天气服务返回的数据不完整。");return result;}
    static double Number(Dictionary<string,object> data,string key){if(!data.ContainsKey(key)||data[key]==null)throw new InvalidOperationException("天气数据缺少 "+key+"。");return Convert.ToDouble(data[key],CultureInfo.InvariantCulture);}
    static double First(Dictionary<string,object> data,string key){var values=data.ContainsKey(key)?data[key] as ArrayList:null;if(values==null||values.Count==0||values[0]==null)throw new InvalidOperationException("天气数据缺少 "+key+"。");return Convert.ToDouble(values[0],CultureInfo.InvariantCulture);}
    public static string Condition(int code){if(code==0)return "晴朗";if(code<=2)return "晴间多云";if(code==3)return "多云";if(code==45||code==48)return "有雾";if(code>=51&&code<=57)return "毛毛雨";if(code>=61&&code<=67)return "下雨";if(code>=71&&code<=77)return "降雪";if(code>=80&&code<=82)return "阵雨";if(code>=85&&code<=86)return "阵雪";if(code>=95)return "雷雨";return "天气变化中";}
    public static async Task<WeatherSnapshot> LoadAsync(string city,bool force=false){
        city=(city??"").Trim();if(city.Length<2||city.Length>80)throw new ArgumentException("请在组件设置中填写城市名称。", "city");
        lock(gate){WeatherSnapshot old;if(!force&&cache.TryGetValue(city,out old)&&DateTime.UtcNow-old.FetchedAt<TimeSpan.FromMinutes(20))return old;}
        ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
        using(var client=new HttpClient(){Timeout=TimeSpan.FromSeconds(12)}){
            string geoUrl="https://geocoding-api.open-meteo.com/v1/search?name="+Uri.EscapeDataString(city)+"&count=1&language=zh";
            var geo=Store.Json.Deserialize<Dictionary<string,object>>(await client.GetStringAsync(geoUrl));var locations=geo.ContainsKey("results")?geo["results"] as ArrayList:null;
            if(locations==null||locations.Count==0)throw new InvalidOperationException("没有找到这个城市，请尝试“城市, 国家”。");
            var location=Object(locations[0]);double latitude=Number(location,"latitude"),longitude=Number(location,"longitude");
            string url="https://api.open-meteo.com/v1/forecast?latitude="+latitude.ToString(CultureInfo.InvariantCulture)+"&longitude="+longitude.ToString(CultureInfo.InvariantCulture)+"&current=temperature_2m,weather_code,relative_humidity_2m,wind_speed_10m&daily=temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=1";
            var forecast=Store.Json.Deserialize<Dictionary<string,object>>(await client.GetStringAsync(url));var current=Object(forecast["current"]);var daily=Object(forecast["daily"]);int code=(int)Number(current,"weather_code");
            var result=new WeatherSnapshot(){City=Convert.ToString(location["name"]),Condition=Condition(code),Temperature=Number(current,"temperature_2m"),High=First(daily,"temperature_2m_max"),Low=First(daily,"temperature_2m_min"),Wind=Number(current,"wind_speed_10m"),Humidity=(int)Number(current,"relative_humidity_2m"),Code=code,FetchedAt=DateTime.UtcNow};
            lock(gate)cache[city]=result;return result;
        }
    }
}
public partial class WidgetWindow {
    TextBlock weatherCity,weatherTemperature,weatherCondition,weatherDetails,weatherStatus;
    bool weatherLoading;DateTime nextWeatherAttempt;
    void BuildWeather(){
        weatherCity=Ui.Text("",13,Ui.DesktopMuted);weatherCity.Margin=new Thickness(0,5,0,0);body.Children.Add(weatherCity);
        var row=new DockPanel(){Margin=new Thickness(0,8,0,2)};weatherCondition=Ui.Text("",19,Ui.DesktopInk);weatherCondition.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(weatherCondition,Dock.Right);row.Children.Add(weatherCondition);
        weatherTemperature=Ui.Text("--°",48,Ui.DesktopInk);weatherTemperature.FontWeight=FontWeights.SemiBold;row.Children.Add(weatherTemperature);body.Children.Add(row);
        weatherDetails=Ui.Text("",12,Ui.DesktopMuted);body.Children.Add(weatherDetails);
        var footer=new DockPanel(){Margin=new Thickness(0,12,0,0)};var retry=Ui.Button("刷新",delegate{RefreshWeather(true);});retry.Padding=new Thickness(8,3,8,3);DockPanel.SetDock(retry,Dock.Right);footer.Children.Add(retry);weatherStatus=Ui.Text("Open-Meteo · 等待更新",10,Ui.DesktopMuted);weatherStatus.VerticalAlignment=VerticalAlignment.Center;footer.Children.Add(weatherStatus);body.Children.Add(footer);
        RefreshWeather(false);
    }
    async void RefreshWeather(bool force){
        if(weatherLoading||!IsVisible)return;if(!force&&DateTime.UtcNow<nextWeatherAttempt)return;weatherLoading=true;nextWeatherAttempt=DateTime.UtcNow.AddMinutes(3);weatherStatus.Text="正在获取天气…";
        try{var result=await WeatherService.LoadAsync(Widget.Text,force);if(weatherStatus==null||!IsLoaded)return;weatherCity.Text=result.City;weatherTemperature.Text=Math.Round(result.Temperature).ToString(CultureInfo.InvariantCulture)+"°";weatherCondition.Text=result.Condition;weatherDetails.Text="最高 "+Math.Round(result.High)+"° · 最低 "+Math.Round(result.Low)+"°\n湿度 "+result.Humidity+"% · 风速 "+Math.Round(result.Wind)+" km/h";weatherStatus.Text="Open-Meteo · "+DateTime.Now.ToString("HH:mm")+" 更新";nextWeatherAttempt=DateTime.UtcNow.AddMinutes(20);}
        catch(Exception ex){if(weatherStatus!=null)weatherStatus.Text="天气不可用 · "+ex.Message;}
        finally{weatherLoading=false;}
    }
}
}
