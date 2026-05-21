import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { Participant, RemoteChangeData, CommandMessage } from '@/types'
import { getRoomCanvas } from '@/services/groupNotes'

export const useCollaborationStore = defineStore('collaboration', () => {
  const roomId = ref<string>('')
  const noteTitle = ref('')
  const participants = ref<Participant[]>([])
  const canvasData = ref<Record<number, string>>({})
  const syncStatus = ref<'connected' | 'connecting' | 'disconnected'>('disconnected')
  const typingUsers = ref<Set<string>>(new Set())
  const commands = ref<CommandMessage[]>([])
  const columns = ref(4)
  const loadError = ref('')

  let socket: WebSocket | null = null

  const typingStatusText = computed(() => {
    const users = Array.from(typingUsers.value)
    if (users.length === 0) return ''
    if (users.length === 1) return `${users[0]}正在输入...`
    return `${users.length}人正在输入...`
  })

  async function joinRoom(id: string) {
    roomId.value = id
    syncStatus.value = 'connecting'
    loadError.value = ''

    const token = localStorage.getItem('auth_token')
    if (!token) {
      syncStatus.value = 'disconnected'
      loadError.value = '未登录，无法连接协同房间'
      return
    }

    connectWebSocket(id, token)
  }

  function connectWebSocket(id: string, token: string) {
    const proto = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
    const host = window.location.host
    const url = `${proto}//${host}/ws/notes/${encodeURIComponent(id)}?token=${encodeURIComponent(token)}`

    try {
      socket = new WebSocket(url)
    } catch {
      syncStatus.value = 'disconnected'
      loadError.value = '连接失败，请检查网络'
      return
    }

    socket.onopen = () => {
      syncStatus.value = 'connected'
    }

    socket.onclose = () => {
      syncStatus.value = 'disconnected'
    }

    socket.onerror = () => {
      if (syncStatus.value === 'connecting') {
        fetchCanvasFallback(id)
      }
    }

    socket.onmessage = (event) => {
      try {
        const payload = JSON.parse(event.data)
        handleMessage(payload)
      } catch {
        /* malformed message */
      }
    }
  }

  function handleMessage(payload: any) {
    switch (payload.event) {
      case 'canvas:sync':
        if (payload.column_id !== undefined) {
          canvasData.value = {
            ...canvasData.value,
            [payload.column_id]: payload.content,
          }
        }
        break
      case 'participant:join':
        if (payload.user_id) {
          const exists = participants.value.find(u => u.user_id === payload.user_id)
          if (!exists) {
            participants.value.push({ ...payload, is_online: true })
          } else {
            exists.is_online = true
          }
        }
        break
      case 'participant:leave':
        if (payload.user_id) {
          const p = participants.value.find(u => u.user_id === payload.user_id)
          if (p) p.is_online = false
        }
        break
      case 'typing:status':
        if (payload.isTyping) {
          typingUsers.value.add(payload.name || payload.user_id)
        } else {
          typingUsers.value.delete(payload.name || payload.user_id)
        }
        break
      case 'command:broadcast':
        commands.value.push(payload)
        break
    }
  }

  async function fetchCanvasFallback(noteId: string) {
    try {
      const res = await getRoomCanvas(noteId)
      if (res.data) {
        columns.value = res.data.columns || 4
        if (res.data.canvas_data) {
          canvasData.value = res.data.canvas_data
        }
        syncStatus.value = 'connected'
      }
    } catch {
      loadError.value = '加载协同画布数据失败，请稍后重试'
      syncStatus.value = 'disconnected'
    }
  }

  function leaveRoom() {
    if (socket) {
      socket.close()
      socket = null
    }
    roomId.value = ''
    participants.value = []
    canvasData.value = {}
    typingUsers.value.clear()
    commands.value = []
    syncStatus.value = 'disconnected'
    loadError.value = ''
  }

  function pushLocalChange(columnId: number, content: string) {
    if (socket && socket.readyState === WebSocket.OPEN) {
      const user = JSON.parse(localStorage.getItem('auth_user') || '{}')
      socket.send(JSON.stringify({
        event: 'canvas:update',
        column_id: columnId,
        content,
        user_id: user.id,
      }))
    }
  }

  function sendTypingStatus(isTyping: boolean) {
    if (socket && socket.readyState === WebSocket.OPEN) {
      const user = JSON.parse(localStorage.getItem('auth_user') || '{}')
      socket.send(JSON.stringify({
        event: isTyping ? 'typing:start' : 'typing:stop',
        user_id: user.id,
        name: user.name,
      }))
    }
  }

  function sendCommand(message: string) {
    if (socket && socket.readyState === WebSocket.OPEN) {
      socket.send(JSON.stringify({
        event: 'command:broadcast',
        message,
        timestamp: new Date().toISOString(),
      }))
    }
  }

  function setColumns(n: number) {
    columns.value = n
  }

  return {
    roomId,
    noteTitle,
    participants,
    canvasData,
    syncStatus,
    typingUsers,
    commands,
    columns,
    loadError,
    typingStatusText,
    joinRoom,
    leaveRoom,
    pushLocalChange,
    sendTypingStatus,
    sendCommand,
    setColumns,
  }
})
