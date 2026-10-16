using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace QiDesk {
public static class Ui {
    public static Brush Ink=new SolidColorBrush(Color.FromRgb(28,40,60));
    public static Brush Muted=new SolidColorBrush(Color.FromRgb(100,112,130));
    public static Brush Base=new SolidColorBrush(Color.FromRgb(233,239,245));
    public static Brush Surface=new SolidColorBrush(Color.FromRgb(241,245,250));
    public static Brush Accent=new SolidColorBrush(Color.FromRgb(0,106,194));
    public static Brush DesktopInk=new SolidColorBrush(Color.FromRgb(232,237,246));
    public static Brush DesktopMuted=new SolidColorBrush(Color.FromRgb(160,175,195));
    public static Brush Brush(string color){try{return (Brush)new BrushConverter().ConvertFromString(color);}catch{return Accent;}}
    public static TextBlock Text(string text,int size,Brush color){return new TextBlock(){Text=text,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};}
    public static DropShadowEffect Shadow(double depth,double blur,double opacity){return new DropShadowEffect(){Color=Color.FromRgb(58,76,100),ShadowDepth=depth,BlurRadius=blur,Opacity=opacity,Direction=315};}
    static ControlTemplate softButton;
    public static Button Button(string text,Action action){var b=new Button(){Content=text,Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,8,8),Background=Surface,Foreground=Ink,BorderBrush=Brushes.White,Cursor=System.Windows.Input.Cursors.Hand};if(softButton==null){softButton=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Grid><Border x:Name='shape' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' CornerRadius='9' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1'><Border.Effect><DropShadowEffect Color='#53657D' BlurRadius='10' ShadowDepth='3' Direction='315' Opacity='0.18'/></Border.Effect><ContentPresenter Margin='{TemplateBinding Padding}' HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><Border x:Name='hover' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' IsHitTestVisible='False' CornerRadius='9' Background='#18006AC2' Visibility='Collapsed'/></Grid><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='hover' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='shape' Property='Opacity' Value='0.7'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='shape' Property='BorderBrush' Value='#006AC2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");}b.Template=softButton;b.Click+=delegate{action();};return b;}
    public static TextBox Input(string value){return new TextBox(){Text=value??"",Padding=new Thickness(10,7,10,7),Background=Base,Foreground=Ink,BorderBrush=Muted,Margin=new Thickness(0,0,0,12)};}
    public static Window Dialog(string title,double width,double height,Window owner){return new Window(){Title=title,Width=width,Height=height,Background=Base,Foreground=Ink,Owner=owner,WindowStartupLocation=WindowStartupLocation.CenterOwner,FontFamily=new FontFamily("Microsoft YaHei UI"),ShowInTaskbar=false};}
    public static string Ask(string title,string label,string current,Window owner){string answer=null;var w=Dialog(title,440,200,owner);var p=new StackPanel(){Margin=new Thickness(22)};p.Children.Add(Text(label,14,Muted));var input=Input(current);p.Children.Add(input);p.Children.Add(Button("确定",delegate{answer=input.Text.Trim();w.Close();}));w.Content=p;w.Loaded+=delegate{input.Focus();input.SelectAll();};w.ShowDialog();return answer;}
    public static MenuItem Menu(string text,Action action){var m=new MenuItem(){Header=text};m.Click+=delegate{action();};return m;}
    public static void Info(string text,Window owner){MessageBox.Show(owner,text,"栖桌",MessageBoxButton.OK,MessageBoxImage.Information);}
    public static bool CopyText(string text){try{var data=new System.Windows.Forms.DataObject();data.SetText(text,System.Windows.Forms.TextDataFormat.UnicodeText);System.Windows.Forms.Clipboard.SetDataObject(data,true,5,60);return true;}catch{return false;}}
}
}
