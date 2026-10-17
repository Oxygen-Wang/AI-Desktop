using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QiDesk {
// The same editor can be hosted by any file view; disk changes stay in FileRenaming.
public sealed class InlineFileName : ContentControl {
    readonly TextBlock label;
    readonly string path;
    readonly Func<string,string> rename;
    readonly Action finished;
    TextBox input;
    TextBlock error;
    public bool Editing {get{return input!=null;}}
    public TextBox Input {get{return input;}}
    public InlineFileName(TextBlock text,string file,Func<string,string> commit,Action end){
        label=text;path=file;rename=commit;finished=end;Content=label;
    }
    public void Begin(){
        if(Editing)return;
        input=Ui.Input(Path.GetFileName(path));input.Margin=new Thickness(0);input.MinWidth=40;
        input.FontSize=13;input.Padding=new Thickness(3);input.Cursor=Cursors.IBeam;
        error=new TextBlock(){Foreground=Brushes.MistyRose,FontSize=11,TextWrapping=TextWrapping.Wrap};
        var stack=new StackPanel();stack.Children.Add(input);stack.Children.Add(error);Content=stack;
        input.PreviewKeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Enter){Commit();e.Handled=true;}else if(e.Key==Key.Escape){Cancel();e.Handled=true;}};
        // A tile must never capture the mouse or start a file drag while editing.
        MouseLeftButtonDown+=KeepInputClick;
        input.Focus();Keyboard.Focus(input);
        string name=Path.GetFileName(path);input.Select(0,Directory.Exists(path)?name.Length:Path.GetFileNameWithoutExtension(name).Length);
    }
    void KeepInputClick(object sender,MouseButtonEventArgs e){e.Handled=true;}
    public void Commit(){
        if(!Editing)return;
        try{rename(input.Text);Cancel();}
        catch(Exception ex){
            // Native Shell errors and validation failures are shown beside the name.
            if(!(ex is ArgumentException||ex is IOException||ex is UnauthorizedAccessException||ex is System.ComponentModel.Win32Exception))throw;
            error.Text=ex.Message;input.Focus();
        }
    }
    public void Cancel(){if(!Editing)return;input=null;Content=label;MouseLeftButtonDown-=KeepInputClick;finished();}
}
public partial class FenceWindow {
    readonly System.Collections.Generic.Dictionary<string,InlineFileName> fileNames=new System.Collections.Generic.Dictionary<string,InlineFileName>(StringComparer.OrdinalIgnoreCase);
    InlineFileName fileNameEditor;
    string selectedFile;
    void InitializeFileRenaming(){
        PreviewKeyDown+=delegate(object sender,KeyEventArgs e){
            if(fileNameEditor!=null&&fileNameEditor.Editing){
                if(e.Key==Key.Enter){fileNameEditor.Commit();e.Handled=true;}
                else if(e.Key==Key.Escape){fileNameEditor.Cancel();e.Handled=true;}
                return;
            }
            if(e.Key==Key.F2&&selectedFile!=null){BeginFileRename(selectedFile);e.Handled=true;}
        };
    }
    InlineFileName FileNameLabel(TextBlock label,string path,Border tile){
        var editor=new InlineFileName(label,path,delegate(string value){
            return FileRenaming.Rename(controller.Store,this,path,value);
        },delegate{
            fileNameEditor=null;
            if(Docked&&!Topmost&&!SearchVisible)Shell.EndInlineInput(this);
            lastSignature=null;controller.SaveAndRefresh(false);Refresh();
        });
        fileNames[path]=editor;return editor;
    }
    public bool BeginFileRename(string path){
        InlineFileName editor;
        if(!fileNames.TryGetValue(path,out editor))return false;
        if(fileNameEditor!=null)fileNameEditor.Cancel();
        fileNameEditor=editor;selectedFile=path;
        var tile=editor.Parent as FrameworkElement;
        while(tile!=null&&!(tile is Border&&Equals(tile.Tag,"qidesk-file")))tile=VisualTreeHelper.GetParent(tile) as FrameworkElement;
        if(tile!=null)tile.Height=Double.NaN;
        Shell.BeginInlineInput(this);editor.Begin();editor.BringIntoView();return true;
    }
}
}
