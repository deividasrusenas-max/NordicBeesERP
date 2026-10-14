-- Apply manually: dev (nordic_bees_erp), test (nordic_bees_erp_test), staging, then prod. Works on MariaDB 11.8 and MySQL 8.0.
-- NOT idempotent (no IF [NOT] EXISTS by design): run once per database. The agent does NOT run DDL.
-- All timestamps are UTC: the application always supplies them explicitly (DateTime.UtcNow);
-- the CURRENT_TIMESTAMP(6) defaults only follow the DB session time zone and are a fallback.
-- No FKs to existing tables (only the attachments -> outbox FK, ON DELETE CASCADE).
-- Verify afterwards: DESCRIBE email_outbox; DESCRIBE email_outbox_attachments; SHOW CREATE TABLE email_outbox_attachments;

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
    original_to VARCHAR(1000) NULL, -- filled when redirect mode rewrote recipients
    related_type VARCHAR(50) NULL,
    related_id INT NULL,
    created_by INT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    CONSTRAINT PK_email_outbox PRIMARY KEY (id),
    INDEX IX_email_outbox_status_next_attempt (status, next_attempt_at),
    INDEX IX_email_outbox_related (related_type, related_id)
) CHARACTER SET utf8mb4;

CREATE TABLE email_outbox_attachments (
    id INT NOT NULL AUTO_INCREMENT,
    outbox_id INT NOT NULL,
    file_name VARCHAR(255) NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    file_id BIGINT NOT NULL, -- files.id (IFileStore reference); same type as files.id, deliberately NO FK
    size_bytes INT NOT NULL,
    CONSTRAINT PK_email_outbox_attachments PRIMARY KEY (id),
    INDEX IX_email_outbox_attachments_outbox (outbox_id),
    CONSTRAINT FK_email_outbox_attachments_outbox FOREIGN KEY (outbox_id) REFERENCES email_outbox (id) ON DELETE CASCADE
) CHARACTER SET utf8mb4;

-- EF history row (check the column names of __EFMigrationsHistory first; same ProductVersion as earlier rows):
-- INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('20261014130000_AddEmailOutbox', '10.0.11');

-- Down:
-- DROP TABLE email_outbox_attachments;
-- DROP TABLE email_outbox;
