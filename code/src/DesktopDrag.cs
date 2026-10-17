using System;
using System.Windows;

namespace QiDesk {
// Only the dedicated hook thread mutates this state. Approval carries a token
// so a slow accessibility result cannot activate an already released gesture.
public sealed class DesktopDrag {
    public int Token {get;private set;}
    public bool Pressed {get;private set;}
    public bool AwaitingChoice {get;private set;}
    public bool Displayed {get;private set;}
    bool approved;
    uint downTime;
    Point start,current;
    public int Begin(uint time,int x,int y,bool eligible){
        if(AwaitingChoice)return -1;
        Cancel();if(!eligible)return -1;
        downTime=time;start=current=new Point(x,y);Pressed=true;return Token;
    }
    public void Approve(int token,bool blank){if(token!=Token||!Pressed)return;if(blank)approved=true;else Cancel();}
    public void Move(int x,int y,bool eligible){if(!Pressed)return;if(!eligible){Cancel();return;}current=new Point(x,y);}
    public Rect Progress(uint time,int holdMilliseconds){
        if(!Pressed||!approved||unchecked(time-downTime)<(uint)holdMilliseconds||Math.Abs(current.X-start.X)<8||Math.Abs(current.Y-start.Y)<8)return Rect.Empty;
        Displayed=true;return SelectionOverlay.Selection(start,current);
    }
    public Rect End(uint time,int x,int y,bool eligible,int holdMilliseconds,double minimumWidth,double minimumHeight){
        if(!Pressed)return Rect.Empty;Move(x,y,eligible);Rect rect=Progress(time,holdMilliseconds);Pressed=false;
        if(rect.IsEmpty||rect.Width<minimumWidth||rect.Height<minimumHeight){Cancel();return Rect.Empty;}
        AwaitingChoice=true;return rect;
    }
    public void Cancel(){Token++;Pressed=false;AwaitingChoice=false;Displayed=false;approved=false;}
    public static Rect Logical(Rect physical,double scale){if(physical.IsEmpty)return Rect.Empty;scale=scale>0?scale:1;return new Rect(physical.X/scale,physical.Y/scale,physical.Width/scale,physical.Height/scale);}
}
// Ctrl+左键在桌面空白处拖出的框选：立即跟手，不需要长按等待。
public sealed class BoxSelect {
    int token;
    bool active;
    Point start,current;
    public bool Active{get{return active;}}
    public int Token{get{return token;}}
    public int Begin(int x,int y){token++;active=true;start=current=new Point(x,y);return token;}
    public bool Move(int x,int y){if(!active)return false;current=new Point(x,y);return true;}
    public Rect Bounds{get{return active?SelectionOverlay.Selection(start,current):Rect.Empty;}}
    public Rect End(int x,int y,double minimum){
        if(!active)return Rect.Empty;
        Move(x,y);active=false;
        Rect rect=SelectionOverlay.Selection(start,current);
        return rect.Width<minimum||rect.Height<minimum?Rect.Empty:rect;
    }
    public void Cancel(){if(active){active=false;token++;}}
}
public sealed class MiddleButtonSwipe {    const uint SampleMilliseconds=60,StaleMilliseconds=120;
    int startX,startY,currentX,currentY;
    bool active,approved,sampled,measured;
    uint sampleTime;
    int sampleX;
    double velocity;
    public bool Active {get{return active;}}
    public bool Approved {get{return active&&approved;}}
    public int DeltaX {get{if(!active)return 0;return currentX-startX;}}
    // 松手前 120 毫秒内的平滑速度（像素/毫秒）。翻页动画用它接上手指速度，下一次按下时清零。
    public double ReleaseVelocity {get{return velocity;}}
    public int LastDelta {get;private set;}
    public void Begin(int x,int y,bool eligible){Reset();if(!eligible)return;active=true;startX=currentX=sampleX=x;startY=currentY=y;velocity=0;sampled=measured=false;sampleTime=0;}
    public void Approve(bool blank){if(active)approved=blank;}
    public void Move(int x,int y){Move(x,y,0);}
    public void Move(int x,int y,uint time){
        if(!active)return;
        if(sampled){
            uint elapsed=unchecked(time-sampleTime);
            // 只用来样窗口内的采样估算即时速度，忽略长时间停顿或时钟回退。
            if(elapsed>0&&elapsed<=SampleMilliseconds){
                double instant=(double)(x-sampleX)/elapsed;
                velocity=measured?velocity*.6+instant*.4:instant;
                measured=true;
            }
        }
        sampleX=x;sampleTime=time;sampled=true;currentX=x;currentY=y;
    }
    public int End(int x,int y){return End(x,y,0);}
    public int End(int x,int y,uint time){
        // 松手前停顿过久说明没有甩动，不能拿旧速度把页面滑出去。
        if(sampled&&unchecked(time-sampleTime)>StaleMilliseconds)velocity=0;
        Move(x,y,time);int dx=currentX-startX,dy=currentY-startY;LastDelta=dx;int direction=active&&approved&&Math.Abs(dx)>=110&&Math.Abs(dx)>=Math.Abs(dy)*1.5?(dx>0?1:-1):0;Reset();return direction;
    }
    public void Reset(){active=false;approved=false;sampled=measured=false;sampleTime=0;}
}
}
