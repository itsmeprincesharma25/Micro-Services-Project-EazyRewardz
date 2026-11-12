CREATE TABLE IF NOT EXISTS notes (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    title varchar(200) NOT NULL,
    content text NOT NULL DEFAULT '',
    is_archived boolean NOT NULL DEFAULT FALSE,
    is_trashed boolean NOT NULL DEFAULT FALSE,
    created_at_utc timestamp with time zone NOT NULL,
    updated_at_utc timestamp with time zone NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_notes_user_id_updated_at_utc
    ON notes (user_id, updated_at_utc DESC);
