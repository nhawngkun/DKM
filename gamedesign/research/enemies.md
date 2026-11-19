# Research: thiết kế địch (2026-11-14)

Phục vụ topic "thiết kế địch từ 4 model trong `Assets/_Asset/3D/_Enemy`" (tiếp theo D-005).

## Asset hiện có

| Folder | Model | Rig (import hiện tại) | Anim có sẵn | Ghi chú |
|---|---|---|---|---|
| `01- Low.Poly.Ghosts` | Ghost-samurai: mặt nạ quỷ đỏ, sừng vàng, áo choàng rách, katana | Generic, xương tên `mixamorig:` -> đổi sang Humanoid được ngay | không | low poly, rẻ để nhân nhiều con |
| `01- Oni.Samurai` | Quỷ da đỏ, tóc trắng, cởi trần, giáp vai, dao/kiếm ngắn | Generic, chưa rõ tên xương | không | cần kiểm tra rig trong Editor |
| `01- Undead.samurai` | Xác sống to con, tay quái vật có móng, giáp, nodachi rất dài | Generic, chưa rõ tên xương | không | dáng to, tầm với dài |
| `02- Hell.Executioner` | Quỷ lửa, vác chùy gai, nham thạch phát sáng | Humanoid | `HEll_Execution`, `Idle_ver_B` (Boss_1.controller) | đã định là boss |

## Tham chiếu

- **[Nguồn]** Sekiro: deflect trong khoảng 0.5s trước khi đòn chạm thì gây posture damage và mở cửa phản đòn; đòn nguy hiểm (Perilous) báo bằng ký hiệu đỏ, không đỡ được, mỗi loại có cách trả lời riêng (đâm -> Mikiri, quét -> nhảy, túm -> né). https://sekiroshadowsdietwice.wiki.fextralife.com/Combat , https://samurai-gamers.com/?p=138085
- **[Nguồn]** Người chơi Sekiro tranh cãi ký hiệu che tầm nhìn, nên đọc thân địch. https://steamcommunity.com/app/814380/discussions/0/594017161232558945
- **[Nguồn]** DMC5: Itsuno nói đã chỉnh đòn đánh của địch ngoài màn hình và hit detection của né. https://gamesradar.com/devil-may-cry-5-preview-it-could-be-the-most-fun-were-going-to-have-from-an-action-game-in-2019
- **[Nguồn]** DMC5 Death Scissors (độ khó SoS) buộc người chơi canh nhịp đòn để phản đòn, có thể giết 1 đòn. https://www.playstationlifestyle.net/?p=775689
- **[Nguồn, thứ cấp]** God of War 2018: hệ "aggression score/token" giới hạn số địch tấn công, cố định theo độ khó và loại địch; vẫn khó với địch ngoài màn hình (tóm tắt talk GDC 2019 của Jason McDonald). https://wp.wpi.edu/imgd-library/218887/media/taking-an-axe-to-god-of-war-gameplay , https://videohighlight.com/v/hE5tWF-Ou2k
- **[Nguồn, thứ cấp]** DOOM 2016: mỗi loại đòn có số token giới hạn tại một thời điểm. https://www.strayspark.studio/blog/attack-token-system-ue5-group-combat-that-feels-fair
- GDC Vault: "Preparing AI Systems for God of War Ragnarök" https://gdcvault.com/play/1029286/Preparing-AI-Systems-for-God (chưa đọc nội dung)

## Rút ra cho DKM

1. Telegraph phải tỉ lệ với sát thương; đòn không đỡ được cần tín hiệu riêng, nhưng tín hiệu nằm trên thân địch (glow vũ khí) chứ không che màn hình. [Suy luận từ Sekiro + D-006]
2. Giới hạn số địch cùng đánh (token) là cách chuẩn để đánh đám đông mà vẫn công bằng. [Nguồn thứ cấp]
3. Địch ngoài màn hình phải đánh ít hơn hoặc báo trước. [Nguồn]
4. Mỗi loại địch nên "hỏi" người chơi một câu hỏi khác nhau, để 3 nhân vật có lý do dùng động từ riêng. [Suy luận]
