using System;
namespace WwInc;
internal static class AcademicMath
{
    internal static int SharedHealing(int damage,int allies)
        =>allies>0?Math.Max(0,damage)/allies:0;
    internal static int ExcessHealing(int healing,int health,int maximum)
        =>healing>=9999?0:(int)Math.Max(0L,Math.Min(int.MaxValue,(long)Math.Max(0,healing)-Math.Max(0L,(long)maximum-health)));
    internal static int OverhealArmor(int excess,int maximum)=>Math.Min(Math.Max(0,excess),Math.Max(0,maximum));
    internal static int ConsumedDamage(int incoming,int perCard,int count)
        =>(int)Math.Max(0,Math.Min(int.MaxValue,(long)incoming+(long)Math.Max(0,perCard)*Math.Max(0,count)));
    internal static (int count,bool refund) RefundProgress(int count,int kills)
    {
        if(kills<=0)return(count,false);
        return count>=4?(0,true):(count+1,false);
    }
    internal static int EducationPower(int attack,int educators)=>(int)Math.Min(int.MaxValue,(long)(Math.Max(0,attack)/2)*Math.Max(0,educators));
    internal static bool FirstQualifyingSpell(System.Collections.Generic.HashSet<string> used,string kind,int floor,int amount)
        =>amount>0&&used.Add(kind+":"+floor);
    internal static int EffectiveX(int paid,int modifier)=>(int)Math.Max(0,Math.Min(int.MaxValue,(long)paid+modifier));
    internal static int ChampionRank(System.Collections.Generic.IEnumerable<string> upgrades,string prefix)
    {
        int rank=0;
        foreach(var name in upgrades)for(int candidate=1;candidate<=3;candidate++)
            if(name.EndsWith(prefix+candidate,StringComparison.Ordinal))rank=Math.Max(rank,candidate);
        return rank;
    }
    internal static int ChampionAura(int rank,int allies)=>(int)Math.Min(int.MaxValue,(long)Math.Max(0,rank)*5*Math.Max(0,allies));
    internal static (int attack,int health) ServingStats(long conduit)
    {int c=(int)Math.Max(0,Math.Min(int.MaxValue-1L,conduit));return(c,c+1);}
    internal static bool Mixed(int left,int right)=>((left&1)!=0&&(right&2)!=0)||((left&2)!=0&&(right&1)!=0);
    internal static bool CanInsertChaperone(int count)=>count<7;
    internal static (int discounts,int remainder) Tuition(int previous,int actualDamage)
    {long total=(long)Math.Max(0,previous)+Math.Max(0,actualDamage);return ((int)(total/30),(int)(total%30));}
    internal static int RedirectionDamage(int previousDazed)=>checked(Math.Max(0,previousDazed)*10);
}
// 2026-10-10-0020||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Reglas compartidas comprobables de adyacencia, daño de redirección y matrícula
// 2026-10-10-0058||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Calcular estadísticas del helado sin valores negativos ni desbordamiento
// 2026-10-10-2359||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Cálculo comprobable de rangos combinados y potencia de los campeones
// 2026-10-11-0009||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Decisión compartida para evitar referencias duplicadas al recuperar hechizos
// 2026-10-11-0010||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Mantener la comprobación de recuperación en la pila real, sin helper trivial

// 2026-10-11-1104||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Calcular X con coste pagado y modificadores nativos sin desbordamiento

// 2026-10-11-1651||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Umbral real de descuentos de Frozen Tuition 50 a 30; acumular resto módulo 30

// 2026-10-12-0055||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Calcular daño base más bono por consumidas sin duplicar Magic Power y evitando overflow

// 2026-10-12-0915||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Calcular bono de Educación con redondeo hacia abajo y acumulación por copias

// 2026-10-12-0923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Registrar primera resolución positiva por tipo y piso sin consumirla con cero daño o curación

// 2026-10-12-1808||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Calcular exceso seguro: curación efectiva menos salud faltante

// 2026-10-12-2056||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Calcular porción igual de daño entre aliados sin redistribuir resto
// 2026-10-12-2232||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Retirar cálculo obsoleto de poder del hechizo robado por sendas Dean
// 2026-10-17-0910||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Regla de contador: un avance por hechizo letal y reinicio al quinto
// 2026-10-21-2043||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Overheal ignora curaciones efectivas de 9999 o más
// 2026-10-21-2059||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/AcademicMath.cs||Limitar armadura de cada Overheal a salud máxima
