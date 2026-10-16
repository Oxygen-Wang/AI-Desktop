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
public sealed class DualButtonSwipe {
    int startX,startY,currentX,currentY;
    bool active,approved;
    public bool Active {get{return active;}}
    public int DeltaX {get{if(!active)return 0;return currentX-startX;}}
    public int LastDelta {get;private set;}
    public void Begin(int x,int y,bool eligible){Reset();if(!eligible)return;active=true;startX=currentX=x;startY=currentY=y;}
    public void Approve(bool blank){if(active)approved=blank;}
    public void Move(int x,int y){if(!active)return;currentX=x;currentY=y;}
    public int End(int x,int y){Move(x,y);int dx=currentX-startX,dy=currentY-startY;LastDelta=dx;int direction=active&&approved&&Math.Abs(dx)>=110&&Math.Abs(dx)>=Math.Abs(dy)*1.5?(dx>0?1:-1):0;Reset();return direction;}
    public void Reset(){active=false;approved=false;}
}
}
