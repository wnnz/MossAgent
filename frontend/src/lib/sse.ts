// SSE 消费：POST 请求返回 text/event-stream，逐事件回调
export interface SseHandlers {
  onEvent: (eventName: string, data: any) => void
}

export async function ssePost(url: string, body: unknown, handlers: SseHandlers, signal?: AbortSignal): Promise<void> {
  const response = await fetch(url, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
    signal,
  })

  if (!response.ok || !response.body) {
    let message = `请求失败（${response.status}）`
    try {
      const data = await response.json()
      if (data?.message) message = data.message
    } catch {
      // 忽略
    }
    throw new Error(message)
  }

  const reader = response.body.getReader()
  const decoder = new TextDecoder()
  let buffer = ''

  for (;;) {
    const { done, value } = await reader.read()
    if (done) break
    buffer += decoder.decode(value, { stream: true })

    // 按 \n\n 分帧，帧内 event:/data: 行
    let index: number
    while ((index = buffer.indexOf('\n\n')) >= 0) {
      const frame = buffer.slice(0, index)
      buffer = buffer.slice(index + 2)
      let eventName = 'message'
      const dataLines: string[] = []
      for (const line of frame.split('\n')) {
        if (line.startsWith('event:')) {
          eventName = line.slice('event:'.length).trim()
        } else if (line.startsWith('data:')) {
          dataLines.push(line.slice('data:'.length).trim())
        }
      }
      if (dataLines.length === 0) continue
      let data: any = {}
      try {
        data = JSON.parse(dataLines.join('\n'))
      } catch {
        data = { raw: dataLines.join('\n') }
      }
      handlers.onEvent(eventName, data)
    }
  }
}
