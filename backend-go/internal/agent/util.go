package agent

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"time"
)

func jsonUnmarshalList(s string, v any) error {
	return json.Unmarshal([]byte(s), v)
}

func newSessionID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	return hex.EncodeToString(b) + "-" + hex.EncodeToString([]byte(time.Now().Format("0102")))
}
