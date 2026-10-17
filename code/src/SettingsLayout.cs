using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace QiDesk {
public partial class ManagerWindow {
    // Reflow appearance sections without changing or copying their settings handlers.
    void ArrangeAppearanceColumns(StackPanel page){
        var original=page.Children.Cast<UIElement>().ToArray();
        var columns=new WrapPanel(){Margin=new Thickness(0,10,0,4)};
        var sections=new List<Border>();
        StackPanel current=null;
        foreach(var element in original){
            var block=element as FrameworkElement;
            if(block!=null&&Convert.ToString(block.Tag)=="settings-section"){
                var heading=(element as Panel).Children.OfType<TextBlock>().FirstOrDefault();
                bool append=current!=null&&heading!=null&&heading.Text=="背景样式和颜色";
                if(!append){
                if(current==null)page.Children.Insert(page.Children.IndexOf(element),columns);
                current=new StackPanel(){Margin=new Thickness(14,0,14,10)};
                var card=new Border(){Child=current,Background=Ui.Surface,CornerRadius=new CornerRadius(6),Margin=new Thickness(0,0,12,12)};
                sections.Add(card);columns.Children.Add(card);
                }
            }
            if(current==null)continue;
            var button=element as Button;
            if(button!=null&&(Convert.ToString(button.Content)=="应用外观"||Convert.ToString(button.Content).StartsWith("重置所选")))continue;
            var expander=element as Expander;
            if(expander!=null&&Convert.ToString(expander.Header)=="桌面组件外观")continue;
            page.Children.Remove(element);current.Children.Add(element);
        }
        Action resize=delegate{
            double available=Math.Max(300,page.ActualWidth-8);
            double width=available>=760?(available-24)/2:available-12;
            foreach(var section in sections)section.Width=width;
        };
        page.SizeChanged+=delegate{resize();};resize();
    }
}
}
