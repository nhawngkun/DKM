using System.Collections.Generic;
using UnityEngine;

// Danh sách vũ khí (định nghĩa WeaponDefinition ở CombatLibraries.cs)
[CreateAssetMenu(menuName = "Combat/Weapon Library", fileName = "WeaponLibrary")]
public class WeaponLibrary : ScriptableObject
{
    public List<WeaponDefinition> Weapons = new List<WeaponDefinition>();
}
