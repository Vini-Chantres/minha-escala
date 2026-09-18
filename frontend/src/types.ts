export interface User { id: string; tenantId: string; name: string; email: string; role: 'Owner' | 'Member'; isActive: boolean }
export interface Entry { id?: string; date: string; type: 'folga' | 'trabalho'; sector: string }
export interface Auth { accessToken: string; expiresAt: string; user: User }
export interface Audit { items: { id: string; userId: string | null; action: string; resource: string; createdAt: string }[]; total: number; page: number }
