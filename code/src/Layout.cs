using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Windows;

namespace QiDesk {
public static class Layout {
    public static string Key(IList<Rect> areas){return String.Join("|",areas.Select(r=>r.X.ToString(CultureInfo.InvariantCulture)+","+r.Y.ToString(CultureInfo.InvariantCulture)+","+r.Width.ToString(CultureInfo.InvariantCulture)+","+r.Height.ToString(CultureInfo.InvariantCulture)));}
    public static void Snap(Group group,IEnumerable<Group> others,int grid,int gap){
        grid=Math.Max(1,grid);group.X=Math.Round(group.X/grid)*grid;group.Y=Math.Round(group.Y/grid)*grid;group.Width=Math.Max(220,Math.Round(group.Width/grid)*grid);if(!group.Rolled)group.Height=Math.Max(100,Math.Round(group.Height/grid)*grid);
        double x=group.X,y=group.Y,bestX=10,bestY=10;
        foreach(var other in others){foreach(double candidate in new[]{other.X,other.X+other.Width+gap,other.X-group.Width-gap}){double distance=Math.Abs(group.X-candidate);if(distance<bestX){bestX=distance;x=candidate;}}foreach(double candidate in new[]{other.Y,other.Y+other.Height+gap,other.Y-group.Height-gap}){double distance=Math.Abs(group.Y-candidate);if(distance<bestY){bestY=distance;y=candidate;}}}group.X=x;group.Y=y;
    }
    public static void ScaleGroups(IEnumerable<Group> groups,IList<Rect> before,IList<Rect> after){if(before.Count==0||after.Count==0)return;foreach(var group in groups){int index=0;for(int i=0;i<before.Count;i++)if(before[i].Contains(new Point(group.X+20,group.Y+20))){index=i;break;}var source=before[index];var target=after[Math.Min(index,after.Count-1)];group.X=target.Left+(group.X-source.Left)*target.Width/source.Width;group.Y=target.Top+(group.Y-source.Top)*target.Height/source.Height;group.Width=Math.Max(220,group.Width*target.Width/source.Width);group.Height=Math.Max(100,group.Height*target.Height/source.Height);}}
    public static bool EnsureVisible(Group g,IList<Rect> screens,int index){
        if(screens.Count==0)return false;
        var header=new Rect(g.X,g.Y,g.Width,42);
        foreach(var screen in screens){var overlap=Rect.Intersect(screen,header);if(!overlap.IsEmpty&&overlap.Width>=60&&overlap.Height>=24)return false;}
        Rect target=screens[0];foreach(var screen in screens)if(screen.Left<=0&&screen.Right>0&&screen.Top<=0&&screen.Bottom>0){target=screen;break;}
        g.Width=Math.Min(g.Width,Math.Max(220,target.Width-32));g.Height=Math.Min(g.Height,Math.Max(100,target.Height-80));
        int columns=Math.Max(1,(int)((target.Width-24)/(g.Width+16)));
        g.X=target.Left+16+(index%columns)*(g.Width+16);g.Y=target.Top+48+(index/columns)*Math.Min(g.Height+16,120);
        g.Y=Math.Min(g.Y,target.Bottom-80);return true;
    }
}
}
