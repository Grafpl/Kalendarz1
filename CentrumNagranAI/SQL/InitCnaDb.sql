-- Schema lokalnej bazy SQLite dla modułu Centrum nagrań AI.
-- Tworzona automatycznie przy pierwszym uruchomieniu indexera.
-- Lokalizacja: %LOCALAPPDATA%\Kalendarz1\CentrumNagranAI\index.db

PRAGMA journal_mode = WAL;
PRAGMA synchronous = NORMAL;
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS camera (
    id           TEXT PRIMARY KEY,
    name         TEXT NOT NULL,
    host         TEXT NOT NULL,
    channel      INTEGER NOT NULL,
    stream_type  INTEGER NOT NULL DEFAULT 0,
    enabled      INTEGER NOT NULL DEFAULT 1,
    last_seen    TEXT
);

CREATE TABLE IF NOT EXISTS frame (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    camera_id         TEXT NOT NULL,
    ts                TEXT NOT NULL,             -- ISO 8601 UTC
    file_path         TEXT NOT NULL,
    file_size         INTEGER,
    embedding_status  INTEGER NOT NULL DEFAULT 0, -- 0=pending, 1=ok, 2=fail
    FOREIGN KEY (camera_id) REFERENCES camera(id)
);
CREATE INDEX IF NOT EXISTS idx_frame_ts        ON frame(ts);
CREATE INDEX IF NOT EXISTS idx_frame_camera_ts ON frame(camera_id, ts);
CREATE INDEX IF NOT EXISTS idx_frame_emb       ON frame(embedding_status) WHERE embedding_status = 0;

CREATE TABLE IF NOT EXISTS frame_embedding (
    frame_id  INTEGER PRIMARY KEY,
    dim       INTEGER NOT NULL,
    vector    BLOB NOT NULL,
    FOREIGN KEY (frame_id) REFERENCES frame(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS query_audit (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    ts            TEXT NOT NULL,
    user_id       TEXT,
    query_text    TEXT NOT NULL,
    top_k_ids     TEXT,
    vlm_calls     INTEGER DEFAULT 0,
    vlm_cost_usd  REAL DEFAULT 0,
    duration_ms   INTEGER
);
CREATE INDEX IF NOT EXISTS idx_audit_ts ON query_audit(ts);
