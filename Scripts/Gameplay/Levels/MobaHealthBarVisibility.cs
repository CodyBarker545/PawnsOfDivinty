using System;
using System.Collections.Generic;
using System.Linq;

namespace PawnsOfDivinity.Gameplay.Levels;

/// <summary>Stateless rendering filter. The caller supplies the existing selection and the active
/// command's validated target; this never chooses targets or determines friendly/enemy legality.</summary>
public static class MobaHealthBarVisibility
{
    public static IReadOnlySet<string> Resolve(string? selectedId,string? targetId,IEnumerable<MobaUnitPresentation> presentedUnits)
    {
        var living=presentedUnits.Where(u=>!u.Defeated).Select(u=>u.Id).ToHashSet(StringComparer.Ordinal);
        var visible=new HashSet<string>(StringComparer.Ordinal);
        if(selectedId is null||!living.Contains(selectedId))return visible;
        visible.Add(selectedId);
        if(targetId is not null&&living.Contains(targetId))visible.Add(targetId);
        return visible;
    }
}
