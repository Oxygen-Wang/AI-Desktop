using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace QiDesk {
public class DesktopWidget : Group {public string PluginId {get;set;}public bool ShowSeconds {get;set;}public string TimeZone {get;set;}public string Text {get;set;}public string TargetDate {get;set;}public int FocusMinutes {get;set;}public string FocusUntil {get;set;}public int FocusPausedSeconds {get;set;}public List<int> CheckedTasks {get;set;}public List<string> Checkins {get;set;}public Dictionary<string,string> TaskTimes {get;set;}public bool ShowLunar {get;set;}public bool ShowCalendarClock {get;set;}public bool WeekStartMonday {get;set;}public DesktopWidget(){PluginId="clock";Name="时钟";Width=280;Height=200;ShowSeconds=true;TimeZone="local";Text="";TargetDate="";FocusMinutes=25;FocusUntil="";CheckedTasks=new List<int>();Checkins=new List<string>();TaskTimes=new Dictionary<string,string>();ShowLunar=true;ShowCalendarClock=true;WeekStartMonday=true;}}
public class WidgetPlugin {
    public string Id {get;set;}public string Name {get;set;}public string Kind {get;set;}public string Description {get;set;}public string Text {get;set;}public string Accent {get;set;}public bool Enabled {get;set;}
    public WidgetPlugin(){Id="";Name="";Kind="note";Description="";Text="";Accent="#77BFA3";Enabled=true;}
}
public static partial class WidgetPlugins {
    public static WidgetPlugin[] Builtins(){return new[]{new WidgetPlugin(){Id="qidesk.search",Name="全盘搜索",Kind="search",Description="搜索 Everything 全部索引文件，支持名称、路径与搜索表达式",Accent="#81B9CB"},new WidgetPlugin(){Id="clock",Name="时钟",Kind="clock",Description="当前时间与日期，可选择时区"},new WidgetPlugin(){Id="calendar",Name="日历",Kind="calendar",Description="月视图、今天高亮、农历节气与内置时钟",Accent="#92AAE5"},new WidgetPlugin(){Id="qidesk.weather",Name="天气",Kind="weather",Description="按城市查看实时天气、温度与今日高低温",Text="北京",Accent="#77BCE3"},new WidgetPlugin(){Id="note",Name="文字卡片",Kind="note",Description="在桌面显示便签与提示"},new WidgetPlugin(){Id="countdown",Name="倒计时",Kind="countdown",Description="显示距离目标日期的天数",Accent="#D9AA75"},new WidgetPlugin(){Id="qidesk.tasks",Name="待办清单",Kind="tasks",Description="左键勾选任务，进度保存在桌面",Text="写下今天最重要的事\n完成一项小任务",Accent="#8CB5DE"},new WidgetPlugin(){Id="qidesk.focus",Name="番茄钟",Kind="focus",Description="开始、暂停或重置一段专注时间",Accent="#CF9AA5"},new WidgetPlugin(){Id="qidesk.choice",Name="随机选择器",Kind="choice",Description="抽签、骰子、今天吃什么，由你填写选项",Text="1\n2\n3\n4\n5\n6",Accent="#B8A1DA"},new WidgetPlugin(){Id="qidesk.calc",Name="计算器",Kind="calc",Description="桌面上随时算一算，支持加减乘除与括号",Accent="#7FB0D9"},new WidgetPlugin(){Id="qidesk.habit",Name="习惯打卡",Kind="habit",Description="每天打卡一次，显示连续与累计天数",Text="每天阅读",Accent="#8CC7A1"},new WidgetPlugin(){Id="qidesk.system",Name="系统状态",Kind="system",Description="查看 CPU、内存与磁盘占用和开机时长",Accent="#B9C0CB"},new WidgetPlugin(){Id="qidesk.photos",Name="照片轮播",Kind="photos",Description="循环播放文件夹里的图片",Accent="#D6A97E"},new WidgetPlugin(){Id="qidesk.music",Name="网易云音乐",Kind="music",Description="显示网易云音乐正在播放，并控制上一首、播放/暂停与下一首",Accent="#C20C0C"}};}
    public static IEnumerable<WidgetPlugin> All(State state){return Builtins().Concat(state.Plugins);}
    public static WidgetPlugin Find(State state,string id){return All(state).FirstOrDefault(p=>p.Id==id);}
    public static void ValidatePlugin(WidgetPlugin plugin){if(plugin==null||!System.Text.RegularExpressions.Regex.IsMatch(plugin.Id??"","^[a-z0-9][a-z0-9.-]{0,39}$")||String.IsNullOrWhiteSpace(plugin.Name)||plugin.Name.Length>80||!SupportedKinds.Contains(plugin.Kind)||plugin.Text.Length>2000||plugin.Description.Length>240)throw new InvalidDataException("插件需包含有效 id、名称和受支持的组件类型。");try{ColorConverter.ConvertFromString(plugin.Accent);}catch{throw new InvalidDataException("插件颜色无效。");}}
    public static WidgetPlugin Read(string file){var info=new FileInfo(file);if(info.Length>128*1024)throw new InvalidDataException("插件配置不能超过 128 KB。");return Parse(File.ReadAllText(file));}
    // 字段白名单与校验只有一份：设置页从文件读，MCP 从 JSON 文本读，都走这里。
    public static WidgetPlugin Parse(string json){
        var data=Store.Json.Deserialize<Dictionary<string,object>>(json);
        if(data==null)throw new InvalidDataException("插件配置必须是 JSON 对象。");
        string[] fields={"id","name","kind","description","text","accent"};
        if(data.Keys.Any(k=>!fields.Contains(k)))throw new InvalidDataException("插件配置包含不支持的字段。");
        Func<string,string> text=k=>data.ContainsKey(k)?Convert.ToString(data[k]):"";
        var plugin=new WidgetPlugin(){Id=text("id"),Name=text("name"),Kind=text("kind"),Description=text("description"),Text=text("text"),Accent=data.ContainsKey("accent")?text("accent"):"#77BFA3"};
        ValidatePlugin(plugin);return plugin;
    }
    public static void Import(State state,WidgetPlugin plugin){ValidatePlugin(plugin);if(Builtins().Any(p=>p.Id==plugin.Id))throw new InvalidDataException("插件 id 与内置组件重名。");if(state.Plugins.Any(p=>p.Id==plugin.Id))throw new InvalidDataException("同名插件已安装，请先移除已有配置。");if(state.Plugins.Count>=30)throw new InvalidDataException("最多安装 30 个自定义插件。");state.Plugins.Add(plugin);}
    public static void Validate(State state){if(state.Plugins.Count>30)throw new InvalidDataException("最多安装 30 个自定义插件。");var ids=new HashSet<string>();var widgetIds=new HashSet<string>(state.Groups.Select(g=>g.Id));foreach(var plugin in state.Plugins){ValidatePlugin(plugin);if(!ids.Add(plugin.Id)||Builtins().Any(p=>p.Id==plugin.Id))throw new InvalidDataException("插件 id 重复。");}if(state.Widgets.Count>30)throw new InvalidDataException("最多显示 30 个桌面组件。");foreach(var widget in state.Widgets){if(widget==null||String.IsNullOrEmpty(widget.Id)||!widgetIds.Add(widget.Id))throw new InvalidDataException("组件 id 重复。");widget.Items=widget.Items??new List<string>();widget.IgnoreRules=widget.IgnoreRules??new List<IgnoreRule>();widget.Width=Bound(widget.Width,220,800);widget.Height=Bound(widget.Height,MinimumHeight(Find(state,widget.PluginId)==null?"note":Find(state,widget.PluginId).Kind),900);widget.X=Bound(widget.X,-20000,20000);widget.Y=Bound(widget.Y,-20000,20000);widget.BackgroundOpacity=Bound(widget.BackgroundOpacity,0,1);widget.BorderOpacity=Bound(widget.BorderOpacity,0,1);widget.Page=Math.Max(0,Math.Min(state.Pages-1,widget.Page));ValidateExtra(widget);widget.TimeZone=widget.TimeZone??"local";widget.Text=widget.Text??"";widget.TargetDate=widget.TargetDate??"";if(widget.Text.Length>2000)throw new InvalidDataException("组件文字最多 2000 字符。");}}
    static double Bound(double x,double min,double max){return Double.IsNaN(x)||Double.IsInfinity(x)?min:Math.Max(min,Math.Min(max,x));}
    public static DesktopWidget Create(State state,string pluginId,IList<Rect> areas){var plugin=Find(state,pluginId);if(plugin==null||!plugin.Enabled)throw new InvalidDataException("插件不可用。");var widget=new DesktopWidget(){PluginId=pluginId,Name=plugin.Name,Color=plugin.Accent,Page=state.Page,Width=plugin.Kind=="search"?560:plugin.Kind=="calendar"?330:plugin.Kind=="weather"?320:plugin.Kind=="system"?300:plugin.Kind=="photos"?320:plugin.Kind=="calc"?260:plugin.Kind=="music"?300:280,Height=plugin.Kind=="search"?360:plugin.Kind=="calendar"?400:plugin.Kind=="tasks"?280:plugin.Kind=="weather"?245:plugin.Kind=="calc"?360:plugin.Kind=="system"?250:plugin.Kind=="photos"?240:plugin.Kind=="habit"?220:plugin.Kind=="music"?200:230,Text=plugin.Text,TargetDate=plugin.Kind=="countdown"?DateTime.Today.AddDays(7).ToString("yyyy-MM-dd"):""};var taken=state.Groups.Cast<Group>().Concat(state.Widgets).Where(g=>g.Page==state.Page).Select(g=>new Rect(g.X-8,g.Y-8,g.Width+16,g.Height+16)).ToList();Rect area=areas.Count==0?new Rect(0,0,1920,1040):areas[0];bool placed=false;for(double x=area.Left+20;x+widget.Width<=area.Right-12&&!placed;x+=widget.Width+20)for(double y=area.Top+30;y+widget.Height<=area.Bottom-12;y+=widget.Height+20){var rect=new Rect(x,y,widget.Width,widget.Height);if(taken.Any(r=>r.IntersectsWith(rect)))continue;widget.X=x;widget.Y=y;placed=true;break;}if(!placed){widget.X=area.Left+32;widget.Y=area.Top+32;}state.Widgets.Add(widget);return widget;}
    public static DateTime Time(DesktopWidget widget,DateTimeOffset now){try{return widget.TimeZone=="local"?now.LocalDateTime:TimeZoneInfo.ConvertTime(now,TimeZoneInfo.FindSystemTimeZoneById(widget.TimeZone)).DateTime;}catch{return now.LocalDateTime;}}
    public static DateTime[] Days(DateTime month){return Days(month,false);}
    public static DateTime[] Days(DateTime month,bool sundayFirst){var first=new DateTime(month.Year,month.Month,1);int offset=((int)first.DayOfWeek+(sundayFirst?0:6))%7;return Enumerable.Range(0,42).Select(i=>first.AddDays(i-offset)).ToArray();}
}
public partial class WidgetWindow : Window {
    public readonly DesktopWidget Widget;readonly Controller controller;readonly WidgetPlugin plugin;readonly Border frame;readonly StackPanel body;readonly TextBlock heading;readonly Thumb resize;readonly DispatcherTimer timer;DateTime month=DateTime.Today;bool moving;Point press;double startX,startY;bool updating;TextBlock time,date,calTitle,calClock,calSub;UniformGrid days;ScrollViewer calScroll;
    public bool Docked;
    public WidgetWindow(Controller owner,DesktopWidget widget){controller=owner;Widget=widget;plugin=WidgetPlugins.Find(owner.Store.Data,widget.PluginId);Title="栖桌 · "+widget.Name;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;FontFamily=new FontFamily("Microsoft YaHei UI");Foreground=Ui.DesktopInk;MinWidth=220;MinHeight=WidgetPlugins.MinimumHeight(plugin==null?"note":plugin.Kind);
        frame=new Border(){CornerRadius=new CornerRadius(22),BorderBrush=Ui.Brush(widget.Color),BorderThickness=new Thickness(1),Effect=Ui.Shadow(5,18,.23)};Content=frame;var root=new Grid();root.RowDefinitions.Add(new RowDefinition(){Height=new GridLength(42)});root.RowDefinitions.Add(new RowDefinition());frame.Child=root;
        var header=new DockPanel(){Margin=new Thickness(18,9,14,4),Background=Brushes.Transparent};var accentDot=new Border(){Width=7,Height=7,CornerRadius=new CornerRadius(4),Background=Ui.Brush(widget.Color),Margin=new Thickness(0,0,9,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(accentDot,Dock.Left);header.Children.Add(accentDot);heading=Ui.Text(widget.Name,15,Ui.DesktopInk);heading.FontWeight=FontWeights.SemiBold;heading.Margin=new Thickness(0);heading.VerticalAlignment=VerticalAlignment.Center;header.Children.Add(heading);root.Children.Add(header);
        header.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){DependencyObject part=e.OriginalSource as DependencyObject;while(part!=null&&part!=header){if(part is ButtonBase||part is TextBox)return;part=part is Visual?VisualTreeHelper.GetParent(part):LogicalTreeHelper.GetParent(part);}if(e.ClickCount==2){StartInlineRename(header);e.Handled=true;return;}if(widget.Locked)return;controller.Store.Checkpoint();moving=true;press=PointToScreen(e.GetPosition(this));startX=Left;startY=Top;header.CaptureMouse();e.Handled=true;};header.MouseMove+=delegate(object s,MouseEventArgs e){if(!moving)return;Point p=PointToScreen(e.GetPosition(this));double scale=Shell.WindowScale(this);Left=startX+(p.X-press.X)/scale;Top=startY+(p.Y-press.Y)/scale;};header.MouseLeftButtonUp+=delegate{if(!moving)return;moving=false;header.ReleaseMouseCapture();SaveBounds();};
        body=new StackPanel(){Margin=new Thickness(18,0,18,18)};Grid.SetRow(body,1);root.Children.Add(body);resize=AddResizeHandle(root,HorizontalAlignment.Right,14,Cursors.SizeNWSE,1);AddResizeHandle(root,HorizontalAlignment.Left,14,Cursors.SizeNESW,-1);AddResizeHandle(root,HorizontalAlignment.Center,64,Cursors.SizeNS,0);
        var menu=new ContextMenu();menu.Items.Add(Ui.Menu("设置组件",delegate{controller.Manager.EditWidget(widget);}));menu.Items.Add(Ui.Menu("锁定 / 解锁",delegate{controller.Store.Checkpoint();widget.Locked=!widget.Locked;controller.SaveAndRefresh(false);}));menu.Items.Add(Ui.Menu("透明背景 / 恢复背景",delegate{controller.Store.Checkpoint();widget.BackgroundOpacity=widget.BackgroundOpacity<.05?.76:0;controller.SaveAndRefresh(false);}));menu.Items.Add(Ui.Menu("移除组件",delegate{controller.Store.Checkpoint();controller.Store.Data.Widgets.Remove(widget);controller.SaveAndRefresh();}));ContextMenu=menu;
        Build();Refresh();Loaded+=delegate{UpdateTime();};Deactivated+=delegate{if(Docked)Shell.EndInlineInput(this);};timer=new DispatcherTimer(){Interval=TimeSpan.FromSeconds(1)};timer.Tick+=delegate{UpdateTime();};timer.Start();Closed+=delegate{timer.Stop();};
    }
    Thumb AddResizeHandle(Grid root,HorizontalAlignment alignment,double width,Cursor cursor,int side){var handle=new Thumb(){Width=width,Height=side==0?8:14,HorizontalAlignment=alignment,VerticalAlignment=VerticalAlignment.Bottom,Cursor=cursor,Background=Ui.Brush(Widget.Color),Opacity=.08};Grid.SetRow(handle,1);root.Children.Add(handle);handle.MouseEnter+=delegate{handle.Opacity=.5;};handle.MouseLeave+=delegate{handle.Opacity=.08;};handle.DragStarted+=delegate{if(!Widget.Locked)controller.Store.Checkpoint();};handle.DragDelta+=delegate(object s,DragDeltaEventArgs e){if(Widget.Locked)return;if(side<0){double change=Math.Min(e.HorizontalChange,Width-220);Left+=change;Width-=change;}else if(side>0)Width=Math.Max(220,Width+e.HorizontalChange);Height=Math.Max(MinHeight,Height+e.VerticalChange);};handle.DragCompleted+=delegate{SaveBounds();};return handle;}
    void StartInlineRename(DockPanel header){int index=header.Children.IndexOf(heading);if(index<0)return;var editor=new TextBox(){Text=Widget.Name,MinWidth=90,MaxWidth=180,FontSize=14};header.Children.RemoveAt(index);header.Children.Insert(index,editor);bool done=false;Action<bool> finish=delegate(bool save){if(done)return;done=true;if(save&&!String.IsNullOrWhiteSpace(editor.Text)&&editor.Text.Trim()!=Widget.Name){controller.Store.Checkpoint();Widget.Name=editor.Text.Trim();Title="栖桌 · "+Widget.Name;controller.Store.Save();}header.Children.Remove(editor);header.Children.Insert(index,heading);heading.Text=Widget.Name;};editor.KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Enter){finish(true);e.Handled=true;}else if(e.Key==Key.Escape){finish(false);e.Handled=true;}};editor.LostKeyboardFocus+=delegate{finish(true);};editor.Focus();editor.SelectAll();}
    void Build(){body.Children.Clear();string kind=plugin==null?"note":plugin.Kind;if(kind=="search"){BuildQuickSearch();return;}if(kind=="weather"){BuildWeather();return;}if(BuildExtra(kind))return;if(BuildKind(kind))return;if(kind=="calendar"){BuildCalendar();return;}else{time=Ui.Text("",kind=="clock"?44:kind=="countdown"?38:16,Ui.DesktopInk);time.FontWeight=FontWeights.SemiBold;time.TextAlignment=kind=="note"?TextAlignment.Left:TextAlignment.Center;time.Margin=new Thickness(0,14,0,10);body.Children.Add(time);date=Ui.Text("",12,Ui.DesktopMuted);date.TextAlignment=TextAlignment.Center;body.Children.Add(date);UpdateTime();}}
    void BuildCalendar(){
        var bar=new DockPanel();
        var prev=Ui.Button("‹",delegate{month=month.AddMonths(-1);DrawCalendar();});prev.Padding=new Thickness(6,2,6,2);prev.Margin=new Thickness(0,0,6,6);bar.Children.Add(prev);
        var next=Ui.Button("›",delegate{month=month.AddMonths(1);DrawCalendar();});next.Padding=prev.Padding;next.Margin=new Thickness(0,0,6,6);DockPanel.SetDock(next,Dock.Right);bar.Children.Add(next);
        var today=Ui.Button("今天",delegate{month=DateTime.Today;DrawCalendar();});today.Padding=new Thickness(10,2,10,2);today.Margin=new Thickness(0,0,6,6);DockPanel.SetDock(today,Dock.Right);bar.Children.Add(today);
        calTitle=Ui.Text("",16,Ui.DesktopInk);calTitle.TextAlignment=TextAlignment.Center;calTitle.FontWeight=FontWeights.SemiBold;calTitle.VerticalAlignment=VerticalAlignment.Center;calTitle.Margin=new Thickness(0);bar.Children.Add(calTitle);
        body.Children.Add(bar);
        var inner=new StackPanel();
        if(Widget.ShowCalendarClock){
            calClock=Ui.Text("",24,Ui.DesktopInk);calClock.TextAlignment=TextAlignment.Center;calClock.FontWeight=FontWeights.SemiBold;calClock.Margin=new Thickness(0,2,0,0);inner.Children.Add(calClock);
            calSub=Ui.Text("",11,Ui.DesktopMuted);calSub.TextAlignment=TextAlignment.Center;calSub.Margin=new Thickness(0,0,0,2);inner.Children.Add(calSub);
        }
        var weeks=new UniformGrid(){Columns=7,Margin=new Thickness(0,6,0,4)};
        foreach(string day in Widget.WeekStartMonday?new[]{"一","二","三","四","五","六","日"}:new[]{"日","一","二","三","四","五","六"})weeks.Children.Add(new TextBlock(){Text=day,Foreground=Ui.DesktopMuted,HorizontalAlignment=HorizontalAlignment.Center,FontSize=11});
        inner.Children.Add(weeks);
        days=new UniformGrid(){Columns=7,Rows=6};
        inner.Children.Add(days);
        calScroll=new ScrollViewer(){Content=inner,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};body.Children.Add(calScroll);
        lastDate=DateTime.MinValue;DrawCalendar();
    }
    void DrawCalendar(){
        if(days==null||calTitle==null)return;
        lastDate=DateTime.Today;
        calTitle.Text=month.ToString("yyyy 年 M 月");
        days.Children.Clear();
        Brush accent=Ui.Brush(Widget.Color);
        bool sundayFirst=!Widget.WeekStartMonday;
        foreach(var day in WidgetPlugins.Days(month,sundayFirst)){
            bool isToday=day.Date==DateTime.Today;
            bool weekend=day.DayOfWeek==DayOfWeek.Saturday||day.DayOfWeek==DayOfWeek.Sunday;
            bool inMonth=day.Month==month.Month;
            var lunar=Widget.ShowLunar?LunarCalendar.FromSolar(day):null;
            string small=lunar==null?"":LunarCalendar.CellLabel(day,lunar);
            var stack=new StackPanel(){VerticalAlignment=VerticalAlignment.Center};
            Brush dayColor=isToday?Ui.Ink:inMonth?(weekend?accent:Ui.DesktopInk):Ui.DesktopMuted;
            stack.Children.Add(new TextBlock(){Text=day.Day.ToString(CultureInfo.InvariantCulture),FontSize=13,FontWeight=isToday?FontWeights.Bold:FontWeights.Normal,HorizontalAlignment=HorizontalAlignment.Center,Foreground=dayColor});
            if(small.Length>0)stack.Children.Add(new TextBlock(){Text=small,FontSize=9,HorizontalAlignment=HorizontalAlignment.Center,Foreground=isToday?Ui.Ink:Ui.DesktopMuted,Margin=new Thickness(0,1,0,0)});
            var cell=new Border(){Height=30,Margin=new Thickness(1),CornerRadius=new CornerRadius(7),Background=isToday?accent:Brushes.Transparent,Child=stack};
            string full=day.ToString("yyyy 年 M 月 d 日 dddd",CultureInfo.GetCultureInfo("zh-CN"));
            if(lunar!=null)full+=" · "+lunar.Text;
            string festival=LunarCalendar.SolarFestival(day);if(festival.Length==0)festival=LunarCalendar.Term(day);if(lunar!=null&&festival.Length==0)festival=LunarCalendar.LunarFestival(lunar);if(festival.Length>0)full+=" · "+festival;
            cell.ToolTip=full;
            days.Children.Add(cell);
        }
    }
    void UpdateCalendarClock(){
        if(calScroll!=null)calScroll.MaxHeight=Math.Max(160,Widget.Height-70);
        if(calClock==null)return;
        var now=WidgetPlugins.Time(Widget,DateTimeOffset.Now);
        calClock.Text=now.ToString(Widget.ShowSeconds?"HH:mm:ss":"HH:mm");
        var lunar=LunarCalendar.FromSolar(now);
        calSub.Text=now.ToString("M 月 d 日 dddd",CultureInfo.GetCultureInfo("zh-CN"))+(lunar==null?"":" · "+lunar.Text);
    }
    void UpdateTime(){if(!IsVisible)return;string kind=plugin==null?"note":plugin.Kind;if(kind=="weather"){RefreshWeather(false);return;}if(UpdateExtra(kind))return;if(UpdateKind(kind))return;if(kind=="calendar"){if(DateTime.Today!=lastDate)DrawCalendar();UpdateCalendarClock();return;}if(time==null)return;var now=WidgetPlugins.Time(Widget,DateTimeOffset.Now);if(kind=="clock"){time.Text=now.ToString(Widget.ShowSeconds?"HH:mm:ss":"HH:mm");date.Text=LunarCalendar.Format(now);}else if(kind=="countdown"){DateTime target;if(DateTime.TryParse(Widget.TargetDate,out target)){int n=(target.Date-now.Date).Days;time.Text=n>=0?n+" 天":"已过 "+(-n)+" 天";date.Text=target.ToString("yyyy-MM-dd");}else{time.Text="设置目标日期";date.Text="在组件设置中选择日期";}}else{time.Text=String.IsNullOrEmpty(Widget.Text)?plugin==null?"插件暂不可用":plugin.Text:Widget.Text;date.Text="";}}
    DateTime lastDate;
    void SaveBounds(){if(updating||Widget.Locked)return;double oldX=Widget.X,oldY=Widget.Y,oldWidth=Widget.Width,oldHeight=Widget.Height;Widget.X=Left;Widget.Y=Top;Widget.Width=Width;Widget.Height=Height;if(controller.Store.Data.SnapEnabled&&(Keyboard.Modifiers&ModifierKeys.Shift)==0)Layout.Snap(Widget,controller.Store.Data.Groups.Cast<Group>().Concat(controller.Store.Data.Widgets.Where(w=>w.Id!=Widget.Id)),controller.Store.Data.SnapGrid,controller.Store.Data.SnapGap);Point free;var obstacles=controller.Store.Data.Groups.Where(g=>g.Page==Widget.Page&&String.IsNullOrEmpty(g.HostGroupId)).Cast<Group>().Concat(controller.Store.Data.Widgets.Where(w=>w.Id!=Widget.Id&&w.Page==Widget.Page));if(GroupArrangement.NearestFree(Widget,obstacles,Shell.WorkingAreas(this),Math.Max(8,controller.Store.Data.SnapGap),out free)){Widget.X=free.X;Widget.Y=free.Y;}else{Widget.X=oldX;Widget.Y=oldY;Widget.Width=oldWidth;Widget.Height=oldHeight;}Refresh();controller.SaveAndRefresh(false);}
    public void Refresh(){updating=true;Left=Widget.X;Top=Widget.Y;Width=Widget.Width;Height=Widget.Height;Color accent;try{accent=(Color)ColorConverter.ConvertFromString(Widget.Color);}catch{accent=Color.FromRgb(119,191,163);}byte alpha=(byte)Math.Round(255*Widget.BackgroundOpacity);frame.Background=new LinearGradientBrush(Color.FromArgb(alpha,(byte)(20+accent.R*.14),(byte)(30+accent.G*.14),(byte)(47+accent.B*.14)),Color.FromArgb(alpha,22,30,47),45);frame.BorderBrush=new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*Widget.BorderOpacity),accent.R,accent.G,accent.B));frame.CornerRadius=new CornerRadius(plugin!=null&&plugin.Kind=="search"?22:Widget.CornerStyle=="方角"?0:22);heading.Text=Widget.Name;updating=false;UpdateTime();}
    public bool PlaceOnDesktop(IntPtr host){Docked=Shell.Attach(this,host);return Docked;}
}
}
