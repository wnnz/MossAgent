// 简单 className 合并（只取字符串项）
export function cn(...classes: unknown[]): string {
  return classes.filter((c): c is string => typeof c === 'string' && c.length > 0).join(' ')
}

// 格式化时间
export function formatTime(iso: string): string {
  const date = new Date(iso)
  return date.toLocaleString('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
}

// 尝试把工具参数 JSON 美化
export function prettyJson(raw?: string | null): string {
  if (!raw) return ''
  try {
    return JSON.stringify(JSON.parse(raw), null, 2)
  } catch {
    return raw
  }
}
