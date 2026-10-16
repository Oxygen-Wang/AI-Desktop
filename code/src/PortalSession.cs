using System;
using System.IO;
using System.Collections.Generic;

namespace QiDesk {
public sealed class PortalSession {
    public readonly string Root;
    public string Current {get;private set;}
    Stack<string> history=new Stack<string>();
    public PortalSession(string root){Root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;Current=Root;}
    public bool Navigate(string path){string full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!Directory.Exists(full)||!full.StartsWith(Root,StringComparison.OrdinalIgnoreCase)||String.Equals(full,Current,StringComparison.OrdinalIgnoreCase))return false;history.Push(Current);Current=full;return true;}
    public bool Parent(){var parent=Directory.GetParent(Current.TrimEnd(Path.DirectorySeparatorChar));return parent!=null&&Navigate(parent.FullName);}
    public bool Back(){if(history.Count==0)return false;Current=history.Pop();return true;}
}
}
