package store

import (
	"database/sql"
	"errors"
	"fmt"

	"codingagent/internal/domain"
)

var ErrNotFound = errors.New("记录不存在")

func nullStr(p *string) any {
	if p == nil {
		return nil
	}
	return *p
}

func ptrStr(s sql.NullString) *string {
	if s.Valid {
		v := s.String
		return &v
	}
	return nil
}

// ---- 认证 ----

func (d *DB) GetAuth() (*domain.AuthRecord, error) {
	var r domain.AuthRecord
	err := d.QueryRow(`SELECT id, password_hash, salt, created_at FROM auth_record LIMIT 1`).
		Scan(&r.ID, &r.PasswordHash, &r.Salt, &r.CreatedAt)
	if err == sql.ErrNoRows {
		return nil, nil
	}
	if err != nil {
		return nil, err
	}
	return &r, nil
}

func (d *DB) AddAuth(r *domain.AuthRecord) error {
	res, err := d.Exec(`INSERT INTO auth_record(password_hash, salt, created_at) VALUES(?, ?, ?)`,
		r.PasswordHash, r.Salt, r.CreatedAt)
	if err != nil {
		return err
	}
	r.ID, _ = res.LastInsertId()
	return nil
}

func (d *DB) UpdateAuth(r *domain.AuthRecord) error {
	_, err := d.Exec(`UPDATE auth_record SET password_hash = ?, salt = ? WHERE id = ?`,
		r.PasswordHash, r.Salt, r.ID)
	return err
}

// ---- 提供商 ----

func (d *DB) ListProviders() ([]domain.Provider, error) {
	rows, err := d.Query(`SELECT id, name, type, base_url, api_key, proxy_id, enabled FROM providers ORDER BY name`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.Provider{}
	var ids []int64
	for rows.Next() {
		var p domain.Provider
		var enabled int
		if err := rows.Scan(&p.ID, &p.Name, &p.Type, &p.BaseURL, &p.APIKey, &p.ProxyID, &enabled); err != nil {
			return nil, err
		}
		p.Enabled = enabled == 1
		out = append(out, p)
		ids = append(ids, p.ID)
	}
	if err := rows.Err(); err != nil {
		return nil, err
	}
	for i := range out {
		models, err := d.ListProviderModels(out[i].ID)
		if err != nil {
			return nil, err
		}
		if models == nil {
			models = []domain.ProviderModel{}
		}
		out[i].Models = models
	}
	_ = ids
	return out, nil
}

func (d *DB) GetProvider(id int64) (*domain.Provider, error) {
	var p domain.Provider
	var enabled int
	err := d.QueryRow(`SELECT id, name, type, base_url, api_key, proxy_id, enabled FROM providers WHERE id = ?`, id).
		Scan(&p.ID, &p.Name, &p.Type, &p.BaseURL, &p.APIKey, &p.ProxyID, &enabled)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	p.Enabled = enabled == 1
	models, err := d.ListProviderModels(p.ID)
	if err != nil {
		return nil, err
	}
	if models == nil {
		models = []domain.ProviderModel{}
	}
	p.Models = models
	return &p, nil
}

func (d *DB) AddProvider(p *domain.Provider) error {
	res, err := d.Exec(`INSERT INTO providers(name, type, base_url, api_key, proxy_id, enabled) VALUES(?, ?, ?, ?, ?, ?)`,
		p.Name, p.Type, p.BaseURL, p.APIKey, p.ProxyID, b2i(p.Enabled))
	if err != nil {
		return err
	}
	p.ID, _ = res.LastInsertId()
	return nil
}

func (d *DB) UpdateProvider(p *domain.Provider) error {
	_, err := d.Exec(`UPDATE providers SET name = ?, type = ?, base_url = ?, api_key = ?, proxy_id = ?, enabled = ? WHERE id = ?`,
		p.Name, p.Type, p.BaseURL, p.APIKey, p.ProxyID, b2i(p.Enabled), p.ID)
	return err
}

func (d *DB) DeleteProvider(id int64) error {
	_, err := d.Exec(`DELETE FROM providers WHERE id = ?`, id)
	return err
}

func b2i(b bool) int {
	if b {
		return 1
	}
	return 0
}

// ---- 模型 ----

func (d *DB) ListProviderModels(providerID int64) ([]domain.ProviderModel, error) {
	rows, err := d.Query(`SELECT id, provider_id, model_id, display_name, supports_tools, supports_reasoning, default_reasoning_effort, max_context_tokens, max_output_tokens, is_custom
		FROM provider_models WHERE provider_id = ? ORDER BY id`, providerID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.ProviderModel{}
	for rows.Next() {
		var m domain.ProviderModel
		var st, sr, ic int
		if err := rows.Scan(&m.ID, &m.ProviderID, &m.ModelID, &m.DisplayName, &st, &sr, &m.DefaultReasoningEffort, &m.MaxContextTokens, &m.MaxOutputTokens, &ic); err != nil {
			return nil, err
		}
		m.SupportsTools = st == 1
		m.SupportsReasoning = sr == 1
		m.IsCustom = ic == 1
		out = append(out, m)
	}
	return out, rows.Err()
}

func (d *DB) AddModel(m *domain.ProviderModel) error {
	res, err := d.Exec(`INSERT INTO provider_models(provider_id, model_id, display_name, supports_tools, supports_reasoning, default_reasoning_effort, max_context_tokens, max_output_tokens, is_custom)
		VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		m.ProviderID, m.ModelID, m.DisplayName, b2i(m.SupportsTools), b2i(m.SupportsReasoning), m.DefaultReasoningEffort, m.MaxContextTokens, m.MaxOutputTokens, b2i(m.IsCustom))
	if err != nil {
		return err
	}
	m.ID, _ = res.LastInsertId()
	return nil
}

func (d *DB) UpdateModel(m *domain.ProviderModel) error {
	_, err := d.Exec(`UPDATE provider_models SET model_id = ?, display_name = ?, supports_tools = ?, supports_reasoning = ?, default_reasoning_effort = ?, max_context_tokens = ?, max_output_tokens = ?, is_custom = ?
		WHERE id = ? AND provider_id = ?`,
		m.ModelID, m.DisplayName, b2i(m.SupportsTools), b2i(m.SupportsReasoning), m.DefaultReasoningEffort, m.MaxContextTokens, m.MaxOutputTokens, b2i(m.IsCustom), m.ID, m.ProviderID)
	return err
}

func (d *DB) DeleteModel(providerID, modelID int64) error {
	_, err := d.Exec(`DELETE FROM provider_models WHERE id = ? AND provider_id = ?`, modelID, providerID)
	return err
}

func (d *DB) GetModel(providerID, modelID int64) (*domain.ProviderModel, error) {
	var m domain.ProviderModel
	var st, sr, ic int
	err := d.QueryRow(`SELECT id, provider_id, model_id, display_name, supports_tools, supports_reasoning, default_reasoning_effort, max_context_tokens, max_output_tokens, is_custom
		FROM provider_models WHERE id = ? AND provider_id = ?`, modelID, providerID).
		Scan(&m.ID, &m.ProviderID, &m.ModelID, &m.DisplayName, &st, &sr, &m.DefaultReasoningEffort, &m.MaxContextTokens, &m.MaxOutputTokens, &ic)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	m.SupportsTools = st == 1
	m.SupportsReasoning = sr == 1
	m.IsCustom = ic == 1
	return &m, nil
}

// ReplaceRemoteModels 用远端模型列表替换非自定义模型（事务）。
func (d *DB) ReplaceRemoteModels(providerID int64, models []domain.ProviderModel) error {
	tx, err := d.Begin()
	if err != nil {
		return err
	}
	defer tx.Rollback()
	if _, err := tx.Exec(`DELETE FROM provider_models WHERE provider_id = ? AND is_custom = 0`, providerID); err != nil {
		return err
	}
	for i := range models {
		models[i].ProviderID = providerID
		if _, err := tx.Exec(`INSERT INTO provider_models(provider_id, model_id, display_name, supports_tools, supports_reasoning, default_reasoning_effort, max_context_tokens, max_output_tokens, is_custom)
			VALUES(?, ?, ?, 1, 0, 'off', 128000, 8192, 0)`,
			providerID, models[i].ModelID, models[i].DisplayName); err != nil {
			return fmt.Errorf("插入模型失败: %w", err)
		}
	}
	return tx.Commit()
}
