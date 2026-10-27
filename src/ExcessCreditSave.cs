using System;
using System.Collections.Generic;
using Conductor.Interfaces;
using UnityEngine;

namespace WwInc;

// Conductor saves this alongside the native deck and restores after LoadDeckFromFile.
// Recompute indices at each save: purging/reordering cards cannot invalidate stored live links.
internal sealed class ExcessCreditSave : ISaveData
{
    [Serializable]
    public sealed class Entry
    {
        public int Spell;
        public int Owner;
        public string SpellData="";
        public string OwnerData="";
    }
    [Serializable]
    public sealed class Snapshot { public List<Entry> Entries=new(); }
    public string Key=>"extra-credit-origins";
    public bool ShouldSerialize()=>AllGameManagers.Instance!=null&&ExcessCredit.Origins.Count>0;
    public void Reset()=>ExcessCredit.Origins.Clear();
    public string Serialize()
    {
        var managers=AllGameManagers.Instance??throw new InvalidOperationException("Game managers unavailable for Extra Credit persistence.");
        var deck=managers.GetSaveManager().GetDeckState();
        var snapshot=new Snapshot();
        foreach(var pair in ExcessCredit.Origins)
        {
            int spell=deck.IndexOf(pair.Key),owner=deck.IndexOf(pair.Value);
            if(spell<0||owner<0)continue;
            snapshot.Entries.Add(new Entry {Spell=spell,Owner=owner,
                SpellData=pair.Key.GetCardDataID(),OwnerData=pair.Value.GetCardDataID()});
        }
        return JsonUtility.ToJson(snapshot);
    }
    public void Deserialize(string payload)
    {
        Reset();
        var snapshot=JsonUtility.FromJson<Snapshot>(payload);
        if(snapshot?.Entries==null)return;
        var managers=AllGameManagers.Instance??throw new InvalidOperationException("Game managers unavailable for Extra Credit persistence.");
        var deck=managers.GetSaveManager().GetDeckState();
        foreach(var entry in snapshot.Entries)
        {
            if(entry.Spell<0||entry.Spell>=deck.Count||entry.Owner<0||entry.Owner>=deck.Count)continue;
            var spell=deck[entry.Spell];var owner=deck[entry.Owner];
            if(!ExcessCredit.IsCredit(spell)||spell.GetCardDataID()!=entry.SpellData||
                owner.GetCardDataID()!=entry.OwnerData||owner.GetCardType()!=CardType.Monster)continue;
            ExcessCredit.Origins[spell]=owner;
        }
    }
}
// 2026-10-24-1923||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ExcessCreditSave.cs||Persistir autoría por Conductor ISaveData y posiciones verificadas del mazo nativo, restauradas tras carga
// 2026-10-24-1925||codex-ww-inc||repos/MT2/repos-propios/mt2-ww-inc/src/ExcessCreditSave.cs||Comprobar managers al serializar/restaurar y eliminar dos avisos nullable sin ocultar fallos de carga
// 2026-10-27-1428||codex-ww-inc||salidas/build/ww-inc/src/ExcessCreditSave.cs||Guardar y restaurar autoría Extra Credit mediante SaveManager persistente disponible en mapa
