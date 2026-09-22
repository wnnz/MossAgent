// Package proxy：按代理配置构建 HTTP 客户端（http / socks5）。
package proxy

import (
	"fmt"
	"net"
	"net/http"
	"net/url"
	"strings"
	"time"

	"golang.org/x/net/proxy"

	"codingagent/internal/domain"
)

// Factory 按代理配置构建 *http.Client（SSE 流式需无整体超时）。
type Factory struct{}

// ClientFor 返回带代理的 HTTP 客户端；proxy 为 nil 时直连。
func (Factory) ClientFor(p *domain.Proxy) (*http.Client, error) {
	transport := &http.Transport{
		// SSE 流式：不设整体响应超时，由请求 ctx 控制
		DisableKeepAlives: false,
		ForceAttemptHTTP2: true,
		// 拨号超时：连不上时 30s 内报错，避免 chat 无限期挂起
		DialContext: (&net.Dialer{Timeout: 30 * time.Second, KeepAlive: 30 * time.Second}).DialContext,
		TLSHandshakeTimeout: 15 * time.Second,
	}

	if p != nil {
		switch domain.ProxyScheme(p.Scheme) {
		case domain.SchemeSocks5:
			var auth *proxy.Auth
			if p.Username != nil && *p.Username != "" {
				pass := ""
				if p.Password != nil {
					pass = *p.Password
				}
				auth = &proxy.Auth{User: *p.Username, Password: pass}
			}
			dialer, err := proxy.SOCKS5("tcp", fmt.Sprintf("%s:%d", p.Host, p.Port), auth, proxy.Direct)
			if err != nil {
				return nil, fmt.Errorf("创建 SOCKS5 拨号器失败: %w", err)
			}
			contextDialer, ok := dialer.(proxy.ContextDialer)
			if !ok {
				return nil, fmt.Errorf("SOCKS5 拨号器不支持 ctx")
			}
			transport.DialContext = contextDialer.DialContext
		case domain.SchemeHTTP, "":
			u := url.URL{Scheme: "http", Host: fmt.Sprintf("%s:%d", p.Host, p.Port)}
			if p.Username != nil && *p.Username != "" {
				pass := ""
				if p.Password != nil {
					pass = *p.Password
				}
				u.User = url.UserPassword(*p.Username, pass)
			}
			transport.Proxy = http.ProxyURL(&u)
		default:
			return nil, fmt.Errorf("不支持的代理协议: %s", strings.TrimSpace(string(p.Scheme)))
		}
	}

	return &http.Client{
		Transport: transport,
		// SSE 流式：零超时，由 ctx 控制
		Timeout: 0,
	}, nil
}

// TestProxy 连通性测试（经代理 GET generate_204，15s 超时）。
func TestProxy(p *domain.Proxy) (bool, string, int) {
	client, err := Factory{}.ClientFor(p)
	if err != nil {
		return false, err.Error(), 0
	}
	client.Timeout = 15 * time.Second
	start := time.Now()
	resp, err := client.Get("https://www.gstatic.com/generate_204")
	latency := int(time.Since(start).Milliseconds())
	if err != nil {
		return false, err.Error(), 0
	}
	defer resp.Body.Close()
	return true, fmt.Sprintf("连通成功（HTTP %d）", resp.StatusCode), latency
}
