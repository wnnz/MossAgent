// Package workspace：文件工具沙箱（拒绝路径穿越/绝对路径逃逸）。
package workspace

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

// Guard 工作区沙箱。
type Guard struct {
	root string
}

// New 创建沙箱并确保根目录存在。
func New(root string) (*Guard, error) {
	abs, err := filepath.Abs(root)
	if err != nil {
		return nil, err
	}
	if err := os.MkdirAll(abs, 0o755); err != nil {
		return nil, fmt.Errorf("创建工作区失败: %w", err)
	}
	return &Guard{root: abs}, nil
}

// Root 工作区根目录。
func (g *Guard) Root() string { return g.root }

// Resolve 解析路径为工作区内绝对路径；越界返回错误。
func (g *Guard) Resolve(path string) (string, error) {
	if strings.TrimSpace(path) == "" {
		return g.root, nil
	}
	var full string
	if filepath.IsAbs(path) {
		abs, err := filepath.Abs(path)
		if err != nil {
			return "", err
		}
		full = abs
	} else {
		abs, err := filepath.Abs(filepath.Join(g.root, path))
		if err != nil {
			return "", err
		}
		full = abs
	}
	if err := g.EnsureInside(full); err != nil {
		return "", err
	}
	return full, nil
}

// EnsureInside 校验路径位于工作区内。
func (g *Guard) EnsureInside(fullPath string) error {
	root := g.root
	if !strings.HasSuffix(root, string(filepath.Separator)) {
		root += string(filepath.Separator)
	}
	candidate, err := filepath.Abs(fullPath)
	if err != nil {
		return err
	}
	// Windows 大小写不敏感比较；等价 OrdIgnoreCase 语义
	if !strings.HasPrefix(strings.ToLower(candidate), strings.ToLower(root)) {
		return fmt.Errorf("路径超出工作区范围: %s", fullPath)
	}
	return nil
}
