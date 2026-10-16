using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QiDesk {
public static partial class WidgetPlugins {
    public static readonly string[] SupportedKinds={"clock","calendar","weather","note","countdown","tasks","focus","choice","calc","habit","system","photos","music"};
    public static int MinimumHeight(string kind){return kind=="calc"?320:kind=="calendar"?300:kind=="system"?230:kind=="weather"?225:kind=="tasks"?220:kind=="music"?180:kind=="photos"?200:kind=="focus"?200:kind=="habit"?190:kind=="choice"?180:130;}
    public static void ValidateExtra(DesktopWidget w){w.FocusMinutes=Math.Max(1,Math.Min(180,w.FocusMinutes==0?25:w.FocusMinutes));w.FocusUntil=w.FocusUntil??"";DateTimeOffset end;if(w.FocusUntil.Length>0&&!DateTimeOffset.TryParse(w.FocusUntil,out end))w.FocusUntil="";w.FocusPausedSeconds=Math.Max(0,Math.Min(10800,w.FocusPausedSeconds));w.CheckedTasks=(w.CheckedTasks??new List<int>()).Where(i=>i>=0&&i<30).Distinct().ToList();w.TaskTimes=w.TaskTimes??new Dictionary<string,string>();var validDone=new HashSet<string>(w.CheckedTasks.Select(i=>i.ToString(CultureInfo.InvariantCulture)));var keptTimes=new Dictionary<string,string>();foreach(var pair in w.TaskTimes){DateTime parsed;if(validDone.Contains(pair.Key)&&!String.IsNullOrEmpty(pair.Value)&&DateTime.TryParse(pair.Value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out parsed))keptTimes[pair.Key]=pair.Value;}w.TaskTimes=keptTimes;w.Checkins=(w.Checkins??new List<string>()).Where(x=>{DateTime parsed;return !String.IsNullOrEmpty(x)&&DateTime.TryParseExact(x,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out parsed);}).Distinct().OrderByDescending(x=>x).Take(400).ToList();}
    public static string[] Lines(string text){return (text??"").Replace("\r","").Split('\n').Where(s=>!String.IsNullOrWhiteSpace(s)).Select(s=>s.Trim()).Take(30).ToArray();}
    public static int FocusRemaining(DesktopWidget w,DateTimeOffset now){DateTimeOffset end;if(DateTimeOffset.TryParse(w.FocusUntil,out end))return Math.Max(0,(int)Math.Ceiling((end-now).TotalSeconds));return w.FocusPausedSeconds>0?w.FocusPausedSeconds:w.FocusMinutes*60;}
}
public partial class WidgetWindow {
    TextBlock extraValue,extraDetail;Button focusToggle;bool focusActive;ScrollViewer taskScroll;StackPanel taskList;TextBox taskInput;bool buildingTasks;bool doneExpanded;
    static readonly Random random=new Random();
    bool BuildExtra(string kind){
        if(kind=="tasks"){
            taskList=new StackPanel();
            taskScroll=new ScrollViewer(){Content=taskList,MaxHeight=Math.Max(80,Widget.Height-160),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};body.Children.Add(taskScroll);
            var addRow=new DockPanel(){Margin=new Thickness(0,6,0,0)};
            var add=Ui.Button("＋",delegate{AddTask(taskInput.Text);});add.Padding=new Thickness(10,2,10,2);add.Margin=new Thickness(6,0,0,0);DockPanel.SetDock(add,Dock.Right);addRow.Children.Add(add);
            taskInput=new TextBox(){Text="",Padding=new Thickness(8,5,8,5),Background=new SolidColorBrush(Color.FromArgb(42,255,255,255)),Foreground=Ui.DesktopInk,BorderThickness=new Thickness(0),CaretBrush=Ui.DesktopInk,VerticalContentAlignment=VerticalAlignment.Center,ToolTip="输入后按回车或点 ＋"};
            taskInput.KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Enter){AddTask(taskInput.Text);e.Handled=true;}};
            taskInput.PreviewMouseLeftButtonDown+=delegate{PrepareInlineInput(taskInput);};
            addRow.Children.Add(taskInput);
            body.Children.Add(addRow);
            extraDetail=Ui.Text("",11,Ui.DesktopMuted);extraDetail.Margin=new Thickness(0,6,0,0);body.Children.Add(extraDetail);
            PopulateTasks();
            return true;
        }
        if(kind=="focus"||kind=="choice"){
            extraValue=Ui.Text(kind=="focus"?"25:00":"点一下，帮你选",kind=="focus"?42:23,Ui.DesktopInk);extraValue.TextAlignment=TextAlignment.Center;extraValue.Margin=new Thickness(0,12,0,12);body.Children.Add(extraValue);extraDetail=Ui.Text("",11,Ui.DesktopMuted);extraDetail.TextAlignment=TextAlignment.Center;body.Children.Add(extraDetail);
            if(kind=="focus"){
                focusActive=Widget.FocusUntil.Length>0;var controls=new WrapPanel(){HorizontalAlignment=HorizontalAlignment.Center};focusToggle=Ui.Button("开始",delegate{controller.Store.Checkpoint();if(Widget.FocusUntil.Length>0){Widget.FocusPausedSeconds=Math.Max(1,WidgetPlugins.FocusRemaining(Widget,DateTimeOffset.UtcNow));Widget.FocusUntil="";focusActive=false;}else{Widget.FocusUntil=DateTimeOffset.UtcNow.AddSeconds(WidgetPlugins.FocusRemaining(Widget,DateTimeOffset.UtcNow)).ToString("o");Widget.FocusPausedSeconds=0;focusActive=true;}controller.SaveAndRefresh(false);});controls.Children.Add(focusToggle);controls.Children.Add(Ui.Button("重置",delegate{controller.Store.Checkpoint();Widget.FocusUntil="";Widget.FocusPausedSeconds=0;focusActive=false;extraValue.Text="";controller.SaveAndRefresh(false);}));body.Children.Add(controls);
            }else{extraDetail.Text="每行一个选项，也可以填 1–6 当骰子";body.Children.Add(Ui.Button("随机选一个",delegate{var options=WidgetPlugins.Lines(Widget.Text);if(options.Length==0){extraValue.Text="先在设置中填写选项";return;}extraValue.Text=options[random.Next(options.Length)];}));}
            return true;
        }return false;
    }
    void PrepareInlineInput(TextBox box){
        if(!Docked)return;
        Shell.BeginInlineInput(this);
        box.Dispatcher.BeginInvoke(new Action(delegate{box.Focus();}));
    }
    void PopulateTasks(){
        if(taskList==null)return;
        buildingTasks=true;
        taskList.Children.Clear();
        var lines=WidgetPlugins.Lines(Widget.Text);
        var done=new List<int>();
        for(int i=0;i<lines.Length;i++)if(Widget.CheckedTasks.Contains(i))done.Add(i);
        for(int i=0;i<lines.Length;i++)if(!Widget.CheckedTasks.Contains(i))taskList.Children.Add(TaskRow(i,lines[i],false));
        if(done.Count>0){
            var header=Ui.Text((doneExpanded?"▾  ":"▸  ")+"已完成 "+done.Count+" 条",11,Ui.DesktopMuted);
            header.Cursor=Cursors.Hand;header.Margin=new Thickness(0,6,0,2);header.ToolTip=doneExpanded?"收起已完成":"展开已完成";
            header.MouseLeftButtonUp+=delegate{doneExpanded=!doneExpanded;PopulateTasks();};
            taskList.Children.Add(header);
            if(doneExpanded){
                done.Sort(delegate(int a,int b){return String.Compare(TimeOf(b),TimeOf(a),StringComparison.Ordinal);});
                foreach(int i in done)taskList.Children.Add(TaskRow(i,lines[i],true));
            }
        }
        buildingTasks=false;
    }
    UIElement TaskRow(int index,string text,bool isDone){
        var row=new DockPanel(){Margin=new Thickness(0,0,0,4)};
        var check=new CheckBox(){IsChecked=isDone,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,6,0),Focusable=false};
        check.Click+=delegate{SetTaskDone(index,check.IsChecked==true);};
        DockPanel.SetDock(check,Dock.Left);row.Children.Add(check);
        var remove=new TextBlock(){Text="✕",Foreground=Ui.DesktopMuted,Opacity=.7,Visibility=Visibility.Hidden,Cursor=Cursors.Hand,Margin=new Thickness(8,0,2,0),FontSize=12,VerticalAlignment=VerticalAlignment.Center,ToolTip="删除这一项"};
        remove.MouseEnter+=delegate{remove.Opacity=1;};remove.MouseLeave+=delegate{remove.Opacity=.7;};
        remove.MouseLeftButtonUp+=delegate{RemoveTask(index);};
        DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
        if(isDone){
            var time=Ui.Text(FormatDone(TimeOf(index)),10,Ui.DesktopMuted);time.Margin=new Thickness(6,0,0,0);time.TextWrapping=TextWrapping.NoWrap;time.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(time,Dock.Right);row.Children.Add(time);
            var label=Ui.Text(text,12,Ui.DesktopMuted);label.Margin=new Thickness(0);label.TextDecorations=TextDecorations.Strikethrough;label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;label.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(label);
        }else{
            var input=new TextBox(){Text=text,TextWrapping=TextWrapping.Wrap,Foreground=Ui.DesktopInk,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0,1,2,1),CaretBrush=Ui.DesktopInk,VerticalAlignment=VerticalAlignment.Center,ToolTip="点击即可直接修改"};
            input.LostFocus+=delegate{CommitTask(index,input);};
            input.KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Enter){CommitTask(index,input);e.Handled=true;}};
            input.PreviewMouseLeftButtonDown+=delegate{PrepareInlineInput(input);};
            row.Children.Add(input);
        }
        row.MouseEnter+=delegate{remove.Visibility=Visibility.Visible;};
        row.MouseLeave+=delegate{remove.Visibility=Visibility.Hidden;};
        return row;
    }
    string TimeOf(int index){string value;return Widget.TaskTimes!=null&&Widget.TaskTimes.TryGetValue(index.ToString(CultureInfo.InvariantCulture),out value)?value:"";}
    string FormatDone(string iso){
        if(String.IsNullOrEmpty(iso))return "";
        DateTime when;if(!DateTime.TryParse(iso,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out when))return "";
        if(when.Date==DateTime.Today)return "今天 "+when.ToString("HH:mm");
        if(when.Date==DateTime.Today.AddDays(-1))return "昨天 "+when.ToString("HH:mm");
        return when.ToString("MM-dd HH:mm");
    }
    void SetTaskDone(int index,bool done){
        var lines=WidgetPlugins.Lines(Widget.Text);
        if(index<0||index>=lines.Length)return;
        controller.Store.Checkpoint();
        if(Widget.TaskTimes==null)Widget.TaskTimes=new Dictionary<string,string>();
        if(done){
            if(!Widget.CheckedTasks.Contains(index))Widget.CheckedTasks.Add(index);
            Widget.TaskTimes[index.ToString(CultureInfo.InvariantCulture)]=DateTime.Now.ToString("o",CultureInfo.InvariantCulture);
        }else{
            Widget.CheckedTasks.Remove(index);
            Widget.TaskTimes.Remove(index.ToString(CultureInfo.InvariantCulture));
        }
        controller.SaveAndRefresh(false);
        PopulateTasks();
    }
    void AddTask(string value){
        string text=(value??"").Trim();
        if(text.Length==0)return;
        var lines=WidgetPlugins.Lines(Widget.Text).ToList();
        if(lines.Count>=30)return;
        if(String.Join("\n",lines).Length+text.Length+1>2000)return;
        controller.Store.Checkpoint();
        lines.Add(text);
        Widget.Text=String.Join("\n",lines);
        if(taskInput!=null)taskInput.Text="";
        controller.SaveAndRefresh(false);
        PopulateTasks();
    }
    void RemoveTask(int index){
        var lines=WidgetPlugins.Lines(Widget.Text).ToList();
        if(index<0||index>=lines.Count)return;
        controller.Store.Checkpoint();
        lines.RemoveAt(index);
        var remaining=new List<int>();
        foreach(int c in Widget.CheckedTasks){if(c==index)continue;remaining.Add(c>index?c-1:c);}
        Widget.CheckedTasks=remaining;
        if(Widget.TaskTimes!=null){var times=new Dictionary<string,string>();foreach(var pair in Widget.TaskTimes){int key;if(!Int32.TryParse(pair.Key,out key))continue;if(key==index)continue;times[(key>index?key-1:key).ToString(CultureInfo.InvariantCulture)]=pair.Value;}Widget.TaskTimes=times;}
        Widget.Text=String.Join("\n",lines);
        controller.SaveAndRefresh(false);
        PopulateTasks();
    }
    void CommitTask(int index,TextBox input){
        if(buildingTasks||input==null)return;
        var lines=WidgetPlugins.Lines(Widget.Text).ToList();
        if(index<0||index>=lines.Count)return;
        string value=(input.Text??"").Trim();
        if(value.Length==0){input.Text=lines[index];return;}
        if(lines[index]==value)return;
        if(Widget.Text.Length-lines[index].Length+value.Length>2000){input.Text=lines[index];return;}
        controller.Store.Checkpoint();
        lines[index]=value;
        Widget.Text=String.Join("\n",lines);
        controller.SaveAndRefresh(false);
    }
    bool UpdateExtra(string kind){
        if(kind=="tasks"){if(taskScroll!=null)taskScroll.MaxHeight=Math.Max(80,Widget.Height-160);if(extraDetail!=null){int total=WidgetPlugins.Lines(Widget.Text).Length,done=Widget.CheckedTasks.Count(i=>i<total);extraDetail.Text="已完成 "+done+" / "+total;}return true;}
        if(kind=="choice")return true;
        if(kind=="focus"){
            if(extraValue==null)return true;int remaining=WidgetPlugins.FocusRemaining(Widget,DateTimeOffset.UtcNow);
            if(focusActive&&Widget.FocusUntil.Length>0&&remaining==0){Widget.FocusUntil="";Widget.FocusPausedSeconds=0;focusActive=false;extraValue.Text="完成了！";extraDetail.Text="休息一下，再开始下一段专注";focusToggle.Content="再开始";controller.Store.Save();return true;}
            if(extraValue.Text=="完成了！"&&!focusActive&&Widget.FocusUntil.Length==0)return true;
            extraValue.Text=(remaining/60).ToString("00")+":"+(remaining%60).ToString("00");extraDetail.Text=Widget.FocusUntil.Length>0?"专注进行中":Widget.FocusPausedSeconds>0?"已暂停":"一段 "+Widget.FocusMinutes+" 分钟的专注时间";focusToggle.Content=Widget.FocusUntil.Length>0?"暂停":"开始";return true;
        }return false;
    }
}
}
