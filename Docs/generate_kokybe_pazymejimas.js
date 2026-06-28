const {
  Document, Packer, Paragraph, TextRun, Table, TableRow, TableCell,
  AlignmentType, HeadingLevel, BorderStyle, WidthType, ShadingType,
  VerticalAlign, Header, Footer, PageNumber, TabStopType, TabStopPosition
} = require('docx');
const fs = require('fs');

// ─── Color palette (minimal, professional) ───────────────────────────────────
const C_DARK   = "1A1A2E";   // heading text / borders
const C_MID    = "4A4A6A";   // subheading text
const C_LIGHT  = "F5F5F7";   // header row shading
const C_WHITE  = "FFFFFF";
const C_BORDER = "C8C8D0";   // table borders
const C_ACCENT = "2C5F8A";   // accent line color

const border = { style: BorderStyle.SINGLE, size: 4, color: C_BORDER };
const borders = { top: border, bottom: border, left: border, right: border };
const noBorder = { style: BorderStyle.NONE, size: 0, color: C_WHITE };
const noBorders = { top: noBorder, bottom: noBorder, left: noBorder, right: noBorder };
const accentBorder = { style: BorderStyle.SINGLE, size: 12, color: C_ACCENT };

// ─── Page layout ─────────────────────────────────────────────────────────────
// A4: 11906 x 16838 DXA, margins 1.2cm top/bottom, 2cm left/right
// Content width = 11906 - (2*1134) = 9638 DXA  (1134 ≈ 0.8in, let's use 1280 each side)
const PAGE_W = 11906;
const MARGIN_H = 1134;  // ~2cm
const MARGIN_V = 1134;
const CONTENT_W = PAGE_W - MARGIN_H * 2; // 9638

// ─── Helpers ─────────────────────────────────────────────────────────────────
const txt = (text, opts = {}) => new TextRun({ text, font: "Arial", size: 20, ...opts });
const bold = (text, opts = {}) => txt(text, { bold: true, ...opts });
const small = (text, opts = {}) => txt(text, { size: 17, ...opts });
const gray = (text, opts = {}) => txt(text, { color: "888888", ...opts });

const para = (children, opts = {}) => new Paragraph({
  children: Array.isArray(children) ? children : [children],
  spacing: { before: 0, after: 60 },
  ...opts
});

const emptyLine = (height = 120) => new Paragraph({
  children: [txt("")],
  spacing: { before: 0, after: height }
});

// Header row for table sections
const sectionHeaderRow = (label, colSpan, colWidths) => new TableRow({
  children: [
    new TableCell({
      columnSpan: colSpan,
      borders,
      shading: { fill: C_LIGHT, type: ShadingType.CLEAR },
      margins: { top: 100, bottom: 100, left: 160, right: 120 },
      width: { size: CONTENT_W, type: WidthType.DXA },
      children: [new Paragraph({
        children: [bold(label, { size: 18, color: C_MID })],
        spacing: { before: 0, after: 0 }
      })]
    })
  ]
});

const dataRow = (label, value, w1, w2, shade = false) => new TableRow({
  children: [
    new TableCell({
      borders,
      shading: { fill: shade ? "FAFAFA" : C_WHITE, type: ShadingType.CLEAR },
      margins: { top: 80, bottom: 80, left: 160, right: 120 },
      width: { size: w1, type: WidthType.DXA },
      children: [para(small(label, { color: C_MID }))]
    }),
    new TableCell({
      borders,
      shading: { fill: shade ? "FAFAFA" : C_WHITE, type: ShadingType.CLEAR },
      margins: { top: 80, bottom: 80, left: 160, right: 120 },
      width: { size: w2, type: WidthType.DXA },
      children: [para(bold(value, { size: 19 }))]
    })
  ]
});

// ─── MAIN DOCUMENT ───────────────────────────────────────────────────────────
const doc = new Document({
  styles: {
    default: {
      document: { run: { font: "Arial", size: 20, color: C_DARK } }
    },
    paragraphStyles: [
      {
        id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 30, bold: true, font: "Arial", color: C_DARK },
        paragraph: { spacing: { before: 200, after: 160 }, outlineLevel: 0 }
      },
      {
        id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 22, bold: true, font: "Arial", color: C_MID },
        paragraph: { spacing: { before: 240, after: 120 }, outlineLevel: 1 }
      }
    ]
  },
  sections: [{
    properties: {
      page: {
        size: { width: PAGE_W, height: 16838 },
        margin: { top: MARGIN_V, right: MARGIN_H, bottom: MARGIN_V, left: MARGIN_H }
      }
    },

    headers: {
      default: new Header({
        children: [
          new Paragraph({
            border: { bottom: { style: BorderStyle.SINGLE, size: 6, color: C_ACCENT, space: 6 } },
            spacing: { before: 0, after: 120 },
            children: [
              bold("MB LAKŠTENA", { size: 18, color: C_DARK }),
              txt("   ·   Pauliaus Širvio g. 3, Juodupė LT-42457   ·   +370 612 24 088   ·   info@9medus.lt", { size: 16, color: "888888" })
            ]
          })
        ]
      })
    },

    footers: {
      default: new Footer({
        children: [
          new Paragraph({
            border: { top: { style: BorderStyle.SINGLE, size: 6, color: C_ACCENT, space: 6 } },
            spacing: { before: 80, after: 0 },
            tabStops: [{ type: TabStopType.RIGHT, position: CONTENT_W }],
            children: [
              new TextRun({ text: "Kokybės pažymėjimas Nr. 2602192", font: "Arial", size: 16, color: "888888" }),
              new TextRun({ text: "\t", font: "Arial", size: 16 }),
              new TextRun({ text: "Puslapis ", font: "Arial", size: 16, color: "888888" }),
              new TextRun({ children: [PageNumber.CURRENT], font: "Arial", size: 16, color: "888888" }),
              new TextRun({ text: " / ", font: "Arial", size: 16, color: "888888" }),
              new TextRun({ children: [PageNumber.TOTAL_PAGES], font: "Arial", size: 16, color: "888888" }),
            ]
          })
        ]
      })
    },

    children: [

      // ── TITLE BLOCK ──────────────────────────────────────────────────────
      emptyLine(80),
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 0, after: 80 },
        children: [bold("KOKYBĖS PAŽYMĖJIMAS", { size: 38, color: C_DARK })]
      }),
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 0, after: 60 },
        children: [bold("Nr. 2602192", { size: 24, color: C_ACCENT })]
      }),
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 0, after: 200 },
        children: [txt("2026-05-29", { size: 20, color: "666666" })]
      }),
      new Paragraph({
        alignment: AlignmentType.CENTER,
        spacing: { before: 0, after: 240 },
        children: [bold("MEDUS", { size: 28, color: C_MID })]
      }),

      // ── SECTION 1: BENDRA INFORMACIJA ────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [3500, CONTENT_W - 3500],
        rows: [
          sectionHeaderRow("Bendra informacija", 2),
          dataRow("Pakuotojas", "MB Lakštena", 3500, CONTENT_W - 3500, false),
          dataRow("Adresas", "Pauliaus Širvio g. 3, Juodupė, LT-42457 Rokiškio r., Lietuva", 3500, CONTENT_W - 3500, true),
          dataRow("Partijos numeris", "2602171-4", 3500, CONTENT_W - 3500, false),
          dataRow("Partijos dydis", "3 480 kg", 3500, CONTENT_W - 3500, true),
          dataRow("Kilmės šalis", "Kinija", 3500, CONTENT_W - 3500, false),
          dataRow("Spalva", "Šviesiai geltona, geltona", 3500, CONTENT_W - 3500, true),
          dataRow("Konsistencija", "Vienalytė, tepi", 3500, CONTENT_W - 3500, false),
          dataRow("Skonis ir kvapas", "Būdingas įvairiažiedžiam medui", 3500, CONTENT_W - 3500, true),
        ]
      }),

      emptyLine(160),

      // ── SECTION 2: CHEMINIAI IR FIZIKINIAI PARAMETRAI ────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [3500, CONTENT_W - 3500],
        rows: [
          sectionHeaderRow("Cheminiai ir fizikiniai parametrai", 2),
          // Sub-header: norma reference
          new TableRow({
            children: [new TableCell({
              columnSpan: 2,
              borders,
              shading: { fill: C_WHITE, type: ShadingType.CLEAR },
              margins: { top: 80, bottom: 80, left: 160, right: 120 },
              width: { size: CONTENT_W, type: WidthType.DXA },
              children: [new Paragraph({
                children: [small("Medaus techninis reglamentas — LR ŽŪM 2003-08-12 įsak. Nr. 3D-333; 2015-04-08 įsak. Nr. 3D-262 red.", { color: "999999" })],
                spacing: { before: 0, after: 0 }
              })]
            })]
          }),
          // column sub-headers
          new TableRow({
            children: [
              new TableCell({
                borders,
                shading: { fill: C_LIGHT, type: ShadingType.CLEAR },
                margins: { top: 60, bottom: 60, left: 160, right: 120 },
                width: { size: 3500, type: WidthType.DXA },
                children: [new Paragraph({ children: [small("Parametras / Norma", { bold: true, color: C_MID })], spacing: { before: 0, after: 0 } })]
              }),
              new TableCell({
                borders,
                shading: { fill: C_LIGHT, type: ShadingType.CLEAR },
                margins: { top: 60, bottom: 60, left: 160, right: 120 },
                width: { size: CONTENT_W - 3500, type: WidthType.DXA },
                children: [new Paragraph({ children: [small("Rezultatas", { bold: true, color: C_MID })], spacing: { before: 0, after: 0 } })]
              })
            ]
          }),
          dataRow("Drėgnumas  (< 20 %)", "17,82 %", 3500, CONTENT_W - 3500, false),
          dataRow("Diastazė  (> 8 Gotės vnt.)", "27,7 Gotės vnt.", 3500, CONTENT_W - 3500, true),
          dataRow("HMF  (< 40 mg/kg)", "3,6 mg/kg", 3500, CONTENT_W - 3500, false),
          dataRow("Sacharozė  (< 5 g/100 g)", "1,47 g/100 g", 3500, CONTENT_W - 3500, true),
          dataRow("Fruktozė", "42,42 %", 3500, CONTENT_W - 3500, false),
          dataRow("Gliukozė", "27,7 %", 3500, CONTENT_W - 3500, true),
        ]
      }),

      emptyLine(160),

      // ── SECTION 3: LABORATORINIAI TYRIMAI ────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [3500, CONTENT_W - 3500],
        rows: [
          sectionHeaderRow("Laboratoriniai tyrimai", 2),
          dataRow("Izotopų analizė (EA/LC-IRMS)", "Intertek Food Services GmbH, Bremen", 3500, CONTENT_W - 3500, false),
          dataRow("Ataskaitos Nr.", "2603020824A  (2026-03-18)", 3500, CONTENT_W - 3500, true),
          dataRow("C4 cukrų kiekis", "0,00 %  (norma ≤ 7,0 %)", 3500, CONTENT_W - 3500, false),
          dataRow("Delta δ¹³C (F−G)", "0,14 ‰  (norma ≤ ±1,0 ‰)", 3500, CONTENT_W - 3500, true),
          dataRow("Delta δ¹³C (maks.)", "1,19 ‰  (norma ≤ ±2,1 ‰)", 3500, CONTENT_W - 3500, false),
          dataRow("Svetimi cukrūs", "Nerasta", 3500, CONTENT_W - 3500, true),
          dataRow("Kompleksinė analizė (daugelis param.)", "Qinhuangdao Customs District Lab, Nr. 13010WTH202601189", 3500, CONTENT_W - 3500, false),
          dataRow("Antibiotikai / pesticidai (~60 medž.)", "Nerasta (nerasta nei viena medžiaga)", 3500, CONTENT_W - 3500, true),
        ]
      }),

      emptyLine(160),

      // ── SECTION 4: ĮPAKAVIMAS IR LAIKYMAS ────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [3500, CONTENT_W - 3500],
        rows: [
          sectionHeaderRow("Įpakavimas ir laikymo sąlygos", 2),
          dataRow("Pakuotė", "Plastikinis kibiras, 29 kg", 3500, CONTENT_W - 3500, false),
          dataRow("Pakuotės atitiktis", "(EB) Nr. 1935/2004 · (ES) Nr. 174/2015 · (ES) Nr. 2023/2006", 3500, CONTENT_W - 3500, true),
          dataRow("Laikymo temperatūra", "Ne aukštesnė kaip 25 °C", 3500, CONTENT_W - 3500, false),
          dataRow("Laikymo vieta", "Sausa, tamsi vieta. Saugoti nuo tiesioginių saulės spindulių.", 3500, CONTENT_W - 3500, true),
          dataRow("Santykinė drėgmė", "50–70 %", 3500, CONTENT_W - 3500, false),
          dataRow("Tinkamumo trukmė", "24 mėnesiai nuo pakavimo dienos", 3500, CONTENT_W - 3500, true),
        ]
      }),

      emptyLine(160),

      // ── SECTION 5: TEISINĖ ATITIKTIS ─────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [CONTENT_W],
        rows: [
          sectionHeaderRow("Teisinė atitiktis", 1),
          new TableRow({
            children: [new TableCell({
              borders,
              shading: { fill: C_WHITE, type: ShadingType.CLEAR },
              margins: { top: 100, bottom: 100, left: 160, right: 120 },
              width: { size: CONTENT_W, type: WidthType.DXA },
              children: [new Paragraph({
                children: [txt("Produktas atitinka Lietuvos Respublikos ir Europos Sąjungos galiojančius teisinius reikalavimus (ES Tarybos direktyva 2001/110/EB, 2001-12-20).", { size: 19 })],
                spacing: { before: 0, after: 0 }
              })]
            })]
          })
        ]
      }),

      emptyLine(280),

      // ── SIGNATURE BLOCK ───────────────────────────────────────────────────
      new Table({
        width: { size: CONTENT_W, type: WidthType.DXA },
        columnWidths: [Math.floor(CONTENT_W * 0.45), CONTENT_W - Math.floor(CONTENT_W * 0.45)],
        rows: [
          new TableRow({
            children: [
              new TableCell({
                borders: noBorders,
                margins: { top: 0, bottom: 0, left: 0, right: 120 },
                width: { size: Math.floor(CONTENT_W * 0.45), type: WidthType.DXA },
                children: [
                  new Paragraph({
                    border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: C_BORDER, space: 8 } },
                    spacing: { before: 0, after: 140 },
                    children: [txt(" ")]
                  }),
                  new Paragraph({
                    children: [small("Diana Rušėnaitė", { color: C_MID, bold: true })],
                    spacing: { before: 0, after: 20 }
                  }),
                  new Paragraph({
                    children: [small("Vardas, pavardė / parašas", { color: "999999" })],
                    spacing: { before: 0, after: 0 }
                  })
                ]
              }),
              new TableCell({
                borders: noBorders,
                margins: { top: 0, bottom: 0, left: 120, right: 0 },
                width: { size: CONTENT_W - Math.floor(CONTENT_W * 0.45), type: WidthType.DXA },
                children: [
                  new Paragraph({
                    border: { bottom: { style: BorderStyle.SINGLE, size: 4, color: C_BORDER, space: 8 } },
                    spacing: { before: 0, after: 140 },
                    children: [txt(" ")]
                  }),
                  new Paragraph({
                    children: [small("2026-05-29", { color: C_MID, bold: true })],
                    spacing: { before: 0, after: 20 }
                  }),
                  new Paragraph({
                    children: [small("Data", { color: "999999" })],
                    spacing: { before: 0, after: 0 }
                  })
                ]
              })
            ]
          })
        ]
      }),

    ]
  }]
});

Packer.toBuffer(doc).then(buffer => {
  fs.writeFileSync("/home/claude/kokybe_pazymejimas_2602192.docx", buffer);
  console.log("Done");
});
