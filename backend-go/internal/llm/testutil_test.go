package llm

import (
	"os"
	"path/filepath"
	"sync"
)

func strPtr(s string) *string { return &s }

func osTempDir() string { return os.TempDir() }

// httpCacheSize 供测试检查缓存数。
func (f *Factory) httpCacheSize() int {
	f.mu.Lock()
	defer f.mu.Unlock()
	return len(f.httpCache)
}

var _ = sync.Mutex{}
var _ = filepath.Join
