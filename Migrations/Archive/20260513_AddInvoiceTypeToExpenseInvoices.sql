ALTER TABLE expense_invoices ADD COLUMN invoice_type VARCHAR(20) NOT NULL DEFAULT 'STANDARD' AFTER supplier_id;
CREATE INDEX idx_expense_invoices_type ON expense_invoices(invoice_type);