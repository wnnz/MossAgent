package llm

import (
	"context"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"strings"
	"sync"

	"codingagent/internal/domain"
	"codingagent/internal/proxy"
	"codingagent/internal/store"
)

// Factory 按提供商协议与代理配置创建客户端（HTTP 客户端按 provider+代理缓存，避免句柄泄漏）。
type Factory struct {
	Proxies    *store.DB
	mu         sync.Mutex
	httpCache  map[string]*http.Client
}

func NewFactory(proxies *store.DB) *Factory {
	return &Factory{Proxies: proxies, httpCache: map[string]*http.Client{}}
}

func (f *Factory) clientFor(p *domain.Provider) (*http.Client, error) {
	key := fmt.Sprintf("%d|%s|%s|", p.ID, p.Type, p.BaseURL)
	if p.ProxyID != nil {
		key += fmt.Sprintf("proxy=%d", *p.ProxyID)
	} else {
		key += "noproxy"
	}

	f.mu.Lock()
	defer f.mu.Unlock()
	if c, ok := f.httpCache[key]; ok {
		return c, nil
	}

	var proxyCfg *domain.Proxy
	if p.ProxyID != nil {
		cfg, err := f.Proxies.GetProxy(*p.ProxyID)
		if err != nil {
			return nil, fmt.Errorf("读取代理配置失败: %w", err)
		}
		proxyCfg = cfg
	}
	client, err := proxy.Factory{}.ClientFor(proxyCfg)
	if err != nil {
		return nil, err
	}
	f.httpCache[key] = client
	return client, nil
}

// ClientForPublic 构建（并缓存）带代理的 HTTP 客户端（供 API 层使用）。
func (f *Factory) ClientForPublic(p *domain.Provider) (*http.Client, error) { return f.clientFor(p) }

// Create 按协议返回流式客户端。
func (f *Factory) Create(p *domain.Provider) (func(context.Context, domain.LlmRequest, chan<- domain.LlmStreamEvent), error) {
	client, err := f.clientFor(p)
	if err != nil {
		return nil, err
	}
	switch domain.ProviderType(p.Type) {
	case domain.ProviderAnthropic:
		c := &AnthropicClient{HTTP: client, BaseURL: p.BaseURL, APIKey: p.APIKey}
		return c.Stream, nil
	case domain.ProviderOpenAiCompatible, "":
		c := &OpenAIClient{HTTP: client, BaseURL: p.BaseURL, APIKey: p.APIKey}
		return c.Stream, nil
	default:
		return nil, fmt.Errorf("不支持的提供商类型: %s", p.Type)
	}
}

// FetchModelIDs 拉取远端模型列表（GET /v1/models，anthropic 同样）。
func FetchModelIDs(ctx context.Context, p *domain.Provider, client *http.Client) ([]string, error) {
	base := strings.TrimRight(p.BaseURL, "/")
	url := base + "/v1/models"
	if strings.HasSuffix(strings.ToLower(base), "/v1") {
		url = base + "/models"
	}
	req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
	if err != nil {
		return nil, err
	}
	if domain.ProviderType(p.Type) == domain.ProviderAnthropic {
		req.Header.Set("x-api-key", p.APIKey)
		req.Header.Set("anthropic-version", "2023-06-01")
	} else if p.APIKey != "" {
		req.Header.Set("Authorization", "Bearer "+p.APIKey)
	}

	resp, err := client.Do(req)
	if err != nil {
		return nil, fmt.Errorf("请求模型列表失败: %w", err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		return nil, fmt.Errorf("模型列表返回 %d", resp.StatusCode)
	}
	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	var parsed struct {
		Data []struct {
			ID string `json:"id"`
		} `json:"data"`
	}
	if err := json.Unmarshal(body, &parsed); err != nil {
		return nil, fmt.Errorf("解析模型列表失败: %w", err)
	}
	seen := map[string]bool{}
	var ids []string
	for _, m := range parsed.Data {
		if m.ID != "" && !seen[m.ID] {
			seen[m.ID] = true
			ids = append(ids, m.ID)
		}
	}
	return ids, nil
}
