package api

import (
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strconv"

	"codingagent/internal/domain"
)

// ---- projects ----

type projectBody struct {
	Name string `json:"name"`
	Path string `json:"path"`
}

func (s *Server) handleListProjects(w http.ResponseWriter, r *http.Request) {
	list, err := s.Proxies.ListProjects()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, list)
}

func (s *Server) handleCreateProject(w http.ResponseWriter, r *http.Request) {
	var body projectBody
	if err := decodeBody(r, &body); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if body.Name == "" {
		writeError(w, http.StatusBadRequest, "name 不能为空")
		return
	}

	path := body.Path
	if path == "" {
		// 创建：无路径时在工作区默认目录下建子目录（安全，拒绝任意路径）
		path = filepath.Join(s.WorkspaceRoot, body.Name)
		if err := os.MkdirAll(path, 0o755); err != nil {
			writeError(w, http.StatusBadRequest, "创建项目目录失败: "+err.Error())
			return
		}
	}

	p := &domain.Project{Name: body.Name, Path: path, CreatedAt: nowRFC3339()}
	if err := s.Proxies.AddProject(p); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusCreated, p)
}

func (s *Server) handleDeleteProject(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	if err := s.Proxies.DeleteProject(id); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	w.WriteHeader(http.StatusNoContent)
}

// handleProjectBranch 返回项目目录的 git 分支（失败回退 main）。
func (s *Server) handleProjectBranch(w http.ResponseWriter, r *http.Request) {
	id, _ := strconv.ParseInt(r.PathValue("id"), 10, 64)
	p, err := s.Proxies.GetProject(id)
	if err != nil {
		writeError(w, http.StatusNotFound, "项目不存在")
		return
	}

	branch := "main"
	if p.Path != "" {
		cmd := exec.Command("git", "rev-parse", "--abbrev-ref", "HEAD")
		cmd.Dir = p.Path
		if out, err := cmd.Output(); err == nil {
			b := string(out)
			if len(b) > 0 && b[len(b)-1] == '\n' {
				b = b[:len(b)-1]
			}
			if b != "" {
				branch = b
			}
		}
	}
	writeJSON(w, http.StatusOK, map[string]any{"branch": branch})
}
