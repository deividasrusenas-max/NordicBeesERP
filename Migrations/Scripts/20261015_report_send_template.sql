-- Apply manually (dev, test, staging, then prod). Works on MariaDB 11.8 and MySQL 8.0.
-- Run with the utf8mb4 client charset (Lithuanian text), e.g.: mariadb --default-character-set=utf8mb4 ...
-- IDEMPOTENT: INSERT IGNORE only, safe to re-run. Requires email_templates (20261014_dunning.sql). The agent does NOT run DDL/DML here.
-- The application works without these rows (built-in defaults in EmailTemplateDefaults.cs are identical).
-- Verify afterwards: SELECT code, language FROM email_templates WHERE code = 'REPORT_SEND';  (2 rows)
SET NAMES utf8mb4;

INSERT IGNORE INTO email_templates (code, language, subject, body_html, is_active) VALUES
    ('REPORT_SEND', 'LT', '{{ReportName}} {{Period}}', '<p>Gerb. {{PartnerName}},</p><p>siunčiame ataskaitą „{{ReportName}}“ ({{Period}}).</p><p>Ataskaita pridėta prie šio laiško.</p>{{CompanySignature}}', 1),
    ('REPORT_SEND', 'EN', '{{ReportName}} {{Period}}', '<p>Dear {{PartnerName}},</p><p>please find attached the report “{{ReportName}}” ({{Period}}).</p>{{CompanySignature}}', 1);

-- Down:
-- DELETE FROM email_templates WHERE code = 'REPORT_SEND';
