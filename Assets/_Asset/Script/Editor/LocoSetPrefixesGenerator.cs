using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Sinh LocoSetPrefixes.Values từ AnimSetLibrary để runtime biết prefix của từng bộ anim
/// (LocoStates dùng prefix để ghép hash state, CharacterAnim.SetWeapon kiểm tra AnimSetId hợp lệ).
/// Chạy lại mỗi khi thêm / bớt / đổi prefix bộ anim trong asset.
/// </summary>
public static class LocoSetPrefixesGenerator
{
    public const string Path = "Assets/_Asset/Script/Contanst/LocoSetPrefixes.Generated.cs";

    [MenuItem("Tools/Combat/Generate Loco Set Prefixes")]
    public static void Generate()
    {
        Generate(LocomotionAnimatorBuilder.FindLibrary());
    }

    public static void Generate(AnimSetLibrary library)
    {
        if (library == null || library.Sets.Count == 0)
        {
            Debug.LogError("[LocoSetPrefixesGenerator] AnimSetLibrary rỗng — không sinh prefix được.");
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("// FILE NÀY ĐƯỢC SINH TỰ ĐỘNG — ĐỪNG SỬA TAY.");
        builder.AppendLine("// Nguồn: " + LocomotionAnimatorBuilder.LibraryPath);
        builder.AppendLine("// Sinh lại: Tools/Combat/Timing Window > tab 'Bộ anim & Vũ khí' > Sinh prefix.");
        builder.AppendLine("public static class LocoSetPrefixes");
        builder.AppendLine("{");
        builder.Append("    public static readonly string[] Values = { ");
        for (int index = 0; index < library.Sets.Count; index++)
        {
            if (index > 0) builder.Append(", ");
            builder.Append('"').Append(library.Sets[index].Prefix).Append('"');
        }
        builder.AppendLine(" };");
        builder.AppendLine("}");

        File.WriteAllText(Path, builder.ToString());
        AssetDatabase.ImportAsset(Path);
        Debug.Log($"[LocoSetPrefixesGenerator] Đã sinh {library.Sets.Count} prefix vào {Path}");
    }
}
