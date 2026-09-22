package store

import (
	"database/sql"

	"codingagent/internal/domain"
)

// ---- 项目 ----

func (d *DB) ListProjects() ([]domain.Project, error) {
	rows, err := d.Query(`SELECT id, name, path, created_at FROM projects ORDER BY created_at ASC`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := []domain.Project{}
	for rows.Next() {
		var p domain.Project
		if err := rows.Scan(&p.ID, &p.Name, &p.Path, &p.CreatedAt); err != nil {
			return nil, err
		}
		out = append(out, p)
	}
	return out, rows.Err()
}

func (d *DB) GetProject(id int64) (*domain.Project, error) {
	var p domain.Project
	err := d.QueryRow(`SELECT id, name, path, created_at FROM projects WHERE id = ?`, id).
		Scan(&p.ID, &p.Name, &p.Path, &p.CreatedAt)
	if err == sql.ErrNoRows {
		return nil, ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	return &p, nil
}

func (d *DB) AddProject(p *domain.Project) error {
	res, err := d.Exec(`INSERT INTO projects(name, path, created_at) VALUES(?, ?, ?)`, p.Name, p.Path, p.CreatedAt)
	if err != nil {
		return err
	}
	p.ID, _ = res.LastInsertId()
	return nil
}

func (d *DB) DeleteProject(id int64) error {
	_, err := d.Exec(`DELETE FROM projects WHERE id = ?`, id)
	return err
}
