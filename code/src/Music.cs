using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QiDesk {
public sealed class MusicInfo {
    public bool Running {get;set;}
    public string Title {get;set;}
    public string Artist {get;set;}
    public bool Playing {get{return Running&&!String.IsNullOrEmpty(Title);}}
    public MusicInfo(){Title="";Artist="";}
}
// 网易云音乐桌面客户端没有公开的本地播放接口，这里读取它的主窗口标题（播放时形如
// “歌名 - 歌手”）作为正在播放信息，并用系统媒体键控制上一首、播放/暂停与下一首。
public static class NetEaseMusic {
    const string ProcessName="cloudmusic";
    const string IdleTitle="网易云音乐";
    // 默认读取 cloudmusic.exe；可用环境变量指向其它客户端或用于本地验证。
    static string TargetProcess(){string custom=Environment.GetEnvironmentVariable("QIDESK_MUSIC_PROCESS");return String.IsNullOrWhiteSpace(custom)?ProcessName:custom.Trim();}
    [DllImport("user32.dll")] static extern void keybd_event(byte key,byte scan,uint flags,UIntPtr extra);
    const byte KeyNext=0xB0,KeyPrevious=0xB1,KeyPlayPause=0xB3;
    const uint Extended=0x0001,KeyUp=0x0002;

    public static MusicInfo Now(){
        var info=new MusicInfo();
        try{
            foreach(var process in Process.GetProcessesByName(TargetProcess())){
                string title="";
                try{title=process.MainWindowTitle;}catch{}
                if(String.IsNullOrWhiteSpace(title))continue;
                info.Running=true;
                if(title.Trim()==IdleTitle)return info;
                string[] parts=Split(title);
                info.Title=parts[0];info.Artist=parts[1];
                return info;
            }
        }catch{}
        return info;
    }

    // 网易云窗口标题为“歌名 - 歌手”，只按第一个分隔符拆分，避免歌曲名中的连字符被误切。
    public static string[] Split(string title){
        string text=(title??"").Trim();
        int dash=text.IndexOf(" - ",StringComparison.Ordinal);
        if(dash>0)return new[]{text.Substring(0,dash).Trim(),text.Substring(dash+3).Trim()};
        return new[]{text,""};
    }

    public static void PlayPause(){Key(KeyPlayPause);}
    public static void Next(){Key(KeyNext);}
    public static void Previous(){Key(KeyPrevious);}
    static void Key(byte key){
        keybd_event(key,0,Extended,UIntPtr.Zero);
        keybd_event(key,0,Extended|KeyUp,UIntPtr.Zero);
    }
}
}
