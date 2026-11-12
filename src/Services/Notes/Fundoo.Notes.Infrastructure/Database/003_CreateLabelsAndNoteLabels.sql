CREATE TABLE IF NOT EXISTS labels (
    id uuid PRIMARY KEY,
    user_id uuid NOT NULL,
    name varchar(100) NOT NULL,
    normalized_name varchar(100) NOT NULL,
    created_at_utc timestamp with time zone NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_labels_user_id_normalized_name
    ON labels (user_id, normalized_name);

CREATE TABLE IF NOT EXISTS note_labels (
    note_id uuid NOT NULL,
    label_id uuid NOT NULL,
    PRIMARY KEY (note_id, label_id),
    CONSTRAINT fk_note_labels_note
        FOREIGN KEY (note_id) REFERENCES notes (id) ON DELETE CASCADE,
    CONSTRAINT fk_note_labels_label
        FOREIGN KEY (label_id) REFERENCES labels (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_note_labels_label_id
    ON note_labels (label_id);
