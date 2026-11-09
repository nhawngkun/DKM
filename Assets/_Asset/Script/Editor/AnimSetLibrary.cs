using System.Collections.Generic;
using UnityEngine;

// Danh sách bộ anim nhân vật (định nghĩa AnimSetDefinition ở CombatLibraries.cs)
[CreateAssetMenu(menuName = "Combat/Anim Set Library", fileName = "AnimSetLibrary")]
public class AnimSetLibrary : ScriptableObject
{
    public List<AnimSetDefinition> Sets = new List<AnimSetDefinition>();
}
