using System;
using System.Collections.Generic;
using System.Windows;

namespace QiDesk {
public static class GroupPages {
    public static int Destination(int page,int total,double horizontalDrag,bool rightButtonHeld){
        if(!rightButtonHeld||total<2||Math.Abs(horizontalDrag)<80)return page;
        return (page+(horizontalDrag>0?1:-1)+total)%total;
    }
}
public partial class Controller {
    public bool TransferGroupPage(Group group,double horizontalDrag,bool rightButtonHeld,Point position,IList<Rect> areas){
        int destination=GroupPages.Destination(group.Page,Store.Data.Pages,horizontalDrag,rightButtonHeld);
        if(destination==group.Page)return false;
        group.X=position.X;group.Y=position.Y;group.Edge="";group.Page=destination;
        Layout.EnsureVisible(group,areas,0);Store.Data.Page=destination;SaveAndRefresh(true);
        return true;
    }
}
}
