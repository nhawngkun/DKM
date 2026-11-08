# -*- coding: utf-8 -*-
"""Chuyển COMBAT_TOOLS_GUIDE.md sang DOCX (A4, tiếng Việt, bảng + code block)."""
import re
import sys

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Cm, Pt, RGBColor

SRC = "COMBAT_TOOLS_GUIDE.md"
DST = "COMBAT_TOOLS_GUIDE.docx"

INLINE = re.compile(r"(\*\*.+?\*\*|`[^`]+`)")


def set_font(run, name, size, bold=None, color=None):
    run.font.name = name
    run.font.size = Pt(size)
    if bold is not None:
        run.bold = bold
    if color is not None:
        run.font.color.rgb = color
    rpr = run._element.get_or_add_rPr()
    rfonts = rpr.find(qn("w:rFonts"))
    if rfonts is None:
        rfonts = OxmlElement("w:rFonts")
        rpr.append(rfonts)
    for attr in ("w:ascii", "w:hAnsi", "w:eastAsia", "w:cs"):
        rfonts.set(qn(attr), name)


def add_inline(paragraph, text, size=10, bold_base=False):
    for token in INLINE.split(text):
        if not token:
            continue
        if token.startswith("**") and token.endswith("**") and len(token) > 4:
            # đệ quy để xử lý `code` nằm trong **bold**
            add_inline(paragraph, token[2:-2], size, True)
        elif token.startswith("`") and token.endswith("`") and len(token) > 2:
            run = paragraph.add_run(token[1:-1])
            set_font(run, "Consolas", size - 0.5, bold_base, RGBColor(0x8B, 0x1A, 0x1A))
        else:
            run = paragraph.add_run(token)
            set_font(run, "Arial", size, bold_base)


def shade_paragraph(paragraph, color):
    ppr = paragraph._p.get_or_add_pPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), color)
    ppr.append(shd)


def shade_cell(cell, color):
    tcpr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), color)
    tcpr.append(shd)


def add_code_block(document, lines):
    for line in lines:
        paragraph = document.add_paragraph()
        paragraph.paragraph_format.left_indent = Cm(0.35)
        paragraph.paragraph_format.space_before = Pt(0)
        paragraph.paragraph_format.space_after = Pt(0)
        paragraph.paragraph_format.line_spacing = 1.0
        shade_paragraph(paragraph, "F3F3F3")
        run = paragraph.add_run(line if line.strip() else " ")
        set_font(run, "Consolas", 7.5)
    document.add_paragraph().paragraph_format.space_after = Pt(2)


def add_table(document, rows):
    header = rows[0]
    body = rows[1:]
    table = document.add_table(rows=1, cols=len(header))
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    table.autofit = True

    for index, text in enumerate(header):
        cell = table.rows[0].cells[index]
        cell.text = ""
        add_inline(cell.paragraphs[0], text, 9, bold_base=True)
        shade_cell(cell, "DCE6F1")

    for row in body:
        cells = table.add_row().cells
        for index, text in enumerate(row[: len(header)]):
            cells[index].text = ""
            add_inline(cells[index].paragraphs[0], text, 9)

    trpr = table.rows[0]._tr.get_or_add_trPr()
    header_flag = OxmlElement("w:tblHeader")
    header_flag.set(qn("w:val"), "true")
    trpr.append(header_flag)

    # không cho 1 hàng bị cắt đôi giữa 2 trang
    for row in table.rows:
        row_props = row._tr.get_or_add_trPr()
        cant_split = OxmlElement("w:cantSplit")
        cant_split.set(qn("w:val"), "true")
        row_props.append(cant_split)

    document.add_paragraph().paragraph_format.space_after = Pt(0)


def add_footer(document, section):
    paragraph = section.footer.paragraphs[0]
    paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = paragraph.add_run("Hướng dẫn 5 tool Combat & Animation — trang ")
    set_font(run, "Arial", 8, color=RGBColor(0x60, 0x60, 0x60))
    field = OxmlElement("w:fldSimple")
    field.set(qn("w:instr"), "PAGE")
    inner = OxmlElement("w:r")
    text = OxmlElement("w:t")
    text.text = "1"
    inner.append(text)
    field.append(inner)
    paragraph._p.append(field)


def main():
    with open(SRC, encoding="utf-8") as handle:
        lines = handle.read().splitlines()

    document = Document()
    section = document.sections[0]
    section.page_width = Cm(21.0)
    section.page_height = Cm(29.7)
    section.left_margin = Cm(1.7)
    section.right_margin = Cm(1.7)
    section.top_margin = Cm(1.6)
    section.bottom_margin = Cm(1.6)

    normal = document.styles["Normal"]
    normal.font.name = "Arial"
    normal.font.size = Pt(10)
    normal.paragraph_format.space_after = Pt(3)
    normal.paragraph_format.line_spacing = 1.12

    for name, size, color, space_before, space_after in (
        ("Title", 24, RGBColor(0x1F, 0x3A, 0x5F), 0, 10),
        ("Heading 1", 16, RGBColor(0x1F, 0x3A, 0x5F), 14, 6),
        ("Heading 2", 13, RGBColor(0x24, 0x4A, 0x73), 14, 5),
        ("Heading 3", 11, RGBColor(0x33, 0x33, 0x33), 10, 4),
    ):
        style = document.styles[name]
        style.font.name = "Arial"
        style.font.size = Pt(size)
        style.font.color.rgb = color
        style.font.bold = True
        style.paragraph_format.space_before = Pt(space_before)
        style.paragraph_format.space_after = Pt(space_after)
        style.paragraph_format.keep_with_next = True
        rpr = style.element.get_or_add_rPr()
        rfonts = rpr.find(qn("w:rFonts"))
        if rfonts is None:
            rfonts = OxmlElement("w:rFonts")
            rpr.append(rfonts)
        for attr in ("w:ascii", "w:hAnsi", "w:eastAsia", "w:cs"):
            rfonts.set(qn(attr), "Arial")

    for bullet_style in ("List Bullet", "List Bullet 2", "List Number"):
        style = document.styles[bullet_style]
        style.font.name = "Arial"
        style.font.size = Pt(10)
        style.paragraph_format.space_after = Pt(1)

    # Trang bìa
    title = next((line[2:].strip() for line in lines if line.startswith("# ")), "DKM")
    for _ in range(6):
        document.add_paragraph()
    cover = document.add_paragraph(title, style="Title")
    cover.alignment = WD_ALIGN_PARAGRAPH.CENTER
    subtitle = document.add_paragraph()
    subtitle.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_inline(subtitle, "Tác dụng tool · Quy trình làm việc · Cảnh báo nguy cơ · Setup nhân vật mới", 12)
    scope = document.add_paragraph()
    scope.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_inline(scope, "Áp dụng cho 5 tool trong `Assets/_Asset/Script/Editor/`", 10)
    updated = document.add_paragraph()
    updated.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_inline(updated, "Cập nhật: 08/11/2026", 10)

    document.add_page_break()

    # Mục lục
    toc = [line[3:].strip() for line in lines if line.startswith("## ")]
    document.add_paragraph("MỤC LỤC", style="Heading 1")
    for entry in toc:
        paragraph = document.add_paragraph(style="List Bullet")
        add_inline(paragraph, entry, 10)
    document.add_page_break()

    in_code = False
    code_lines = []
    table_rows = []

    def flush_table():
        if table_rows:
            add_table(document, list(table_rows))
            table_rows.clear()

    for raw in lines:
        line = raw.rstrip()

        if line.startswith("```"):
            if in_code:
                add_code_block(document, code_lines)
                code_lines.clear()
                in_code = False
            else:
                flush_table()
                in_code = True
            continue
        if in_code:
            code_lines.append(line)
            continue

        if line.startswith("|"):
            cells = [cell.strip() for cell in line.strip().strip("|").split("|")]
            if all(re.fullmatch(r":?-{2,}:?", cell) for cell in cells):
                continue
            table_rows.append(cells)
            continue
        flush_table()

        if not line.strip():
            continue
        if line.strip() == "---":
            continue

        stripped = line.strip()
        indent = len(line) - len(line.lstrip(" "))
        bullet_style = "List Bullet" if indent < 2 else "List Bullet 2"

        if stripped.startswith("# "):
            continue  # tiêu đề đã in ở trang bìa
        if stripped.startswith("## "):
            document.add_paragraph(stripped[3:], style="Heading 2")
        elif stripped.startswith("### "):
            document.add_paragraph(stripped[4:], style="Heading 3")
        elif stripped.startswith("> "):
            paragraph = document.add_paragraph()
            paragraph.paragraph_format.left_indent = Cm(0.5)
            add_inline(paragraph, stripped[2:], 9)
            for run in paragraph.runs:
                run.italic = True
        elif stripped.startswith("- [ ] "):
            paragraph = document.add_paragraph(style=bullet_style)
            add_inline(paragraph, "[ ] " + stripped[6:], 10)
        elif stripped.startswith("- "):
            paragraph = document.add_paragraph(style=bullet_style)
            add_inline(paragraph, stripped[2:], 10)
        elif re.match(r"^\d+\.\s", stripped):
            # giữ số gốc của markdown: style List Number dùng chung 1 dãy số nên sẽ chạy tiếp qua các mục
            paragraph = document.add_paragraph()
            paragraph.paragraph_format.left_indent = Cm(0.75)
            paragraph.paragraph_format.first_line_indent = Cm(-0.75)
            paragraph.paragraph_format.space_after = Pt(1)
            add_inline(paragraph, stripped, 10)
        else:
            paragraph = document.add_paragraph()
            add_inline(paragraph, stripped, 10)

    flush_table()
    add_footer(document, section)
    document.save(DST)
    print("saved", DST)


if __name__ == "__main__":
    main()
