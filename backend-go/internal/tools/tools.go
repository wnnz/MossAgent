// Package tools：内置工具集与注册表。
package tools

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"os"
	"os/exec"
	"path/filepath"
	"regexp"
	"runtime"
	"strconv"
	"strings"
	"time"

	"codingagent/internal/domain"
	"codingagent/internal/workspace"
)

// ---- 参数解析辅助 ----

func argString(argsJSON, key, fallback string) string {
	var m map[string]any
	if err := json.Unmarshal([]byte(orJSON(argsJSON)), &m); err != nil {
		return fallback
	}
	if v, ok := m[key].(string); ok {
		return v
	}
	return fallback
}

func argBool(argsJSON, key string, fallback bool) bool {
	var m map[string]any
	if err := json.Unmarshal([]byte(orJSON(argsJSON)), &m); err != nil {
		return fallback
	}
	if v, ok := m[key].(bool); ok {
		return v
	}
	return fallback
}

func argInt(argsJSON, key string, fallback int) int {
	var m map[string]any
	if err := json.Unmarshal([]byte(orJSON(argsJSON)), &m); err != nil {
		return fallback
	}
	if v, ok := m[key].(float64); ok {
		return int(v)
	}
	return fallback
}

func orJSON(s string) string {
	if strings.TrimSpace(s) == "" {
		return "{}"
	}
	return s
}

func truncate(s string, max int) string {
	if len(s) <= max {
		return s
	}
	return s[:max] + fmt.Sprintf("\n[输出已截断，原始长度 %d]", len(s))
}

func fail(err error) domain.ToolResult {
	return domain.ToolResult{Success: false, Output: err.Error()}
}

// resolveInWorkspace 用会话工作区解析路径。
func resolveInWorkspace(ctx domain.ToolContext, path string) (string, error) {
	guard, err := workspace.New(ctx.WorkspacePath)
	if err != nil {
		return "", err
	}
	return guard.Resolve(path)
}

// ---- read_file ----

type ReadFile struct{}

func (ReadFile) Name() string { return "read_file" }
func (ReadFile) Description() string {
	return "读取工作区内文件内容。参数: {\"path\": \"相对路径\"}"
}
func (ReadFile) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"path":{"type":"string","description":"文件相对路径"}},"required":["path"]}`
}
func (ReadFile) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	path, err := resolveInWorkspace(ctx, argString(argsJSON, "path", ""))
	if err != nil {
		return fail(err)
	}
	info, err := os.Stat(path)
	if err != nil {
		return domain.ToolResult{Success: false, Output: "文件不存在: " + path}
	}
	if info.Size() > 256*1024 {
		return domain.ToolResult{Success: false, Output: fmt.Sprintf("文件过大（%d 字节），超出读取上限 %d", info.Size(), 256*1024)}
	}
	data, err := os.ReadFile(path)
	if err != nil {
		return fail(err)
	}
	return domain.ToolResult{Success: true, Output: truncate(string(data), 32_000)}
}

// ---- write_file ----

type WriteFile struct{}

func (WriteFile) Name() string { return "write_file" }
func (WriteFile) Description() string {
	return "写入/覆盖工作区内文件（自动创建目录）。参数: {\"path\": \"相对路径\", \"content\": \"内容\"}"
}
func (WriteFile) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"path":{"type":"string"},"content":{"type":"string"}},"required":["path","content"]}`
}
func (WriteFile) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	path, err := resolveInWorkspace(ctx, argString(argsJSON, "path", ""))
	if err != nil {
		return fail(err)
	}
	content := argString(argsJSON, "content", "")
	if dir := filepath.Dir(path); dir != "" {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return fail(err)
		}
	}
	if err := os.WriteFile(path, []byte(content), 0o644); err != nil {
		return fail(err)
	}
	return domain.ToolResult{Success: true, Output: fmt.Sprintf("已写入 %s（%d 字符）", path, len(content))}
}

// ---- edit_file ----

type EditFile struct{}

func (EditFile) Name() string { return "edit_file" }
func (EditFile) Description() string {
	return "精确替换工作区内文件中的字符串。参数: {\"path\": \"相对路径\", \"old_string\": \"待替换\", \"new_string\": \"替换为\", \"replace_all\": false}"
}
func (EditFile) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"path":{"type":"string"},"old_string":{"type":"string"},"new_string":{"type":"string"},"replace_all":{"type":"boolean"}},"required":["path","old_string","new_string"]}`
}
func (EditFile) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	path, err := resolveInWorkspace(ctx, argString(argsJSON, "path", ""))
	if err != nil {
		return fail(err)
	}
	oldString := argString(argsJSON, "old_string", "")
	newString := argString(argsJSON, "new_string", "")
	replaceAll := argBool(argsJSON, "replace_all", false)

	data, err := os.ReadFile(path)
	if err != nil {
		return domain.ToolResult{Success: false, Output: "文件不存在: " + path}
	}
	if oldString == "" {
		return domain.ToolResult{Success: false, Output: "old_string 不能为空"}
	}
	content := string(data)
	count := strings.Count(content, oldString)
	if count == 0 {
		return domain.ToolResult{Success: false, Output: "old_string 在文件中未找到"}
	}
	if count > 1 && !replaceAll {
		return domain.ToolResult{Success: false, Output: fmt.Sprintf("old_string 出现 %d 次，请提供更长的上下文或设置 replace_all=true", count)}
	}
	if replaceAll {
		content = strings.ReplaceAll(content, oldString, newString)
	} else {
		content = strings.Replace(content, oldString, newString, 1)
	}
	if err := os.WriteFile(path, []byte(content), 0o644); err != nil {
		return fail(err)
	}
	replaced := 1
	if replaceAll {
		replaced = count
	}
	return domain.ToolResult{Success: true, Output: fmt.Sprintf("已替换 %s 中 %d 处", path, replaced)}
}

// ---- list_dir ----

type ListDir struct{}

func (ListDir) Name() string { return "list_dir" }
func (ListDir) Description() string {
	return "列出工作区内目录内容（含子目录与文件大小）。参数: {\"path\": \"相对路径，可省略\"}"
}
func (ListDir) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"path":{"type":"string","description":"目录相对路径，省略时为工作区根"}}}`
}
func (ListDir) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	guard, err := workspace.New(ctx.WorkspacePath)
	if err != nil {
		return fail(err)
	}
	path, err := guard.Resolve(argString(argsJSON, "path", ""))
	if err != nil {
		return fail(err)
	}
	entries, err := os.ReadDir(path)
	if err != nil {
		return domain.ToolResult{Success: false, Output: "目录不存在: " + path}
	}
	rel, _ := filepath.Rel(guard.Root(), path)
	var sb strings.Builder
	sb.WriteString("目录 " + rel + "\n")
	for _, e := range entries {
		if e.IsDir() {
			sb.WriteString("[dir]  " + e.Name() + "\n")
		} else {
			if info, err := e.Info(); err == nil {
				sb.WriteString(fmt.Sprintf("[file] %s (%d B)\n", e.Name(), info.Size()))
			}
		}
	}
	return domain.ToolResult{Success: true, Output: truncate(sb.String(), 32_000)}
}

// ---- glob ----

type Glob struct{}

func (Glob) Name() string { return "glob" }
func (Glob) Description() string {
	return "按 glob 模式搜索工作区文件（支持 ** 递归）。参数: {\"pattern\": \"**/*.go\", \"path\": \"可选基目录\"}"
}
func (Glob) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"pattern":{"type":"string"},"path":{"type":"string"}},"required":["pattern"]}`
}
func (Glob) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	guard, err := workspace.New(ctx.WorkspacePath)
	if err != nil {
		return fail(err)
	}
	pattern := argString(argsJSON, "pattern", "")
	if pattern == "" {
		return domain.ToolResult{Success: false, Output: "pattern 不能为空"}
	}
	base, err := guard.Resolve(argString(argsJSON, "path", ""))
	if err != nil {
		return fail(err)
	}
	var matches []string
	root := base
	filepath.WalkDir(root, func(p string, d os.DirEntry, err error) error {
		if err != nil || d.IsDir() {
			return nil
		}
		rel, _ := filepath.Rel(base, p)
		if matchGlob(pattern, rel) || matchGlob(pattern, filepath.ToSlash(rel)) {
			matches = append(matches, rel)
		}
		return nil
	})
	if len(matches) == 0 {
		return domain.ToolResult{Success: true, Output: "无匹配文件"}
	}
	return domain.ToolResult{Success: true, Output: truncate(strings.Join(matches, "\n"), 32_000)}
}

// matchGlob 支持 ** 的简易 glob 匹配。
func matchGlob(pattern, name string) bool {
	return globMatch(strings.Split(filepath.ToSlash(pattern), "/"), strings.Split(filepath.ToSlash(name), "/"))
}

func globMatch(pattern, name []string) bool {
	if len(pattern) == 0 {
		return len(name) == 0
	}
	if pattern[0] == "**" {
		// ** 匹配零层或多层
		for i := 0; i <= len(name); i++ {
			if globMatch(pattern[1:], name[i:]) {
				return true
			}
		}
		return false
	}
	if len(name) == 0 {
		return false
	}
	ok, _ := filepath.Match(pattern[0], name[0])
	if !ok {
		return false
	}
	return globMatch(pattern[1:], name[1:])
}

// ---- grep ----

type Grep struct{}

func (Grep) Name() string { return "grep" }
func (Grep) Description() string {
	return "正则搜索工作区文件内容。参数: {\"pattern\": \"正则\", \"path\": \"可选目录\", \"glob\": \"可选文件过滤如 *.go\", \"ignore_case\": true}"
}
func (Grep) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"pattern":{"type":"string"},"path":{"type":"string"},"glob":{"type":"string"},"ignore_case":{"type":"boolean"}},"required":["pattern"]}`
}
func (Grep) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	guard, err := workspace.New(ctx.WorkspacePath)
	if err != nil {
		return fail(err)
	}
	pattern := argString(argsJSON, "pattern", "")
	if pattern == "" {
		return domain.ToolResult{Success: false, Output: "pattern 不能为空"}
	}
	pathArg := argString(argsJSON, "path", "")
	root := guard.Root()
	if pathArg != "" {
		if root, err = guard.Resolve(pathArg); err != nil {
			return fail(err)
		}
	}
	globFilter := argString(argsJSON, "glob", "")
	ignoreCase := argBool(argsJSON, "ignore_case", true)

	flags := ""
	if ignoreCase {
		flags = "(?i)"
	}
	re, err := regexp.Compile(flags + pattern)
	if err != nil {
		return domain.ToolResult{Success: false, Output: "正则编译失败: " + err.Error()}
	}

	var results []string
	maxMatches := 200
	_ = filepath.WalkDir(root, func(p string, d os.DirEntry, err error) error {
		if err != nil || d.IsDir() {
			return nil
		}
		if globFilter != "" {
			ok, _ := filepath.Match(globFilter, d.Name())
			if !ok {
				return nil
			}
		}
		info, err := d.Info()
		if err != nil || info.Size() > 1024*1024 {
			return nil
		}
		data, err := os.ReadFile(p)
		if err != nil || len(data) == 0 || containsNull(data) {
			return nil
		}
		rel, _ := filepath.Rel(guard.Root(), p)
		for i, line := range strings.Split(string(data), "\n") {
			if re.MatchString(line) {
				results = append(results, fmt.Sprintf("%s:%d: %s", filepath.ToSlash(rel), i+1, strings.TrimSpace(line)))
				if len(results) >= maxMatches {
					return filepath.SkipAll
				}
			}
		}
		return nil
	})
	if len(results) == 0 {
		return domain.ToolResult{Success: true, Output: "无匹配"}
	}
	suffix := ""
	if len(results) >= maxMatches {
		suffix = fmt.Sprintf("\n[已达 %d 条上限]", maxMatches)
	}
	return domain.ToolResult{Success: true, Output: truncate(strings.Join(results, "\n")+suffix, 32_000)}
}

func containsNull(data []byte) bool {
	for _, b := range data[:min(len(data), 512)] {
		if b == 0 {
			return true
		}
	}
	return false
}

func min(a, b int) int {
	if a < b {
		return a
	}
	return b
}

// ---- run_command ----

type RunCommand struct{}

func (RunCommand) Name() string { return "run_command" }
func (RunCommand) Description() string {
	return "在工作区内执行命令（限时限量，Windows 用 cmd /c，其他平台用 bash -c）。参数: {\"command\": \"命令\", \"cwd\": \"可选相对工作目录\", \"timeout_ms\": 可选超时毫秒}"
}
func (RunCommand) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"command":{"type":"string"},"cwd":{"type":"string"},"timeout_ms":{"type":"integer"}},"required":["command"]}`
}
func (RunCommand) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	command := argString(argsJSON, "command", "")
	if command == "" {
		return domain.ToolResult{Success: false, Output: "command 不能为空"}
	}
	timeoutMs := argInt(argsJSON, "timeout_ms", 120_000)
	if timeoutMs < 1000 {
		timeoutMs = 1000
	}
	if timeoutMs > 600_000 {
		timeoutMs = 600_000
	}

	guard, err := workspace.New(ctx.WorkspacePath)
	if err != nil {
		return fail(err)
	}
	cwd := argString(argsJSON, "cwd", "")
	workDir := guard.Root()
	if cwd != "" {
		if workDir, err = guard.Resolve(cwd); err != nil {
			return fail(err)
		}
	}

	var cmd *exec.Cmd
	if runtime.GOOS == "windows" {
		cmd = exec.Command("cmd", "/c", command)
	} else {
		cmd = exec.Command("bash", "-c", command)
	}
	cmd.Dir = workDir
	var stdout, stderr strings.Builder
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr

	if err := cmd.Start(); err != nil {
		return domain.ToolResult{Success: false, Output: "无法启动进程: " + err.Error()}
	}

	done := make(chan error, 1)
	go func() { done <- cmd.Wait() }()

	select {
	case err := <-done:
		output := fmt.Sprintf("--- stdout ---\n%s\n--- stderr ---\n%s", stdout.String(), stderr.String())
		return domain.ToolResult{Success: err == nil, Output: truncate(output, 32_000)}
	case <-time.After(time.Duration(timeoutMs) * time.Millisecond):
		if cmd.Process != nil {
			// Windows 终止整个进程树；其他平台仅终止主进程（bash -c 单进程为主）
			if runtime.GOOS == "windows" {
				kill := exec.Command("taskkill", "/T", "/F", "/PID", strconv.Itoa(cmd.Process.Pid))
				_ = kill.Run()
			} else {
				_ = cmd.Process.Kill()
			}
		}
		return domain.ToolResult{Success: false, Output: fmt.Sprintf("命令超时（%dms）已终止", timeoutMs)}
	}
}

// ---- memory_save / memory_search ----

type MemoryStore interface {
	AddMemory(scope, title, content, tags string) error
	SearchMemories(q string) ([]MemoryHit, error)
}

type MemoryHit struct {
	Scope, Title, Content, Tags string
}

type MemorySave struct{ Store MemoryStore }

func (MemorySave) Name() string { return "memory_save" }
func (MemorySave) Description() string {
	return "保存记忆条目。参数: {\"scope\": \"global|project\", \"title\": \"标题\", \"content\": \"内容\", \"tags\": \"可选，逗号分隔\"}"
}
func (MemorySave) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"scope":{"type":"string","enum":["global","project"]},"title":{"type":"string"},"content":{"type":"string"},"tags":{"type":"string"}},"required":["scope","title","content"]}`
}
func (t MemorySave) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	title := argString(argsJSON, "title", "")
	content := argString(argsJSON, "content", "")
	if title == "" || content == "" {
		return domain.ToolResult{Success: false, Output: "title 和 content 不能为空"}
	}
	scope := argString(argsJSON, "scope", "global")
	tags := argString(argsJSON, "tags", "")
	if err := t.Store.AddMemory(scope, title, content, tags); err != nil {
		return fail(err)
	}
	return domain.ToolResult{Success: true, Output: fmt.Sprintf("已保存记忆: [%s] %s", scope, title)}
}

type MemorySearch struct{ Store MemoryStore }

func (MemorySearch) Name() string { return "memory_search" }
func (MemorySearch) Description() string {
	return "按关键词检索记忆。参数: {\"query\": \"关键词\"}"
}
func (MemorySearch) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"query":{"type":"string"}},"required":["query"]}`
}
func (t MemorySearch) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	query := argString(argsJSON, "query", "")
	if query == "" {
		return domain.ToolResult{Success: false, Output: "query 不能为空"}
	}
	hits, err := t.Store.SearchMemories(query)
	if err != nil {
		return fail(err)
	}
	if len(hits) == 0 {
		return domain.ToolResult{Success: true, Output: "无匹配记忆"}
	}
	var parts []string
	for _, h := range hits {
		suffix := ""
		if h.Tags != "" {
			suffix = " (tags: " + h.Tags + ")"
		}
		parts = append(parts, fmt.Sprintf("[%s] %s%s\n%s", h.Scope, h.Title, suffix, h.Content))
	}
	return domain.ToolResult{Success: true, Output: truncate(strings.Join(parts, "\n---\n"), 32_000)}
}

// ---- skill_load ----

type SkillLoader interface {
	LoadSkill(name string) (title, instructions string, err error)
}

type SkillLoad struct{ Store SkillLoader }

func (SkillLoad) Name() string { return "skill_load" }
func (SkillLoad) Description() string {
	return "按名加载技能正文。参数: {\"name\": \"技能名\"}"
}
func (SkillLoad) ParametersSchemaJSON() string {
	return `{"type":"object","properties":{"name":{"type":"string"}},"required":["name"]}`
}
func (t SkillLoad) Execute(argsJSON string, ctx domain.ToolContext) domain.ToolResult {
	name := argString(argsJSON, "name", "")
	if name == "" {
		return domain.ToolResult{Success: false, Output: "name 不能为空"}
	}
	title, instructions, err := t.Store.LoadSkill(name)
	if err != nil {
		return domain.ToolResult{Success: false, Output: "技能不存在或未启用: " + name}
	}
	return domain.ToolResult{Success: true, Output: "# " + title + "\n\n" + instructions}
}

// ---- new call id ----

// NewCallID 生成工具调用 ID。
func NewCallID() string {
	b := make([]byte, 8)
	_, _ = rand.Read(b)
	return "call_" + hex.EncodeToString(b)
}
