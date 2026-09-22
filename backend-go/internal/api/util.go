package api

import (
	"crypto/rand"
	"encoding/hex"
	"os"
	"time"
)

func osStat(path string) (os.FileInfo, error) { return os.Stat(path) }

func timeNow() time.Time { return time.Now() }

const timeRFC3339 = time.RFC3339

// newID 生成会话/记录 ID。
func newID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	return hex.EncodeToString(b)
}
