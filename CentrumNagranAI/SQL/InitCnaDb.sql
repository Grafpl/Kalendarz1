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

CREATE TABLE IF NOT EXISTS frame_caption (
    frame_id  INTEGER PRIMARY KEY,
    caption   TEXT NOT NULL,
    tags      TEXT,                    -- JSON list np. ["osoba","wozek","czepek"]
    yolo      TEXT,                    -- JSON list YOLO detekcji
    created   TEXT NOT NULL,
    FOREIGN KEY (frame_id) REFERENCES frame(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_caption_tags ON frame_caption(tags);

CREATE TABLE IF NOT EXISTS guard_rule (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    name          TEXT NOT NULL,
    prompt        TEXT NOT NULL,        -- pytanie po polsku, np. "Czy widać osobę bez czepka?"
    threshold     INTEGER NOT NULL DEFAULT 70,  -- min score żeby alert
    cooldown_min  INTEGER NOT NULL DEFAULT 10,  -- minimum minut między alertami
    enabled       INTEGER NOT NULL DEFAULT 1,
    camera_filter TEXT,                 -- CSV cameraId, NULL=wszystkie
    last_alert    TEXT,
    notify_sms    INTEGER NOT NULL DEFAULT 0,
    created       TEXT NOT NULL DEFAULT (datetime('now'))
);

-- Migration: dodaj notify_sms gdy starsze schema
-- (SQLite nie obsługuje IF NOT EXISTS dla ALTER COLUMN, więc try/catch w kodzie)

CREATE TABLE IF NOT EXISTS guard_alert (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    rule_id     INTEGER NOT NULL,
    frame_id    INTEGER NOT NULL,
    ts          TEXT NOT NULL,
    score       INTEGER NOT NULL,
    reason      TEXT,
    notified    INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (rule_id) REFERENCES guard_rule(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_alert_ts ON guard_alert(ts);

CREATE TABLE IF NOT EXISTS daily_brief (
    id        INTEGER PRIMARY KEY AUTOINCREMENT,
    day       TEXT NOT NULL UNIQUE,    -- yyyy-MM-dd
    summary   TEXT NOT NULL,
    sample_frame_ids TEXT,
    cost_usd  REAL,
    created   TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS camera_baseline (
    camera_id    TEXT NOT NULL,
    hour         INTEGER NOT NULL,        -- 0-23
    centroid     BLOB,                    -- średnia embedingów
    sample_count INTEGER NOT NULL DEFAULT 0,
    updated      TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (camera_id, hour)
);

CREATE TABLE IF NOT EXISTS anomaly_alert (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    frame_id    INTEGER NOT NULL,
    camera_id   TEXT NOT NULL,
    ts          TEXT NOT NULL,
    distance    REAL NOT NULL,           -- 1.0 - cosine similarity
    threshold   REAL NOT NULL,
    notified    INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS idx_anomaly_ts ON anomaly_alert(ts);

-- Wykryte tablice rejestracyjne (#14 OCR)
CREATE TABLE IF NOT EXISTS plate_detection (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    frame_id    INTEGER NOT NULL,
    camera_id   TEXT NOT NULL,
    ts          TEXT NOT NULL,
    plate       TEXT NOT NULL,           -- np. "WK 12345" znormalizowane
    confidence  REAL,                    -- 0..1 albo NULL
    raw_text    TEXT,                    -- surowy output VLM
    FOREIGN KEY (frame_id) REFERENCES frame(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_plate_text ON plate_detection(plate);
CREATE INDEX IF NOT EXISTS idx_plate_ts ON plate_detection(ts);

-- Aktywność per klatka (#20 Heatmapa) — delta embedingu vs poprzednia klatka
-- tej samej kamery. Wartości 0..1 gdzie 1 = duża zmiana.
CREATE TABLE IF NOT EXISTS frame_activity (
    frame_id    INTEGER PRIMARY KEY,
    camera_id   TEXT NOT NULL,
    ts          TEXT NOT NULL,
    activity    REAL NOT NULL,
    FOREIGN KEY (frame_id) REFERENCES frame(id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_activity_cam_ts ON frame_activity(camera_id, ts);

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
