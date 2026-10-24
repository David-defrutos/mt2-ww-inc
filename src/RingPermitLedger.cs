namespace WwInc;
internal sealed class RingPermitLedger
{
    string ring="";
    // Card data IDs and ability CardStates are shared by duplicate equipment.
    // Track the physical equipment card, preserving its use across re-equips.
    sealed class IdentityComparer : System.Collections.Generic.IEqualityComparer<object>
    {
        public new bool Equals(object? a,object? b)=>ReferenceEquals(a,b);
        public int GetHashCode(object value)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
    readonly System.Collections.Generic.HashSet<object> used=new(new IdentityComparer());
    internal void BeginBattle(string key,bool replay)
    {if(replay||ring!=key){ring=key;used.Clear();}}
    internal bool Available(string key,object equipment)=>ring!=key||!used.Contains(equipment);
    internal void MarkUsed(string key,object equipment)
    {
        if(ring!=key){ring=key;used.Clear();}
        used.Add(equipment);
    }
}
// 2026-10-10-2358||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/RingPermitLedger.cs||Permiso por ring reconstruido por replay sin configuración persistente
// 2026-10-20-0017||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/RingPermitLedger.cs||Permiso por ring y referencia de cada equipo, no global ni por ID compartido
