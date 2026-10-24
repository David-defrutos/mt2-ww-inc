using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace WwInc;
// Copies mod-owned containers, retaining native object identities. Unity copies their internal state.
internal sealed class CollectionSnapshot
{
    readonly List<(FieldInfo field,object? value)> fields=new();
    internal CollectionSnapshot(params Type[] owners)
    {
        foreach(var owner in owners)
            foreach(var f in owner.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
                if(!f.IsLiteral)fields.Add((f,Clone(f.GetValue(null))));
    }
    internal void Restore()
    {
        foreach(var entry in fields)
        {
            if(!entry.field.IsInitOnly)entry.field.SetValue(null,entry.value);
            else if(IsContainer(entry.value))Fill(entry.field.GetValue(null)!,entry.value!);
        }
    }
    static bool IsContainer(object? value)
    {
        if(value==null)return false;
        var type=value.GetType();
        return type.IsGenericType&&new[]{typeof(Dictionary<,>),typeof(List<>),typeof(HashSet<>),typeof(Stack<>)}.Contains(type.GetGenericTypeDefinition());
    }
    static object? Clone(object? value)
    {
        if(!IsContainer(value))return value;
        var copy=Activator.CreateInstance(value!.GetType())!;Fill(copy,value);return copy;
    }
    static void Fill(object target,object source)
    {
        target.GetType().GetMethod("Clear")!.Invoke(target,null);
        if(target is IDictionary dictionary)
        {foreach(DictionaryEntry pair in (IDictionary)source)dictionary.Add(pair.Key,Clone(pair.Value));return;}
        var items=((IEnumerable)source).Cast<object?>();
        bool stack=target.GetType().GetGenericTypeDefinition()==typeof(Stack<>);
        if(stack)items=items.Reverse();
        var add=target.GetType().GetMethod(stack?"Push":"Add")!;
        foreach(var value in items)add.Invoke(target,new[]{Clone(value)});
    }
}
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/CollectionSnapshot.cs||Instantáneas reversibles de colecciones anidadas y estado escalar del mod
