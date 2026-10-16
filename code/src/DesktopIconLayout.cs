using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;

namespace QiDesk {
// 把与分区/插件重叠的原生桌面图标挪到最近的空位。通过跨进程给桌面 SysListView32
// 发送 LVM_GETITEMPOSITION / LVM_SETITEMPOSITION 实现。仅在图标视图且未开启“自动排列”时有效。
// 移动过程支持逐帧插值（缓出）动画，让图标“滑”到新位置。
public static class DesktopIconLayout {
    const uint LVM_FIRST=0x1000,LVM_GETITEMCOUNT=LVM_FIRST+4,LVM_SETITEMPOSITION=LVM_FIRST+15,LVM_GETITEMPOSITION=LVM_FIRST+16,LVM_GETITEMSPACING=LVM_FIRST+51;
    const int GWL_STYLE=-16,LVS_AUTOARRANGE=0x0100;
    const uint PROCESS_VM_OPERATION=0x0008,PROCESS_VM_READ=0x0010,PROCESS_VM_WRITE=0x0020;
    const uint MEM_COMMIT=0x1000,MEM_RELEASE=0x8000,PAGE_READWRITE=0x04;
    [StructLayout(LayoutKind.Sequential)] struct RECT {public int Left,Top,Right,Bottom;}
    struct Move {public int Index;public double SX,SY,EX,EY;}
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd,uint msg,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd,out RECT rect);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr hWnd,int index);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint processId);
    [DllImport("kernel32.dll")] static extern IntPtr VirtualAllocEx(IntPtr process,IntPtr address,IntPtr size,uint type,uint protect);
    [DllImport("kernel32.dll")] static extern bool VirtualFreeEx(IntPtr process,IntPtr address,IntPtr size,uint type);
    [DllImport("kernel32.dll")] static extern bool WriteProcessMemory(IntPtr process,IntPtr address,byte[] buffer,IntPtr size,out IntPtr written);
    [DllImport("kernel32.dll")] static extern bool ReadProcessMemory(IntPtr process,IntPtr address,byte[] buffer,IntPtr size,out IntPtr read);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static string LastError="";
    // 返回移动的图标数量。obstacles 为 WPF 逻辑坐标（分区/插件窗口），scale 为物理/逻辑比例。
    public static int PushAside(IntPtr list,double scale,IEnumerable<Rect> obstacles,bool animate){
        LastError="";if(list==IntPtr.Zero||!Shell.IsWindow(list))return 0;
        var blocked=obstacles.Where(r=>!r.IsEmpty).ToList();if(blocked.Count==0)return 0;
        long style=GetWindowLongPtr(list,GWL_STYLE).ToInt64();if((style&LVS_AUTOARRANGE)!=0){LastError="桌面开启了“自动排列图标”，无法固定位置。";return 0;}
        int count=(int)SendMessage(list,LVM_GETITEMCOUNT,IntPtr.Zero,IntPtr.Zero);if(count<=0||count>500)return 0;
        int spacing=(int)SendMessage(list,LVM_GETITEMSPACING,IntPtr.Zero,IntPtr.Zero);int cellW=spacing&0xffff,cellH=(spacing>>16)&0xffff;if(cellW<24)cellW=76;if(cellH<24)cellH=76;
        if(scale<=0)scale=1;cellW=(int)Math.Round(cellW*scale);cellH=(int)Math.Round(cellH*scale);
        uint pid=Shell.ProcessId(list);IntPtr process=OpenProcess(PROCESS_VM_OPERATION|PROCESS_VM_READ|PROCESS_VM_WRITE,false,pid);
        if(process==IntPtr.Zero){LastError="无法访问桌面图标进程。";return 0;}
        IntPtr buffer=VirtualAllocEx(process,IntPtr.Zero,(IntPtr)8,MEM_COMMIT,PAGE_READWRITE);if(buffer==IntPtr.Zero){CloseHandle(process);LastError="无法分配远程内存。";return 0;}
        try{
            RECT rect;if(!GetWindowRect(list,out rect))return 0;double originX=rect.Left,originY=rect.Top;
            var positions=new Point[count];var valid=new bool[count];
            for(int i=0;i<count;i++){
                byte[] zero=new byte[8];IntPtr written;WriteProcessMemory(process,buffer,zero,(IntPtr)8,out written);
                SendMessage(list,LVM_GETITEMPOSITION,(IntPtr)i,buffer);
                byte[] data=new byte[8];IntPtr read;if(!ReadProcessMemory(process,buffer,data,(IntPtr)8,out read))continue;
                positions[i]=new Point(originX+BitConverter.ToInt32(data,0),originY+BitConverter.ToInt32(data,4));valid[i]=true;
            }
            var blockedRects=blocked.Select(r=>new Rect(r.X*scale,r.Y*scale,r.Width*scale,r.Height*scale)).ToList();
            // 像“自动排列图标”一样：在可用区域内生成按列（先列后行）的网格空位，避开分区/插件。
            const int margin=8;
            int cols=Math.Max(1,(int)Math.Floor((rect.Right-rect.Left-margin*2)/(double)cellW));
            int rows=Math.Max(1,(int)Math.Floor((rect.Bottom-rect.Top-margin*2)/(double)cellH));
            var slots=new List<Point>();
            for(int column=0;column<cols;column++)for(int row=0;row<rows;row++){
                double x=originX+margin+column*cellW,y=originY+margin+row*cellH;var cell=new Rect(x,y,cellW,cellH);
                if(blockedRects.Any(b=>b.IntersectsWith(Inflate(cell,6))))continue;
                slots.Add(new Point(x,y));
            }
            if(slots.Count==0){LastError="可用区域已被分区/插件占满，无法排布图标。";return 0;}
            // 保持图标的相对顺序：先按当前所在列，再按从上到下。
            var order=Enumerable.Range(0,count).Where(i=>valid[i]).OrderBy(i=>Math.Round((positions[i].X-originX-margin)/cellW)).ThenBy(i=>positions[i].Y).ToArray();
            var plan=new List<Move>();
            for(int k=0;k<order.Length&&k<slots.Count;k++){
                int index=order[k];Point start=positions[index],end=slots[k];
                if(Math.Abs(start.X-end.X)<1&&Math.Abs(start.Y-end.Y)<1)continue;
                plan.Add(new Move(){Index=index,SX=start.X,SY=start.Y,EX=end.X,EY=end.Y});
            }
            if(plan.Count==0)return 0;
            if(!animate){foreach(var move in plan)SendMessage(list,LVM_SETITEMPOSITION,(IntPtr)move.Index,MakeLParam((int)Math.Round(move.EX-originX),(int)Math.Round(move.EY-originY)));return plan.Count;}
            return Animate(list,plan,originX,originY);
        }finally{VirtualFreeEx(process,buffer,IntPtr.Zero,MEM_RELEASE);CloseHandle(process);}
    }
    static int Animate(IntPtr list,List<Move> plan,double originX,double originY){
        // 缓出插值：先快后慢，像被吸到空位。
        int frames=plan.Count>60?8:16;int delay=frames<=8?18:14;
        for(int frame=1;frame<=frames;frame++){
            double t=(double)frame/frames;double e=1-Math.Pow(1-t,3);
            foreach(var move in plan){
                double x=move.SX+(move.EX-move.SX)*e,y=move.SY+(move.EY-move.SY)*e;
                SendMessage(list,LVM_SETITEMPOSITION,(IntPtr)move.Index,MakeLParam((int)Math.Round(x-originX),(int)Math.Round(y-originY)));
            }
            if(frame<frames)Thread.Sleep(delay);
        }
        return plan.Count;
    }
    public static Point FindFree(Point from,int cellW,int cellH,double minX,double minY,double maxX,double maxY,IEnumerable<Rect> blockedRects,IEnumerable<Rect> occupiedRects){
        var blocked=blockedRects.ToList();var occupied=occupiedRects.ToList();
        // 以图标当前格为起点向外扩展搜索，优先最近的空位。
        for(int ring=0;ring<60;ring++){
            for(int dx=-ring;dx<=ring;dx++)for(int dy=-ring;dy<=ring;dy++){
                if(Math.Abs(dx)!=ring&&Math.Abs(dy)!=ring)continue;
                double x=from.X+dx*cellW,y=from.Y+dy*cellH;
                if(x<minX||y<minY||x>maxX||y>maxY)continue;
                var cell=Inflate(new Rect(x,y,cellW,cellH),4);
                if(blocked.Any(b=>b.IntersectsWith(cell)))continue;
                if(occupied.Any(o=>o.IntersectsWith(cell)))continue;
                return new Point(x,y);
            }
        }
        return new Point(Double.NaN,Double.NaN);
    }
    static Rect Inflate(Rect r,double amount){return new Rect(r.X-amount,r.Y-amount,r.Width+amount*2,r.Height+amount*2);}
    static IntPtr MakeLParam(int x,int y){return (IntPtr)((y<<16)|(x&0xffff));}
}
}
