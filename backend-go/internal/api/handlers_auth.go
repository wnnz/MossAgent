package api

import (
	"net/http"
	"time"

	"codingagent/internal/domain"
	"codingagent/internal/security"
)

const (
	sevenDays  = 7 * 24 * time.Hour
	thirtyDays = 30 * 24 * time.Hour
)

func authRow(hash, salt string) *domain.AuthRecord {
	return &domain.AuthRecord{PasswordHash: hash, Salt: salt, CreatedAt: nowRFC3339()}
}

// ---- 认证 ----

type authRequest struct {
	Password string `json:"password"`
}

type loginRequest struct {
	Password   string `json:"password"`
	RememberMe bool   `json:"rememberMe"`
}

type changePasswordRequest struct {
	CurrentPassword string `json:"currentPassword"`
	NewPassword     string `json:"newPassword"`
}

func (s *Server) handleAuthStatus(w http.ResponseWriter, r *http.Request) {
	record, err := s.Proxies.GetAuth()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{
		"needsSetup":    record == nil,
		"authenticated": s.isAuthenticated(r),
	})
}

func (s *Server) handleAuthSetup(w http.ResponseWriter, r *http.Request) {
	var req authRequest
	if err := decodeBody(r, &req); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if len(req.Password) < 6 {
		writeError(w, http.StatusBadRequest, "密码至少 6 位")
		return
	}
	existing, err := s.Proxies.GetAuth()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	if existing != nil {
		writeError(w, http.StatusConflict, "密码已设置，不允许重复初始化")
		return
	}

	salt, err := security.GenerateSalt()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	hash, err := security.HashPassword(req.Password, salt)
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	if err := s.Proxies.AddAuth(authRow(hash, salt)); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	s.setCookie(w, s.Tokens.IssueToken(sevenDays))
	writeJSON(w, http.StatusOK, map[string]any{"needsSetup": false, "authenticated": true})
}

func (s *Server) handleAuthLogin(w http.ResponseWriter, r *http.Request) {
	var req loginRequest
	if err := decodeBody(r, &req); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	record, err := s.Proxies.GetAuth()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	if record == nil || !security.VerifyPassword(req.Password, record.Salt, record.PasswordHash) {
		writeError(w, http.StatusUnauthorized, "密码错误")
		return
	}
	lifetime := sevenDays
	if req.RememberMe {
		lifetime = thirtyDays
	}
	s.setCookie(w, s.Tokens.IssueToken(lifetime))
	writeJSON(w, http.StatusOK, map[string]any{"needsSetup": false, "authenticated": true})
}

func (s *Server) handleAuthLogout(w http.ResponseWriter, r *http.Request) {
	http.SetCookie(w, &http.Cookie{Name: cookieName, Value: "", Path: "/", MaxAge: -1, HttpOnly: true, SameSite: http.SameSiteLaxMode})
	writeJSON(w, http.StatusOK, map[string]any{"message": "已登出"})
}

func (s *Server) handleAuthPassword(w http.ResponseWriter, r *http.Request) {
	var req changePasswordRequest
	if err := decodeBody(r, &req); err != nil {
		writeError(w, http.StatusBadRequest, "请求体解析失败")
		return
	}
	if len(req.NewPassword) < 6 {
		writeError(w, http.StatusBadRequest, "新密码至少 6 位")
		return
	}
	record, err := s.Proxies.GetAuth()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	if record == nil {
		writeError(w, http.StatusNotFound, "尚未设置密码")
		return
	}
	if !security.VerifyPassword(req.CurrentPassword, record.Salt, record.PasswordHash) {
		writeError(w, http.StatusUnauthorized, "当前密码错误")
		return
	}
	salt, err := security.GenerateSalt()
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	hash, err := security.HashPassword(req.NewPassword, salt)
	if err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	record.Salt = salt
	record.PasswordHash = hash
	if err := s.Proxies.UpdateAuth(record); err != nil {
		writeError(w, http.StatusInternalServerError, err.Error())
		return
	}
	writeJSON(w, http.StatusOK, map[string]any{"message": "密码已更新"})
}
