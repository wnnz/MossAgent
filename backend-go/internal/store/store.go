// Package store：SQLite 存储（modernc.org/sqlite 纯驱动，无 CGO）。
package store

import (
	"database/sql"
	"fmt"

	"codingagent/internal/domain"

	_ "modernc.org/sqlite"
)

// DB 封装 *sql.DB 与建库/种子。
type DB struct {
	*sql.DB
}

// Open 打开（或创建）SQLite 数据库并建表。
func Open(path string) (*DB, error) {
	db, err := sql.Open("sqlite", path+"?_pragma=journal_mode(WAL)&_pragma=busy_timeout(5000)&_pragma=foreign_keys(ON)")
	if err != nil {
		return nil, fmt.Errorf("打开数据库失败: %w", err)
	}
	// modernc/sqlite 写并发受限：单连接串行化
	db.SetMaxOpenConns(1)
	if err := migrate(db); err != nil {
		db.Close()
		return nil, err
	}
	return &DB{db}, nil
}

func migrate(db *sql.DB) error {
	schema := `
CREATE TABLE IF NOT EXISTS auth_record (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	password_hash TEXT NOT NULL,
	salt TEXT NOT NULL,
	created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS providers (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	name TEXT NOT NULL,
	type TEXT NOT NULL,
	base_url TEXT NOT NULL,
	api_key TEXT NOT NULL DEFAULT '',
	proxy_id INTEGER,
	enabled INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS provider_models (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	provider_id INTEGER NOT NULL REFERENCES providers(id) ON DELETE CASCADE,
	model_id TEXT NOT NULL,
	display_name TEXT NOT NULL DEFAULT '',
	supports_tools INTEGER NOT NULL DEFAULT 1,
	supports_reasoning INTEGER NOT NULL DEFAULT 0,
	default_reasoning_effort TEXT NOT NULL DEFAULT 'off',
	max_context_tokens INTEGER NOT NULL DEFAULT 128000,
	max_output_tokens INTEGER NOT NULL DEFAULT 8192,
	is_custom INTEGER NOT NULL DEFAULT 0,
	UNIQUE(provider_id, model_id)
);
CREATE TABLE IF NOT EXISTS proxies (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	name TEXT NOT NULL,
	scheme TEXT NOT NULL,
	host TEXT NOT NULL,
	port INTEGER NOT NULL,
	username TEXT,
	password TEXT,
	enabled INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS sessions (
	id TEXT PRIMARY KEY,
	title TEXT NOT NULL,
	provider_id INTEGER NOT NULL,
	model_id TEXT NOT NULL,
	reasoning_effort TEXT NOT NULL DEFAULT 'off',
	workspace_path TEXT,
	status TEXT NOT NULL DEFAULT 'active',
	created_at TEXT NOT NULL,
	updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS messages (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
	role TEXT NOT NULL,
	content TEXT NOT NULL DEFAULT '',
	tool_calls_json TEXT,
	tool_call_id TEXT,
	name TEXT,
	created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_messages_session ON messages(session_id);
CREATE TABLE IF NOT EXISTS sub_agents (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	name TEXT NOT NULL,
	description TEXT NOT NULL DEFAULT '',
	invocation_rule TEXT NOT NULL DEFAULT '',
	system_prompt TEXT NOT NULL DEFAULT '',
	provider_id INTEGER NOT NULL,
	model_id TEXT NOT NULL,
	reasoning_effort TEXT NOT NULL DEFAULT 'off',
	max_turns INTEGER NOT NULL DEFAULT 10,
	allowed_tools_json TEXT NOT NULL DEFAULT '[]',
	enabled INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS memories (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	scope TEXT NOT NULL DEFAULT 'global',
	title TEXT NOT NULL,
	content TEXT NOT NULL DEFAULT '',
	tags TEXT,
	updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS skills (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	name TEXT NOT NULL UNIQUE,
	description TEXT NOT NULL DEFAULT '',
	instructions TEXT NOT NULL DEFAULT '',
	enabled INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS mcp_servers (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	name TEXT NOT NULL,
	transport TEXT NOT NULL DEFAULT 'stdio',
	command TEXT,
	args_json TEXT,
	env_json TEXT,
	url TEXT,
	headers_json TEXT,
	enabled INTEGER NOT NULL DEFAULT 1,
	auto_connect INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS mcp_tools (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	server_id INTEGER NOT NULL REFERENCES mcp_servers(id) ON DELETE CASCADE,
	name TEXT NOT NULL,
	description TEXT NOT NULL DEFAULT '',
	schema_json TEXT NOT NULL DEFAULT '{}'
);
CREATE TABLE IF NOT EXISTS settings (
	key TEXT PRIMARY KEY,
	value TEXT NOT NULL DEFAULT ''
);
CREATE TABLE IF NOT EXISTS projects (
	id INTEGER PRIMARY KEY AUTOINCREMENT,
	name TEXT NOT NULL,
	path TEXT NOT NULL DEFAULT '',
	created_at TEXT NOT NULL
);
`
	if _, err := db.Exec(schema); err != nil {
		return fmt.Errorf("建表失败: %w", err)
	}
	// 会话项目归属迁移（旧库无 project_id 列）
	if err := migrateAddSessionProjectID(db); err != nil {
		return err
	}
	return nil
}

func migrateAddSessionProjectID(db *sql.DB) error {
	rows, err := db.Query(`PRAGMA table_info(sessions)`)
	if err != nil {
		return err
	}
	defer rows.Close()
	hasColumn := false
	for rows.Next() {
		var cid int
		var name, typ string
		var notNull int
		var dflt any
		var pk int
		if err := rows.Scan(&cid, &name, &typ, &notNull, &dflt, &pk); err != nil {
			return err
		}
		if name == "project_id" {
			hasColumn = true
		}
	}
	if err := rows.Err(); err != nil {
		return err
	}
	if hasColumn {
		return nil
	}
	if _, err := db.Exec(`ALTER TABLE sessions ADD COLUMN project_id INTEGER`); err != nil {
		return fmt.Errorf("迁移 sessions.project_id 失败: %w", err)
	}
	return nil
}

// SeedDefault 种子默认设置与 JWT 密钥（幂等）。
func (d *DB) SeedDefault(jwtSecret string) error {
	defaults := map[string]string{
		"workspace_path": "",
		"max_turns":      "25",
		"jwt_secret":     jwtSecret,
	}
	for k, v := range defaults {
		if _, err := d.Exec(`INSERT OR IGNORE INTO settings(key, value) VALUES(?, ?)`, k, v); err != nil {
			return fmt.Errorf("种子设置失败: %w", err)
		}
	}
	return nil
}

// GetSetting 读取单个设置。
func (d *DB) GetSetting(key string) (string, error) {
	var v string
	err := d.QueryRow(`SELECT value FROM settings WHERE key = ?`, key).Scan(&v)
	if err == sql.ErrNoRows {
		return "", nil
	}
	return v, err
}

// SetSetting upsert 设置。
func (d *DB) SetSetting(key, value string) error {
	_, err := d.Exec(`INSERT INTO settings(key, value) VALUES(?, ?)
		ON CONFLICT(key) DO UPDATE SET value = excluded.value`, key, value)
	return err
}

// AllSettings 全部设置（按 key 排序）。
func (d *DB) AllSettings() ([]domain.Setting, error) {
	rows, err := d.Query(`SELECT key, value FROM settings ORDER BY key`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.Setting{}
	for rows.Next() {
		var s domain.Setting
		if err := rows.Scan(&s.Key, &s.Value); err != nil {
			return nil, err
		}
		out = append(out, s)
	}
	return out, rows.Err()
}
