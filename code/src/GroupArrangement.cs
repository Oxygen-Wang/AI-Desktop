using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace QiDesk {
// 只保留拖拽/碰撞用到的几何帮助函数；“桌面自动排列”功能已移除。
public static class GroupArrangement {
    public const double HeaderHeight=48;
    public static Rect Bounds(Group group){return new Rect(group.X,group.Edge=="bottom"?group.Y+HeaderHeight-group.Height:group.Y,group.Width,group.Height);}
    public static bool Overlaps(Rect a,Rect b,double gap){return a.Left<b.Right+gap-.001&&a.Right>b.Left-gap+.001&&a.Top<b.Bottom+gap-.001&&a.Bottom>b.Top-gap+.001;}
    public static Rect VisibleBounds(Group group){return new Rect(group.X,group.Y,group.Width,group.Rolled?HeaderHeight:group.Height);}
    public static bool NearestFree(Group moved,IEnumerable<Group> others,IList<Rect> areas,double gap,out Point result){
        var current=VisibleBounds(moved);result=current.TopLeft;
        var taken=others.Where(g=>g!=null&&g.Id!=moved.Id&&g.Page==moved.Page).Select(VisibleBounds).ToArray();
        if(taken.All(r=>!Overlaps(current,r,gap)))return true;
        var xs=new List<double>{current.X};var ys=new List<double>{current.Y};
        foreach(var r in taken){xs.Add(r.Right+gap);xs.Add(r.Left-current.Width-gap);ys.Add(r.Bottom+gap);ys.Add(r.Top-current.Height-gap);}
        foreach(var area in areas){xs.Add(area.Left);xs.Add(area.Right-current.Width);ys.Add(area.Top);ys.Add(area.Bottom-current.Height);}
        var options=new List<Point>();foreach(var area in areas){
            foreach(double x in xs)foreach(double y in ys){var candidate=new Rect(x,y,current.Width,current.Height);if(candidate.Left<area.Left-.001||candidate.Top<area.Top-.001||candidate.Right>area.Right+.001||candidate.Bottom>area.Bottom+.001)continue;if(taken.All(r=>!Overlaps(candidate,r,gap)))options.Add(candidate.TopLeft);}
        }
        if(options.Count==0)return false;
        result=options.OrderBy(p=>Math.Pow(p.X-current.X,2)+Math.Pow(p.Y-current.Y,2)).First();return true;
    }
}
}
