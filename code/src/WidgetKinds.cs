using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace QiDesk {
public static class Calc {
    public static string Evaluate(string expression){
        if(String.IsNullOrWhiteSpace(expression))return null;
        try{
            var parser=new CalcParser(expression.Replace("×","*").Replace("÷","/").Replace("−","-"));
            double value=parser.Parse();
            if(Double.IsNaN(value)||Double.IsInfinity(value))return null;
            return value.ToString("0.########",CultureInfo.InvariantCulture);
        }catch{return null;}
    }
    sealed class CalcParser {
        readonly string text;int index;
        public CalcParser(string value){text=value;}
        public double Parse(){double value=Expression();Skip();if(index<text.Length)throw new FormatException();return value;}
        double Expression(){double value=Term();while(true){Skip();if(index<text.Length&&(text[index]=='+'||text[index]=='-')){char op=text[index++];double right=Term();value=op=='+'?value+right:value-right;}else return value;}}
        double Term(){double value=Factor();while(true){Skip();if(index<text.Length&&(text[index]=='*'||text[index]=='/')){char op=text[index++];double right=Factor();value=op=='*'?value*right:value/right;}else return value;}}
        double Factor(){Skip();if(index<text.Length&&(text[index]=='+'||text[index]=='-')){char op=text[index++];double value=Factor();return op=='-'?-value:value;}if(index<text.Length&&text[index]=='('){index++;double value=Expression();Skip();if(index>=text.Length||text[index]!=')')throw new FormatException();index++;return value;}return Number();}
        double Number(){Skip();int start=index;while(index<text.Length&&(Char.IsDigit(text[index])||text[index]=='.'))index++;if(start==index)throw new FormatException();return Double.Parse(text.Substring(start,index-start),CultureInfo.InvariantCulture);}
        void Skip(){while(index<text.Length&&Char.IsWhiteSpace(text[index]))index++;}
    }
}
public static class SystemMetrics {
    [StructLayout(LayoutKind.Sequential)] struct MemoryStatus {public uint Length;public uint Load;public ulong TotalPhysical;public ulong AvailablePhysical;public ulong TotalPageFile;public ulong AvailablePageFile;public ulong TotalVirtual;public ulong AvailableVirtual;public ulong AvailableExtendedVirtual;}
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetSystemTimes(out long idle,out long kernel,out long user);
    [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
    public static bool Cpu(out long idle,out long kernel,out long user){return GetSystemTimes(out idle,out kernel,out user);}
    public static bool Memory(out int percent,out double usedGb,out double totalGb){
        var status=new MemoryStatus();status.Length=(uint)Marshal.SizeOf(typeof(MemoryStatus));
        if(!GlobalMemoryStatusEx(ref status)){percent=0;usedGb=0;totalGb=0;return false;}
        totalGb=status.TotalPhysical/1073741824.0;usedGb=(status.TotalPhysical-status.AvailablePhysical)/1073741824.0;percent=(int)status.Load;return true;
    }
    public static bool Disk(string drive,out double freeGb,out double totalGb,out string label){
        freeGb=0;totalGb=0;label="磁盘";
        try{
            string path=String.IsNullOrWhiteSpace(drive)?Path.GetPathRoot(Environment.SystemDirectory):drive.Trim();
            if(path.Length==0)return false;
            if(path.Length==1)path+=":";
            if(!path.EndsWith("\\"))path+="\\";
            var info=new DriveInfo(path);
            if(!info.IsReady)return false;
            totalGb=info.TotalSize/1073741824.0;freeGb=info.AvailableFreeSpace/1073741824.0;label=info.Name.TrimEnd('\\')+" 磁盘";return true;
        }catch{return false;}
    }
    public static string Uptime(){
        try{var span=TimeSpan.FromMilliseconds(GetTickCount64());return span.TotalDays>=1?(int)span.TotalDays+" 天 "+span.Hours+" 小时":span.Hours+" 小时 "+span.Minutes+" 分";}catch{return "未知";}
    }
}
public static partial class WidgetPlugins {
    public static int HabitStreak(IEnumerable<string> days,DateTime today){
        var set=new HashSet<string>(days??new string[0]);
        var cursor=today;
        if(!set.Contains(cursor.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture)))cursor=cursor.AddDays(-1);
        int streak=0;
        while(set.Contains(cursor.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture))){streak++;cursor=cursor.AddDays(-1);}
        return streak;
    }
    public static string[] Pictures(string folder,int max){
        try{
            if(String.IsNullOrWhiteSpace(folder)||!Directory.Exists(folder))return new string[0];
            return Directory.GetFiles(folder).Where(Thumbnails.IsPicture).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).Take(max).ToArray();
        }catch{return new string[0];}
    }
}
public partial class WidgetWindow {
    TextBlock calcDisplay,calcPreview;string calcText="";
    TextBlock habitValue,habitDetail;Button habitButton;
    ProgressBar systemCpuBar,systemMemBar;TextBlock systemCpuText,systemMemText,systemDiskText,systemUptimeText;long lastCpuIdle,lastCpuKernel,lastCpuUser;bool cpuReady;
    Image photoView;TextBlock photoStatus;string[] photoFiles;int photoIndex;DateTime nextSlide;
    TextBlock musicTitle,musicArtist,musicStatus;DateTime musicNext;
    bool BuildKind(string kind){
        if(kind=="calc"){BuildCalculator();return true;}
        if(kind=="habit"){BuildHabit();return true;}
        if(kind=="system"){BuildSystem();return true;}
        if(kind=="photos"){BuildPhotos();return true;}
        if(kind=="music"){BuildMusic();return true;}
        return false;
    }
    bool UpdateKind(string kind){
        if(kind=="system"){UpdateSystem();return true;}
        if(kind=="photos"){UpdatePhotos();return true;}
        if(kind=="music"){UpdateMusic();return true;}
        if(kind=="calc"||kind=="habit")return true;
        return false;
    }
    void BuildCalculator(){
        calcText="";
        calcDisplay=Ui.Text("0",30,Ui.DesktopInk);calcDisplay.TextAlignment=TextAlignment.Right;calcDisplay.TextWrapping=TextWrapping.NoWrap;calcDisplay.TextTrimming=TextTrimming.CharacterEllipsis;calcDisplay.Margin=new Thickness(0,2,0,2);body.Children.Add(calcDisplay);
        calcPreview=Ui.Text("",11,Ui.DesktopMuted);calcPreview.TextAlignment=TextAlignment.Right;body.Children.Add(calcPreview);
        var grid=new UniformGrid(){Columns=4,Margin=new Thickness(0,6,0,0)};
        foreach(string key in new[]{"C","(",")","÷","7","8","9","×","4","5","6","−","1","2","3","+","0",".","⌫","="}){string pressed=key;var button=Ui.Button(pressed,delegate{CalcKey(pressed);});button.Margin=new Thickness(3);button.Padding=new Thickness(0);button.MinHeight=34;button.FontSize=pressed.Length>1?13:17;grid.Children.Add(button);}
        body.Children.Add(grid);
    }
    void CalcKey(string key){
        if(key=="C")calcText="";
        else if(key=="⌫"){if(calcText.Length>0)calcText=calcText.Substring(0,calcText.Length-1);}
        else if(key=="="){string result=Calc.Evaluate(calcText);if(result!=null)calcText=result;}
        else if(calcText.Length<80)calcText+=key;
        calcDisplay.Text=String.IsNullOrEmpty(calcText)?"0":calcText;
        string preview=Calc.Evaluate(calcText);
        calcPreview.Text=(calcText.Length>0&&preview!=null&&preview!=calcText)?"= "+preview:" ";
    }
    void BuildHabit(){
        habitValue=Ui.Text("0",42,Ui.DesktopInk);habitValue.TextAlignment=TextAlignment.Center;habitValue.Margin=new Thickness(0,8,0,0);body.Children.Add(habitValue);
        habitDetail=Ui.Text("",11,Ui.DesktopMuted);habitDetail.TextAlignment=TextAlignment.Center;body.Children.Add(habitDetail);
        habitButton=Ui.Button("今天打卡",delegate{ToggleHabit();});habitButton.HorizontalAlignment=HorizontalAlignment.Center;body.Children.Add(habitButton);
        UpdateHabit();
    }
    void ToggleHabit(){
        controller.Store.Checkpoint();
        string today=DateTime.Today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        if(Widget.Checkins.Contains(today))Widget.Checkins.Remove(today);else Widget.Checkins.Add(today);
        controller.SaveAndRefresh(false);UpdateHabit();
    }
    void UpdateHabit(){
        if(habitValue==null)return;
        string today=DateTime.Today.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        bool done=Widget.Checkins.Contains(today);
        habitValue.Text=WidgetPlugins.HabitStreak(Widget.Checkins,DateTime.Today).ToString(CultureInfo.InvariantCulture);
        habitDetail.Text="连续天数 · 累计 "+Widget.Checkins.Count+" 天";
        habitButton.Content=done?"今天已打卡 ✓":"今天打卡";
    }
    ProgressBar Meter(){return new ProgressBar(){Height=6,Maximum=100,Value=0,Margin=new Thickness(0,0,0,10),Foreground=Ui.Brush(Widget.Color),Background=new SolidColorBrush(Color.FromArgb(46,255,255,255)),BorderThickness=new Thickness(0)};}
    void BuildSystem(){
        systemCpuText=Ui.Text("CPU  --",14,Ui.DesktopInk);systemCpuText.Margin=new Thickness(0,2,0,3);body.Children.Add(systemCpuText);
        systemCpuBar=Meter();body.Children.Add(systemCpuBar);
        systemMemText=Ui.Text("内存  --",14,Ui.DesktopInk);systemMemText.Margin=new Thickness(0,2,0,3);body.Children.Add(systemMemText);
        systemMemBar=Meter();body.Children.Add(systemMemBar);
        systemDiskText=Ui.Text("磁盘  --",12,Ui.DesktopMuted);body.Children.Add(systemDiskText);
        systemUptimeText=Ui.Text("",11,Ui.DesktopMuted);body.Children.Add(systemUptimeText);
        cpuReady=false;UpdateSystem();
    }
    void UpdateSystem(){
        if(systemCpuText==null)return;
        long idle,kernel,user;
        if(SystemMetrics.Cpu(out idle,out kernel,out user)){
            if(cpuReady){
                long total=(kernel-lastCpuKernel)+(user-lastCpuUser),busy=total-(idle-lastCpuIdle);
                int percent=total>0?(int)Math.Round(100.0*busy/total):0;percent=Math.Max(0,Math.Min(100,percent));
                systemCpuBar.Value=percent;systemCpuText.Text="CPU  "+percent+"%";
            }
            cpuReady=true;lastCpuIdle=idle;lastCpuKernel=kernel;lastCpuUser=user;
        }else systemCpuText.Text="CPU  不可用";
        int memoryPercent;double usedGb,totalGb;
        if(SystemMetrics.Memory(out memoryPercent,out usedGb,out totalGb)){systemMemBar.Value=memoryPercent;systemMemText.Text="内存  "+memoryPercent+"% · "+usedGb.ToString("0.0")+" / "+totalGb.ToString("0.0")+" GB";}else systemMemText.Text="内存  不可用";
        double freeGb,diskTotal;string diskLabel;
        if(SystemMetrics.Disk(Widget.Text,out freeGb,out diskTotal,out diskLabel))systemDiskText.Text=diskLabel+"  可用 "+freeGb.ToString("0.0")+" / "+diskTotal.ToString("0.0")+" GB";else systemDiskText.Text="磁盘  不可用";
        systemUptimeText.Text="已运行 "+SystemMetrics.Uptime();
    }
    void BuildPhotos(){
        photoView=new Image(){Stretch=Stretch.Uniform,Height=Math.Max(90,Widget.Height-118),Margin=new Thickness(0,4,0,8)};RenderOptions.SetBitmapScalingMode(photoView,BitmapScalingMode.HighQuality);body.Children.Add(photoView);
        photoStatus=Ui.Text("",11,Ui.DesktopMuted);photoStatus.TextAlignment=TextAlignment.Center;body.Children.Add(photoStatus);
        var controls=new WrapPanel(){HorizontalAlignment=HorizontalAlignment.Center};
        controls.Children.Add(Ui.Button("‹",delegate{StepPhoto(-1);}));
        controls.Children.Add(Ui.Button("›",delegate{StepPhoto(1);}));
        controls.Children.Add(Ui.Button("选择文件夹",delegate{controller.Manager.EditWidget(Widget);}));
        body.Children.Add(controls);
        ReloadPhotos();
    }
    void ReloadPhotos(){photoFiles=WidgetPlugins.Pictures(Widget.Text,200);photoIndex=0;nextSlide=DateTime.UtcNow.AddSeconds(8);ShowPhoto();}
    void StepPhoto(int delta){if(photoFiles==null||photoFiles.Length==0){ReloadPhotos();return;}photoIndex=((photoIndex+delta)%photoFiles.Length+photoFiles.Length)%photoFiles.Length;nextSlide=DateTime.UtcNow.AddSeconds(8);ShowPhoto();}
    void ShowPhoto(){
        if(photoView==null)return;
        photoView.Height=Math.Max(90,Widget.Height-118);
        if(photoFiles==null||photoFiles.Length==0){photoView.Source=null;photoStatus.Text=String.IsNullOrWhiteSpace(Widget.Text)?"在设置中选择图片文件夹":"这个文件夹里没有图片";return;}
        photoIndex=((photoIndex%photoFiles.Length)+photoFiles.Length)%photoFiles.Length;
        photoStatus.Text=(photoIndex+1)+" / "+photoFiles.Length+" · "+Path.GetFileName(photoFiles[photoIndex]);
        Thumbnails.Bind(photoView,photoFiles[photoIndex],Math.Max(240,Math.Max(photoView.ActualWidth,photoView.ActualHeight)));
    }
    void UpdatePhotos(){
        if(photoView==null)return;
        photoView.Height=Math.Max(90,Widget.Height-118);
        if(photoFiles!=null&&photoFiles.Length>1&&DateTime.UtcNow>=nextSlide){photoIndex=(photoIndex+1)%photoFiles.Length;nextSlide=DateTime.UtcNow.AddSeconds(8);ShowPhoto();}
    }
    void BuildMusic(){
        var note=Ui.Text("♫",26,Ui.Brush(Widget.Color));note.TextAlignment=TextAlignment.Center;note.Margin=new Thickness(0,2,0,0);body.Children.Add(note);
        musicTitle=Ui.Text("",15,Ui.DesktopInk);musicTitle.TextAlignment=TextAlignment.Center;musicTitle.FontWeight=FontWeights.SemiBold;musicTitle.TextWrapping=TextWrapping.Wrap;musicTitle.TextTrimming=TextTrimming.CharacterEllipsis;musicTitle.MaxHeight=43;musicTitle.Margin=new Thickness(0,2,0,2);body.Children.Add(musicTitle);
        musicArtist=Ui.Text("",11,Ui.DesktopMuted);musicArtist.TextAlignment=TextAlignment.Center;musicArtist.TextTrimming=TextTrimming.CharacterEllipsis;musicArtist.Margin=new Thickness(0,0,0,8);body.Children.Add(musicArtist);
        var controls=new WrapPanel(){HorizontalAlignment=HorizontalAlignment.Center};
        controls.Children.Add(Ui.Button("上一首",delegate{NetEaseMusic.Previous();musicNext=DateTime.MinValue;}));
        controls.Children.Add(Ui.Button("播放 / 暂停",delegate{NetEaseMusic.PlayPause();musicNext=DateTime.MinValue;}));
        controls.Children.Add(Ui.Button("下一首",delegate{NetEaseMusic.Next();musicNext=DateTime.MinValue;}));
        body.Children.Add(controls);
        musicStatus=Ui.Text("",10,Ui.DesktopMuted);musicStatus.TextAlignment=TextAlignment.Center;musicStatus.Margin=new Thickness(0,4,0,0);body.Children.Add(musicStatus);
        musicNext=DateTime.MinValue;UpdateMusic();
    }
    void UpdateMusic(){
        if(musicTitle==null)return;
        if(DateTime.UtcNow<musicNext)return;
        musicNext=DateTime.UtcNow.AddSeconds(2);
        var info=NetEaseMusic.Now();
        if(!info.Running){musicTitle.Text="未检测到网易云音乐";musicArtist.Text="打开客户端并播放歌曲";musicStatus.Text="点下方按钮可控制当前播放器";return;}
        if(!info.Playing){musicTitle.Text="网易云音乐";musicArtist.Text="未在播放";musicStatus.Text="按播放 / 暂停开始";return;}
        musicTitle.Text=info.Title;
        musicArtist.Text=String.IsNullOrEmpty(info.Artist)?"网易云音乐":info.Artist;
        musicStatus.Text="正在播放 · 网易云音乐";
    }
}
}
