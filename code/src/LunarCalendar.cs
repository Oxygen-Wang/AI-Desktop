using System;
using System.Collections.Generic;
using System.Globalization;

namespace QiDesk {
// 中国农历、二十四节气与常用节日。数据覆盖 1900-2100，超出范围返回 null 由界面回退为公历。
public static class LunarCalendar {
    static readonly int[] YearInfo = {
        0x04bd8,0x04ae0,0x0a570,0x054d5,0x0d260,0x0d950,0x16554,0x056a0,0x09ad0,0x055d2,
        0x04ae0,0x0a5b6,0x0a4d0,0x0d250,0x1d255,0x0b540,0x0d6a0,0x0ada2,0x095b0,0x14977,
        0x04970,0x0a4b0,0x0b4b5,0x06a50,0x06d40,0x1ab54,0x02b60,0x09570,0x052f2,0x04970,
        0x06566,0x0d4a0,0x0ea50,0x06e95,0x05ad0,0x02b60,0x186e3,0x092e0,0x1c8d7,0x0c950,
        0x0d4a0,0x1d8a6,0x0b550,0x056a0,0x1a5b4,0x025d0,0x092d0,0x0d2b2,0x0a950,0x0b557,
        0x06ca0,0x0b550,0x15355,0x04da0,0x0a5b0,0x14573,0x052b0,0x0a9a8,0x0e950,0x06aa0,
        0x0aea6,0x0ab50,0x04b60,0x0aae4,0x0a570,0x05260,0x0f263,0x0d950,0x05b57,0x056a0,
        0x096d0,0x04dd5,0x04ad0,0x0a4d0,0x0d4d4,0x0d250,0x0d558,0x0b540,0x0b6a0,0x195a6,
        0x095b0,0x049b0,0x0a974,0x0a4b0,0x0b27a,0x06a50,0x06d40,0x0af46,0x0ab60,0x09570,
        0x04af5,0x04970,0x064b0,0x074a3,0x0ea50,0x06b58,0x055c0,0x0ab60,0x096d5,0x092e0,
        0x0c960,0x0d954,0x0d4a0,0x0da50,0x07552,0x056a0,0x0abb7,0x025d0,0x092d0,0x0cab5,
        0x0a950,0x0b4a0,0x0baa4,0x0ad50,0x055d9,0x04ba0,0x0a5b0,0x15176,0x052b0,0x0a930,
        0x07954,0x06aa0,0x0ad50,0x05b52,0x04b60,0x0a6e6,0x0a4e0,0x0d260,0x0ea65,0x0d530,
        0x05aa0,0x076a3,0x096d0,0x04afb,0x04ad0,0x0a4d0,0x1d0b6,0x0d250,0x0d520,0x0dd45,
        0x0b5a0,0x056d0,0x055b2,0x049b0,0x0a577,0x0a4b0,0x0aa50,0x1b255,0x06d20,0x0ada0,
        0x14b63,0x09370,0x049f8,0x04970,0x064b0,0x168a6,0x0ea50,0x06b20,0x1a6c4,0x0aae0,
        0x0a2e0,0x0d2e3,0x0c960,0x0d557,0x0d4a0,0x0da50,0x05d55,0x056a0,0x0a6d0,0x055d4,
        0x052d0,0x0a9b8,0x0a950,0x0b4a0,0x0b6a6,0x0ad50,0x055a0,0x0aba4,0x0a5b0,0x052b0,
        0x0b273,0x06930,0x07337,0x06aa0,0x0ad50,0x14b55,0x04b60,0x0a570,0x054e4,0x0d160,
        0x0e968,0x0d520,0x0daa0,0x16aa6,0x056d0,0x04ae0,0x0a9d4,0x0a2d0,0x0d150,0x0f252,
        0x0d520 };
    static readonly int[] TermMinutes = {0,21208,42467,63836,85337,107014,128867,150921,173149,195551,218072,240693,263343,285989,308563,331033,353350,375494,397447,419210,440795,462224,483532,504758};
    static readonly string[] MonthNames = {"正","二","三","四","五","六","七","八","九","十","冬","腊"};
    static readonly string[] DayNames = {"初一","初二","初三","初四","初五","初六","初七","初八","初九","初十","十一","十二","十三","十四","十五","十六","十七","十八","十九","二十","廿一","廿二","廿三","廿四","廿五","廿六","廿七","廿八","廿九","三十"};
    static readonly string[] Zodiacs = {"鼠","牛","虎","兔","龙","蛇","马","羊","猴","鸡","狗","猪"};
    static readonly string[] Gan = {"甲","乙","丙","丁","戊","己","庚","辛","壬","癸"};
    static readonly string[] Zhi = {"子","丑","寅","卯","辰","巳","午","未","申","酉","戌","亥"};
    static readonly string[] TermNames = {"小寒","大寒","立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"};
    static readonly Dictionary<string,string> SolarFestivals = new Dictionary<string,string> {
        {"01-01","元旦"},{"02-14","情人节"},{"03-08","妇女节"},{"03-12","植树节"},{"04-01","愚人节"},
        {"05-01","劳动节"},{"05-04","青年节"},{"06-01","儿童节"},{"07-01","建党节"},{"08-01","建军节"},
        {"09-10","教师节"},{"10-01","国庆节"},{"12-25","圣诞节"} };
    static readonly DateTime BaseDate = new DateTime(1900,1,31);

    public sealed class LunarDay {
        public int Year {get;set;}
        public int Month {get;set;}
        public int Day {get;set;}
        public bool Leap {get;set;}
        public string MonthName {get;set;}
        public string DayName {get;set;}
        public string Zodiac {get;set;}
        public string GanZhi {get;set;}
        public string Text {get{return "农历"+(Leap?"闰":"")+MonthName+"月"+DayName;}}
    }

    static int LeapMonth(int year){return YearInfo[year-1900]&0xf;}
    static int LeapDays(int year){return LeapMonth(year)==0?0:((YearInfo[year-1900]&0x10000)==0?29:30);}
    static int MonthDays(int year,int month){return (YearInfo[year-1900]&(0x10000>>month))==0?29:30;}
    static int YearDays(int year){int sum=348;for(int i=0x8000;i>0x8;i>>=1)sum+=(YearInfo[year-1900]&i)!=0?1:0;return sum+LeapDays(year);}

    public static LunarDay FromSolar(DateTime date){
        if(date.Year<1900||date.Year>2100)return null;
        int offset=(int)(date.Date-BaseDate).TotalDays;
        if(offset<0)return null;
        int year=1900,temp=0;
        for(;year<2101&&offset>0;year++){temp=YearDays(year);offset-=temp;}
        if(offset<0){offset+=temp;year--;}
        int leap=LeapMonth(year);bool isLeap=false;int month=1;
        for(month=1;month<13&&offset>0;month++){
            if(leap>0&&month==leap+1&&!isLeap){month--;isLeap=true;temp=LeapDays(year);}
            else temp=MonthDays(year,month);
            if(isLeap&&month==leap+1)isLeap=false;
            offset-=temp;
        }
        if(offset==0&&leap>0&&month==leap+1){if(isLeap)isLeap=false;else{isLeap=true;month--;}}
        if(offset<0){offset+=temp;month--;}
        int day=offset+1;
        var result=new LunarDay();
        result.Year=year;result.Month=month;result.Day=day;result.Leap=isLeap;
        result.MonthName=MonthNames[month-1];
        result.DayName=DayNames[day-1];
        result.Zodiac=Zodiacs[(year-4)%12];
        result.GanZhi=Gan[(year-4)%10]+Zhi[(year-4)%12];
        return result;
    }

    public static string ZodiacOf(int year){return Zodiacs[((year-4)%12+12)%12];}

    // 返回该公历日对应的节气名称，没有则为空。
    public static string Term(DateTime date){
        int index=(date.Month-1)*2;
        for(int n=index;n<index+2&&n<24;n++){if(TermDay(date.Year,n)==date.Day)return TermNames[n];}
        return "";
    }
    static int TermDay(int year,int n){
        double ms=31556925974.7*(year-1900)+TermMinutes[n]*60000.0;
        return new DateTime(1900,1,6,2,5,0,DateTimeKind.Utc).AddMilliseconds(ms).Day;
    }

    public static string SolarFestival(DateTime date){
        string value;
        return SolarFestivals.TryGetValue(date.ToString("MM-dd",CultureInfo.InvariantCulture),out value)?value:"";
    }

    public static string LunarFestival(LunarDay lunar){
        if(lunar==null)return "";
        if(lunar.Leap)return "";
        if(lunar.Month==1&&lunar.Day==1)return "春节";
        if(lunar.Month==1&&lunar.Day==15)return "元宵节";
        if(lunar.Month==5&&lunar.Day==5)return "端午节";
        if(lunar.Month==7&&lunar.Day==7)return "七夕";
        if(lunar.Month==7&&lunar.Day==15)return "中元节";
        if(lunar.Month==8&&lunar.Day==15)return "中秋节";
        if(lunar.Month==9&&lunar.Day==9)return "重阳节";
        if(lunar.Month==12&&lunar.Day==8)return "腊八节";
        if(lunar.Month==12&&lunar.Day==MonthDays(lunar.Year,12))return "除夕";
        return "";
    }

    // 日历格子里的小字：优先节气与节日，其次农历日，初一显示月份。
    public static string CellLabel(DateTime date,LunarDay lunar){
        string festival=SolarFestival(date);
        if(festival.Length>0)return festival;
        string term=Term(date);
        if(term.Length>0)return term;
        if(lunar==null)return "";
        string lunarFestival=LunarFestival(lunar);
        if(lunarFestival.Length>0)return lunarFestival;
        if(lunar.Day==1)return lunar.MonthName+"月";
        return lunar.DayName;
    }

    // 时钟/日历共用的农历日期文字，例如“农历九月初六”。
    public static string Text(DateTime date){
        var lunar=FromSolar(date);
        return lunar==null?"":lunar.Text;
    }

    public static string Format(DateTime date){
        var lunar=FromSolar(date);
        string text=date.ToString("M 月 d 日 · dddd",CultureInfo.GetCultureInfo("zh-CN"));
        return lunar==null?text:text+" · "+(lunar.Day==1?lunar.MonthName+"月":lunar.DayName);
    }
}
}
