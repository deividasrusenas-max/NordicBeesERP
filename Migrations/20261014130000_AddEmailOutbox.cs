using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NordicBeesERP.Migrations
{
    /// <summary>
    /// Outbound e-mail queue: <c>email_outbox</c> + <c>email_outbox_attachments</c>. Hand-written (like
    /// AddFilesTableAndExpenseInvoiceFileId): the tables are accessed only through raw SQL, so there is no
    /// DbSet/model-snapshot change. Mirrors Migrations/Scripts/20261014_email_outbox.sql, which the human applies.
    /// No IF [NOT] EXISTS: valid on MySQL 8.0 and MariaDB 11.8. No FKs to existing tables.
    /// </summary>
    public partial class AddEmailOutbox : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE TABLE email_outbox (
                    id INT NOT NULL AUTO_INCREMENT,
                    to_addresses VARCHAR(1000) NOT NULL,
                    cc_addresses VARCHAR(1000) NULL,
                    reply_to VARCHAR(255) NULL,
                    subject VARCHAR(500) NOT NULL,
                    body_html MEDIUMTEXT NOT NULL,
                    body_text MEDIUMTEXT NULL,
                    status ENUM('queued','sending','sent','failed','cancelled') NOT NULL DEFAULT 'queued',
                    attempts INT NOT NULL DEFAULT 0,
                    next_attempt_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                    last_error TEXT NULL,
                    message_id VARCHAR(255) NULL,
                    sent_at DATETIME(6) NULL,
                    original_to VARCHAR(1000) NULL,
                    related_type VARCHAR(50) NULL,
                    related_id INT NULL,
                    created_by INT NULL,
                    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
                    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
                    CONSTRAINT PK_email_outbox PRIMARY KEY (id),
                    INDEX IX_email_outbox_status_next_attempt (status, next_attempt_at),
                    INDEX IX_email_outbox_related (related_type, related_id)
                ) CHARACTER SET utf8mb4;");

            migrationBuilder.Sql(@"
                CREATE TABLE email_outbox_attachments (
                    id INT NOT NULL AUTO_INCREMENT,
                    outbox_id INT NOT NULL,
                    file_name VARCHAR(255) NOT NULL,
                    content_type VARCHAR(100) NOT NULL,
                    file_id BIGINT NOT NULL,
                    size_bytes INT NOT NULL,
                    CONSTRAINT PK_email_outbox_attachments PRIMARY KEY (id),
                    INDEX IX_email_outbox_attachments_outbox (outbox_id),
                    CONSTRAINT FK_email_outbox_attachments_outbox FOREIGN KEY (outbox_id) REFERENCES email_outbox (id) ON DELETE CASCADE
                ) CHARACTER SET utf8mb4;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE email_outbox_attachments;");
            migrationBuilder.Sql("DROP TABLE email_outbox;");
        }
    }
}
