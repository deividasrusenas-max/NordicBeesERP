-- ============================================================================
-- Artwork label types — manageable Settings table (mirrors artwork_brands pattern)
-- This script is DRAFT ONLY. An agent does NOT execute it. Deividas applies it
-- manually against the dev DB (FROZEN.md: DDL/DML is human-only).
-- Idempotent via CREATE TABLE IF NOT EXISTS.
-- ============================================================================

CREATE TABLE IF NOT EXISTS artwork_label_types (
    id         INT          AUTO_INCREMENT PRIMARY KEY,
    name       VARCHAR(100) NOT NULL,
    sort_order INT          NOT NULL DEFAULT 0,
    is_active  BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at DATETIME(6)  NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE UNIQUE INDEX uq_artwork_label_types_name ON artwork_label_types (name);

-- Seed data. Plain INSERTs. Names MUST match existing artwork_files.label_type
-- string values exactly so the upload dropdown stays meaningful for legacy data.
-- "Bendra" (ArtworkLabelTypes.General) is intentionally NOT seeded — it is a
-- sentinel value, not a selectable option.
INSERT INTO artwork_label_types (name, sort_order) VALUES ('Viršutinė (seal)', 1);
INSERT INTO artwork_label_types (name, sort_order) VALUES ('Wraparound', 2);
INSERT INTO artwork_label_types (name, sort_order) VALUES ('Galinė etiketė', 3);
INSERT INTO artwork_label_types (name, sort_order) VALUES ('Dėžės etiketė', 4);
