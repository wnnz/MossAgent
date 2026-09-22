import { http } from '@/lib/http'
import type { McpServer, McpTool } from '@/types/domain'

export const mcpApi = {
  getServers: () => http.get<McpServer[]>('/api/mcp/servers'),
  createServer: (data: Omit<McpServer, 'id'>) => http.post<McpServer>('/api/mcp/servers', data),
  updateServer: (id: number, data: Omit<McpServer, 'id'>) => http.put<McpServer>(`/api/mcp/servers/${id}`, data),
  removeServer: (id: number) => http.delete<void>(`/api/mcp/servers/${id}`),
  connect: (id: number) => http.post<McpTool[]>(`/api/mcp/servers/${id}/connect`),
  disconnect: (id: number) => http.post<{ message: string }>(`/api/mcp/servers/${id}/disconnect`),
  tools: (id: number) => http.get<McpTool[]>(`/api/mcp/servers/${id}/tools`),
  callTool: (id: number, toolName: string, args: Record<string, unknown>) =>
    http.post<{ success: boolean; result: string }>(`/api/mcp/servers/${id}/tools/${toolName}/call`, { arguments: args }),
}
