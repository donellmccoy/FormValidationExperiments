using System;
using System.Linq;
using System.Reflection;

var asm = Assembly.LoadFrom(@"ECTSystem.Web\bin\Debug\net10.0\Radzen.Blazor.dll");
var t = asm.GetType("Radzen.Blazor.RadzenTabs");
foreach(var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
    .Where(x => x.DeclaringType == t)
    .OrderBy(x => x.Name))
{
    var parms = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name));
    Console.WriteLine(m.Name + "(" + parms + ")");
}
