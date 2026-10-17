using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace QiDesk {
// One discovery order for the indexer, CLI fallback and SDK. Builds carry the
// runtime, without depending on local tools or reference repositories.
public static class EverythingRuntime {
    public static string Find(State state,string fileName){
        return Candidates(AppDomain.CurrentDomain.BaseDirectory,state.EverythingPath,fileName)
            .FirstOrDefault(File.Exists)??"";
    }

    internal static string[] Candidates(string appDirectory,string configuredPath,string fileName){
        if(fileName!="Everything.exe"&&fileName!="es.exe"&&fileName!="Everything64.dll")
            throw new ArgumentException("Unsupported Everything runtime file.","fileName");

        var paths=new List<string>();
        if(File.Exists(configuredPath)){
            string configured=Path.GetFullPath(configuredPath);
            paths.Add(fileName=="Everything.exe"?configured:Path.Combine(Path.GetDirectoryName(configured),fileName));
        }
        paths.Add(Path.Combine(appDirectory,"Everything",fileName));
        paths.Add(Path.Combine(appDirectory,fileName));
        foreach(string folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}){
            if(!String.IsNullOrEmpty(folder))paths.Add(Path.Combine(folder,"Everything",fileName));
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
}
