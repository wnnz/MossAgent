package workspace

import (
	"os"
	"path/filepath"
	"strings"
	"testing"
)

func TestResolve_RelativeInside(t *testing.T) {
	root := filepath.Join(os.TempDir(), "codingagent-go-tests", "root1")
	g, err := New(root)
	if err != nil {
		t.Fatal(err)
	}
	resolved, err := g.Resolve("sub/file.txt")
	if err != nil {
		t.Fatal(err)
	}
	if !strings.HasPrefix(strings.ToLower(resolved), strings.ToLower(g.Root())) {
		t.Fatalf("解析结果应在工作区内: %s", resolved)
	}
}

func TestResolve_DotDotTraversal_Throws(t *testing.T) {
	root := filepath.Join(os.TempDir(), "codingagent-go-tests", "root2")
	g, _ := New(root)
	if _, err := g.Resolve("../outside.txt"); err == nil {
		t.Fatal(".. 穿越应拒绝")
	}
}

func TestResolve_DeepTraversal_Throws(t *testing.T) {
	root := filepath.Join(os.TempDir(), "codingagent-go-tests", "root3")
	g, _ := New(root)
	if _, err := g.Resolve("a/b/../../../etc/passwd"); err == nil {
		t.Fatal("深层穿越应拒绝")
	}
}

func TestResolve_AbsoluteOutside_Throws(t *testing.T) {
	root := filepath.Join(os.TempDir(), "codingagent-go-tests", "root4")
	g, _ := New(root)
	outside := filepath.Join(os.TempDir(), "outside-secret.txt")
	if _, err := g.Resolve(outside); err == nil {
		t.Fatal("绝对路径逃逸应拒绝")
	}
}

func TestResolve_SimilarPrefixDirectory_Throws(t *testing.T) {
	root := filepath.Join(os.TempDir(), "codingagent-go-tests", "root5")
	g, _ := New(root)
	root2 := root + "2"
	if err := os.MkdirAll(root2, 0o755); err != nil {
		t.Fatal(err)
	}
	defer os.RemoveAll(root2)
	if _, err := g.Resolve(filepath.Join(root2, "x.txt")); err == nil {
		t.Fatal("同前缀不同目录应拒绝")
	}
}

func TestResolve_Empty_ReturnsRoot(t *testing.T) {
	root := filepath.Join(os.TempDir(), "codingagent-go-tests", "root6")
	g, _ := New(root)
	resolved, err := g.Resolve("")
	if err != nil {
		t.Fatal(err)
	}
	if resolved != g.Root() {
		t.Fatalf("空路径应返回根: %s", resolved)
	}
}
