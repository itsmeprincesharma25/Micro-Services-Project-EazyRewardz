CREATE TABLE IF NOT EXISTS note_collaborators (
    note_id uuid NOT NULL,
    collaborator_user_id uuid NOT NULL,
    created_at_utc timestamp with time zone NOT NULL,
    PRIMARY KEY (note_id, collaborator_user_id),
    CONSTRAINT fk_note_collaborators_note
        FOREIGN KEY (note_id) REFERENCES notes (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_note_collaborators_collaborator_user_id
    ON note_collaborators (collaborator_user_id);
