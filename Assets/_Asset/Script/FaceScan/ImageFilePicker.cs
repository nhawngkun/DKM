using System;
using System.Runtime.InteropServices;
using UnityEngine;

// hộp thoại chọn ảnh trong máy: editor dùng EditorUtility, bản build Windows gọi thẳng comdlg32
public static class ImageFilePicker
{
    public static string Open(string title)
    {
#if UNITY_EDITOR
        return UnityEditor.EditorUtility.OpenFilePanel(title, "", "jpg,jpeg,png");
#elif UNITY_STANDALONE_WIN
        var ofn = new OpenFileName
        {
            filter = "Ảnh chân dung (*.jpg;*.jpeg;*.png)\0*.jpg;*.jpeg;*.png\0\0",
            file = new string('\0', 1024),
            title = title,
            flags = 0x00001000 | 0x00000800 | 0x00000008, // FILEMUSTEXIST | PATHMUSTEXIST | NOCHANGEDIR
        };
        ofn.maxFile = ofn.file.Length;
        ofn.structSize = Marshal.SizeOf(ofn);
        return GetOpenFileName(ofn) ? ofn.file.TrimEnd('\0') : null;
#else
        Debug.LogError("ImageFilePicker: chỉ hỗ trợ Editor và Windows");
        return null;
#endif
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    class OpenFileName
    {
        public int structSize;
        public IntPtr dlgOwner, instance;
        public string filter, customFilter;
        public int maxCustFilter, filterIndex;
        public string file;
        public int maxFile;
        public string fileTitle;
        public int maxFileTitle;
        public string initialDir, title;
        public int flags;
        public short fileOffset, fileExtension;
        public string defExt;
        public IntPtr custData, hook;
        public string templateName;
        public IntPtr reservedPtr;
        public int reservedInt, flagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
    static extern bool GetOpenFileName([In, Out] OpenFileName ofn);
}
