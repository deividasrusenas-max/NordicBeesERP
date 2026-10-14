using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// Dunning module: email_templates, dunning_config, dunning_levels, dunning_partner_settings,
    /// dunning_invoice_holds, dunning_log + seed rows. Hand-written (no DbSet / snapshot change: raw SQL only);
    /// mirrors Migrations/Scripts/20261014_dunning.sql, which the human applies. Idempotent (IF NOT EXISTS / INSERT IGNORE),
    /// valid on MySQL 8.0 and MariaDB 11.8. No FKs, no ALTER of existing tables.
    /// </summary>
    public partial class AddDunning : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS email_templates (
    id INT NOT NULL AUTO_INCREMENT,
    code VARCHAR(50) NOT NULL,
    language VARCHAR(5) NOT NULL,
    subject VARCHAR(500) NOT NULL,
    body_html MEDIUMTEXT NOT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT PK_email_templates PRIMARY KEY (id),
    UNIQUE KEY UX_email_templates_code_language (code, language)
) CHARACTER SET utf8mb4;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS dunning_config (
    id INT NOT NULL,
    enabled TINYINT(1) NOT NULL DEFAULT 0,
    auto_send TINYINT(1) NOT NULL DEFAULT 0,
    run_hour INT NOT NULL DEFAULT 9,
    grace_days INT NOT NULL DEFAULT 3,
    min_days_between INT NOT NULL DEFAULT 7,
    min_amount DECIMAL(10,2) NOT NULL DEFAULT 1.00,
    include_individuals TINYINT(1) NOT NULL DEFAULT 0,
    stale_import_days INT NOT NULL DEFAULT 14,
    show_interest TINYINT(1) NOT NULL DEFAULT 0,
    interest_rate_pct DECIMAL(5,2) NULL,
    show_fixed_fee TINYINT(1) NOT NULL DEFAULT 0,
    fixed_fee_eur DECIMAL(10,2) NOT NULL DEFAULT 40.00,
    attach_invoice_pdfs TINYINT(1) NOT NULL DEFAULT 1,
    attach_statement TINYINT(1) NOT NULL DEFAULT 0,
    cc_address VARCHAR(1000) NULL,
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT PK_dunning_config PRIMARY KEY (id)
) CHARACTER SET utf8mb4;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS dunning_levels (
    id INT NOT NULL AUTO_INCREMENT,
    level_no INT NOT NULL,
    name VARCHAR(100) NOT NULL,
    days_after_due INT NOT NULL,
    template_code VARCHAR(50) NOT NULL,
    repeat_every_days INT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    CONSTRAINT PK_dunning_levels PRIMARY KEY (id),
    UNIQUE KEY UX_dunning_levels_level_no (level_no)
) CHARACTER SET utf8mb4;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS dunning_partner_settings (
    partner_id INT NOT NULL,
    excluded TINYINT(1) NOT NULL DEFAULT 0,
    paused_until DATE NULL,
    email_override VARCHAR(1000) NULL,
    note TEXT NULL,
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    updated_by VARCHAR(255) NULL,
    CONSTRAINT PK_dunning_partner_settings PRIMARY KEY (partner_id)
) CHARACTER SET utf8mb4;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS dunning_invoice_holds (
    invoice_id INT NOT NULL,
    paused_until DATE NULL, -- NULL = indefinite
    reason VARCHAR(500) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    created_by VARCHAR(255) NULL,
    CONSTRAINT PK_dunning_invoice_holds PRIMARY KEY (invoice_id)
) CHARACTER SET utf8mb4;");

            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS dunning_log (
    id INT NOT NULL AUTO_INCREMENT,
    invoice_id INT NOT NULL,
    partner_id INT NOT NULL,
    level_no INT NOT NULL,
    outbox_id INT NULL,
    sent_on DATE NOT NULL,
    mode ENUM('manual','auto') NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    created_by VARCHAR(255) NULL,
    CONSTRAINT PK_dunning_log PRIMARY KEY (id),
    UNIQUE KEY UX_dunning_log_invoice_level_day (invoice_id, level_no, sent_on),
    INDEX IX_dunning_log_partner_sent (partner_id, sent_on),
    INDEX IX_dunning_log_outbox (outbox_id)
) CHARACTER SET utf8mb4;");

            migrationBuilder.Sql(@"INSERT IGNORE INTO dunning_config (id) VALUES (1);");

            migrationBuilder.Sql(@"INSERT IGNORE INTO dunning_levels (level_no, name, days_after_due, template_code, repeat_every_days, is_active) VALUES
    (1, 'Pirmas priminimas', 3, 'DUNNING_L1', NULL, 1),
    (2, 'Antras priminimas', 14, 'DUNNING_L2', NULL, 1),
    (3, 'Paskutinis priminimas', 30, 'DUNNING_L3', 14, 1);");

            migrationBuilder.Sql(@"INSERT IGNORE INTO email_templates (code, language, subject, body_html, is_active) VALUES
    ('INVOICE_SEND', 'LT', 'Sąskaita faktūra {{InvoiceNumber}}', '<p>Gerb. {{PartnerName}},</p><p>siunčiame sąskaitą faktūrą <strong>{{InvoiceNumber}}</strong> ({{InvoiceDate}}) už {{Total}}. Apmokėjimo terminas – {{DueDate}}.</p><p>Sąskaita pridėta PDF formatu.</p>{{CompanySignature}}', 1),
    ('INVOICE_SEND', 'EN', 'Invoice {{InvoiceNumber}}', '<p>Dear {{PartnerName}},</p><p>please find attached invoice <strong>{{InvoiceNumber}}</strong> dated {{InvoiceDate}} for {{Total}}. The payment due date is {{DueDate}}.</p>{{CompanySignature}}', 1),
    ('CREDIT_NOTE_SEND', 'LT', 'Kreditinė sąskaita {{InvoiceNumber}}', '<p>Gerb. {{PartnerName}},</p><p>siunčiame kreditinę sąskaitą <strong>{{InvoiceNumber}}</strong> ({{InvoiceDate}}) už {{Total}}.</p><p>Dokumentas pridėtas PDF formatu.</p>{{CompanySignature}}', 1),
    ('CREDIT_NOTE_SEND', 'EN', 'Credit note {{InvoiceNumber}}', '<p>Dear {{PartnerName}},</p><p>please find attached credit note <strong>{{InvoiceNumber}}</strong> dated {{InvoiceDate}} for {{Total}}.</p>{{CompanySignature}}', 1),
    ('DUNNING_L1', 'LT', 'Priminimas dėl neapmokėtų sąskaitų', '<p>Gerb. {{PartnerName}},</p><p>primename, kad žemiau išvardytų sąskaitų apmokėjimo terminas jau suėjo. Jei apmokėjimą jau atlikote, prašome šio laiško nepaisyti.</p>{{InvoiceTable}}<p>Iš viso pradelsta: <strong>{{TotalOverdue}}</strong>.</p><p>Jei turite klausimų, mielai padėsime.</p>{{CompanySignature}}', 1),
    ('DUNNING_L1', 'EN', 'Reminder: overdue invoices', '<p>Dear {{PartnerName}},</p><p>this is a friendly reminder that the payment due date of the invoices listed below has passed. If you have already made the payment, please disregard this message.</p>{{InvoiceTable}}<p>Total overdue: <strong>{{TotalOverdue}}</strong>.</p><p>Please let us know if you have any questions.</p>{{CompanySignature}}', 1),
    ('DUNNING_L2', 'LT', 'Antrasis priminimas dėl pradelstų sąskaitų', '<p>Gerb. {{PartnerName}},</p><p>nepaisant ankstesnio priminimo, žemiau išvardytų sąskaitų apmokėjimo negavome.</p>{{InvoiceTable}}<p>Iš viso pradelsta: <strong>{{TotalOverdue}}</strong>.</p><p>Prašome apmokėti per 7 dienas arba informuoti mus apie galimas priežastis, trukdančias tai padaryti.</p>{{CompanySignature}}', 1),
    ('DUNNING_L2', 'EN', 'Second reminder: overdue invoices', '<p>Dear {{PartnerName}},</p><p>despite our previous reminder, we have not received payment for the invoices listed below.</p>{{InvoiceTable}}<p>Total overdue: <strong>{{TotalOverdue}}</strong>.</p><p>Please arrange payment within 7 days or let us know if anything prevents you from doing so.</p>{{CompanySignature}}', 1),
    ('DUNNING_L3', 'LT', 'Paskutinis priminimas dėl pradelstų sąskaitų', '<p>Gerb. {{PartnerName}},</p><p>tai paskutinis priminimas dėl žemiau išvardytų pradelstų sąskaitų.</p>{{InvoiceTable}}<p>Iš viso pradelsta: <strong>{{TotalOverdue}}</strong>.</p><p>Prašome sumokėti per 5 darbo dienas. Jei apmokėjimas jau atliktas arba yra neaiškumų dėl sąskaitų, prašome nedelsiant su mumis susisiekti.</p>{{InterestBlock}}{{CompanySignature}}', 1),
    ('DUNNING_L3', 'EN', 'Final reminder: overdue invoices', '<p>Dear {{PartnerName}},</p><p>this is our final reminder regarding the overdue invoices listed below.</p>{{InvoiceTable}}<p>Total overdue: <strong>{{TotalOverdue}}</strong>.</p><p>Please make the payment within 5 business days. If payment has already been made or anything about the invoices is unclear, please contact us right away.</p>{{InterestBlock}}{{CompanySignature}}', 1);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS dunning_log;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS dunning_invoice_holds;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS dunning_partner_settings;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS dunning_levels;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS dunning_config;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS email_templates;");
        }
    }
}
