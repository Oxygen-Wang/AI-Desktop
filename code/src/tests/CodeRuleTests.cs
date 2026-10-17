using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace QiDesk {
// Guards the guard. code-budget.json is a plain file, so nothing stops an author from
// raising a ceiling instead of fixing the code. These checks make that visible in
// --self-test, which already runs on every build.
public static class CodeRuleTests {
    public static void Run(string root,Action<bool,string> check){
        // Both the source tree and the baseline are read from the repository, not the
        // build output, so the check sees exactly what a reviewer would see.
        string repo=RepositoryRoot();
        check(repo!=null,"the repository root is reachable from the running test");
        if(repo==null)return;
        string budgetPath=Path.Combine(repo,@"code\code-budget.json");
        check(File.Exists(budgetPath),"code-budget.json exists");
        if(!File.Exists(budgetPath))return;

        var json=new JavaScriptSerializer(){MaxJsonLength=16*1024*1024};
        Dictionary<string,object> data=null;
        try{data=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(budgetPath));}catch{}
        check(data!=null,"code-budget.json parses as an object");
        if(data==null)return;

        var limits=Field(data,"limits") as Dictionary<string,object>;
        var budget=Field(data,"budget") as Dictionary<string,object>;
        var legacy=Field(data,"legacy") as Dictionary<string,object>;
        check(limits!=null&&budget!=null&&legacy!=null,"the baseline has limits, budget and legacy sections");
        if(limits==null||budget==null||legacy==null)return;

        // A ceiling that only grows is the failure mode this file exists to prevent.
        check(Number(limits,"lineLength")<=300&&Number(limits,"lineLength")>=200,"the line length limit stays between 200 and 300 bytes");
        check(Number(limits,"fileLines")<=900&&Number(limits,"fileLines")>=400,"the file length limit stays between 400 and 900 lines");
        check(Number(limits,"nestedBlockDepth")<=10&&Number(limits,"nestedBlockDepth")>=4,"the nesting limit stays between 4 and 10");
        check(Number(limits,"duplicateFile")==0,"no duplicated source file is allowed");

        // The counts and the legacy lists must describe the same code. If someone
        // fixes a file and does not run -Accept, the count stays high and the next
        // author inherits headroom; if someone deletes a legacy entry without fixing
        // the file, the guard starts failing for the wrong reason.
        var counts=new Dictionary<string,int>();
        foreach(string key in new[]{"longLines","deepBlocks","emptyCatch","oversizedFiles"}){
            object entry=Field(legacy,key);
            var entryMap=entry as Dictionary<string,object>;
            check(entryMap!=null,"legacy."+key+" is an object holding files and counts");
            if(entryMap==null)continue;
            object raw=Field(entryMap,"files");
            // A single entry serialized as a bare string would silently become one
            // character per iteration. The baseline must always hold a list.
            check(raw is System.Collections.IEnumerable&&!(raw is string),"legacy."+key+".files is a list, even when it holds one entry");
            var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list=raw as System.Collections.IEnumerable;
            if(list!=null&&!(raw is string))foreach(object item in list){string text=Convert.ToString(item);if(!String.IsNullOrWhiteSpace(text))names.Add(text);}
            counts[key]=names.Count;
            check(names.All(n=>n.StartsWith("code/src/",StringComparison.OrdinalIgnoreCase)),"legacy entries for "+key+" point at source files");
            check(names.All(n=>File.Exists(Path.Combine(repo,n.Replace('/',Path.DirectorySeparatorChar)))),"every legacy file for "+key+" still exists");
            check(Number(budget,key)>=names.Count,"the "+key+" budget is not lower than the files listed for it");

            // The per-file allowance is what stops "listed once, unlimited forever":
            // a file with one old long line must not be allowed to collect more.
            var perFile=Field(entryMap,"counts") as Dictionary<string,object>;
            check(perFile!=null,"legacy."+key+" records how many each file may keep");
            if(perFile==null)continue;
            check(perFile.Count==names.Count,"legacy."+key+" has one limit per listed file");
            bool everyFileNamed=true;
            foreach(string name in names)if(!perFile.ContainsKey(name))everyFileNamed=false;
            check(everyFileNamed,"legacy."+key+" counts mentions every listed file");
            int allowedTotal=0;
            bool everyLimitValid=true;
            foreach(var pair in perFile){
                // A non-numeric limit is a broken baseline. Collecting the verdict
                // instead of checking inside the loop keeps one test per property
                // rather than one test per file, which would inflate the count.
                int allowed;
                try{allowed=Convert.ToInt32(pair.Value);}
                catch(FormatException){allowed=-1;}
                catch(InvalidCastException){allowed=-1;}
                catch(OverflowException){allowed=-1;}
                if(allowed<1)everyLimitValid=false;
                allowedTotal+=Math.Max(allowed,0);
            }
            check(everyLimitValid,"every "+key+" limit allows at least one violation");
            check(allowedTotal==Number(budget,key),"the "+key+" budget equals the sum of its per-file limits");
        }
        check(Number(budget,"longLines")==0||(Field(legacy,"longLines") as Dictionary<string,object>)!=null,"a non-zero long-line count has a legacy list to explain it");

        check(File.Exists(Path.Combine(repo,@"code\check-code.ps1")),"the guard script ships with the baseline");
        check(File.Exists(Path.Combine(repo,"CODE-RULES.md")),"the written rules are part of the repository");
    }

    static object Field(Dictionary<string,object> data,string name){
        foreach(var pair in data)if(String.Equals(pair.Key,name,StringComparison.Ordinal))return pair.Value;
        return null;
    }

    static double Number(Dictionary<string,object> data,string name){
        object value=Field(data,name);
        if(value==null)return Double.NaN;
        try{return Convert.ToDouble(value);}catch{return Double.NaN;}
    }

    // The test binary lives in dist, so the repository is two levels up. The check
    // stays silent rather than guessing when the layout is different.
    static string RepositoryRoot(){
        string directory=AppDomain.CurrentDomain.BaseDirectory;
        for(int i=0;i<4&&!String.IsNullOrEmpty(directory);i++){
            if(File.Exists(Path.Combine(directory,@"code\code-budget.json")))return directory;
            directory=Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar));
        }
        return null;
    }
}
}
