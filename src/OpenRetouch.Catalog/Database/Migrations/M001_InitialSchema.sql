-- M001: 初期スキーマ (docs/functional-design.md 準拠)

CREATE TABLE folders (
    id TEXT PRIMARY KEY,
    path TEXT NOT NULL UNIQUE,
    name TEXT NOT NULL,
    parent_id TEXT,
    created_at TEXT NOT NULL,
    FOREIGN KEY(parent_id) REFERENCES folders(id)
);

CREATE TABLE photos (
    id TEXT PRIMARY KEY,
    folder_id TEXT NOT NULL,
    file_path TEXT NOT NULL UNIQUE,
    file_name TEXT NOT NULL,
    file_extension TEXT NOT NULL,
    file_size INTEGER,
    file_hash TEXT,
    imported_at TEXT NOT NULL,
    captured_at TEXT,
    width INTEGER,
    height INTEGER,
    orientation INTEGER DEFAULT 1,
    camera_make TEXT,
    camera_model TEXT,
    lens_model TEXT,
    iso INTEGER,
    aperture REAL,
    shutter_speed TEXT,
    focal_length REAL,
    rating INTEGER NOT NULL DEFAULT 0,
    flag TEXT,
    color_label TEXT,
    is_missing INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY(folder_id) REFERENCES folders(id)
);
CREATE INDEX idx_photos_captured_at ON photos(captured_at);
CREATE INDEX idx_photos_rating ON photos(rating);
CREATE INDEX idx_photos_folder ON photos(folder_id);

CREATE TABLE edits (
    id TEXT PRIMARY KEY,
    photo_id TEXT NOT NULL,
    version INTEGER NOT NULL,
    edit_json TEXT NOT NULL,
    is_current INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY(photo_id) REFERENCES photos(id),
    UNIQUE(photo_id, version)
);
CREATE INDEX idx_edits_photo ON edits(photo_id);

CREATE TABLE albums (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE photo_album_map (
    photo_id TEXT NOT NULL,
    album_id TEXT NOT NULL,
    added_at TEXT NOT NULL,
    PRIMARY KEY(photo_id, album_id),
    FOREIGN KEY(photo_id) REFERENCES photos(id),
    FOREIGN KEY(album_id) REFERENCES albums(id)
);

CREATE TABLE presets (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    category TEXT,
    preset_json TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE export_jobs (
    id TEXT PRIMARY KEY,
    settings_json TEXT NOT NULL,
    status TEXT NOT NULL,
    created_at TEXT NOT NULL,
    completed_at TEXT
);

CREATE TABLE export_job_items (
    id TEXT PRIMARY KEY,
    job_id TEXT NOT NULL,
    photo_id TEXT NOT NULL,
    status TEXT NOT NULL,
    output_path TEXT,
    error_message TEXT,
    FOREIGN KEY(job_id) REFERENCES export_jobs(id),
    FOREIGN KEY(photo_id) REFERENCES photos(id)
);

CREATE TABLE thumbnail_cache (
    photo_id TEXT PRIMARY KEY,
    thumb_path TEXT NOT NULL,
    preview_path TEXT,
    source_modified_at TEXT NOT NULL,
    generated_at TEXT NOT NULL,
    FOREIGN KEY(photo_id) REFERENCES photos(id)
);
