package store

import (
	"database/sql"

	"codingagent/internal/domain"
)

// ---- 代理 ----

func (d *DB) ListProxies() ([]domain.Proxy, error) {
	rows, err := d.Query(`SELECT id, name, scheme, host, port, username, password, enabled FROM proxies ORDER BY name`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.Proxy{}
	for rows.Next() {
		var p domain.Proxy
		var enabled int
		if err := rows.Scan(&p.ID, &p.Name, &p.Scheme, &p.Host, &p.Port, &p.Username, &p.Password, &enabled); err != nil {
			return nil, err
		}
		p.Enabled = enabled == 1
		out = append(out, p)
	}
	return out, rows.Err()
}

func (d *DB) GetProxy(id int64) (*domain.Proxy, error) {
	var p domain.Proxy
	var enabled int
	err := d.QueryRow(`SELECT id, name, scheme, host, port, username, password, enabled FROM proxies WHERE id = ?`, id).
		Scan(&p.ID, &p.Name, &p.Scheme, &p.Host, &p.Port, &p.Username, &p.Password, &enabled)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	p.Enabled = enabled == 1
	return &p, nil
}

func (d *DB) AddProxy(p *domain.Proxy) error {
	res, err := d.Exec(`INSERT INTO proxies(name, scheme, host, port, username, password, enabled) VALUES(?, ?, ?, ?, ?, ?, ?)`,
		p.Name, p.Scheme, p.Host, p.Port, p.Username, p.Password, b2i(p.Enabled))
	if err != nil {
		return err
	}
	p.ID, _ = res.LastInsertId()
	return nil
}

func (d *DB) UpdateProxy(p *domain.Proxy) error {
	_, err := d.Exec(`UPDATE proxies SET name = ?, scheme = ?, host = ?, port = ?, username = ?, password = ?, enabled = ? WHERE id = ?`,
		p.Name, p.Scheme, p.Host, p.Port, p.Username, p.Password, b2i(p.Enabled), p.ID)
	return err
}

func (d *DB) DeleteProxy(id int64) error {
	_, err := d.Exec(`DELETE FROM proxies WHERE id = ?`, id)
	return err
}

// ---- 会话 ----

func (d *DB) ListSessions(includeArchived bool) ([]domain.Session, error) {
	query := `SELECT id, title, project_id, provider_id, model_id, reasoning_effort, workspace_path, status, created_at, updated_at FROM sessions`
	if !includeArchived {
		query += ` WHERE status = 'active'`
	}
	query += ` ORDER BY updated_at DESC`
	rows, err := d.Query(query)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.Session{}
	for rows.Next() {
		var s domain.Session
		if err := rows.Scan(&s.ID, &s.Title, &s.ProjectID, &s.ProviderID, &s.ModelID, &s.ReasoningEffort, &s.WorkspacePath, &s.Status, &s.CreatedAt, &s.UpdatedAt); err != nil {
			return nil, err
		}
		out = append(out, s)
	}
	return out, rows.Err()
}

func (d *DB) GetSession(id string) (*domain.Session, error) {
	var s domain.Session
	err := d.QueryRow(`SELECT id, title, project_id, provider_id, model_id, reasoning_effort, workspace_path, status, created_at, updated_at FROM sessions WHERE id = ?`, id).
		Scan(&s.ID, &s.Title, &s.ProjectID, &s.ProviderID, &s.ModelID, &s.ReasoningEffort, &s.WorkspacePath, &s.Status, &s.CreatedAt, &s.UpdatedAt)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	return &s, nil
}

func (d *DB) AddSession(s *domain.Session) error {
	_, err := d.Exec(`INSERT INTO sessions(id, title, project_id, provider_id, model_id, reasoning_effort, workspace_path, status, created_at, updated_at)
		VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		s.ID, s.Title, s.ProjectID, s.ProviderID, s.ModelID, s.ReasoningEffort, s.WorkspacePath, s.Status, s.CreatedAt, s.UpdatedAt)
	return err
}

func (d *DB) UpdateSession(s *domain.Session) error {
	_, err := d.Exec(`UPDATE sessions SET title = ?, project_id = ?, provider_id = ?, model_id = ?, reasoning_effort = ?, workspace_path = ?, status = ?, updated_at = ? WHERE id = ?`,
		s.Title, s.ProjectID, s.ProviderID, s.ModelID, s.ReasoningEffort, s.WorkspacePath, s.Status, s.UpdatedAt, s.ID)
	return err
}

func (d *DB) DeleteSession(id string) error {
	_, err := d.Exec(`DELETE FROM messages WHERE session_id = ?`, id)
	if err != nil {
		return err
	}
	_, err = d.Exec(`DELETE FROM sessions WHERE id = ?`, id)
	return err
}

// ---- 消息 ----

func (d *DB) ListMessages(sessionID string) ([]domain.ChatMessage, error) {
	rows, err := d.Query(`SELECT id, session_id, role, content, tool_calls_json, tool_call_id, name, created_at FROM messages WHERE session_id = ? ORDER BY id`, sessionID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.ChatMessage{}
	for rows.Next() {
		var m domain.ChatMessage
		if err := rows.Scan(&m.ID, &m.SessionID, &m.Role, &m.Content, &m.ToolCallsJSON, &m.ToolCallID, &m.Name, &m.CreatedAt); err != nil {
			return nil, err
		}
		out = append(out, m)
	}
	return out, rows.Err()
}

func (d *DB) AddMessage(m *domain.ChatMessage) error {
	res, err := d.Exec(`INSERT INTO messages(session_id, role, content, tool_calls_json, tool_call_id, name, created_at) VALUES(?, ?, ?, ?, ?, ?, ?)`,
		m.SessionID, m.Role, m.Content, m.ToolCallsJSON, m.ToolCallID, m.Name, m.CreatedAt)
	if err != nil {
		return err
	}
	m.ID, _ = res.LastInsertId()
	return nil
}

// ---- 子代理 ----

func (d *DB) ListSubAgents(enabledOnly bool) ([]domain.SubAgent, error) {
	query := `SELECT id, name, description, invocation_rule, system_prompt, provider_id, model_id, reasoning_effort, max_turns, allowed_tools_json, enabled FROM sub_agents`
	if enabledOnly {
		query += ` WHERE enabled = 1`
	}
	query += ` ORDER BY name`
	rows, err := d.Query(query)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.SubAgent{}
	for rows.Next() {
		var s domain.SubAgent
		var enabled int
		if err := rows.Scan(&s.ID, &s.Name, &s.Description, &s.InvocationRule, &s.SystemPrompt, &s.ProviderID, &s.ModelID, &s.ReasoningEffort, &s.MaxTurns, &s.AllowedToolsJSON, &enabled); err != nil {
			return nil, err
		}
		s.Enabled = enabled == 1
		out = append(out, s)
	}
	return out, rows.Err()
}

func (d *DB) GetSubAgent(id int64) (*domain.SubAgent, error) {
	var s domain.SubAgent
	var enabled int
	err := d.QueryRow(`SELECT id, name, description, invocation_rule, system_prompt, provider_id, model_id, reasoning_effort, max_turns, allowed_tools_json, enabled FROM sub_agents WHERE id = ?`, id).
		Scan(&s.ID, &s.Name, &s.Description, &s.InvocationRule, &s.SystemPrompt, &s.ProviderID, &s.ModelID, &s.ReasoningEffort, &s.MaxTurns, &s.AllowedToolsJSON, &enabled)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	s.Enabled = enabled == 1
	return &s, nil
}

func (d *DB) AddSubAgent(s *domain.SubAgent) error {
	res, err := d.Exec(`INSERT INTO sub_agents(name, description, invocation_rule, system_prompt, provider_id, model_id, reasoning_effort, max_turns, allowed_tools_json, enabled)
		VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		s.Name, s.Description, s.InvocationRule, s.SystemPrompt, s.ProviderID, s.ModelID, s.ReasoningEffort, s.MaxTurns, s.AllowedToolsJSON, b2i(s.Enabled))
	if err != nil {
		return err
	}
	s.ID, _ = res.LastInsertId()
	return nil
}

func (d *DB) UpdateSubAgent(s *domain.SubAgent) error {
	_, err := d.Exec(`UPDATE sub_agents SET name = ?, description = ?, invocation_rule = ?, system_prompt = ?, provider_id = ?, model_id = ?, reasoning_effort = ?, max_turns = ?, allowed_tools_json = ?, enabled = ? WHERE id = ?`,
		s.Name, s.Description, s.InvocationRule, s.SystemPrompt, s.ProviderID, s.ModelID, s.ReasoningEffort, s.MaxTurns, s.AllowedToolsJSON, b2i(s.Enabled), s.ID)
	return err
}

func (d *DB) DeleteSubAgent(id int64) error {
	_, err := d.Exec(`DELETE FROM sub_agents WHERE id = ?`, id)
	return err
}

// ---- 记忆 ----

func (d *DB) ListMemories(scope string) ([]domain.Memory, error) {
	query := `SELECT id, scope, title, content, tags, updated_at FROM memories`
	var args []any
	if scope != "" {
		query += ` WHERE scope = ?`
		args = append(args, scope)
	}
	query += ` ORDER BY updated_at DESC`
	rows, err := d.Query(query, args...)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.Memory{}
	for rows.Next() {
		var m domain.Memory
		if err := rows.Scan(&m.ID, &m.Scope, &m.Title, &m.Content, &m.Tags, &m.UpdatedAt); err != nil {
			return nil, err
		}
		out = append(out, m)
	}
	return out, rows.Err()
}

func (d *DB) SearchMemories(q string) ([]domain.Memory, error) {
	term := "%" + q + "%"
	rows, err := d.Query(`SELECT id, scope, title, content, tags, updated_at FROM memories
		WHERE title LIKE ? OR content LIKE ? OR (tags IS NOT NULL AND tags LIKE ?) ORDER BY updated_at DESC`, term, term, term)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.Memory{}
	for rows.Next() {
		var m domain.Memory
		if err := rows.Scan(&m.ID, &m.Scope, &m.Title, &m.Content, &m.Tags, &m.UpdatedAt); err != nil {
			return nil, err
		}
		out = append(out, m)
	}
	return out, rows.Err()
}

func (d *DB) GetMemory(id int64) (*domain.Memory, error) {
	var m domain.Memory
	err := d.QueryRow(`SELECT id, scope, title, content, tags, updated_at FROM memories WHERE id = ?`, id).
		Scan(&m.ID, &m.Scope, &m.Title, &m.Content, &m.Tags, &m.UpdatedAt)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	return &m, nil
}

func (d *DB) AddMemory(m *domain.Memory) error {
	res, err := d.Exec(`INSERT INTO memories(scope, title, content, tags, updated_at) VALUES(?, ?, ?, ?, ?)`,
		m.Scope, m.Title, m.Content, m.Tags, m.UpdatedAt)
	if err != nil {
		return err
	}
	m.ID, _ = res.LastInsertId()
	return nil
}

func (d *DB) UpdateMemory(m *domain.Memory) error {
	_, err := d.Exec(`UPDATE memories SET scope = ?, title = ?, content = ?, tags = ?, updated_at = ? WHERE id = ?`,
		m.Scope, m.Title, m.Content, m.Tags, m.UpdatedAt, m.ID)
	return err
}

func (d *DB) DeleteMemory(id int64) error {
	_, err := d.Exec(`DELETE FROM memories WHERE id = ?`, id)
	return err
}

// ---- 技能 ----

func (d *DB) ListSkills(enabledOnly bool) ([]domain.Skill, error) {
	query := `SELECT id, name, description, instructions, enabled FROM skills`
	if enabledOnly {
		query += ` WHERE enabled = 1`
	}
	query += ` ORDER BY name`
	rows, err := d.Query(query)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.Skill{}
	for rows.Next() {
		var s domain.Skill
		var enabled int
		if err := rows.Scan(&s.ID, &s.Name, &s.Description, &s.Instructions, &enabled); err != nil {
			return nil, err
		}
		s.Enabled = enabled == 1
		out = append(out, s)
	}
	return out, rows.Err()
}

func (d *DB) GetSkill(id int64) (*domain.Skill, error) {
	var s domain.Skill
	var enabled int
	err := d.QueryRow(`SELECT id, name, description, instructions, enabled FROM skills WHERE id = ?`, id).
		Scan(&s.ID, &s.Name, &s.Description, &s.Instructions, &enabled)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	s.Enabled = enabled == 1
	return &s, nil
}

func (d *DB) GetSkillByName(name string) (*domain.Skill, error) {
	var s domain.Skill
	var enabled int
	err := d.QueryRow(`SELECT id, name, description, instructions, enabled FROM skills WHERE name = ?`, name).
		Scan(&s.ID, &s.Name, &s.Description, &s.Instructions, &enabled)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	s.Enabled = enabled == 1
	return &s, nil
}

func (d *DB) AddSkill(s *domain.Skill) error {
	res, err := d.Exec(`INSERT INTO skills(name, description, instructions, enabled) VALUES(?, ?, ?, ?)`,
		s.Name, s.Description, s.Instructions, b2i(s.Enabled))
	if err != nil {
		return err
	}
	s.ID, _ = res.LastInsertId()
	return nil
}

func (d *DB) UpdateSkill(s *domain.Skill) error {
	_, err := d.Exec(`UPDATE skills SET name = ?, description = ?, instructions = ?, enabled = ? WHERE id = ?`,
		s.Name, s.Description, s.Instructions, b2i(s.Enabled), s.ID)
	return err
}

func (d *DB) DeleteSkill(id int64) error {
	_, err := d.Exec(`DELETE FROM skills WHERE id = ?`, id)
	return err
}

// ---- MCP ----

func (d *DB) ListMcpServers() ([]domain.McpServer, error) {
	rows, err := d.Query(`SELECT id, name, transport, command, args_json, env_json, url, headers_json, enabled, auto_connect FROM mcp_servers ORDER BY name`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.McpServer{}
	for rows.Next() {
		var s domain.McpServer
		var enabled, ac int
		if err := rows.Scan(&s.ID, &s.Name, &s.Transport, &s.Command, &s.ArgsJSON, &s.EnvJSON, &s.URL, &s.HeadersJSON, &enabled, &ac); err != nil {
			return nil, err
		}
		s.Enabled = enabled == 1
		s.AutoConnect = ac == 1
		out = append(out, s)
	}
	return out, rows.Err()
}

func (d *DB) GetMcpServer(id int64) (*domain.McpServer, error) {
	var s domain.McpServer
	var enabled, ac int
	err := d.QueryRow(`SELECT id, name, transport, command, args_json, env_json, url, headers_json, enabled, auto_connect FROM mcp_servers WHERE id = ?`, id).
		Scan(&s.ID, &s.Name, &s.Transport, &s.Command, &s.ArgsJSON, &s.EnvJSON, &s.URL, &s.HeadersJSON, &enabled, &ac)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	s.Enabled = enabled == 1
	s.AutoConnect = ac == 1
	return &s, nil
}

func (d *DB) AddMcpServer(s *domain.McpServer) error {
	res, err := d.Exec(`INSERT INTO mcp_servers(name, transport, command, args_json, env_json, url, headers_json, enabled, auto_connect) VALUES(?, ?, ?, ?, ?, ?, ?, ?, ?)`,
		s.Name, s.Transport, nullStr(s.Command), s.ArgsJSON, s.EnvJSON, s.URL, s.HeadersJSON, b2i(s.Enabled), b2i(s.AutoConnect))
	if err != nil {
		return err
	}
	s.ID, _ = res.LastInsertId()
	return nil
}

func (d *DB) UpdateMcpServer(s *domain.McpServer) error {
	_, err := d.Exec(`UPDATE mcp_servers SET name = ?, transport = ?, command = ?, args_json = ?, env_json = ?, url = ?, headers_json = ?, enabled = ?, auto_connect = ? WHERE id = ?`,
		s.Name, s.Transport, nullStr(s.Command), s.ArgsJSON, s.EnvJSON, s.URL, s.HeadersJSON, b2i(s.Enabled), b2i(s.AutoConnect), s.ID)
	return err
}

func (d *DB) DeleteMcpServer(id int64) error {
	_, err := d.Exec(`DELETE FROM mcp_servers WHERE id = ?`, id)
	return err
}

func (d *DB) ListMcpTools(serverID int64) ([]domain.McpTool, error) {
	rows, err := d.Query(`SELECT id, server_id, name, description, schema_json FROM mcp_tools WHERE server_id = ? ORDER BY name`, serverID)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.McpTool{}
	for rows.Next() {
		var t domain.McpTool
		if err := rows.Scan(&t.ID, &t.ServerID, &t.Name, &t.Description, &t.SchemaJSON); err != nil {
			return nil, err
		}
		out = append(out, t)
	}
	return out, rows.Err()
}

func (d *DB) ReplaceMcpTools(serverID int64, tools []domain.McpToolInfo) error {
	tx, err := d.Begin()
	if err != nil {
		return err
	}
	defer tx.Rollback()
	if _, err := tx.Exec(`DELETE FROM mcp_tools WHERE server_id = ?`, serverID); err != nil {
		return err
	}
	for _, t := range tools {
		if _, err := tx.Exec(`INSERT INTO mcp_tools(server_id, name, description, schema_json) VALUES(?, ?, ?, ?)`,
			serverID, t.Name, t.Description, t.SchemaJSON); err != nil {
			return err
		}
	}
	return tx.Commit()
}
