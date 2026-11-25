using System.Text.Json;
using System.Text.RegularExpressions;
using StoryTranslator.Core;
var root = Directory.GetCurrentDirectory();
var corpus = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"data/evaluation/translation-cases.json")))
 .RootElement.GetProperty("cases").EnumerateArray().ToDictionary(c=>c.GetProperty("id").GetString()!,c=>c.GetProperty("text").GetString()!);
var names = new[]{"Kabura","Haru","Nazuna","Haruka","Azami","Northern Alliance","Glass Harbor","Riven","Ashen Tower"};
var glossary = new GlossaryProtector(names.Select(n=>new GlossaryEntry(n,[],"evaluation")));
var ids = new[]{"synthetic-11","synthetic-12","m5-loses","m5-back","m5-haru","p23-dry","p23-detective","m2-listening"};
string Normalize(string text) {
 text=text.ToLowerInvariant();
 foreach(var name in names) text=Regex.Replace(text,@"\b"+Regex.Escape(name)+@"\b",name,RegexOptions.IgnoreCase);
 return text;
}
var jobs=new List<object>();
foreach(var (protect,context,label) in new[]{(false,false,"plain"),(true,false,"glossary"),(true,true,"glossary-context"),(false,true,"context")}) {
 var items=new List<object>();
 var maps=new Dictionary<string,IReadOnlyDictionary<string,string>>();
 foreach(var id in ids) {
  var text=Normalize(corpus[id]);
  var contextIds=id.StartsWith("m5-") ? corpus.Keys.Where(k=>k.StartsWith("m5-")&&k!=id).ToArray()
   :id.StartsWith("p23-") ? new[]{"p23-drink","p23-sure","p23-azami","p23-guess","p23-blood"}
   :id=="m2-listening" ? new[]{"m2-anyway","m2-haru","m2-amazing"} : Array.Empty<string>();
  var ctx=context ? string.Join("\n",contextIds.Where(k=>k!=id).Select(k=>Normalize(corpus[k]))) : "";
  if(protect) {
   const string sep="\n---CONTEXT---\n";
   var p=glossary.Protect(text+sep+ctx);
   var split=p.Text.Split(sep,StringSplitOptions.None);
   if(split.Length!=2) throw new Exception("Separator failure");
   var targetMap=p.TokenMap.Where(kv=>split[0].Contains(kv.Key)).ToDictionary(kv=>kv.Key,kv=>kv.Value);
   if(GlossaryProtector.Restore(split[0],targetMap)!=text) throw new Exception("Roundtrip failure");
   text=split[0];ctx=split[1];maps[id]=targetMap;
  } else maps[id]=new Dictionary<string,string>();
  items.Add(new{id,text,context=ctx});
 }
 jobs.Add(new{name=label,items,tokenMaps=maps});
}
var output=Path.Combine(root,"artifacts/translation-context-study");Directory.CreateDirectory(output);
File.WriteAllText(Path.Combine(output,"prepared.json"),JsonSerializer.Serialize(new{names,sourceIds=ids,jobs},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("PASS: actual Core glossary roundtrips; four conditions, eight targets; saved English context only. No network.");
