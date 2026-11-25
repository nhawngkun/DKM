using System;
using System.Collections.Generic;
using UnityEngine;

// các thanh trượt hình dạng mặt: vùng ảnh hưởng đặt theo 468 landmark MediaPipe (a),
// rồi tính delta tại bất kỳ tập điểm nào (p) - điểm neo để khớp, đỉnh lưới để hiển thị.
// mỗi thanh trượt tuyến tính: đỉnh = gốc + giá trị * delta.
public static class FaceSliders
{
    static readonly int[] RightEye = { 33, 7, 163, 144, 145, 153, 154, 155, 133, 173, 157, 158, 159, 160, 161, 246 };
    static readonly int[] LeftEye = { 263, 249, 390, 373, 374, 380, 381, 382, 362, 398, 384, 385, 386, 387, 388, 466 };
    static readonly int[] RightBrow = { 46, 53, 52, 65, 55, 70, 63, 105, 66, 107 };
    static readonly int[] LeftBrow = { 276, 283, 282, 295, 285, 300, 293, 334, 296, 336 };

    public static List<(string name, Vector3[] delta)> Build(Vector3[] v, Vector3[] points)
    {
        var list = new List<(string, Vector3[])>();
        float r = Mathf.Abs(v[234].x - v[454].x);
        Vector3 mouth = (v[13] + v[14]) * 0.5f;
        Vector3 chin = v[152], noseBase = v[2], noseTip = v[4];
        Vector3 eyeR = Mean(v, RightEye), eyeL = Mean(v, LeftEye);
        Vector3 browR = Mean(v, RightBrow), browL = Mean(v, LeftBrow);

        void Add(string name, Func<Vector3, Vector3> f)
        {
            var d = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) d[i] = f(points[i]);
            list.Add((name, d));
        }

        // vùng đối xứng: lấy tâm cùng phía với đỉnh, side = +1 bên trái nhân vật (x dương)
        void AddPair(string name, Vector3 cR, Vector3 cL, float radius, Func<Vector3, Vector3, float, Vector3> f)
        {
            Add(name, p =>
            {
                Vector3 c = p.x < 0 ? cR : cL;
                float w = Falloff(p, c, radius);
                return w <= 0 ? Vector3.zero : f(p, c, Mathf.Sign(c.x)) * w;
            });
        }

        Add("Rộng mặt", p => new Vector3(p.x * 0.12f, 0, 0));
        Add("Dài mặt", p => new Vector3(0, (p.y - v[1].y) * 0.10f, 0));
        Add("Rộng hàm", p =>
        {
            float w = Smooth01((mouth.y - p.y) / (mouth.y - chin.y));
            return new Vector3(p.x * 0.2f * w, 0, 0);
        });
        Add("Dài cằm", p => new Vector3(0, -0.08f * r * Falloff(p, chin, 0.35f * r), 0));
        Add("Rộng cằm", p => new Vector3((p.x - chin.x) * 0.4f * Falloff(p, chin, 0.3f * r), 0, 0));
        AddPair("Gò má", v[116], v[345], 0.3f * r, (p, c, s) => new Vector3(s * 0.06f * r, 0, 0));
        AddPair("Khoảng cách mắt", eyeR, eyeL, 0.22f * r, (p, c, s) => new Vector3(s * 0.06f * r, 0, 0));
        AddPair("Cỡ mắt", eyeR, eyeL, 0.2f * r, (p, c, s) => new Vector3(p.x - c.x, p.y - c.y, 0) * 0.35f);
        AddPair("Độ xếch mắt", eyeR, eyeL, 0.2f * r, (p, c, s) => new Vector3(-(p.y - c.y), p.x - c.x, 0) * (0.25f * s));
        AddPair("Cao mắt", eyeR, eyeL, 0.22f * r, (p, c, s) => new Vector3(0, 0.05f * r, 0));
        AddPair("Cao lông mày", browR, browL, 0.22f * r, (p, c, s) => new Vector3(0, 0.06f * r, 0));
        Add("Dài mũi", p => new Vector3(0, -0.07f * r * Falloff(p, noseBase, 0.18f * r), 0));
        Add("Rộng mũi", p => new Vector3(p.x * 0.35f * Falloff(p, noseBase, 0.2f * r), 0, 0));
        // mặt nhìn về -z nên nhô ra là -z
        Add("Cao sống mũi", p => new Vector3(0, 0, -0.06f * r * Falloff(p, noseTip, 0.22f * r)));
        Add("Rộng miệng", p => new Vector3((p.x - mouth.x) * 0.3f * Falloff(p, mouth, 0.3f * r), 0, 0));
        Add("Cao miệng", p => new Vector3(0, 0.05f * r * Falloff(p, mouth, 0.3f * r), 0));
        Add("Dày môi", p => new Vector3(0, (p.y - mouth.y) * 0.5f * Falloff(p, mouth, 0.25f * r), 0));
        return list;
    }

    static Vector3 Mean(Vector3[] v, int[] ids)
    {
        Vector3 sum = Vector3.zero;
        foreach (int i in ids) sum += v[i];
        return sum / ids.Length;
    }

    static float Falloff(Vector3 p, Vector3 c, float radius) => Smooth01(1f - Vector3.Distance(p, c) / radius);

    static float Smooth01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
