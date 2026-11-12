CREATE TABLE IF NOT EXISTS notification_email_requests (
    idempotency_key uuid PRIMARY KEY,
    request_hash character(64) NOT NULL,
    status varchar(20) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL DEFAULT now(),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT ck_notification_email_requests_status
        CHECK (status IN ('processing', 'completed', 'failed'))
);
