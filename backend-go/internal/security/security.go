// Package security：PBKDF2 密码哈希与 HS256 JWT（零外部依赖）。
package security

import (
	"crypto/hmac"
	"crypto/rand"
	"crypto/sha256"
	"crypto/subtle"
	"encoding/base64"
	"encoding/binary"
	"encoding/json"
	"fmt"
	"strings"
	"time"
)

// HashPassword PBKDF2-SHA256（100k 迭代，32 字节），格式与内部校验配套。
const (
	saltSize   = 32
	hashSize   = 32
	iterations = 100_000
)

// GenerateSalt 生成随机盐（base64）。
func GenerateSalt() (string, error) {
	salt := make([]byte, saltSize)
	if _, err := rand.Read(salt); err != nil {
		return "", err
	}
	return base64.StdEncoding.EncodeToString(salt), nil
}

// pbkdf2 RFC 2898 标准实现（stdlib HMAC-SHA256）。
func pbkdf2(password, salt []byte, iter, keyLen int) []byte {
	prf := hmac.New(sha256.New, password)
	hashLen := prf.Size()
	numBlocks := (keyLen + hashLen - 1) / hashLen

	var dk []byte
	buf := make([]byte, 4)
	for block := 1; block <= numBlocks; block++ {
		prf.Reset()
		prf.Write(salt)
		binary.BigEndian.PutUint32(buf, uint32(block))
		prf.Write(buf)
		u := prf.Sum(nil)

		t := make([]byte, len(u))
		copy(t, u)
		for n := 2; n <= iter; n++ {
			prf.Reset()
			prf.Write(u)
			u = prf.Sum(nil)
			for i := range t {
				t[i] ^= u[i]
			}
		}
		dk = append(dk, t...)
	}
	return dk[:keyLen]
}

// HashPassword 派生密码哈希（base64）。
func HashPassword(password, salt string) (string, error) {
	saltBytes, err := base64.StdEncoding.DecodeString(salt)
	if err != nil {
		return "", fmt.Errorf("盐解码失败: %w", err)
	}
	hash := pbkdf2([]byte(password), saltBytes, iterations, hashSize)
	return base64.StdEncoding.EncodeToString(hash), nil
}

// VerifyPassword 常量时间校验。
func VerifyPassword(password, salt, expectedHash string) bool {
	actual, err := HashPassword(password, salt)
	if err != nil {
		return false
	}
	expected, err := base64.StdEncoding.DecodeString(expectedHash)
	if err != nil {
		return false
	}
	actualBytes, err := base64.StdEncoding.DecodeString(actual)
	if err != nil {
		return false
	}
	return subtle.ConstantTimeCompare(actualBytes, expected) == 1
}

// GenerateSecret 生成随机密钥（base64，64 字节）。
func GenerateSecret() (string, error) {
	secret := make([]byte, 64)
	if _, err := rand.Read(secret); err != nil {
		return "", err
	}
	return base64.StdEncoding.EncodeToString(secret), nil
}

// ---- JWT HS256 ----

// TokenService HS256 JWT（httpOnly Cookie 携带）。
type TokenService struct {
	key []byte
}

func NewTokenService(signingKeyBase64 string) (*TokenService, error) {
	key, err := base64.StdEncoding.DecodeString(signingKeyBase64)
	if err != nil {
		return nil, fmt.Errorf("签名密钥解码失败: %w", err)
	}
	return &TokenService{key: key}, nil
}

func base64URL(b []byte) string {
	return base64.RawURLEncoding.EncodeToString(b)
}

// IssueToken 签发令牌（含 exp）。
func (t *TokenService) IssueToken(lifetime time.Duration) string {
	now := time.Now()
	payload := map[string]int64{
		"iat": now.Unix(),
		"exp": now.Add(lifetime).Unix(),
	}
	payloadBytes, _ := json.Marshal(payload)
	header := base64URL([]byte(`{"alg":"HS256","typ":"JWT"}`))
	body := base64URL(payloadBytes)
	mac := hmac.New(sha256.New, t.key)
	mac.Write([]byte(header + "." + body))
	signature := base64URL(mac.Sum(nil))
	return header + "." + body + "." + signature
}

// TryValidate 校验令牌（签名 + 过期）。
func (t *TokenService) TryValidate(token string) bool {
	parts := strings.Split(token, ".")
	if len(parts) != 3 {
		return false
	}
	mac := hmac.New(sha256.New, t.key)
	mac.Write([]byte(parts[0] + "." + parts[1]))
	expected := mac.Sum(nil)
	given, err := base64.RawURLEncoding.DecodeString(parts[2])
	if err != nil {
		return false
	}
	if subtle.ConstantTimeCompare(expected, given) != 1 {
		return false
	}

	payloadBytes, err := base64.RawURLEncoding.DecodeString(parts[1])
	if err != nil {
		return false
	}
	var payload struct {
		Exp int64 `json:"exp"`
	}
	if err := json.Unmarshal(payloadBytes, &payload); err != nil {
		return false
	}
	return payload.Exp >= time.Now().Unix()
}
