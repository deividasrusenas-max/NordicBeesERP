const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  AlignmentType, BorderStyle, WidthType, ShadingType,
  VerticalAlign, Header, Footer, PageNumber, TabStopType
} = require('docx');
const fs = require('fs');

const C_DARK   = "1A1A2E";
const C_MID    = "4A4A6A";
const C_LIGHT  = "F5F5F7";
const C_WHITE  = "FFFFFF";
const C_BORDER = "C8C8D0";
const C_ACCENT = "2C5F8A";

const border  = { style: BorderStyle.SINGLE, size: 4, color: C_BORDER };
const borders = { top: border, bottom: border, left: border, right: border };
const noBorder  = { style: BorderStyle.NONE, size: 0, color: C_WHITE };
const noBorders = { top: noBorder, bottom: noBorder, left: noBorder, right: noBorder };

const PAGE_W    = 11906;
const MARGIN_H  = 1134;
const CONTENT_W = PAGE_W - MARGIN_H * 2; // 9638

const txt   = (t, o={}) => new TextRun({ text: t, font: "Arial", size: 20, ...o });
const bold  = (t, o={}) => txt(t, { bold: true, ...o });
const small = (t, o={}) => txt(t, { size: 17, ...o });

const para = (children, opts={}) => new Paragraph({
  children: Array.isArray(children) ? children : [children],
  spacing: { before: 0, after: 60 },
  ...opts
});
const emptyLine = (h=120) => new Paragraph({ children: [txt("")], spacing: { before: 0, after: h } });

// ── Table helpers ─────────────────────────────────────────────────────────────

const secHeader = (label, cols) => new TableRow({
  children: [new TableCell({
    columnSpan: cols,
    borders,
    shading: { fill: C_LIGHT, type: ShadingType.CLEAR },
    margins: { top: 100, bottom: 100, left: 160, right: 120 },
    width: { size: CONTENT_W, type: WidthType.DXA },
    children: [new Paragraph({
      children: [bold(label, { size: 18, color: C_MID })],
      spacing: { before: 0, after: 0 }
    })]
  })]
});

const colHeaders = (labels, widths) => new TableRow({
  children: labels.map((lbl, i) => new TableCell({
    borders,
    shading: { fill: C_LIGHT, type: ShadingType.CLEAR },
    margins: { top: 60, bottom: 60, left: 160, right: 120 },
    width: { size: widths[i], type: WidthType.DXA },
    children: [new Paragraph({
      children: [small(lbl, { bold: true, color: C_MID })],
      spacing: { before: 0, after: 0 }
    })]
  }))
});

const row2 = (a, b, w1, w2, shade=false) => new TableRow({
  children: [
    new TableCell({
      borders,
      shading: { fill: shade ? "FAFAFA" : C_WHITE, type: ShadingType.CLEAR },
      margins: { top: 80, bottom: 80, left: 160, right: 120 },
      width: { size: w1, type: WidthType.DXA },
      children: [para(small(a, { color: C_MID }))]
    }),
    new TableCell({
      borders,
      shading: { fill: shade ? "FAFAFA" : C_WHITE, type: ShadingType.CLEAR },
      margins: { top: 80, bottom: 80, left: 160, right: 120 },
      width: { size: w2, type: WidthType.DXA },
      children: [para(bold(b, { size: 19 }))]
    })
  ]
});

const row3 = (a, b, c, w1, w2, w3, shade=false) => new TableRow({
  children: [
    new TableCell({
      borders,
      shading: { fill: shade ? "FAFAFA" : C_WHITE, type: ShadingType.CLEAR },
      margins: { top: 80, bottom: 80, left: 160, right: 120 },
      width: { size: w1, type: WidthType.DXA },
      children: [para(small(a, { color: C_MID }))]
    }),
    new TableCell({
      borders,
      shading: { fill: shade ? "FAFAFA" : C_WHITE, type: ShadingType.CLEAR },
      margins: { top: 80, bottom: 80, left: 160, right: 120 },
      width: { size: w2, type: WidthType.DXA },
      children: [para(bold(b, { size: 19 }))]
    }),
    new TableCell({
      borders,
      shading: { fill: shade ? "FAFAFA" : C_WHITE, type: ShadingType.CLEAR },
      margins: { top: 80, bottom: 80, left: 160, right: 120 },
      width: { size: w3, type: WidthType.DXA },
      children: [para(small(c, { color: "888888" }))]
    })
  ]
});

const fullRow = (text, shade=false) => new TableRow({
  children: [new TableCell({
    columnSpan: 2,
    borders,
    shading: { fill: shade ? "FAFAFA" : C_WHITE, type: ShadingType.CLEAR },
    margins: { top: 80, bottom: 80, left: 160, right: 120 },
    width: { size: CONTENT_W, type: WidthType.DXA },
    children: [para(txt(text, { size: 19 }))]
  })]
});

// Column widths
const W1 = 3200;
const W2 = CONTENT_W - W1;           // 2-col tables
const P1 = 3200, P2 = 2000, P3 = CONTENT_W - P1 - P2; // 3-col param table
const N1 = 5200, N2 = CONTENT_W - N1; // nutrition

// ── DOCUMENT ──────────────────────────────────────────────────────────────────
const doc = new Document({
  styles: {
    default: { document: { run: { font: "Arial", size: 20, color: C_DARK } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 30, bold: true, font: "Arial", color: C_DARK },
        paragraph: { spacing: { before: 200, after: 160 }, outlineLevel: 0 } }
    ]
  },
  sections: [{
    properties: {
      page: {
        size: { width: PAGE_W, height: 16838 },
        margin: { top: 1134, right: MARGIN_H, bottom: 1134, left: MARGIN_H }
      }
    },

    headers: {
      default: new Header({
        children: [new Paragraph({
          border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: C_ACCENT, space: 6 } },
          spacing: { before: 0, after: 120 },
          children: [
            bold("MB LAKŠTENA", { size: 18, color: C_DARK }),
            txt("   \u00B7   Pauliaus \u0160irvio g. 3, Juodup\u0117 LT-42457   \u00B7   +370 612 24 088   \u00B7   info@9medus.lt", { size: 16, color: "888888" })
          ]
        })]
      })
    },

    footers: {
      default: new Footer({
        children: [new Paragraph({
          border: { top: { style: BorderStyle.SINGLE, size: 6, color: C_ACCENT, space: 6 } },
          spacing: { before: 80, after: 0 },
          tabStops: [{ type: TabStopType.RIGHT, position: CONTENT_W }],
          children: [
            new TextRun({ text: "Produkto specifikacija Nr. 250601  \u00B7  v1.1", font: "Arial", size: 16, color: "888888" }),
            new TextRun({ text: "\t", font: "Arial", size: 16 }),
            new TextRun({ text: "Puslapis ", font: "Arial", size: 16, color: "888888" }),
            new TextRun({ children: [PageNumber.CURRENT], font: "Arial", size: 16, color: "888888" }),
            new TextRun({ text: " / ", font: "Arial", size: 16, color: "888888" }),
            new TextRun({ children: [PageNumber.TOTAL_PAGES], font: "Arial", size: 16, color: "888888" }),
          ]
        })]
      })
    },

    children: [

      // ── TITLE BLOCK ────────────────────────────────────────────────────
      emptyLine(80),
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 0, after: 80 },
        children: [bold("PRODUKTO SPECIFIKACIJA", { size: 38, color: C_DARK })]
      }),
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 0, after: 60 },
        children: [bold("Nr. 250601", { size: 24, color: C_ACCENT })]
      }),
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 0, after: 200 },
        children: [bold("\u012EVAIRIAŽ IEDIS MEDUS", { size: 28, color: C_MID })]
      }),

      // ── META TABLE ─────────────────────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [W1, W2],
        rows: [
          row2("Kilm\u0117s \u0161alis", "Lietuva", W1, W2, false),
          row2("Pakuotojas", "MB Lak\u0161tena", W1, W2, true),
          row2("Adresas", "Pauliaus \u0160irvio g. 3, Juodup\u0117, LT-42457 Rokи\u0161kio r., Lietuva", W1, W2, false),
          row2("HS / CN muit\u0173 kodas", "0409 00 00", W1, W2, true),
          row2("Versija", "v1.1", W1, W2, false),
          row2("Per\u017Ei\u016Bros data", "2025-06-02", W1, W2, true),
          row2("Pareng\u0117 / Tvirtino", "Diana Ru\u0161\u0117nait\u0117", W1, W2, false),
        ]
      }),

      emptyLine(160),

      // ── 1. BENDRA INFORMACIJA ─────────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [W1, W2],
        rows: [
          secHeader("1. Bendra informacija", 2),
          row2("Spalva", "Šviesi geltona, geltona", W1, W2, false),
          row2("Konsistencija", "Vienalytė, tepi", W1, W2, true),
          row2("Skonis ir kvapas", "Būdingas šviesiam daugiažiedžiam medui, be pašalinio kvapo ar skonio", W1, W2, false),
        ]
      }),

      emptyLine(160),

      // ── 2. CHEMINIAI IR FIZIKINIAI PARAMETRAI ─────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [P1, P2, P3],
        rows: [
          secHeader("2. Cheminiai ir fizikiniai parametrai", 3),
          new TableRow({ children: [
            new TableCell({ columnSpan: 3, borders,
              shading: { fill: C_WHITE, type: ShadingType.CLEAR },
              margins: { top: 80, bottom: 80, left: 160, right: 120 },
              width: { size: CONTENT_W, type: WidthType.DXA },
              children: [para(small("Medaus techninis reglamentas — LR ŽŪM 2003-08-12 įsak. Nr. 3D-333; 2015-04-08 įsak. Nr. 3D-262 red.", { color: "999999" }))]
            })
          ]}),
          colHeaders(["Parametras", "Specifikacija", "Pastaba"], [P1, P2, P3]),
          row3("Drėgnumas",                  "< 20 %",              "Refraktometras",        P1, P2, P3, false),
          row3("Diastazė",                   "> 8 Gotės vnt.",       "EN 1988-2:1998",        P1, P2, P3, true),
          row3("HMF",                        "< 40 mg/kg",          "EN 1140:1994",           P1, P2, P3, false),
          row3("Sacharozė",                  "< 5 g/100 g",         "LR 3D-262",              P1, P2, P3, true),
          row3("Fruktozė + gliukozė",        "\u2265 60 g/100 g",   "LR 3D-262",              P1, P2, P3, false),
          row3("Elektrinis laidumas",        "< 0,8 mS/cm",         "EN 13040:2011",          P1, P2, P3, true),
          row3("Laisvas rūgštingumas",       "< 50 meq/kg",         "EN 1378:1997",           P1, P2, P3, false),
          row3("Netirpios priemaišos",       "< 0,1 g/100 g",       "EN 1741:2003",           P1, P2, P3, true),
          row3("Vandens turinys netirpiose priemaišose", "< 25 %",  "—",                      P1, P2, P3, false),
        ]
      }),

      emptyLine(160),

      // ── 3. MAISTINGUMO INFORMACIJA ─────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [N1, N2],
        rows: [
          secHeader("3. Maistingumo informacija (100 g produkto)", 2),
          new TableRow({ children: [
            new TableCell({ columnSpan: 2, borders,
              shading: { fill: C_WHITE, type: ShadingType.CLEAR },
              margins: { top: 60, bottom: 60, left: 160, right: 120 },
              width: { size: CONTENT_W, type: WidthType.DXA },
              children: [para(small("Pagal ES Reg. Nr. 1169/2011 (tipinės vertės įvairiažiedžiam medui)", { color: "999999" }))]
            })
          ]}),
          row2("Energin\u0117 vert\u0117", "1 406 kJ / 331 kcal", N1, N2, false),
          row2("Riebalai", "0 g", N1, N2, true),
          row2("\u2014 i\u0161 kuri\u0173 so\u010Di\u0173j\u0173 riebal\u0173 r\u016Bg\u0161\u010Di\u0173", "0 g", N1, N2, false),
          row2("Angliavandeniai", "82,4 g", N1, N2, true),
          row2("\u2014 i\u0161 kuri\u0173 cukr\u0173", "82,1 g", N1, N2, false),
          row2("Skaidulin\u0117s med\u017Eiagos", "0 g", N1, N2, true),
          row2("Baltymai", "0,3 g", N1, N2, false),
          row2("Druska", "0 g", N1, N2, true),
        ]
      }),

      emptyLine(160),

      // ── 4. ALERGENAI IR GMO ────────────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [W1, W2],
        rows: [
          secHeader("4. Alergenai ir GMO", 2),
          row2("Alergenai  (ES Reg. Nr. 1169/2011, II priedas)", "N\u0117ra", W1, W2, false),
          row2("GMO  (ES Reg. Nr. 1829/2003 ir Nr. 1830/2003)", "N\u0117ra", W1, W2, true),
        ]
      }),

      emptyLine(160),

      // ── 5. ĮPAKAVIMAS ─────────────────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [W1, W2],
        rows: [
          secHeader("5. \u012Epakavimas", 2),
          row2("Pakuot\u0117 / Talpa", "Plastikinis kibiras, 25 kg", W1, W2, false),
          row2("Maisto kontaktin\u0117s med\u017Eiagos", "Atitinka (EB) Nr.\u00A01935/2004, (ES) Nr.\u00A0174/2015, (ES) Nr.\u00A02023/2006", W1, W2, true),
        ]
      }),

      emptyLine(160),

      // ── 6. LAIKYMO IR TRANSPORTO SĄLYGOS ──────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [W1, W2],
        rows: [
          secHeader("6. Laikymo ir transporto s\u0105lygos", 2),
          row2("Laikymo temperat\u016Bra", "\u2264 25\u00A0\u00B0C", W1, W2, false),
          row2("Laikymo vieta", "Sausa, tamsi patalpa. Saugoti nuo tiesiogini\u0173 saul\u0117s spinduliams.", W1, W2, true),
          row2("Santykin\u0117 dr\u0117gm\u0117", "50\u201370\u00A0%", W1, W2, false),
          row2("Tinkamumo vartoti trukm\u0117", "24 m\u0117nesiai nuo pakavimo dienos", W1, W2, true),
          row2("Transportavimas", "Dengtas transportas \u2264\u00A025\u00A0\u00B0C, be kontakto su kvapiais produktais", W1, W2, false),
        ]
      }),

      emptyLine(160),

      // ── 7. TEISINĖ ATITIKTIS ───────────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [CONTENT_W],
        rows: [
          secHeader("7. Teisin\u0117 atitiktis", 1),
          new TableRow({ children: [new TableCell({
            borders,
            shading: { fill: C_WHITE, type: ShadingType.CLEAR },
            margins: { top: 100, bottom: 100, left: 160, right: 120 },
            width: { size: CONTENT_W, type: WidthType.DXA },
            children: [
              para(txt("Produktas atitinka Lietuvos Respublikos ir Europos S\u0105jungos galiojan\u010Dius teisinius reikalavimus:", { size: 19 })),
              para(small("\u2022  LR \u017D\u016AM 2003-08-12 \u012Fsak. Nr.\u00A03D-333 (Medaus techninis reglamentas)", { color: C_MID })),
              para(small("\u2022  LR \u017D\u016AM 2015-04-08 \u012Fsak. Nr.\u00A03D-262 (Redakcija)", { color: C_MID })),
              para(small("\u2022  ES Tarybos direktyva 2001/110/EB (2001-12-20) \u2014 medaus standartas", { color: C_MID })),
              para(small("\u2022  ES Reg. Nr.\u00A01169/2011 \u2014 maisto \u017Enklinimas", { color: C_MID })),
              para(small("\u2022  ES Reg. Nr.\u00A01935/2004, Nr.\u00A0174/2015, Nr.\u00A02023/2006 \u2014 pakuot\u0117s atitiktis", { color: C_MID })),
            ]
          })]})
        ]
      }),

      emptyLine(280),

      // ── SIGNATURE ─────────────────────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [Math.floor(CONTENT_W * 0.45), CONTENT_W - Math.floor(CONTENT_W * 0.45)],
        rows: [new TableRow({
          children: [
            new TableCell({
              borders: noBorders,
              margins: { top: 0, bottom: 0, left: 0, right: 120 },
              width: { size: Math.floor(CONTENT_W * 0.45), type: WidthType.DXA },
              children: [
                new Paragraph({ border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: C_BORDER, space: 8 } }, spacing: { before: 0, after: 140 }, children: [txt(" ")] }),
                para(small("Diana Rušėnaitė", { color: C_MID, bold: true })),
                para(small("Vardas, pavardė / parašas", { color: "999999" }))
              ]
            }),
            new TableCell({
              borders: noBorders,
              margins: { top: 0, bottom: 0, left: 120, right: 0 },
              width: { size: CONTENT_W - Math.floor(CONTENT_W * 0.45), type: WidthType.DXA },
              children: [
                new Paragraph({ border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: C_BORDER, space: 8 } }, spacing: { before: 0, after: 140 }, children: [txt(" ")] }),
                para(small("2025-06-02", { color: C_MID, bold: true })),
                para(small("Data", { color: "999999" }))
              ]
            })
          ]
        })]
      }),

    ]
  }]
});

Packer.toBuffer(doc).then(buffer => {
  fs.writeFileSync("/home/claude/produkto_spec_medus_v1.1.docx", buffer);
  console.log("Done");
});
