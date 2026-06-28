CREATE TABLE IF NOT EXISTS invoice_audit (
    id INT AUTO_INCREMENT PRIMARY KEY,
    invoice_id INT NOT NULL,
    invoice_number VARCHAR(100),
    action VARCHAR(50) NOT NULL,
    action_details TEXT,
    old_status VARCHAR(30),
    new_status VARCHAR(30),
    performed_by VARCHAR(100),
    performed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_invoice_id (invoice_id)
);