// Package llm：LLM 客户端（OpenAI 兼容 / Anthropic SSE 流式）。
package llm

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
)

// pumpSSE 行式增量读取 SSE 流的 data: 行（破坏性最小：回调式，保持真流式）。
func pumpSSE(resp *http.Response, onData func(line string) bool) error {
	reader := bufio.NewReader(resp.Body)
	for {
		line, err := reader.ReadString('\n')
		line = strings.TrimRight(line, "\r\n")
		if strings.HasPrefix(line, "data:") {
			data := strings.TrimSpace(line[len("data:"):])
			if !onData(data) {
				// 消费方中止（取消/失败）：停止读取
				return fmt.Errorf("流已中止")
			}
		}
		if err != nil {
			if err == io.EOF {
				return nil
			}
			return fmt.Errorf("读取 SSE 流失败: %w", err)
		}
	}
}

// decodeJSON 宽松解析 JSON，失败返回 false。
func decodeJSON(data string, v any) bool {
	if data == "" {
		return false
	}
	if err := json.Unmarshal([]byte(data), v); err != nil {
		return false
	}
	return true
}
