package security

import (
	"strings"
	"testing"
	"time"
)

func TestGenerateSalt_Unique(t *testing.T) {
	s1, err := GenerateSalt()
	if err != nil {
		t.Fatal(err)
	}
	s2, _ := GenerateSalt()
	if s1 == s2 {
		t.Fatal("盐应唯一")
	}
}

func TestHashVerify_CorrectPassword(t *testing.T) {
	salt, _ := GenerateSalt()
	hash, err := HashPassword("s3cret-密码", salt)
	if err != nil {
		t.Fatal(err)
	}
	if !VerifyPassword("s3cret-密码", salt, hash) {
		t.Fatal("正确密码应通过")
	}
}

func TestVerify_WrongPassword(t *testing.T) {
	salt, _ := GenerateSalt()
	hash, _ := HashPassword("correct", salt)
	if VerifyPassword("wrong", salt, hash) {
		t.Fatal("错误密码应拒绝")
	}
}

func TestVerify_InvalidHash(t *testing.T) {
	salt, _ := GenerateSalt()
	if VerifyPassword("x", salt, "not-base64!!!") {
		t.Fatal("非法哈希应拒绝")
	}
}

func TestToken_IssueThenValidate(t *testing.T) {
	secret, _ := GenerateSecret()
	svc, err := NewTokenService(secret)
	if err != nil {
		t.Fatal(err)
	}
	token := svc.IssueToken(5 * time.Minute)
	if !svc.TryValidate(token) {
		t.Fatal("有效令牌应通过")
	}
}

func TestToken_Expired(t *testing.T) {
	secret, _ := GenerateSecret()
	svc, _ := NewTokenService(secret)
	token := svc.IssueToken(-10 * time.Second)
	if svc.TryValidate(token) {
		t.Fatal("过期令牌应拒绝")
	}
}

func TestToken_TamperedPayload(t *testing.T) {
	secret, _ := GenerateSecret()
	svc, _ := NewTokenService(secret)
	token := svc.IssueToken(5 * time.Minute)
	parts := strings.Split(token, ".")
	// 篡改 payload（替换为其他合法 base64url），不重签
	tampered := parts[0] + "." + "eyJleHAiOjk5OTk5OTk5OTl9" + "." + parts[2]
	if svc.TryValidate(tampered) {
		t.Fatal("篡改 payload 应拒绝")
	}
}

func TestToken_WrongKeySignature(t *testing.T) {
	secret1, _ := GenerateSecret()
	secret2, _ := GenerateSecret()
	svc1, _ := NewTokenService(secret1)
	svc2, _ := NewTokenService(secret2)
	token := svc1.IssueToken(5 * time.Minute)
	forged := svc2.IssueToken(5 * time.Minute)
	fParts := strings.Split(forged, ".")
	parts := strings.Split(token, ".")
	if svc1.TryValidate(parts[0] + "." + parts[1] + "." + fParts[2]) {
		t.Fatal("错误密钥签名应拒绝")
	}
}

func TestToken_BadFormat(t *testing.T) {
	secret, _ := GenerateSecret()
	svc, _ := NewTokenService(secret)
	for _, bad := range []string{"", "not-a-jwt", "a.b.c.d"} {
		if svc.TryValidate(bad) {
			t.Fatalf("非法格式应拒绝: %q", bad)
		}
	}
}
