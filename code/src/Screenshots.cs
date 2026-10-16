using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace QiDesk {
public partial class ManagerWindow {
    public string[] Screenshots(){var found=new List<string>();var roots=new[]{controller.Store.Data.ScreenshotFolder,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"Screenshots"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),"屏幕截图")};foreach(string root in roots.Where(x=>!String.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))try{found.AddRange(Directory.GetFiles(root).Where(Thumbnails.IsPicture).Take(500));}catch{}return found.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(File.GetLastWriteTime).Take(100).ToArray();}
    StackPanel ScreenshotsPage(){var p=SettingsPage("截图与图像","按时间预览截图文件夹中的图片，保留原文件。");p.Children.Add(Ui.Button("选择截图文件夹",delegate{using(var d=new System.Windows.Forms.FolderBrowserDialog()){d.Description="选择截图文件夹";if(d.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;controller.Store.Data.ScreenshotFolder=d.SelectedPath;SaveSettings(false);ShowSettingsPage("截图与图像");}}));var controls=new WrapPanel();controls.Children.Add(Ui.Button("系统截图 · Win＋Shift＋S",delegate{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-screenclip:"){UseShellExecute=true});}catch(Exception ex){SetStatus(ex.Message);}}));controls.Children.Add(Ui.Button("刷新",delegate{ShowSettingsPage("截图与图像");}));p.Children.Add(controls);string currentDay="";foreach(string path in Screenshots()){string day=File.GetLastWriteTime(path).ToString("yyyy-MM-dd");if(day!=currentDay){Section(p,day);currentDay=day;}var card=new StackPanel();var image=new Image(){Height=170,HorizontalAlignment=HorizontalAlignment.Left,Stretch=Stretch.Uniform,MaxWidth=600};Thumbnails.Bind(image,path,512);card.Children.Add(image);card.Children.Add(ActivityCard(path,FilePresentation.Size(path)));p.Children.Add(card);}if(currentDay=="")p.Children.Add(Ui.Text("还没有图片。选择已有截图文件夹，或截图时保存到系统截图文件夹。",13,Ui.Muted));return p;}
}
}
